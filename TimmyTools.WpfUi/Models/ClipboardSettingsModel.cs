namespace TimmyTools.WpfUi.Models;

// User-tunable preferences for the Clipboard tool. Mirrors the BreakTimerSettings
// pattern: an observable BaseModel persisted through SettingsService alongside the
// other settings groups. AlwaysOnTop is an explicit toggle, never tied to focus
// (see the topmost-when-focused lesson the notes carry).
public class ClipboardSettingsModel : BaseModel
{
    public bool AlwaysOnTop { get; set => SetProperty(ref field, value); } = false;
    public int HistoryLimit { get; set => SetProperty(ref field, value); } = 200;
    public bool ShowCopyNotification { get; set => SetProperty(ref field, value); } = true;
    public string GlobalHotkey { get; set => SetProperty(ref field, value); } = "Ctrl+Alt+C";
    public bool IgnoreSensitiveClipboard { get; set => SetProperty(ref field, value); } = true;
}
