using Microsoft.Data.Sqlite;

using TimmyTools.Core.Configurations;
using TimmyTools.Core.DataTransferObjects;

namespace TimmyTools.Core.Repositories;

// Persistence for captured clipboard items. Modeled on NoteRepository: own table,
// own schema constant (referenced from DatabaseInitialiser when creating a fresh
// database), and a small CRUD surface plus the few clipboard-specific queries the
// monitor and the view model need (dedupe-by-hash, newest-first paging, pin
// toggling, and unpinned history trimming).
public class ClipboardRepository(DatabaseConfiguration databaseConfiguration) : BaseRepository(databaseConfiguration)
{
    public static readonly string TableName = "ClipboardHistory";

    public static readonly string TableSchema = @"
        (
            Id          INTEGER PRIMARY KEY AUTOINCREMENT,

            Content     TEXT    NOT NULL,
            ContentType TEXT    NOT NULL DEFAULT 'text',
            Preview     TEXT    NOT NULL DEFAULT '',

            CreatedUtc  INTEGER NOT NULL,

            Pinned      INTEGER NOT NULL DEFAULT 0,
            Hash        TEXT    NOT NULL DEFAULT ''
        )
    ";

    public async Task<int> Create(ClipboardEntryDto entry)
    {
        using SqliteConnection connection = new(ConnectionString);
        connection.Open();

        await ExecuteNonQuery(
            connection,
            @"
                INSERT INTO ClipboardHistory
                (
                    Content,
                    ContentType,
                    Preview,
                    CreatedUtc,
                    Pinned,
                    Hash
                )
                VALUES
                (
                    @content,
                    @contentType,
                    @preview,
                    @createdUtc,
                    @pinned,
                    @hash
                );
            ",
            parameters: [
                new("@content", entry.Content),
                new("@contentType", entry.ContentType),
                new("@preview", entry.Preview),
                new("@createdUtc", entry.CreatedUtc),
                new("@pinned", entry.Pinned),
                new("@hash", entry.Hash)
            ]
        );

        int newId = await GetLastInsertRowId(connection);

        return newId;
    }

    // Pinned items float to the top, then newest-first within each group, so the
    // most recent capture and every pinned item stay visible at a glance. The
    // limit caps how many rows the window loads, independent of the trim policy.
    public async Task<IEnumerable<ClipboardEntryDto>> GetRecent(int limit)
    {
        List<ClipboardEntryDto> entries = [];

        using SqliteConnection connection = new(ConnectionString);
        connection.Open();

        using SqliteDataReader reader = await ExecuteReader(
            connection,
            @"
                SELECT *
                FROM ClipboardHistory
                ORDER BY Pinned DESC, CreatedUtc DESC, Id DESC
                LIMIT @limit;
            ",
            parameters: [
                new("@limit", limit)
            ]
        );

        while (reader.Read())
            entries.Add(GetClipboardEntryDtoFromReader(reader));

        return entries;
    }

    // Dedupe guard for the monitor: a re-copy of the still-current clip should not
    // spam the history. Matches against the most recent capture only, so copying
    // text A, then B, then A again correctly records the second A.
    public async Task<bool> IsMostRecentHash(string hash)
    {
        using SqliteConnection connection = new(ConnectionString);
        connection.Open();

        object? result = await ExecuteScalar(
            connection,
            @"
                SELECT Hash
                FROM ClipboardHistory
                ORDER BY CreatedUtc DESC, Id DESC
                LIMIT 1;
            "
        );

        if (result is null or DBNull)
            return false;

        return string.Equals((string)result, hash, StringComparison.Ordinal);
    }

    public async Task SetPinned(int id, bool pinned)
    {
        using SqliteConnection connection = new(ConnectionString);
        connection.Open();

        await ExecuteNonQuery(
            connection,
            @"
                UPDATE ClipboardHistory
                SET Pinned = @pinned
                WHERE Id = @id;
            ",
            parameters: [
                new("@pinned", pinned),
                new("@id", id)
            ]
        );
    }

    public async Task Delete(int id)
    {
        using SqliteConnection connection = new(ConnectionString);
        connection.Open();

        await ExecuteNonQuery(
            connection,
            @"
                DELETE FROM ClipboardHistory
                WHERE Id = @id;
            ",
            parameters: [
                new("@id", id)
            ]
        );
    }

    // The user-facing "Clear all" must respect pins: pinned items are explicitly
    // kept by the user and survive a clear, matching the documented behaviour.
    public async Task ClearUnpinned()
    {
        using SqliteConnection connection = new(ConnectionString);
        connection.Open();

        await ExecuteNonQuery(
            connection,
            @"
                DELETE FROM ClipboardHistory
                WHERE Pinned = 0;
            "
        );
    }

    // History-cap enforcement after each capture: keep all pinned rows plus the
    // newest `keep` unpinned rows, delete the rest. Pinned items never count
    // against the cap, so a wall of pins cannot evict each other.
    public async Task TrimUnpinned(int keep)
    {
        using SqliteConnection connection = new(ConnectionString);
        connection.Open();

        await ExecuteNonQuery(
            connection,
            @"
                DELETE FROM ClipboardHistory
                WHERE Pinned = 0
                  AND Id NOT IN (
                      SELECT Id
                      FROM ClipboardHistory
                      WHERE Pinned = 0
                      ORDER BY CreatedUtc DESC, Id DESC
                      LIMIT @keep
                  );
            ",
            parameters: [
                new("@keep", keep)
            ]
        );
    }

    private static ClipboardEntryDto GetClipboardEntryDtoFromReader(SqliteDataReader reader)
    {
        ClipboardEntryDto dto = new(
            GetInt(reader, "Id"),

            GetString(reader, "Content"),
            GetString(reader, "ContentType"),
            GetString(reader, "Preview"),

            GetLong(reader, "CreatedUtc"),

            GetBool(reader, "Pinned"),
            GetString(reader, "Hash")
        );

        return dto;
    }
}
