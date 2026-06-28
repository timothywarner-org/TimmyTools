using System.Drawing.Text;

using TimmyTools.Core.Enums;
using TimmyTools.WpfUi.Models;
using TimmyTools.WpfUi.Services;

namespace TimmyTools.WpfUi.ViewModels;

public class SettingsViewModel : BaseViewModel
{
    public SettingsViewModel(
        AppMetadataService appMetadata,
        SettingsService settingsService,
        MessengerService messengerService
    ) : base(appMetadata, settingsService, messengerService)
    {
        ApplicationSettings = SettingsService.ApplicationSettings;
        NoteSettings = SettingsService.NoteSettings;
        EditorSettings = SettingsService.EditorSettings;
        BreakTimerSettings = SettingsService.BreakTimerSettings;
        ClipboardSettings = SettingsService.ClipboardSettings;

        IsTransparencyEnabled = (NoteSettings.TransparencyMode != TransparencyMode.Disabled);
    }

    public ApplicationSettingsModel ApplicationSettings { get; set; }
    public NoteSettingsModel NoteSettings { get; set; }
    public EditorSettingsModel EditorSettings { get; set; }
    public BreakTimerSettingsModel BreakTimerSettings { get; set; }
    public ClipboardSettingsModel ClipboardSettings { get; set; }

    public static KeyValuePair<StartupPosition, string>[] StartupPositionsList { get; } = [
        new(StartupPosition.TopLeft, "Top left"),
        new(StartupPosition.TopCentre, "Top centre"),
        new(StartupPosition.TopRight, "Top right"),
        new(StartupPosition.MiddleLeft, "Middle left"),
        new(StartupPosition.MiddleCentre, "Middle centre"),
        new(StartupPosition.MiddleRight, "Middle right"),
        new(StartupPosition.BottomLeft, "Bottom left"),
        new(StartupPosition.BottomCentre, "Bottom centre"),
        new(StartupPosition.BottomRight, "Bottom right")
    ];

    public static KeyValuePair<MinimizeMode, string>[] MinimizeModeList { get; } = [
        new(MinimizeMode.Allow, "Yes"),
        new(MinimizeMode.Prevent, "No")
    ];

    public static KeyValuePair<VisibilityMode, string>[] VisibilityModeList { get; } = [
        new(VisibilityMode.ShowInTaskbar, "Show in taskbar"),
        new(VisibilityMode.HideInTaskbar, "Hide in taskbar"),
        new(VisibilityMode.HideInTaskbarAndTaskSwitcher, "Hide in taskbar and task switcher")
    ];

    public static KeyValuePair<ColourMode, string>[] ColourModeList { get; } = [
        new(ColourMode.Light, "Light"),
        new(ColourMode.Dark, "Dark"),
        new(ColourMode.System, "System default")
    ];

    public static KeyValuePair<TransparencyMode, string>[] TransparencyModeList { get; } = [
        new(TransparencyMode.Disabled, "Disabled"),
        new(TransparencyMode.Enabled, "Enabled")
    ];

    public static KeyValuePair<string, string>[] FontFamilyList { get; } = [..
        new InstalledFontCollection().Families
                                     .Select(f => new KeyValuePair<string, string>(f.Name, f.Name))
    ];

    public static KeyValuePair<CopyAction, string>[] CopyActionList { get; } = [
        new(CopyAction.None, "None"),
        new(CopyAction.CopySelected, "Copy selected"),
        new(CopyAction.CopyLine, "Copy line"),
        new(CopyAction.CopyAll, "Copy all")
    ];

    public static KeyValuePair<PasteAction, string>[] PasteActionList { get; } = [
        new(PasteAction.None, "None"),
        new(PasteAction.Paste, "Paste"),
        new(PasteAction.PasteAndReplaceAll, "Paste and replace all"),
        new(PasteAction.PasteAtEnd, "Paste at end")
    ];

    public static KeyValuePair<CopyFallbackAction, string>[] CopyFallbackActionList { get; } = [
        new(CopyFallbackAction.None, "None"),
        new(CopyFallbackAction.CopyLine, "Copy line"),
        new(CopyFallbackAction.CopyNote, "Copy note")
    ];

    public static KeyValuePair<double, string>[] CaretThicknessList { get; } = [
        new(1.0, "1.0"),
        new(1.5, "1.5"),
        new(2.0, "2.0"),
        new(2.5, "2.5"),
        new(3.0, "3.0"),
        new(3.5, "3.5"),
        new(4.0, "4.0"),
        new(5.0, "5.0")
    ];

    public static KeyValuePair<CaretColour, string>[] CaretColourList { get; } = [
        new(CaretColour.Default, "Default"),
        new(CaretColour.Black, "Black"),
        new(CaretColour.White, "White"),
        new(CaretColour.Red, "Red"),
        new(CaretColour.Blue, "Blue"),
        new(CaretColour.Green, "Green"),
        new(CaretColour.Orange, "Orange"),
        new(CaretColour.Purple, "Purple"),
        new(CaretColour.Brown, "Brown"),
        new(CaretColour.Gray, "Gray")
    ];

    public bool IsTransparencyEnabled { get; set; }
}
