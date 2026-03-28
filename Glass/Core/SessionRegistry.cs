using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Glass.Core;

// Tracks active EverQuest sessions and their associated process and window information.
public class SessionRegistry
{
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    public event Action? AllSessionsDisconnected;

    private int _sessionCount = 0;

    // Represents a single active EverQuest session.
    public class SessionEntry
    {
        public string SessionName { get; set; } = string.Empty;
        public string CharacterName { get; set; } = string.Empty;
        public uint Pid { get; set; }
        public IntPtr Hwnd { get; set; }
    }

    private readonly Dictionary<string, SessionEntry> _sessions = new();
    private readonly object _lock = new();

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // SessionRegistry Constructor
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public SessionRegistry()
    {
        GlassContext.FocusTracker.SessionActivated += OnSessionActivated;
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // OnSessionActivated
    //
    // Called when FocusTracker detects a new EQ session has gained focus.
    // Notifies ISXGlass of the active session.
    //
    // sessionName:  The session that gained focus e.g. "is7"
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void OnSessionActivated(string sessionName)
    {
        GlassContext.ISXGlassPipe.Send($"activate {sessionName}");
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // OnSessionConnected
    //
    // Adds or updates a session when ISXGlass reports it has connected.
    //
    // sessionName:    The session name e.g. "is7"
    // characterName:  The character logged in to this session
    // pid:            The process ID of the EQ client
    // hwnd:           The main window handle of the EQ client
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public void OnSessionConnected(string sessionName, string characterName, uint pid, IntPtr hwnd)
    {
       lock (_lock)
        {
            if (!_sessions.TryGetValue(sessionName, out var entry))
            {
                entry = new SessionEntry { SessionName = sessionName };
                _sessions[sessionName] = entry;
            }
            entry.CharacterName = characterName;
            entry.Pid = pid;
            entry.Hwnd = hwnd;
        }

        _sessionCount++;
        DebugLog.Write(DebugLog.Log_Sessions, $"SessionRegistry.OnSessionConnected: session={sessionName} count={_sessionCount} character={characterName} pid={pid} hwnd={hwnd}");

    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // OnSessionDisconnected
    //
    // Removes a session from the registry when it disconnects.
    //
    // sessionName:  The session that disconnected
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void OnSessionDisconnected(string sessionName)
    {
        DebugLog.Write(DebugLog.Log_Sessions, $"SessionRegistry.OnSessionDisconnected: session={sessionName} count will be={_sessionCount - 1}.");

        lock (_lock)
        {
            _sessions.Remove(sessionName);
        }

        _sessionCount--;

        if (_sessionCount <= 0)
        {
            _sessionCount = 0;
            DebugLog.Write(DebugLog.Log_Sessions, "SessionRegistry.OnSessionDisconnected: all sessions disconnected.");
            AllSessionsDisconnected?.Invoke();
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // FindSessionByHwnd
    //
    // Returns the session name for the given window handle, or null if not found.
    //
    // hwnd:  The window handle to look up
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public string? FindSessionByHwnd(IntPtr hwnd)
    {
        lock (_lock)
        {
            foreach (KeyValuePair<string, SessionEntry> pair in _sessions)
            {
                if (pair.Value.Hwnd == hwnd)
                {
                    return pair.Key;
                }
            }
        }
        return null;
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // GetSessions
    //
    // Returns a snapshot of all current sessions.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public IReadOnlyList<SessionEntry> GetSessions()
    {
        lock (_lock)
        {
            return _sessions.Values.ToList();
        }
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // GetSession
    //
    // Returns the entry for a specific session, or null if not found.
    //
    // sessionName:  The session to query
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public SessionEntry? GetSession(string sessionName)
    {
        lock (_lock)
        {
            return _sessions.TryGetValue(sessionName, out var entry) ? entry : null;
        }
    }
}