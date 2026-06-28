namespace TimmyTools.WpfUi.Interop.Constants;

internal static class WM
{
    // Sent to clipboard-format-listener windows when the clipboard changes.
    public const int CLIPBOARDUPDATE = 0x031D;

    // Sent to the owning window when a registered global hotkey is pressed.
    public const int HOTKEY = 0x0312;
}
