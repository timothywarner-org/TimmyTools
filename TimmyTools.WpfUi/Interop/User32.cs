using System.Runtime.InteropServices;

using TimmyTools.WpfUi.Interop.Structures;

namespace TimmyTools.WpfUi.Interop;

internal partial class User32
{
    [LibraryImport("user32.dll")]
    public static partial nint MonitorFromWindow(nint hwnd, int dwFlags);

    [LibraryImport("user32.dll")]
    public static partial nint MonitorFromPoint(POINT pt, int dwFlags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetMonitorInfoW(nint hMonitor, ref MONITORINFO lpmi);


    [LibraryImport("user32.dll")]
    public static partial int GetWindowLongPtrW(nint hWnd, int nIndex);

    [LibraryImport("user32.dll")]
    public static partial int SetWindowLongPtrW(nint hWnd, int nIndex, nint dwNewLong);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    // Modern clipboard-change notification (Windows Vista+). Preferred over the
    // legacy SetClipboardViewer chain, which is fragile when any link in the
    // viewer chain misbehaves. Each listener window gets a WM_CLIPBOARDUPDATE
    // message whenever the clipboard contents change.
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AddClipboardFormatListener(nint hWnd);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RemoveClipboardFormatListener(nint hWnd);

    // System-wide hotkey registration. The owning window receives WM_HOTKEY with
    // the registration id in wParam whenever the chord is pressed, regardless of
    // which application currently has focus.
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterHotKey(nint hWnd, int id);
}
