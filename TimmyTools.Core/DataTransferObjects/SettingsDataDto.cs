using TimmyTools.Core.Enums;

namespace TimmyTools.Core.DataTransferObjects;

public record SettingsDataDto(
     int Id,

     bool ShowTrayIcon,
     bool CheckForUpdates,

     double DefaultNoteWidth,
     double DefaultNoteHeight,
     StartupPosition StartupPosition,
     MinimizeMode MinimizeMode,
     VisibilityMode VisibilityMode,
     bool HideTitleBar,
     bool CycleColours,
     ColourMode ColourMode,
     TransparencyMode TransparencyMode,
     bool OpaqueWhenFocused,
     double OpaqueOpacity,
     double TransparentOpacity,

     bool SpellCheck,
     bool AutoIndent,
     bool NewLineAtEnd,
     bool KeepNewLineVisible,
     bool WrapText,
     string StandardFontFamily,
     string MonoFontFamily,
     bool UseMonoFont,
     bool TabUsesSpaces,
     bool ConvertIndentationOnPaste,
     int TabWidth,
     CopyAction CopyAction,
     bool TrimTextOnCopy,
     CopyAction CopyAltAction,
     bool TrimTextOnAltCopy,
     CopyFallbackAction CopyFallbackAction,
     bool TrimTextOnFallbackCopy,
     CopyFallbackAction CopyAltFallbackAction,
     bool TrimTextOnAltFallbackCopy,
     bool CopyTextOnHighlight,
     PasteAction PasteAction,
     bool TrimTextOnPaste,
     PasteAction PasteAltAction,
     bool TrimTextOnAltPaste,
     bool MiddleClickPaste,
     double CaretThickness,
     CaretColour CaretColour,

     string ClassTitle,
     string NextUp,
     int Segment
);
