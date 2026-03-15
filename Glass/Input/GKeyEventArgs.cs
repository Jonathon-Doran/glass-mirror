namespace Glass.Input;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// GKeyEventArgs
//
// Arguments for a G-key press event.
// DeviceHandle uniquely identifies the physical device.
// KeyIndex is 1-based (G1=1, G2=2, etc.).
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
public class GKeyEventArgs : EventArgs
{
    public IntPtr DeviceHandle { get; }
    public int KeyIndex { get; }

    public GKeyEventArgs(IntPtr deviceHandle, int keyIndex)
    {
        DeviceHandle = deviceHandle;
        KeyIndex = keyIndex;
    }
}
