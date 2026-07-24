using System.Runtime.InteropServices;

namespace TimmyTools.WpfUi.Interop.Structures;

// Layout matches the Win32 MINMAXINFO passed by lParam on WM_GETMINMAXINFO.
// Only ptMaxSize and ptMaxPosition are written by the handler; the reserved and
// track fields are kept so the sequential layout marshals at the right offsets.
[StructLayout(LayoutKind.Sequential)]
internal struct MINMAXINFO
{
    public POINT ptReserved;
    public POINT ptMaxSize;
    public POINT ptMaxPosition;
    public POINT ptMinTrackSize;
    public POINT ptMaxTrackSize;
}
