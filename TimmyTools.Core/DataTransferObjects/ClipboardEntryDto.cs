namespace TimmyTools.Core.DataTransferObjects;

// One captured clipboard item. Immutable like every other DTO in the layer.
// ContentType is stored as a short discriminator string ("text" today; "image"
// and "files" reserved for later passes) so the schema does not need a fresh
// migration when richer formats land. CreatedUtc is Unix epoch seconds to match
// AppMetadata's LastUpdateCheck convention rather than introducing a second
// date-time storage format. Hash is a content fingerprint used to dedupe
// consecutive identical copies without comparing full payloads.
public record ClipboardEntryDto(
    int Id,

    string Content,
    string ContentType,
    string Preview,

    long CreatedUtc,

    bool Pinned,
    string Hash
);
