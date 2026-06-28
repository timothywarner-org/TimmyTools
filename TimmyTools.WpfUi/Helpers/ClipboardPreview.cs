namespace TimmyTools.WpfUi.Helpers;

// Shared one-line preview formatting for clipboard text. Used by both the capture
// service (when persisting a clip) and the view model (when seeding the live
// readout from the OS clipboard) so a clip looks identical in both places.
public static class ClipboardPreview
{
    private const int MaxPreviewLength = 120;

    // Collapses newlines and runs of whitespace to a single space and caps the
    // length so a multi-line clip renders as one tidy row.
    public static string Build(string text)
    {
        string collapsed = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        if (collapsed.Length <= MaxPreviewLength)
            return collapsed;

        return string.Concat(collapsed.AsSpan(0, MaxPreviewLength), "…");
    }
}
