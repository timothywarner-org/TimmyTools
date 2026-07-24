namespace TimmyTools.WpfUi.Interop.Constants;

internal static class SWP
{
    public const uint NOSIZE = 0x0001;
    public const uint NOMOVE = 0x0002;
    public const uint NOZORDER = 0x0004;
    public const uint NOACTIVATE = 0x0010;

    // Forces the window to recalculate its non-client frame and repaint it.
    // Required after SetWindowLongPtr changes an extended style, otherwise the
    // cached frame stays stale and the title bar can render at the wrong size.
    public const uint FRAMECHANGED = 0x0020;
}
