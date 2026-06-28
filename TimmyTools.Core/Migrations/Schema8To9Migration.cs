namespace TimmyTools.Core.Migrations;

// Adds the Clipboard tool: a ClipboardHistory table for captured items and the
// Clipboard_* preference columns on the single-row Settings table (following the
// BreakTimer_* precedent). New columns carry sensible defaults so existing rows
// upgrade cleanly with no backfill required.
//
// Changed by Tim Warner, 2026 (GPL v2 section 2(a) notice).
public class Schema8To9Migration : SchemaMigration
{
    public override int TargetSchemaVersion => 8;
    public override int ResultingSchemaVersion => 9;
    public override string UpdateQuery => $@"
        CREATE TABLE IF NOT EXISTS ClipboardHistory
        (
            Id          INTEGER PRIMARY KEY AUTOINCREMENT,

            Content     TEXT    NOT NULL,
            ContentType TEXT    NOT NULL DEFAULT 'text',
            Preview     TEXT    NOT NULL DEFAULT '',

            CreatedUtc  INTEGER NOT NULL,

            Pinned      INTEGER NOT NULL DEFAULT 0,
            Hash        TEXT    NOT NULL DEFAULT ''
        );

        ALTER TABLE Settings ADD COLUMN Clipboard_AlwaysOnTop              INTEGER DEFAULT 0;
        ALTER TABLE Settings ADD COLUMN Clipboard_HistoryLimit            INTEGER DEFAULT 200;
        ALTER TABLE Settings ADD COLUMN Clipboard_ShowCopyNotification    INTEGER DEFAULT 1;
        ALTER TABLE Settings ADD COLUMN Clipboard_GlobalHotkey            TEXT    DEFAULT 'Ctrl+Alt+C';
        ALTER TABLE Settings ADD COLUMN Clipboard_IgnoreSensitiveClipboard INTEGER DEFAULT 1;

        -- Update schema version
        UPDATE SchemaInfo
        SET Version = {ResultingSchemaVersion}
        WHERE Id = 0;
    ";
}
