namespace TimmyTools.WpfUi.Interop.Constants;

// Modifier flags for RegisterHotKey (fsModifiers). MOD_NOREPEAT stops a held
// chord from firing repeatedly.
internal static class MOD
{
    public const uint ALT = 0x0001;
    public const uint CONTROL = 0x0002;
    public const uint SHIFT = 0x0004;
    public const uint WIN = 0x0008;
    public const uint NOREPEAT = 0x4000;
}
