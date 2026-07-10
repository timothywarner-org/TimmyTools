using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace TimmyTools.WpfUi.Helpers;

/// <summary>
/// Checklist support for note paragraphs.
///
/// A checklist item is an ordinary paragraph whose first character is a ballot-box
/// glyph. The glyph is the single source of truth for checked state: it is plain
/// text, so it survives the RTF round-trip used to persist notes, and it still reads
/// correctly if the note is pasted into Word or Outlook.
///
/// Checked state is deliberately NOT derived from the strikethrough formatting.
/// WPF's RTF reader restores TextDecorations onto a wrapping Span rather than the
/// inner Run, so TextRange.GetPropertyValue(Inline.TextDecorationsProperty) reports
/// an empty collection on any reloaded note even though the text renders struck.
/// Reading formatting to determine state would therefore silently break on reopen.
/// </summary>
internal static class ChecklistHelper
{
    /// <summary>U+2610 BALLOT BOX.</summary>
    public const char UncheckedGlyph = '☐';

    /// <summary>U+2611 BALLOT BOX WITH CHECK.</summary>
    public const char CheckedGlyph = '☑';

    /// <summary>
    /// U+FE0E VARIATION SELECTOR-15. Requests text (monochrome) presentation.
    /// Without it the checked glyph can fall back to Segoe UI Emoji and render as a
    /// coloured emoji of a different size, which shifts line metrics.
    /// </summary>
    public const char TextPresentationSelector = '︎';

    /// <summary>Separates the glyph from the item text.</summary>
    public const char GlyphSeparator = ' ';

    /// <summary>
    /// Neither Segoe UI nor the default note font (Georgia) contains the ballot-box
    /// code points, so every render relies on font fallback. Segoe UI Symbol supplies
    /// clean monochrome boxes; pinning it here keeps fallback deterministic.
    /// </summary>
    public const string GlyphFontFamily = "Segoe UI Symbol";

    /// <summary>Foreground applied to a checked item. Neutral grey reads as dimmed on light and dark themes alike.</summary>
    public static readonly Color CheckedForeground = Color.FromRgb(0x8A, 0x8A, 0x8A);

    /// <summary>The full text prefix inserted when converting a line into a checklist item.</summary>
    public static string UncheckedPrefix => $"{UncheckedGlyph}{GlyphSeparator}";

    /// <summary>True when <paramref name="value"/> is either ballot-box glyph.</summary>
    public static bool IsGlyph(char value)
        => value == UncheckedGlyph || value == CheckedGlyph;

    /// <summary>Returns the opposite glyph, preserving the checked/unchecked pair.</summary>
    public static char Toggle(char glyph)
        => glyph == CheckedGlyph ? UncheckedGlyph : CheckedGlyph;

    /// <summary>
    /// Builds the replacement text for a toggled glyph. The checked glyph carries a
    /// trailing variation selector; the unchecked one does not need it (U+2610 has no
    /// emoji presentation), so the string length differs between states. Callers must
    /// never assume a fixed width.
    /// </summary>
    public static string GlyphText(char glyph)
        => glyph == CheckedGlyph
            ? $"{CheckedGlyph}{TextPresentationSelector}"
            : UncheckedGlyph.ToString();

    /// <summary>
    /// Reads the checklist glyph at the start of <paramref name="paragraph"/>, ignoring
    /// leading whitespace. Returns null when the paragraph is not a checklist item.
    /// </summary>
    public static char? GetGlyph(Paragraph? paragraph)
    {
        if (paragraph is null)
        {
            return null;
        }

        string text = new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text;
        foreach (char character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                continue;
            }
            return IsGlyph(character) ? character : null;
        }
        return null;
    }

    /// <summary>True when the paragraph begins with either ballot-box glyph.</summary>
    public static bool IsChecklistItem(Paragraph? paragraph)
        => GetGlyph(paragraph) is not null;

    /// <summary>True when the paragraph begins with the checked glyph.</summary>
    public static bool IsChecked(Paragraph? paragraph)
        => GetGlyph(paragraph) == CheckedGlyph;

    /// <summary>
    /// Finds the position of the checklist glyph inside <paramref name="paragraph"/>,
    /// or null when the paragraph is not a checklist item. The returned pointer is
    /// positioned immediately before the glyph character.
    /// </summary>
    public static TextPointer? FindGlyphPosition(Paragraph? paragraph)
    {
        if (paragraph is null)
        {
            return null;
        }

        TextPointer? position = paragraph.ContentStart.GetInsertionPosition(LogicalDirection.Forward);
        while (position is not null && position.CompareTo(paragraph.ContentEnd) < 0)
        {
            if (position.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
            {
                string run = position.GetTextInRun(LogicalDirection.Forward);
                for (int offset = 0; offset < run.Length; offset++)
                {
                    char character = run[offset];
                    if (IsGlyph(character))
                    {
                        return position.GetPositionAtOffset(offset, LogicalDirection.Forward);
                    }
                    if (!char.IsWhiteSpace(character))
                    {
                        return null; // Real text before any glyph: not a checklist item.
                    }
                }
            }
            position = position.GetNextInsertionPosition(LogicalDirection.Forward);
        }
        return null;
    }

    /// <summary>
    /// Applies or removes the "checked" appearance across the whole paragraph:
    /// strikethrough plus a dimmed foreground. The strike line carries the signal by
    /// shape, so the state remains legible without relying on colour alone.
    /// </summary>
    public static void ApplyCheckedAppearance(Paragraph paragraph, bool isChecked)
    {
        if (isChecked)
        {
            // Style only the text after the glyph, never the glyph itself. Styling the whole
            // paragraph and then trying to exempt the box does not work: TextDecorations and
            // Foreground inherit, so a later "clear" on the glyph's sub-range loses to the value
            // set on the parent, and the strike line gets drawn straight through the box.
            TextRange range = BodyRange(paragraph);
            range.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Strikethrough);
            range.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(CheckedForeground));
        }
        else
        {
            // Clear across the whole inline tree, not just the body range. Both properties inherit,
            // and WPF's RTF reader restores a reloaded note's formatting onto a wrapping Span, so
            // applying null to the inner runs would leave that Span's strikethrough in force.
            // Clearing the local value everywhere lets Foreground fall back to the note's theme,
            // which keeps the text readable on light and dark notes alike.
            ClearInherited(paragraph.Inlines, Inline.TextDecorationsProperty);
            ClearInherited(paragraph.Inlines, TextElement.ForegroundProperty);
        }

        PinGlyphFont(paragraph);
    }

    /// <summary>
    /// The item's text, excluding the leading glyph and its separator. Falls back to the whole
    /// paragraph when no glyph is present.
    /// </summary>
    private static TextRange BodyRange(Paragraph paragraph)
    {
        TextPointer? glyphStart = FindGlyphPosition(paragraph);
        if (glyphStart is null)
        {
            return new TextRange(paragraph.ContentStart, paragraph.ContentEnd);
        }

        TextPointer? bodyStart = glyphStart.GetPositionAtOffset(GlyphLength(glyphStart), LogicalDirection.Forward);
        if (bodyStart is null || bodyStart.CompareTo(paragraph.ContentEnd) >= 0)
        {
            return new TextRange(paragraph.ContentEnd, paragraph.ContentEnd);
        }

        return new TextRange(bodyStart, paragraph.ContentEnd);
    }

    /// <summary>
    /// Removes a locally-set value for an inheriting property from every inline in the subtree, so
    /// the property falls back to what it inherits. Recursing matters because WPF's RTF reader parks
    /// a reloaded note's character formatting on a wrapping Span rather than on the inner Run, and
    /// applying null to the Run cannot override the value set on that Span.
    /// </summary>
    private static void ClearInherited(InlineCollection inlines, DependencyProperty property)
    {
        foreach (Inline inline in inlines)
        {
            inline.ClearValue(property);
            if (inline is Span span)
            {
                ClearInherited(span.Inlines, property);
            }
        }
    }

    /// <summary>
    /// Normalises the ballot-box run so the box always looks like a checkbox: a font that actually
    /// contains the glyph, and upright, unbolded, unstruck rendering.
    ///
    /// The glyph is inserted at the paragraph start and therefore inherits whatever character
    /// formatting the first inline carries, so a 48pt bold heading would otherwise produce a giant
    /// bold box. The strike is cleared separately because a checked item strikes the whole
    /// paragraph, and the line must not be drawn through the box itself.
    /// </summary>
    public static void PinGlyphFont(Paragraph paragraph)
    {
        TextPointer? glyphStart = FindGlyphPosition(paragraph);
        if (glyphStart is null)
        {
            return;
        }

        // Span the glyph plus any trailing variation selector.
        TextPointer? glyphEnd = glyphStart.GetPositionAtOffset(GlyphLength(glyphStart), LogicalDirection.Forward);
        if (glyphEnd is null)
        {
            return;
        }

        TextRange glyphRange = new(glyphStart, glyphEnd);
        glyphRange.ApplyPropertyValue(TextElement.FontFamilyProperty, new FontFamily(GlyphFontFamily));
        glyphRange.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
        glyphRange.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Normal);

        // An EMPTY collection, not null. TextDecorations inherits, so null leaves no local value and
        // an ancestor Span's strikethrough (which the RTF reader creates) would still cross the box.
        glyphRange.ApplyPropertyValue(Inline.TextDecorationsProperty, new TextDecorationCollection());
    }

    /// <summary>
    /// Number of characters the glyph occupies at <paramref name="glyphStart"/>: one for
    /// the ballot box, plus one when a variation selector follows it.
    /// </summary>
    public static int GlyphLength(TextPointer glyphStart)
    {
        string run = glyphStart.GetTextInRun(LogicalDirection.Forward);
        if (run.Length >= 2 && IsGlyph(run[0]) && run[1] == TextPresentationSelector)
        {
            return 2;
        }
        return 1;
    }
}
