#include "GlassVideo.h"
#include "Consumers\ConsumerManager.h"
#include "Logger.h"

static void OnConsumerOutput(const char* message);

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// ConsumerManager::ConsumerManager
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
ConsumerManager::ConsumerManager()
{
    Logger::Instance().Write("ConsumerManager: initialized.");
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// ConsumerManager::~ConsumerManager
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
ConsumerManager::~ConsumerManager()
{
    UnloadAll();
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// ConsumerManager::Load
//
// Loads a consumer DLL from the given path, calls ConsumerFactory_Create,
// verifies the interface version, queries region names, and instantiates
// one IFrameConsumer per region.
//
// path:  Path to the consumer DLL to load
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
bool ConsumerManager::Load(const std::string& path)
{
    Logger::Instance().Write("ConsumerManager::Load: path=%s", path.c_str());

    // Check if already loaded.
    for (const LoadedDll& dll : _dlls)
    {
        if (dll.path == path)
        {
            Logger::Instance().Write("ConsumerManager::Load: already loaded, ignoring.");
            return true;
        }
    }

    HMODULE module = LoadLibraryA(path.c_str());
    if (module == NULL)
    {
        Logger::Instance().Write("ConsumerManager::Load: LoadLibrary failed. error=%u", GetLastError());
        return false;
    }

    Logger::Instance().Write("ConsumerManager::Load: DLL loaded. module=%p", module);

    ConsumerFactory_CreateFunc createFunc =
        (ConsumerFactory_CreateFunc)GetProcAddress(module, "ConsumerFactory_Create");

    if (createFunc == nullptr)
    {
        Logger::Instance().Write("ConsumerManager::Load: ConsumerFactory_Create not found in DLL.");
        FreeLibrary(module);
        return false;
    }

    IConsumerFactory* factory = createFunc();
    if (factory == nullptr)
    {
        Logger::Instance().Write("ConsumerManager::Load: ConsumerFactory_Create returned nullptr.");
        FreeLibrary(module);
        return false;
    }

    int version = factory->GetVersion();
    if (version != CONSUMER_INTERFACE_VERSION)
    {
        Logger::Instance().Write("ConsumerManager::Load: version mismatch. expected=%d got=%d",
            CONSUMER_INTERFACE_VERSION, version);
        factory->Destroy();
        FreeLibrary(module);
        return false;
    }

    Logger::Instance().Write("ConsumerManager::Load: factory created. version=%d", version);

    const char* const* regionNames = factory->GetRegionNames();
    int consumerCount = 0;

    for (int i = 0; regionNames[i] != nullptr; i++)
    {
        const char* regionName = regionNames[i];
        Logger::Instance().Write("ConsumerManager::Load: creating consumer for region='%s'.", regionName);

        // Warn if a consumer for this region already exists.
        if (_consumers.count(regionName) > 0)
        {
            Logger::Instance().Write("ConsumerManager::Load: region='%s' already has a consumer, replacing.", regionName);

            _consumers[regionName].consumer->Destroy();
            _consumers.erase(regionName);
        }

        IFrameConsumer* consumer = factory->CreateConsumer(regionName, OnConsumerOutput);
        if (consumer == nullptr)
        {
            Logger::Instance().Write("ConsumerManager::Load: CreateConsumer returned nullptr for region='%s', skipping.",
                regionName);
            continue;
        }

        ActiveConsumer active;
        active.regionName = regionName;
        active.consumer = consumer;
        active.lastDeliveryMs = 0;
        active.dllPath = path;

        _consumers[regionName] = active;
        consumerCount++;

        Logger::Instance().Write("ConsumerManager::Load: consumer created for region='%s' intervalMs=%d.",
            regionName, consumer->GetIntervalMs());
    }

    LoadedDll dll;
    dll.path = path;
    dll.module = module;
    dll.factory = factory;
    _dlls.push_back(dll);

    Logger::Instance().Write("ConsumerManager::Load: loaded %d consumers from '%s'.", consumerCount, path.c_str());
    return true;
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// ConsumerManager::Unload
//
// Shuts down and destroys all consumers belonging to the named DLL,
// calls IConsumerFactory::Destroy, and frees the module.
//
// path:  Path that was passed to Load()
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
void ConsumerManager::Unload(const std::string& path)
{
    Logger::Instance().Write("ConsumerManager::Unload: path=%s", path.c_str());

    for (auto it = _dlls.begin(); it != _dlls.end(); ++it)
    {
        if (it->path != path)
        {
            continue;
        }

        // Destroy all consumers that belong to this DLL.
        for (auto consumerIt = _consumers.begin(); consumerIt != _consumers.end(); )
        {
            if (consumerIt->second.dllPath == path)
            {
                Logger::Instance().Write("ConsumerManager::Unload: destroying consumer for region='%s'.",
                    consumerIt->second.regionName.c_str());
                consumerIt->second.consumer->Destroy();
                consumerIt = _consumers.erase(consumerIt);
            }
            else
            {
                ++consumerIt;
            }
        }

        Logger::Instance().Write("ConsumerManager::Unload: destroying factory. module=%p", it->module);
        it->factory->Destroy();
        FreeLibrary(it->module);
        _dlls.erase(it);

        Logger::Instance().Write("ConsumerManager::Unload: done.");
        return;
    }

    Logger::Instance().Write("ConsumerManager::Unload: path='%s' not found.", path.c_str());
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// ConsumerManager::UnloadAll
//
// Unloads all loaded DLLs.  Called on shutdown.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
void ConsumerManager::UnloadAll()
{
    Logger::Instance().Write("ConsumerManager::UnloadAll: unloading %zu DLLs.", _dlls.size());

    // Destroy all consumers first.
    for (auto& pair : _consumers)
    {
        Logger::Instance().Write("ConsumerManager::UnloadAll: destroying consumer for region='%s'.",
            pair.second.regionName.c_str());
        pair.second.consumer->Destroy();
    }
    _consumers.clear();

    // Then destroy all factories and free modules.
    for (LoadedDll& dll : _dlls)
    {
        Logger::Instance().Write("ConsumerManager::UnloadAll: destroying factory path='%s' module=%p.",
            dll.path.c_str(), dll.module);
        dll.factory->Destroy();
        FreeLibrary(dll.module);
    }
    _dlls.clear();

    Logger::Instance().Write("ConsumerManager::UnloadAll: done.");
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// ConsumerManager::Deliver
//
// Called from the render loop once per frame for a given region.
// Checks the consumer's requested interval and delivers a frame
// snapshot if enough time has elapsed since the last delivery.
//
// regionName:  The name of the region being delivered
// pixels:      Pointer to the top-left pixel of the region snapshot
// width:       Width of the region in pixels
// height:      Height of the region in pixels
// stride:      Row stride in bytes
// frameId:     Monotonically increasing frame counter
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
void ConsumerManager::Deliver(const std::string& regionName,
    const uint8_t* pixels,
    int                width,
    int                height,
    int                stride,
    uint64_t           frameId)
{
    auto it = _consumers.find(regionName);
    if (it == _consumers.end())
    {
        return;
    }

    ActiveConsumer& active = it->second;

    DWORD now = GetTickCount();
    int intervalMs = active.consumer->GetIntervalMs();

    if ((intervalMs > 0) && ((now - active.lastDeliveryMs) < (DWORD)intervalMs))
    {
        return;
    }

    RegionFrame frame;
    frame.regionName = regionName.c_str();
    frame.pixels = pixels;
    frame.width = width;
    frame.height = height;
    frame.stride = stride;
    frame.frameId = frameId;

    active.consumer->Consume(&frame);
    active.lastDeliveryMs = now;
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// OnConsumerOutput
//
// Output callback passed to each IFrameConsumer instance.
// Forwards messages from the consumer to the Glass notify pipe.
//
// message:  A null-terminated UTF-8 string to forward to Glass
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
static void OnConsumerOutput(const char* message)
{
    Logger::Instance().Write("ConsumerManager::OnConsumerOutput: %s", message);
    g_pipeManager.Send(message);
}