using System.Runtime.InteropServices;

namespace Glass.Input;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// LogitechGSDK
//
// P/Invoke declarations for the Logitech G-key SDK.
// Requires LogitechGKey.dll (64-bit) in the application output directory.
// Requires Logitech Gaming Software to be running.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
internal static class LogitechGSDK
{
    public const int MaxGKeys = 29;
    public const int MaxMStates = 3;

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public struct GkeyCode
    {
        public ushort complete;

        public int KeyIdx
        {
            get { return complete & 0xFF; }
        }

        public int KeyDown
        {
            get { return (complete >> 8) & 1; }
        }

        public int MState
        {
            get { return (complete >> 9) & 3; }
        }

        public int Mouse
        {
            get { return (complete >> 11) & 0xF; }
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void GkeyCB(
        GkeyCode gkeyCode,
        [MarshalAs(UnmanagedType.LPWStr)] string gkeyOrButtonString,
        IntPtr context);

    [DllImport("LogitechGKey", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    public static extern int LogiGkeyInitWithoutContext(GkeyCB gkeyCB);

    [DllImport("LogitechGKey", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool LogiGkeyInitWithoutCallback();

    [DllImport("LogitechGKey", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    public static extern int LogiGkeyIsMouseButtonPressed(int buttonNumber);

    [DllImport("LogitechGKey", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr LogiGkeyGetMouseButtonString(int buttonNumber);

    [DllImport("LogitechGKey", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    public static extern int LogiGkeyIsKeyboardGkeyPressed(int gkeyNumber, int modeNumber);

    [DllImport("LogitechGKey", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr LogiGkeyGetKeyboardGkeyString(int gkeyNumber, int modeNumber);

    [DllImport("LogitechGKey", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    public static extern void LogiGkeyShutdown();
}