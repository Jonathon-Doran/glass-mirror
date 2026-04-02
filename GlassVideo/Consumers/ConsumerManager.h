#pragma once

#include <windows.h>
#include <string>
#include <map>
#include <vector>
#include "ConsumerInterface.h"

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// LoadedDll
//
// Tracks a single loaded consumer DLL and its factory instance.
//
// path:     The path the DLL was loaded from, used as the unload key
// module:   The HMODULE returned by LoadLibrary
// factory:  The factory instance returned by ConsumerFactory_Create
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
struct LoadedDll
{
    std::string       path;
    HMODULE           module;
    IConsumerFactory* factory;
};

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// ActiveConsumer
//
// Tracks a single active IFrameConsumer instance and its delivery state.
//
// regionName:      The region name this consumer handles
// consumer:        The consumer instance
// lastDeliveryMs:  Timestamp of the last frame delivery in milliseconds
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
struct ActiveConsumer
{
    std::string         regionName;
    IFrameConsumer*     consumer;
    std::string         dllPath;
    DWORD               lastDeliveryMs;
};

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// ConsumerManager
//
// Manages loading and unloading of consumer DLLs and the lifecycle of
// all active IFrameConsumer instances.
//
// Owned by main.cpp as a global alongside g_slotManager.
// Load() and Unload() are called from OnCommand on the main thread.
// Deliver() is called from the render loop.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
class ConsumerManager
{
public:
    ConsumerManager();
    ~ConsumerManager();

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Load
    //
    // Loads a consumer DLL from the given path, calls ConsumerFactory_Create,
    // queries region names, and instantiates one IFrameConsumer per region.
    // Returns false if the DLL cannot be loaded or the version does not match.
    //
    // path:  Path to the consumer DLL to load
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    bool Load(const std::string& path);

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Unload
    //
    // Shuts down and destroys all consumers belonging to the named DLL,
    // calls IConsumerFactory::Destroy, and frees the module.
    //
    // path:  Path that was passed to Load()
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    void Unload(const std::string& path);

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // UnloadAll
    //
    // Unloads all loaded DLLs.  Called on shutdown.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    void UnloadAll();

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Deliver
    //
    // Called from the render loop once per frame for a given region.
    // Checks each consumer's requested interval and delivers a frame
    // snapshot if enough time has elapsed.
    //
    // regionName:  The name of the region being delivered
    // pixels:      Pointer to the top-left pixel of the region snapshot
    // width:       Width of the region in pixels
    // height:      Height of the region in pixels
    // stride:      Row stride in bytes
    // frameId:     Monotonically increasing frame counter
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    void Deliver(const std::string& regionName,
        const uint8_t* pixels,
        int                width,
        int                height,
        int                stride,
        uint64_t           frameId);

private:
    std::vector<LoadedDll>                        _dlls;
    std::map<std::string, ActiveConsumer>         _consumers;
};