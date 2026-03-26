#include "ISXGlass.h"
#include "KeyManager.h"
#include "Logger.h"

KeyManager g_KeyManager;


//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// KeyManager::Start
//
// Starts the execution worker thread.
// Called once during ISXGlass initialization.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
void KeyManager::Start()
{
    _execRunning = true;
    _execThread = std::thread(&KeyManager::ExecutionWorker, this);
    Logger::Instance().Write("KeyManager::Start: execution worker thread started.");
}

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
// KeyManager::DeclareCommand
//
// Registers a command ID, clearing any existing steps.
// Called when Glass sends cmd_define before the cmd_step messages.
//
// commandId:  The command ID to declare
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
void KeyManager::DeclareCommand(CommandID commandId)
{
    std::lock_guard<std::mutex> lock(_mutex);

    CommandDefinition& def = _commands[commandId];
    def.id = commandId;
    def.steps.clear();

    Logger::Instance().Write("KeyManager::DeclareCommand: commandId=%u", commandId);
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// KeyManager::AddCommandStep
//
// Adds a single step to an existing command definition.
// If the command has not been declared, logs a warning and returns.
//
// commandId:   The command to add the step to
// sequence:    The step sequence number — steps execute in ascending sequence order
// actionType:  Keystroke or Text
// delayMs:     Milliseconds to wait after executing this step
// value:       The keystroke string or text string to execute
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
void KeyManager::AddCommandStep(CommandID commandId, StepID sequence, CommandActionType actionType, unsigned int delayMs, const std::string& value)
{
    std::lock_guard<std::mutex> lock(_mutex);

    auto it = _commands.find(commandId);
    if (it == _commands.end())
    {
        Logger::Instance().Write("KeyManager::AddCommandStep: commandId=%u not declared, ignoring step sequence=%u.", commandId, sequence);
        return;
    }

    CommandStep step;
    step.sequence = sequence;
    step.actionType = actionType;
    step.delayMs = delayMs;
    step.value = value;

    it->second.steps[sequence] = step;

    Logger::Instance().Write("KeyManager::AddCommandStep: commandId=%u sequence=%u type=%d delayMs=%u value=%s",
        commandId, sequence, (int)actionType, delayMs, value.c_str());
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
// KeyManager::ExecuteCommand
//
// Executes a command on all sessions in the given group via the execution queue.
// If the command definition does not exist or the group is empty, logs and returns.
//
// commandId:  The command to execute
// groupId:    The target group
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
void KeyManager::ExecuteCommand(CommandID commandId, GroupID groupId)
{
    std::lock_guard<std::mutex> lock(_mutex);

    auto cmdIt = _commands.find(commandId);
    if (cmdIt == _commands.end())
    {
        Logger::Instance().Write("KeyManager::ExecuteCommand: commandId=%u not found.", commandId);
        return;
    }

    auto groupIt = _groups.find(groupId);
    if ((groupIt == _groups.end()) || groupIt->second.empty())
    {
        Logger::Instance().Write("KeyManager::ExecuteCommand: groupId=%u not found or empty.", groupId);
        return;
    }

    Logger::Instance().Write("KeyManager::ExecuteCommand: commandId=%u groupId=%u members=%zu",
        commandId, groupId, groupIt->second.size());

    for (const auto& sessionName : groupIt->second)
    {
        EnqueueExecution(cmdIt->second, sessionName);
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
                ExecuteCommand(commandId, groupId);
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
// KeyManager::Reset
//
// Stops all running repeat threads and clears all group, command, round-robin,
// and execution queue state. The execution worker thread continues running.
// Called when a new profile is launched to ensure stale state does not carry over.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
void KeyManager::Reset()
{
    Logger::Instance().Write("KeyManager::Reset: stopping all repeat threads.");

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

    {
        std::lock_guard<std::mutex> execLock(_execMutex);
        while (!_execQueue.empty())
        {
            _execQueue.pop();
        }
        Logger::Instance().Write("KeyManager::Reset: execution queue cleared.");
    }

    std::lock_guard<std::mutex> relock(_mutex);
    _repeats.clear();
    _groups.clear();
    _commands.clear();
    _roundRobinIterators.clear();

    Logger::Instance().Write("KeyManager::Reset: complete.");
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// KeyManager::ExecutionWorker
//
// Worker thread that drains the execution queue.
// Blocks on the condition variable when the queue is empty.
// Exits cleanly when _execRunning is false and the queue is empty.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
void KeyManager::ExecutionWorker()
{
    Logger::Instance().Write("KeyManager::ExecutionWorker: started.");

    while (true)
    {
        std::function<void()> task;

        {
            std::unique_lock<std::mutex> lock(_execMutex);
            _execCV.wait(lock, [this]()
                {
                    return (!_execQueue.empty() || !_execRunning);
                });

            if (!_execRunning && _execQueue.empty())
            {
                Logger::Instance().Write("KeyManager::ExecutionWorker: exiting.");
                return;
            }

            task = std::move(_execQueue.front());
            _execQueue.pop();
        }

        task();
    }
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// KeyManager::CharacterDelay
//
// Returns a randomized inter-character delay in milliseconds simulating
// natural human typing at approximately 120wpm.
// Distribution is non-linear:
//   60% chance: 60-100ms  (fast keystrokes)
//   30% chance: 100-180ms (brief pause)
//   10% chance: 180-400ms (occasional hesitation)
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
unsigned int KeyManager::CharacterDelay()
{
    int roll = rand() % 100;

    if (roll < 60)
    {
        return 60 + (rand() % 41);
    }
    else if (roll < 90)
    {
        return 100 + (rand() % 81);
    }
    else
    {
        return 180 + (rand() % 221);
    }
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// KeyManager::EnqueueExecution
//
// Enqueues all steps of a command for execution on the given session.
// Keystroke steps are sent as a single relay press command.
// Text steps are sent one character at a time with randomized inter-character delays.
// A step delay is applied after all characters of a step are sent, if delayMs is non-zero.
//
// cmd:         The command definition containing the steps to execute
// sessionName: The session to execute on
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
void KeyManager::EnqueueExecution(const CommandDefinition& cmd, const std::string& sessionName)
{
    if (cmd.steps.empty())
    {
        Logger::Instance().Write("KeyManager::EnqueueExecution: commandId=%u has no steps, skipping session=%s.",
            cmd.id, sessionName.c_str());
        return;
    }

    Logger::Instance().Write("KeyManager::EnqueueExecution: commandId=%u steps=%zu session=%s.",
        cmd.id, cmd.steps.size(), sessionName.c_str());


    for (const auto& pair : cmd.steps)
    {
        const CommandStep step = pair.second;
        const std::string session = sessionName;
        const CommandID cmdId = cmd.id;

        if (step.actionType == CommandActionType::Keystroke)
        {
            std::lock_guard<std::mutex> lock(_execMutex);
            _execQueue.push([this, step, session, cmdId]()
                {
                    char command[512] = {};
                    snprintf(command, sizeof(command), "relay %s \"press %s\"",
                        session.c_str(), step.value.c_str());
                    Logger::Instance().Write("KeyManager::ExecutionWorker: commandId=%u sequence=%u keystroke: %s",
                        cmdId, step.sequence, command);
                    pISInterface->ExecuteCommand(command);

                    if (step.delayMs > 0)
                    {
                        std::this_thread::sleep_for(std::chrono::milliseconds(step.delayMs));
                    }
                });
        }
        else
        {
            // Text — one task per character with randomized delay
            for (char ch : step.value)
            {
                std::lock_guard<std::mutex> lock(_execMutex);
                const char character = ch;
                unsigned int delay = CharacterDelay();
                _execQueue.push([this, character, session, cmdId, step, delay]()
                    {
                        char command[512] = {};
                        snprintf(command, sizeof(command), "relay %s \"press %c\"",
                            session.c_str(), character);
                        Logger::Instance().Write("KeyManager::ExecutionWorker: commandId=%u sequence=%u text char='%c'",
                            cmdId, step.sequence, character);
                        pISInterface->ExecuteCommand(command);
                        std::this_thread::sleep_for(std::chrono::milliseconds(delay));
                    });
            }

            if (step.delayMs > 0)
            {
                std::lock_guard<std::mutex> lock(_execMutex);
                unsigned int delayMs = step.delayMs;
                _execQueue.push([delayMs]()
                    {
                        std::this_thread::sleep_for(std::chrono::milliseconds(delayMs));
                    });
            }
        }
    }

    _execCV.notify_one();
}

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// KeyManager::Shutdown
//
// Stops all running repeat threads and the execution worker thread cleanly.
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

    {
        std::lock_guard<std::mutex> relock(_mutex);
        _repeats.clear();
    }

    // Stop execution worker thread
    _execRunning = false;
    _execCV.notify_all();

    if (_execThread.joinable())
    {
        _execThread.join();
    }

    Logger::Instance().Write("KeyManager::Shutdown: complete.");
}