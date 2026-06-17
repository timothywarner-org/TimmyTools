using Microsoft.Data.Sqlite;

using TimmyTools.Core.Configurations;
using TimmyTools.Core.DataTransferObjects;
using TimmyTools.Core.Enums;

namespace TimmyTools.Core.Repositories;

public class SettingsRepository(DatabaseConfiguration databaseConfiguration) : BaseRepository(databaseConfiguration)
{
    public static readonly string TableName = "Settings";

    public static readonly string TableSchema = $@"
        (
            Id  INTEGER PRIMARY KEY AUTOINCREMENT,

            Application_ShowTrayIcon            INTEGER DEFAULT 1,
            Application_CheckForUpdates         INTEGER DEFAULT 0,

            Notes_DefaultWidth                  INTEGER DEFAULT 300,
            Notes_DefaultHeight                 INTEGER DEFAULT 300,
            Notes_StartupPosition               INTEGER DEFAULT 0,
            Notes_MinimizeMode                  INTEGER DEFAULT 0,
            Notes_VisibilityMode                INTEGER DEFAULT 1,
            Notes_HideTitleBar                  INTEGER DEFAULT 0,
            Notes_CycleColours                  INTEGER DEFAULT 1,
            Notes_ColourMode                    INTEGER DEFAULT 0,
            Notes_TransparencyMode              INTEGER DEFAULT 1,
            Notes_OpaqueWhenFocused             INTEGER DEFAULT 1,
            Notes_OpaqueOpacity                 REAL    DEFAULT 1.0,
            Notes_TransparentOpacity            REAL    DEFAULT 0.8,

            Editor_SpellCheck                   INTEGER DEFAULT 1,
            Editor_AutoIndent                   INTEGER DEFAULT 1,
            Editor_NewLineAtEnd                 INTEGER DEFAULT 1,
            Editor_KeepNewLineVisible           INTEGER DEFAULT 1,
            Editor_WrapText                     INTEGER DEFAULT 1,
            Editor_StandardFontFamily           TEXT    DEFAULT 'Segoe UI',
            Editor_MonoFontFamily               TEXT    DEFAULT 'Consolas',
            Editor_UseMonoFont                  INTEGER DEFAULT 0,
            Editor_TabUsesSpaces                INTEGER DEFAULT 0,
            Editor_ConvertIndentationOnPaste    INTEGER DEFAULT 0,
            Editor_TabWidth                     INTEGER DEFAULT 4,
            Editor_CopyAction                   INTEGER DEFAULT 1,
            Editor_TrimTextOnCopy               INTEGER DEFAULT 0,
            Editor_CopyAltAction                INTEGER DEFAULT 1,
            Editor_TrimTextOnAltCopy            INTEGER DEFAULT 1,
            Editor_CopyFallbackAction           INTEGER DEFAULT 1,
            Editor_TrimTextOnFallbackCopy       INTEGER DEFAULT 0,
            Editor_CopyAltFallbackAction        INTEGER DEFAULT 2,
            Editor_TrimTextOnAltFallbackCopy    INTEGER DEFAULT 0,
            Editor_CopyTextOnHighlight          INTEGER DEFAULT 0,
            Editor_PasteAction                  INTEGER DEFAULT 1,
            Editor_TrimTextOnPaste              INTEGER DEFAULT 0,
            Editor_PasteAltAction               INTEGER DEFAULT 1,
            Editor_TrimTextOnAltPaste           INTEGER DEFAULT 1,
            Editor_MiddleClickPaste             INTEGER DEFAULT 1,
            Editor_CaretThickness               REAL    DEFAULT 2.0,
            Editor_CaretColour                  INTEGER DEFAULT 0,

            Tool_Base64State                    INTEGER DEFAULT 1,
            Tool_BracketState                   INTEGER DEFAULT 1,
            Tool_CaseState                      INTEGER DEFAULT 1,
            Tool_ColourState                    INTEGER DEFAULT 1,
            Tool_DateTimeState                  INTEGER DEFAULT 1,
            Tool_GibberishState                 INTEGER DEFAULT 1,
            Tool_GuidState                      INTEGER DEFAULT 1,
            Tool_HashState                      INTEGER DEFAULT 1,
            Tool_HTMLEntityState                INTEGER DEFAULT 1,
            Tool_IndentState                    INTEGER DEFAULT 1,
            Tool_JoinState                      INTEGER DEFAULT 1,
            Tool_JSONState                      INTEGER DEFAULT 1,
            Tool_ListState                      INTEGER DEFAULT 1,
            Tool_QuoteState                     INTEGER DEFAULT 1,
            Tool_RemoveState                    INTEGER DEFAULT 1,
            Tool_SlashState                     INTEGER DEFAULT 1,
            Tool_SortState                      INTEGER DEFAULT 1,
            Tool_SplitState                     INTEGER DEFAULT 1,
            Tool_TrimState                      INTEGER DEFAULT 1,
            Tool_UrlState                       INTEGER DEFAULT 1,

            BreakTimer_ClassTitle               TEXT    DEFAULT '',
            BreakTimer_NextUp                   TEXT    DEFAULT '',
            BreakTimer_Segment                  INTEGER DEFAULT 0
        )
    ";

    public async Task<SettingsDataDto> GetById(int id)
    {
        using SqliteConnection connection = new(ConnectionString);
        connection.Open();

        using SqliteDataReader reader = await ExecuteReader(
            connection,
            @"
                SELECT *
                FROM Settings
                WHERE Id = @id;
            ",
            parameters: [
                new("@id", id)
            ]
        );
        if (!reader.Read())
            throw new Exception($"Could not find settings with id: {id}.");

        return new SettingsDataDto(
            Id: GetInt(reader, "Id"),

            ShowTrayIcon: GetBool(reader, "Application_ShowTrayIcon"),
            CheckForUpdates: GetBool(reader, "Application_CheckForUpdates"),

            DefaultNoteWidth: GetInt(reader, "Notes_DefaultWidth"),
            DefaultNoteHeight: GetInt(reader, "Notes_DefaultHeight"),
            StartupPosition: GetEnum<StartupPosition>(reader, "Notes_StartupPosition"),
            MinimizeMode: GetEnum<MinimizeMode>(reader, "Notes_MinimizeMode"),
            VisibilityMode: GetEnum<VisibilityMode>(reader, "Notes_VisibilityMode"),
            HideTitleBar: GetBool(reader, "Notes_HideTitleBar"),
            CycleColours: GetBool(reader, "Notes_CycleColours"),
            ColourMode: GetEnum<ColourMode>(reader, "Notes_ColourMode"),
            TransparencyMode: GetEnum<TransparencyMode>(reader, "Notes_TransparencyMode"),
            OpaqueWhenFocused: GetBool(reader, "Notes_OpaqueWhenFocused"),
            OpaqueOpacity: GetDouble(reader, "Notes_OpaqueOpacity"),
            TransparentOpacity: GetDouble(reader, "Notes_TransparentOpacity"),

            SpellCheck: GetBool(reader, "Editor_SpellCheck"),
            AutoIndent: GetBool(reader, "Editor_AutoIndent"),
            NewLineAtEnd: GetBool(reader, "Editor_NewLineAtEnd"),
            KeepNewLineVisible: GetBool(reader, "Editor_KeepNewLineVisible"),
            WrapText: GetBool(reader, "Editor_WrapText"),
            StandardFontFamily: GetString(reader, "Editor_StandardFontFamily"),
            MonoFontFamily: GetString(reader, "Editor_MonoFontFamily"),
            UseMonoFont: GetBool(reader, "Editor_UseMonoFont"),
            TabUsesSpaces: GetBool(reader, "Editor_TabUsesSpaces"),
            ConvertIndentationOnPaste: GetBool(reader, "Editor_ConvertIndentationOnPaste"),
            TabWidth: GetInt(reader, "Editor_TabWidth"),
            CopyAction: GetEnum<CopyAction>(reader, "Editor_CopyAction"),
            TrimTextOnCopy: GetBool(reader, "Editor_TrimTextOnCopy"),
            CopyAltAction: GetEnum<CopyAction>(reader, "Editor_CopyAltAction"),
            TrimTextOnAltCopy: GetBool(reader, "Editor_TrimTextOnAltCopy"),
            CopyFallbackAction: GetEnum<CopyFallbackAction>(reader, "Editor_CopyFallbackAction"),
            TrimTextOnFallbackCopy: GetBool(reader, "Editor_TrimTextOnFallbackCopy"),
            CopyAltFallbackAction: GetEnum<CopyFallbackAction>(reader, "Editor_CopyAltFallbackAction"),
            TrimTextOnAltFallbackCopy: GetBool(reader, "Editor_TrimTextOnAltFallbackCopy"),
            CopyTextOnHighlight: GetBool(reader, "Editor_CopyTextOnHighlight"),
            PasteAction: GetEnum<PasteAction>(reader, "Editor_PasteAction"),
            TrimTextOnPaste: GetBool(reader, "Editor_TrimTextOnPaste"),
            PasteAltAction: GetEnum<PasteAction>(reader, "Editor_PasteAltAction"),
            TrimTextOnAltPaste: GetBool(reader, "Editor_TrimTextOnAltPaste"),
            MiddleClickPaste: GetBool(reader, "Editor_MiddleClickPaste"),
            CaretThickness: GetDouble(reader, "Editor_CaretThickness"),
            CaretColour: GetEnum<CaretColour>(reader, "Editor_CaretColour"),

            ClassTitle: GetString(reader, "BreakTimer_ClassTitle"),
            NextUp: GetString(reader, "BreakTimer_NextUp"),
            Segment: GetInt(reader, "BreakTimer_Segment")
        );
    }

    public async Task<int> Update(SettingsDataDto settings)
    {
        using SqliteConnection connection = new(ConnectionString);
        connection.Open();

        return await ExecuteNonQuery(
            connection,
            @"
                UPDATE
                    Settings
                SET
                    Application_ShowTrayIcon = @application_ShowTrayIcon,
                    Application_CheckForUpdates = @application_CheckForUpdates,

                    Notes_DefaultWidth = @notes_DefaultWidth,
                    Notes_DefaultHeight = @notes_DefaultHeight,
                    Notes_StartupPosition = @notes_StartupPosition,
                    Notes_MinimizeMode = @notes_MinimizeMode,
                    Notes_VisibilityMode = @notes_VisibilityMode,
                    Notes_HideTitleBar = @notes_HideTitleBar,
                    Notes_CycleColours = @notes_CycleColours,
                    Notes_ColourMode = @notes_ColourMode,
                    Notes_TransparencyMode = @notes_TransparencyMode,
                    Notes_OpaqueWhenFocused = @notes_OpaqueWhenFocused,
                    Notes_OpaqueOpacity = @notes_OpaqueOpacity,
                    Notes_TransparentOpacity = @notes_TransparentOpacity,

                    Editor_SpellCheck = @editor_SpellCheck,
                    Editor_AutoIndent = @editor_AutoIndent,
                    Editor_NewLineAtEnd = @editor_NewLineAtEnd,
                    Editor_KeepNewLineVisible = @editor_KeepNewLineVisible,
                    Editor_WrapText = @editor_WrapText,
                    Editor_StandardFontFamily = @editor_StandardFontFamily,
                    Editor_MonoFontFamily = @editor_MonoFontFamily,
                    Editor_UseMonoFont = @editor_UseMonoFont,
                    Editor_TabUsesSpaces = @editor_TabUsesSpaces,
                    Editor_ConvertIndentationOnPaste = @editor_ConvertIndentationOnPaste,
                    Editor_TabWidth = @editor_TabWidth,
                    Editor_CopyAction = @editor_CopyAction,
                    Editor_TrimTextOnCopy = @editor_TrimTextOnCopy,
                    Editor_CopyAltAction = @editor_CopyAltAction,
                    Editor_TrimTextOnAltCopy = @editor_TrimTextOnAltCopy,
                    Editor_CopyFallbackAction = @editor_CopyFallbackAction,
                    Editor_TrimTextOnFallbackCopy = @editor_TrimTextOnFallbackCopy,
                    Editor_CopyAltFallbackAction = @editor_CopyAltFallbackAction,
                    Editor_TrimTextOnAltFallbackCopy = @editor_TrimTextOnAltFallbackCopy,
                    Editor_CopyTextOnHighlight = @editor_CopyTextOnHighlight,
                    Editor_PasteAction = @editor_PasteAction,
                    Editor_TrimTextOnPaste = @editor_TrimTextOnPaste,
                    Editor_PasteAltAction = @editor_PasteAltAction,
                    Editor_TrimTextOnAltPaste = @editor_TrimTextOnAltPaste,
                    Editor_MiddleClickPaste = @editor_MiddleClickPaste,
                    Editor_CaretThickness = @editor_CaretThickness,
                    Editor_CaretColour = @editor_CaretColour,

                    BreakTimer_ClassTitle = @breakTimer_ClassTitle,
                    BreakTimer_NextUp = @breakTimer_NextUp,
                    BreakTimer_Segment = @breakTimer_Segment
                WHERE
                    Id = @id;
            ",
            parameters: [
                new("@application_ShowTrayIcon", settings.ShowTrayIcon),
                new("@application_CheckForUpdates", settings.CheckForUpdates),

                new("@notes_DefaultWidth", settings.DefaultNoteWidth),
                new("@notes_DefaultHeight", settings.DefaultNoteHeight),
                new("@notes_StartupPosition", settings.StartupPosition),
                new("@notes_MinimizeMode", settings.MinimizeMode),
                new("@notes_VisibilityMode", settings.VisibilityMode),
                new("@notes_HideTitleBar", settings.HideTitleBar),
                new("@notes_CycleColours", settings.CycleColours),
                new("@notes_ColourMode", settings.ColourMode),
                new("@notes_TransparencyMode", settings.TransparencyMode),
                new("@notes_OpaqueWhenFocused", settings.OpaqueWhenFocused),
                new("@notes_OpaqueOpacity", settings.OpaqueOpacity),
                new("@notes_TransparentOpacity", settings.TransparentOpacity),

                new("@editor_SpellCheck", settings.SpellCheck),
                new("@editor_AutoIndent", settings.AutoIndent),
                new("@editor_NewLineAtEnd", settings.NewLineAtEnd),
                new("@editor_KeepNewLineVisible", settings.KeepNewLineVisible),
                new("@editor_WrapText", settings.WrapText),
                new("@editor_StandardFontFamily", settings.StandardFontFamily),
                new("@editor_MonoFontFamily", settings.MonoFontFamily),
                new("@editor_UseMonoFont", settings.UseMonoFont),
                new("@editor_TabUsesSpaces", settings.TabUsesSpaces),
                new("@editor_ConvertIndentationOnPaste", settings.ConvertIndentationOnPaste),
                new("@editor_TabWidth", settings.TabWidth),
                new("@editor_CopyAction", settings.CopyAction),
                new("@editor_TrimTextOnCopy", settings.TrimTextOnCopy),
                new("@editor_CopyAltAction", settings.CopyAltAction),
                new("@editor_TrimTextOnAltCopy", settings.TrimTextOnAltCopy),
                new("@editor_CopyFallbackAction", settings.CopyFallbackAction),
                new("@editor_TrimTextOnFallbackCopy", settings.TrimTextOnFallbackCopy),
                new("@editor_CopyAltFallbackAction", settings.CopyAltFallbackAction),
                new("@editor_TrimTextOnAltFallbackCopy", settings.TrimTextOnAltFallbackCopy),
                new("@editor_CopyTextOnHighlight", settings.CopyTextOnHighlight),
                new("@editor_PasteAction", settings.PasteAction),
                new("@editor_TrimTextOnPaste", settings.TrimTextOnPaste),
                new("@editor_PasteAltAction", settings.PasteAltAction),
                new("@editor_TrimTextOnAltPaste", settings.TrimTextOnAltPaste),
                new("@editor_MiddleClickPaste", settings.MiddleClickPaste),
                new("@editor_CaretThickness", settings.CaretThickness),
                new("@editor_CaretColour", settings.CaretColour),

                new("@breakTimer_ClassTitle", settings.ClassTitle),
                new("@breakTimer_NextUp", settings.NextUp),
                new("@breakTimer_Segment", settings.Segment),

                new("@id", settings.Id)
            ]
        );
    }
}
