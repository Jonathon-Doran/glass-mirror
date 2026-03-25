#include "ISXGlass.h"
#include "KeyManager.h"
#include "Logger.h"

KeyManager g_KeyManager;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// KeyManager::LoadRelayGroup
//
// Stores pending group membership from a bulk relay_group message.
// Defines the group, stores all character IDs as pending, and resolves
// any characters that are already connected.
//
// groupId:      The group to define
// characterIds: The Glass character IDs that belong to this group
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
void KeyManager::LoadRelayGroup(GroupID groupId, const std::vector<CharacterID>& characterIds)
{
    Logger::Instance().Write("KeyManager::LoadRelayGroup: groupId=%u count=%zu", groupId, characterIds.size());

    std::vector<std::pair<CharacterID, std::string>> toResolve;

    {
        std::lock_guard<std::mutex> lock(_mutex);

        if (_groups.find(groupId) == _groups.end())
        {
            _groups[groupId] = MemberSet();
        }

        _pendingGroupMembers[groupId] = std::set<CharacterID>(characterIds.begin(), characterIds.end());

        for (CharacterID characterId : characterIds)
        {
            SessionEntry* entry = g_SessionManager.FindSessionForCharacter(characterId);
            if (entry != nullptr)
            {
                toResolve.push_back({ characterId, entry->sessionName });
            }
        }
    }

    for (const auto& pair : toResolve)
    {
        ResolvePending(pair.first, pair.second);
    }
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// KeyManager::ResolvePending
//
// Resolves pending group memberships for the given character.
// Called when a session connects. If the character has no pending memberships, returns silently.
//
// characterId:  The Glass character ID that just connected
// sessionName:  The session name to add to any pending groups
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
void KeyManager::ResolvePending(CharacterID characterId, const std::string& sessionName)
{
    std::lock_guard<std::mutex> lock(_mutex);

    for (auto& pair : _pendingGroupMembers)
    {
        if (pair.second.find(characterId) != pair.second.end())
        {
            GroupID groupId = pair.first;
            _groups[groupId].insert(sessionName);
            _roundRobinIterators[groupId] = _groups[groupId].begin();
            Logger::Instance().Write("KeyManager::ResolvePending: characterId=%u resolved to session=%s in groupId=%u",
                characterId, sessionName.c_str(), groupId);
        }
    }
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// KeyManager::DefineGroup
//
// Registers a group ID. Creates an empty member set if the group does not already exist.
//
// groupId:  The group to register
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
void KeyManager::DefineGroup(GroupID groupId)
{
    std::lock_guard<std::mutex> lock(_mutex);

    if (_groups.find(groupId) == _groups.end())
    {
        _groups[groupId] = MemberSet();
        Logger::Instance().Write("KeyManager::DefineGroup: groupId=%u", groupId);
    }
    else
    {
        Logger::Instance().Write("KeyManager::DefineGroup: groupId=%u already exists, skipping.", groupId);
    }
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// KeyManager::AddToGroup
//
// Adds a session to a group. Creates the group if it does not exist.
// Resets the round-robin iterator for the group.
//
// groupId:      The group to add to
// sessionName:  The session to add
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
void KeyManager::AddToGroup(GroupID groupId, const std::string& sessionName)
{
    std::lock_guard<std::mutex> lock(_mutex);

    _groups[groupId].insert(sessionName);
    _roundRobinIterators[groupId] = _groups[groupId].begin();

    Logger::Instance().Write("KeyManager::AddToGroup: groupId=%u session=%s", groupId, sessionName.c_str());
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// KeyManager::RemoveFromGroup
//
// Removes a session from a group. Resets the round-robin iterator for the group.
//
// groupId:      The group to remove from
// sessionName:  The session to remove
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
void KeyManager::RemoveFromGroup(GroupID groupId, const std::string& sessionName)
{
    std::lock_guard<std::mutex> lock(_mutex);

    auto groupIt = _groups.find(groupId);
    if (groupIt == _groups.end())
    {
        Logger::Instance().Write("KeyManager::RemoveFromGroup: groupId=%u not found.", groupId);
        return;
    }

    groupIt->second.erase(sessionName);
    _roundRobinIterators[groupId] = groupIt->second.begin();

    Logger::Instance().Write("KeyManager::RemoveFromGroup: groupId=%u session=%s removed.", groupId, sessionName.c_str());
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// KeyManager::DefineCommand
//
// Defines or replaces a command definition.
//
// commandId:   The command ID
// actionType:  Keystroke or Text
// action:      The keystroke combo or text string
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
void KeyManager::DefineCommand(CommandID commandId, CommandActionType actionType, const std::string& action)
{
    std::lock_guard<std::mutex> lock(_mutex);

    CommandDefinition def;
    def.id = commandId;
    def.actionType = actionType;
    def.action = action;
    _commands[commandId] = def;

    Logger::Instance().Write("KeyManager::DefineCommand: commandId=%u type=%d action=%s",
        commandId, (int)actionType, action.c_str());
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// KeyManager::RoundRobinNext
//
// Returns the next session name in round-robin order for the given group.
// Advances the iterator, wrapping around. Returns empty string if the group
// is empty or not found. Caller must hold _mutex.
//
// groupId:  The group to advance
// 
// Returns:  The session name for the next member in round-robin order.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
std::string KeyManager::RoundRobinNext(GroupID groupId)
{
    auto groupIt = _groups.find(groupId);
    if ((groupIt == _groups.end()) || groupIt->second.empty())
    {
        return std::string();
    }

    auto& members = groupIt->second;
    auto& it = _roundRobinIterators[groupId];

    if ((it == members.end()) || (it == members.begin() && members.size() == 0))
    {
        it = members.begin();
    }

    std::string session = *it;
    ++it;
    if (it == members.end())
    {
        it = members.begin();
    }

    return session;
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// KeyManager::ExecuteOnSession
//
// Executes a command on a single session via IS relay.
// For Keystroke actions, relays a press command.
// For Text actions, relays the text string.
//
// cmd:         The command definition
// sessionName: The session to execute on
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
void KeyManager::ExecuteOnSession(const CommandDefinition& cmd, const std::string& sessionName)
{
    char command[512] = {};

    if (cmd.actionType == CommandActionType::Keystroke)
    {
        snprintf(command, sizeof(command), "relay %s \"press %s\"",
            sessionName.c_str(), cmd.action.c_str());
    }
    else
    {
        snprintf(command, sizeof(command), "relay %s \"%s\"",
            sessionName.c_str(), cmd.action.c_str());
    }

    Logger::Instance().Write("KeyManager::ExecuteOnSession: %s", command);
    pISInterface->ExecuteCommand(command);
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// KeyManager::ExecuteKey
//
// Executes a command on all sessions in the given group.
// If the command definition does not exist or the group is empty, logs and returns.
//
// commandId:  The command to execute
// groupId:    The target group
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
void KeyManager::ExecuteKey(CommandID commandId, GroupID groupId)
{
    std::lock_guard<std::mutex> lock(_mutex);

    auto cmdIt = _commands.find(commandId);
    if (cmdIt == _commands.end())
    {
        Logger::Instance().Write("KeyManager::ExecuteKey: commandId=%u not found.", commandId);
        return;
    }

    auto groupIt = _groups.find(groupId);
    if ((groupIt == _groups.end()) || groupIt->second.empty())
    {
        Logger::Instance().Write("KeyManager::ExecuteKey: groupId=%u not found or empty.", groupId);
        return;
    }

    Logger::Instance().Write("KeyManager::ExecuteKey: commandId=%u groupId=%u members=%zu",
        commandId, groupId, groupIt->second.size());

    for (const auto& sessionName : groupIt->second)
    {
        ExecuteOnSession(cmdIt->second, sessionName);
    }
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// KeyManager::StartRepeat
//
// Starts auto-repeat for a command on a group at the given interval.
// If already running, logs and returns.
//
// commandId:   The command to repeat
// groupId:     The target group
// intervalMs:  The repeat interval in milliseconds
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
void KeyManager::StartRepeat(CommandID commandId, GroupID groupId, unsigned int intervalMs)
{
    std::lock_guard<std::mutex> lock(_mutex);

    auto key = std::make_pair(commandId, groupId);
    auto it = _repeats.find(key);
    if ((it != _repeats.end()) && it->second.running)
    {
        Logger::Instance().Write("KeyManager::StartRepeat: commandId=%u groupId=%u already running.", commandId, groupId);
        return;
    }

    Logger::Instance().Write("KeyManager::StartRepeat: commandId=%u groupId=%u intervalMs=%u", commandId, groupId, intervalMs);

    RepeatState& state = _repeats[key];
    state.commandId = commandId;
    state.groupId = groupId;
    state.intervalMs = intervalMs;
    state.running = true;

    state.thread = std::thread([this, commandId, groupId, intervalMs, &state]()
        {
            Logger::Instance().Write("KeyManager: repeat thread started. commandId=%u groupId=%u", commandId, groupId);
            while (state.running)
            {
                ExecuteKey(commandId, groupId);
                std::this_thread::sleep_for(std::chrono::milliseconds(intervalMs));
            }
            Logger::Instance().Write("KeyManager: repeat thread stopped. commandId=%u groupId=%u", commandId, groupId);
        });
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// KeyManager::StopRepeat
//
// Stops auto-repeat for a command on a group and joins the thread.
//
// commandId:  The command to stop
// groupId:    The target group
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
void KeyManager::StopRepeat(CommandID commandId, GroupID groupId)
{
    std::unique_lock<std::mutex> lock(_mutex);

    auto key = std::make_pair(commandId, groupId);
    auto it = _repeats.find(key);
    if ((it == _repeats.end()) || !it->second.running)
    {
        Logger::Instance().Write("KeyManager::StopRepeat: commandId=%u groupId=%u not running.", commandId, groupId);
        return;
    }

    Logger::Instance().Write("KeyManager::StopRepeat: commandId=%u groupId=%u stopping.", commandId, groupId);

    it->second.running = false;
    lock.unlock();
    it->second.thread.join();

    std::lock_guard<std::mutex> relock(_mutex);
    _repeats.erase(key);

    Logger::Instance().Write("KeyManager::StopRepeat: commandId=%u groupId=%u stopped.", commandId, groupId);
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// KeyManager::Shutdown
//
// Stops all running repeat threads cleanly.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
void KeyManager::Shutdown()
{
    Logger::Instance().Write("KeyManager::Shutdown: stopping all repeat threads.");

    std::unique_lock<std::mutex> lock(_mutex);

    for (auto& pair : _repeats)
    {
        if (pair.second.running)
        {
            pair.second.running = false;
        }
    }

    lock.unlock();

    for (auto& pair : _repeats)
    {
        if (pair.second.thread.joinable())
        {
            pair.second.thread.join();
        }
    }

    std::lock_guard<std::mutex> relock(_mutex);
    _repeats.clear();

    Logger::Instance().Write("KeyManager::Shutdown: complete.");
}