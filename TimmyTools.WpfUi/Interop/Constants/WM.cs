namespace TimmyTools.WpfUi.Interop.Constants;

internal static class WM
{
    // Sent to clipboard-format-listener windows when the clipboard changes.
    public const int CLIPBOARDUPDATE = 0x031D;

    // Sent to the owning window when a registered global hotkey is pressed.
    public const int HOTKEY = 0x0312;

    // Sent while a window is being sized/positioned so it can override its
    // maximised size and position. A borderless (WindowStyle=None) window
    // maximises to cover the whole monitor including the taskbar unless it
    // clamps ptMaxSize/ptMaxPosition to the work area in response to this.
    public const int GETMINMAXINFO = 0x0024;
}
