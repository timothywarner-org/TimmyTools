using TimmyTools.Core.DataTransferObjects;

namespace TimmyTools.WpfUi.Models;

// Observable view of a single clipboard history row. Only Pinned is mutable from
// the UI (the toggle); the rest are display-only and set once from the DTO. The
// full Content is kept off the visible surface but carried here so "click to
// restore" can put the original payload back on the clipboard, not the truncated
// preview.
public class ClipboardEntryModel : BaseModel
{
    public ClipboardEntryModel(ClipboardEntryDto dto)
    {
        Id = dto.Id;
        Content = dto.Content;
        ContentType = dto.ContentType;
        Preview = dto.Preview;
        CreatedUtc = DateTimeOffset.FromUnixTimeSeconds(dto.CreatedUtc).LocalDateTime;
        Pinned = dto.Pinned;
    }

    public int Id { get; }
    public string Content { get; }
    public string ContentType { get; }
    public string Preview { get; }
    public DateTime CreatedUtc { get; }

    public bool Pinned { get; set => SetProperty(ref field, value); }

    // A relative, screen-reader-friendly time label. Pairs with the icon so the
    // recency cue is never colour-only.
    public string TimeDisplay => CreatedUtc.ToString("g");
}
