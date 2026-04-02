namespace Glass.Core;

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// LayoutConstants
//
// Global constants used for layout calculations and UI rendering.
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
public static class LayoutConstants
{
    // Minimum horizontal space (in pixels) reserved around the edges of a monitor when arranging slots.
    public const int HorizontalMargin = 128;

    // Minimum vertical space (in pixels) reserved around the edges of a monitor when arranging slots.
    public const int VerticalMargin = 180;

    // Standard aspect ratio for EverQuest client windows (16:9).
    public const double AspectRatio = 16.0 / 9.0;

    // Scaling factor for rendering monitor previews in the UI.
    // Monitor dimensions are divided by this value to create preview rectangles.
    public const double ScalingFactor = 20.0;

    // Reference width for video source/destination NDC (Normalized Device Coordinates).
    // All video coordinates are normalized relative to this width value.
    public const double VideoNormalizedWidth = 1920.0;
}