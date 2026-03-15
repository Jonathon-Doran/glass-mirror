using Glass.Core;

namespace Glass.Input;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// GKeyInput
//
// Receives G-key input from Logitech devices via the Logitech G-key SDK.
// Raises GKeyPressed when a G-key is pressed.
// Requires LogitechGKey.dll (64-bit) and Logitech Gaming Software to be running.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
public class GKeyInput
{
    public event EventHandler<GKeyEventArgs>? GKeyPressed;

    // Hold a reference to the callback delegate to prevent garbage collection.
    private LogitechGSDK.GkeyCB? _callback;

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Start
    //
    // Initializes the Logitech G-key SDK and registers the callback.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void Start()
    {
        DebugLog.Write("GKeyInput: Start called.");

        _callback = new LogitechGSDK.GkeyCB(OnGkeyCB);

        int initResult = LogitechGSDK.LogiGkeyInitWithoutContext(_callback);
        DebugLog.Write($"GKeyInput: LogiGkeyInitWithoutContext returned {initResult}.");

        DebugLog.Write("GKeyInput: initialized successfully.");
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Stop
    //
    // Shuts down the Logitech G-key SDK.
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    public void Stop()
    {
        LogitechGSDK.LogiGkeyShutdown();
        _callback = null;
        DebugLog.Write("GKeyInput: stopped.");
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // OnGkeyCB
    //
    // Called by the Logitech SDK when a G-key or mouse button event occurs.
    // Filters to key-down events on keyboard G-keys only.
    //
    // gkeyCode:           The key code structure containing key index and state
    // gkeyOrButtonString: Human-readable key name from the SDK
    // context:            Unused context pointer
    //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    private void OnGkeyCB(LogitechGSDK.GkeyCode gkeyCode, string gkeyOrButtonString, IntPtr context)
    {
        DebugLog.Write("OnGkeyCB");
        if (gkeyCode.KeyDown != 1)
        {
            return;
        }

        if (gkeyCode.Mouse != 0)
        {
            return;
        }

        DebugLog.Write($"GKeyInput: G{gkeyCode.KeyIdx} pressed.");
        GKeyPressed?.Invoke(this, new GKeyEventArgs(IntPtr.Zero, gkeyCode.KeyIdx));
    }
}