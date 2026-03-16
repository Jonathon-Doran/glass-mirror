#pragma once
#include <map>
#include <vector>
#include <set>
#include <string>
#include <mutex>
#include <thread>
#include <atomic>
#include <windows.h>

typedef unsigned int CommandID;
typedef unsigned int GroupID;
typedef std::set<std::string>  MemberSet;

// Describes the type of action a command performs.
enum class CommandActionType
{
    Keystroke,
    Text
};

// A command definition — maps a command ID to an action.
struct CommandDefinition
{
    CommandID            id;
    CommandActionType    actionType;
    std::string          action;  // keystroke string or text string
};

// Tracks repeat state for a running auto-repeat binding.
struct RepeatState
{
    CommandID       commandId;
    GroupID         groupId;
    unsigned int    intervalMs;
    std::thread     thread;
    std::atomic<bool> running;

    RepeatState() : commandId(0), groupId(0), intervalMs(0), running(false) {}
};

// KeyManager owns group membership, command definitions, round-robin state,
// and auto-repeat threads for the ISXGlass keyboard system.
class KeyManager
{
public:
    // Registers a group ID.
    void DefineGroup(GroupID groupId);

    // Adds a session to a group.
    void AddToGroup(GroupID groupId, const std::string& sessionName);

    // Removes a session from a group.
    void RemoveFromGroup(GroupID groupId, const std::string& sessionName);

    // Defines or replaces a command definition.
    void DefineCommand(CommandID commandId, CommandActionType actionType, const std::string& action);

    // Executes a command on the given target group.
    void ExecuteKey(CommandID commandId, GroupID groupId);

    // Starts auto-repeat for a command on a group at the given interval.
    void StartRepeat(CommandID commandId, GroupID groupId, unsigned int intervalMs);

    // Stops auto-repeat for a command on a group.
    void StopRepeat(CommandID commandId, GroupID groupId);

    // Shuts down all repeat threads cleanly.
    void Shutdown();

private:
    // Returns the next session name in round-robin order for the given group,
    // or empty string if the group is empty.
    std::string RoundRobinNext(GroupID groupId);

    // Executes the given command on a single session.
    void ExecuteOnSession(const CommandDefinition& cmd, const std::string& sessionName);

    std::mutex                                             _mutex;
    std::map<GroupID, MemberSet>                           _groups;                 // map of group to membership
    std::map<GroupID, MemberSet::iterator>                 _roundRobinIterators;
    std::map<CommandID, CommandDefinition>                 _commands;
    std::map<std::pair<CommandID, GroupID>, RepeatState>   _repeats;
};

extern KeyManager g_KeyManager;