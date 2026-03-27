using System.Runtime.InteropServices;

namespace Glass.Core;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// FocusTracker
//
// Monitors foreground window changes using SetWinEventHook and tracks which
// EverQuest session is currently active. When focus moves to a non-EQ window,
// the last known EQ session is retained.
//
// Raises SessionActivated when a new EQ session gains focus.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
public class FocusTracker
{
    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin, uint eventMax,
        IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc,
        uint idProcess, uint idThread,
        uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll")]
    private static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern void PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    private delegate void WinEventDelegate(
        IntPtr hWinEventHook, uint eventType,
        IntPtr hwnd, int idObject, int idChild,
        uint dwEventThread, uint dwmsEventTime);

    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WM_QUIT = 0x0012;

    public event Action<string>? SessionActivated;

    public string? ActiveSession { get; private set; }

    private Thread? _thread;
    private uint _threadId;
    private WinEventDelegate? _delegate;

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Start
    //
    // Starts the focus tracking message pump thread.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void Start()
    {
        _thread = new Thread(MessagePumpThread);
        _thread.IsBackground = true;
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();

        DebugLog.Write("FocusTracker.Start: thread started.");
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Stop
    //
    // Stops focus tracking by posting WM_QUIT to the message pump thread.
    // Does not wait for the thread to exit — call Shutdown for that.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void Stop()
    {
        DebugLog.Write("FocusTracker.Stop: stopping.");

        if (_threadId != 0)
        {
            PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        }

        DebugLog.Write("FocusTracker.Stop: quit posted.");
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // MessagePumpThread
    //
    // Runs on the dedicated focus tracking thread.
    // Installs the WinEvent hook, runs the message loop, then unhooks on exit.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void MessagePumpThread()
    {
        DebugLog.Write("FocusTracker.MessagePumpThread: started.");

        _threadId = GetCurrentThreadId();

        // Keep a strong reference to the delegate to prevent GC collection.
        _delegate = OnWinEvent;

        IntPtr hook = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero,
            _delegate,
            0, 0,
            WINEVENT_OUTOFCONTEXT);

        if (hook == IntPtr.Zero)
        {
            DebugLog.Write("FocusTracker.MessagePumpThread: SetWinEventHook failed.");
            return;
        }

        DebugLog.Write("FocusTracker.MessagePumpThread: hook installed. running message loop.");

        MSG msg;
        while (GetMessage(out msg, IntPtr.Zero, 0, 0))
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        UnhookWinEvent(hook);

        DebugLog.Write("FocusTracker.MessagePumpThread: message loop exited, hook removed.");
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // OnWinEvent
    //
    // Called by Windows when the foreground window changes.
    // Looks up the new foreground HWND in SessionRegistry.
    // Updates ActiveSession if an EQ session is found, otherwise retains the last known session.
    // Raises SessionActivated when a new EQ session gains focus.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        string? session = GlassContext.SessionRegistry.FindSessionByHwnd(hwnd);

        // Not an EQ window, so returning and leaving the previously current session marked as current
        if (session == null)
        {
            return;
        }

        // This session is currently the active session.  Can happen if focus switches to non-eq window and back
        if (session == ActiveSession)
        {
            return;
        }

        ActiveSession = session;
        DebugLog.Write($"FocusTracker.OnWinEvent: active session changed to '{session}'.");

        SessionActivated?.Invoke(session);
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // ClearActiveSession
    //
    // Clears the active session, called when all sessions have disconnected.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void ClearActiveSession()
    {
        DebugLog.Write("FocusTracker.ClearActiveSession: clearing active session.");
        ActiveSession = null;
    }
}