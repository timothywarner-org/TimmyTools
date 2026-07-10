using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

using TimmyTools.Core.Enums;
using TimmyTools.WpfUi.Commands;
using TimmyTools.WpfUi.Controls.ContextMenus;
using TimmyTools.WpfUi.Helpers;

namespace TimmyTools.WpfUi.Controls;

public partial class NoteTextBoxControl : RichTextBox
{
    private readonly NoteTextBoxContextMenu _contextMenu;

    private bool _isUpdatingRtfContent;

    public NoteTextBoxControl() : base()
    {
        AcceptsReturn = true;
        AcceptsTab = true;
        AllowDrop = true;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;

        // Required for hyperlinks to raise navigation inside a RichTextBox. Must be set before
        // any RTF loads so links restored from a saved note are clickable too. While editing,
        // WPF follows links on Ctrl+Click; a plain click navigates only when the note is Locked
        // (IsReadOnly == true). Set here, ahead of LoadRtfContent.
        IsDocumentEnabled = true;
        AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(OnHyperlinkRequestNavigate));

        TextChanged += OnTextChanged;
        SelectionChanged += OnSelectionChanged;
        PreviewDragOver += OnPreviewDragOver;
        PreviewDrop += OnPreviewDrop;
        MouseDoubleClick += OnMouseDoubleClick;
        MouseUp += OnMouseUp;
        PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        PreviewKeyDown += OnPreviewKeyDown;
        ContextMenuOpening += OnContextMenuOpening;

        CopyCommand = new(Copy);
        CutCommand = new(Cut);
        PasteCommand = new(Paste);
        ClearCommand = new(ClearDocument);
        SetReadOnlyCommand = new(SetReadOnly);

        CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, OnCopyExecuted));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Cut, OnCutExecuted));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Paste, OnPasteExecuted));

        InputBindings.Add(new InputBinding(CopyCommand, new KeyGesture(Key.C, ModifierKeys.Control)));
        InputBindings.Add(new InputBinding(CopyCommand, new KeyGesture(Key.C, ModifierKeys.Control | ModifierKeys.Shift)));
        InputBindings.Add(new InputBinding(CutCommand, new KeyGesture(Key.X, ModifierKeys.Control)));
        InputBindings.Add(new InputBinding(CutCommand, new KeyGesture(Key.X, ModifierKeys.Control | ModifierKeys.Shift)));
        InputBindings.Add(new InputBinding(PasteCommand, new KeyGesture(Key.V, ModifierKeys.Control)));
        InputBindings.Add(new InputBinding(PasteCommand, new KeyGesture(Key.V, ModifierKeys.Control | ModifierKeys.Shift)));

        // Formatting shortcuts
        InputBindings.Add(new InputBinding(new RelayCommand(ToggleBold), new KeyGesture(Key.B, ModifierKeys.Control)));
        InputBindings.Add(new InputBinding(new RelayCommand(ToggleItalic), new KeyGesture(Key.I, ModifierKeys.Control)));
        InputBindings.Add(new InputBinding(new RelayCommand(ToggleUnderline), new KeyGesture(Key.U, ModifierKeys.Control)));
        InputBindings.Add(new InputBinding(new RelayCommand(ToggleChecklistItemAtCaret), new KeyGesture(Key.K, ModifierKeys.Control)));

        // Override built-in bold/italic/underline commands to use our handlers
        CommandBindings.Add(new CommandBinding(EditingCommands.ToggleBold, (s, e) => ToggleBold()));
        CommandBindings.Add(new CommandBinding(EditingCommands.ToggleItalic, (s, e) => ToggleItalic()));
        CommandBindings.Add(new CommandBinding(EditingCommands.ToggleUnderline, (s, e) => ToggleUnderline()));

        _contextMenu = new NoteTextBoxContextMenu(this);
        ContextMenu = _contextMenu;

        Loaded += (_, _) => UpdateCaretAppearance();
    }

    #region DependencyProperties

    // RTF Content (for binding to model)
    public string RtfContent
    {
        get => (string)GetValue(RtfContentProperty);
        set => SetValue(RtfContentProperty, value);
    }
    public static readonly DependencyProperty RtfContentProperty = DependencyProperty.Register(
        nameof(RtfContent),
        typeof(string),
        typeof(NoteTextBoxControl),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRtfContentChanged)
    );

    // Caret
    public double CaretThickness
    {
        get => (double)GetValue(CaretThicknessProperty);
        set => SetValue(CaretThicknessProperty, value);
    }
    public static readonly DependencyProperty CaretThicknessProperty = DependencyProperty.Register(
        nameof(CaretThickness),
        typeof(double),
        typeof(NoteTextBoxControl),
        new PropertyMetadata(2.0, OnCaretPropertyChanged)
    );

    public CaretColour CaretColour
    {
        get => (CaretColour)GetValue(CaretColourProperty);
        set => SetValue(CaretColourProperty, value);
    }
    public static readonly DependencyProperty CaretColourProperty = DependencyProperty.Register(
        nameof(CaretColour),
        typeof(CaretColour),
        typeof(NoteTextBoxControl),
        new PropertyMetadata(CaretColour.Default, OnCaretPropertyChanged)
    );

    // General
    public bool AutoIndent
    {
        get => (bool)GetValue(AutoIndentProperty);
        set => SetValue(AutoIndentProperty, value);
    }
    public static readonly DependencyProperty AutoIndentProperty = DependencyProperty.Register(nameof(AutoIndent), typeof(bool), typeof(NoteTextBoxControl));

    public bool NewLineAtEnd
    {
        get => (bool)GetValue(NewLineAtEndProperty);
        set => SetValue(NewLineAtEndProperty, value);
    }
    public static readonly DependencyProperty NewLineAtEndProperty = DependencyProperty.Register(nameof(NewLineAtEnd), typeof(bool), typeof(NoteTextBoxControl));

    public bool KeepNewLineAtEndVisible
    {
        get => (bool)GetValue(KeepNewLineAtEndVisibleProperty);
        set => SetValue(KeepNewLineAtEndVisibleProperty, value);
    }
    public static readonly DependencyProperty KeepNewLineAtEndVisibleProperty = DependencyProperty.Register(nameof(KeepNewLineAtEndVisible), typeof(bool), typeof(NoteTextBoxControl));

    public bool WrapText
    {
        get => (bool)GetValue(WrapTextProperty);
        set => SetValue(WrapTextProperty, value);
    }
    public static readonly DependencyProperty WrapTextProperty = DependencyProperty.Register(nameof(WrapText), typeof(bool), typeof(NoteTextBoxControl), new PropertyMetadata(OnWrapTextChanged));

    // Fonts
    public string StandardFontFamily
    {
        get => (string)GetValue(StandardFontFamilyProperty);
        set => SetValue(StandardFontFamilyProperty, value);
    }
    public static readonly DependencyProperty StandardFontFamilyProperty = DependencyProperty.Register(nameof(StandardFontFamily), typeof(string), typeof(NoteTextBoxControl), new PropertyMetadata(OnFontPropertyChanged));

    public string MonoFontFamily
    {
        get => (string)GetValue(MonoFontFamilyProperty);
        set => SetValue(MonoFontFamilyProperty, value);
    }
    public static readonly DependencyProperty MonoFontFamilyProperty = DependencyProperty.Register(nameof(MonoFontFamily), typeof(string), typeof(NoteTextBoxControl), new PropertyMetadata(OnFontPropertyChanged));

    public bool UseMonoFont
    {
        get => (bool)GetValue(UseMonoFontProperty);
        set => SetValue(UseMonoFontProperty, value);
    }
    public static readonly DependencyProperty UseMonoFontProperty = DependencyProperty.Register(nameof(UseMonoFont), typeof(bool), typeof(NoteTextBoxControl), new PropertyMetadata(OnFontPropertyChanged));

    // Indentation
    public bool TabSpaces
    {
        get => (bool)GetValue(TabSpacesProperty);
        set => SetValue(TabSpacesProperty, value);
    }
    public static readonly DependencyProperty TabSpacesProperty = DependencyProperty.Register(nameof(TabSpaces), typeof(bool), typeof(NoteTextBoxControl));

    public bool ConvertIndentation
    {
        get => (bool)GetValue(ConvertIndentationProperty);
        set => SetValue(ConvertIndentationProperty, value);
    }
    public static readonly DependencyProperty ConvertIndentationProperty = DependencyProperty.Register(nameof(ConvertIndentation), typeof(bool), typeof(NoteTextBoxControl));

    public int TabWidth
    {
        get => (int)GetValue(TabWidthProperty);
        set => SetValue(TabWidthProperty, value);
    }
    public static readonly DependencyProperty TabWidthProperty = DependencyProperty.Register(nameof(TabWidth), typeof(int), typeof(NoteTextBoxControl));

    // Copy and Paste
    public CopyAction CopyAction
    {
        get => (CopyAction)GetValue(CopyActionProperty);
        set => SetValue(CopyActionProperty, value);
    }
    public static readonly DependencyProperty CopyActionProperty = DependencyProperty.Register(nameof(CopyAction), typeof(CopyAction), typeof(NoteTextBoxControl));

    public bool TrimTextOnCopy
    {
        get => (bool)GetValue(TrimTextOnCopyProperty);
        set => SetValue(TrimTextOnCopyProperty, value);
    }
    public static readonly DependencyProperty TrimTextOnCopyProperty = DependencyProperty.Register(nameof(TrimTextOnCopy), typeof(bool), typeof(NoteTextBoxControl));

    public CopyAction CopyAltAction
    {
        get => (CopyAction)GetValue(CopyAltActionProperty);
        set => SetValue(CopyAltActionProperty, value);
    }
    public static readonly DependencyProperty CopyAltActionProperty = DependencyProperty.Register(nameof(CopyAltAction), typeof(CopyAction), typeof(NoteTextBoxControl));

    public bool TrimTextOnAltCopy
    {
        get => (bool)GetValue(TrimTextOnAltCopyProperty);
        set => SetValue(TrimTextOnAltCopyProperty, value);
    }
    public static readonly DependencyProperty TrimTextOnAltCopyProperty = DependencyProperty.Register(nameof(TrimTextOnAltCopy), typeof(bool), typeof(NoteTextBoxControl));

    public CopyFallbackAction CopyFallbackAction
    {
        get => (CopyFallbackAction)GetValue(CopyFallbackActionProperty);
        set => SetValue(CopyFallbackActionProperty, value);
    }
    public static readonly DependencyProperty CopyFallbackActionProperty = DependencyProperty.Register(nameof(CopyFallbackAction), typeof(CopyFallbackAction), typeof(NoteTextBoxControl));

    public bool TrimTextOnFallbackCopy
    {
        get => (bool)GetValue(TrimTextOnFallbackCopyProperty);
        set => SetValue(TrimTextOnFallbackCopyProperty, value);
    }
    public static readonly DependencyProperty TrimTextOnFallbackCopyProperty = DependencyProperty.Register(nameof(TrimTextOnFallbackCopy), typeof(bool), typeof(NoteTextBoxControl));

    public CopyFallbackAction CopyAltFallbackAction
    {
        get => (CopyFallbackAction)GetValue(CopyAltFallbackActionProperty);
        set => SetValue(CopyAltFallbackActionProperty, value);
    }
    public static readonly DependencyProperty CopyAltFallbackActionProperty = DependencyProperty.Register(nameof(CopyAltFallbackAction), typeof(CopyFallbackAction), typeof(NoteTextBoxControl));

    public bool TrimTextOnAltFallbackCopy
    {
        get => (bool)GetValue(TrimTextOnAltFallbackCopyProperty);
        set => SetValue(TrimTextOnAltFallbackCopyProperty, value);
    }
    public static readonly DependencyProperty TrimTextOnAltFallbackCopyProperty = DependencyProperty.Register(nameof(TrimTextOnAltFallbackCopy), typeof(bool), typeof(NoteTextBoxControl));

    public bool AutoCopy
    {
        get => (bool)GetValue(AutoCopyProperty);
        set => SetValue(AutoCopyProperty, value);
    }
    public static readonly DependencyProperty AutoCopyProperty = DependencyProperty.Register(nameof(AutoCopy), typeof(bool), typeof(NoteTextBoxControl));

    public PasteAction PasteAction
    {
        get => (PasteAction)GetValue(PasteActionProperty);
        set => SetValue(PasteActionProperty, value);
    }
    public static readonly DependencyProperty PasteActionProperty = DependencyProperty.Register(nameof(PasteAction), typeof(PasteAction), typeof(NoteTextBoxControl));

    public bool TrimTextOnPaste
    {
        get => (bool)GetValue(TrimTextOnPasteProperty);
        set => SetValue(TrimTextOnPasteProperty, value);
    }
    public static readonly DependencyProperty TrimTextOnPasteProperty = DependencyProperty.Register(nameof(TrimTextOnPaste), typeof(bool), typeof(NoteTextBoxControl));

    public PasteAction PasteAltAction
    {
        get => (PasteAction)GetValue(PasteAltActionProperty);
        set => SetValue(PasteAltActionProperty, value);
    }
    public static readonly DependencyProperty PasteAltActionProperty = DependencyProperty.Register(nameof(PasteAltAction), typeof(PasteAction), typeof(NoteTextBoxControl));

    public bool TrimTextOnAltPaste
    {
        get => (bool)GetValue(TrimTextOnAltPasteProperty);
        set => SetValue(TrimTextOnAltPasteProperty, value);
    }
    public static readonly DependencyProperty TrimTextOnAltPasteProperty = DependencyProperty.Register(nameof(TrimTextOnAltPaste), typeof(bool), typeof(NoteTextBoxControl));

    public bool MiddleClickPaste
    {
        get => (bool)GetValue(MiddleClickPasteProperty);
        set => SetValue(MiddleClickPasteProperty, value);
    }
    public static readonly DependencyProperty MiddleClickPasteProperty = DependencyProperty.Register(nameof(MiddleClickPaste), typeof(bool), typeof(NoteTextBoxControl));

    #endregion

    #region Commands

    public RelayCommand CopyCommand;
    public RelayCommand CutCommand;
    public RelayCommand PasteCommand;
    public RelayCommand ClearCommand;
    public RelayCommand<bool> SetReadOnlyCommand;

    #endregion

    #region Public Helpers

    public string GetPlainText()
    {
        TextRange range = new(Document.ContentStart, Document.ContentEnd);
        string text = range.Text;
        // FlowDocument always appends a trailing \r\n; remove it for consistency
        if (text.EndsWith("\r\n"))
            text = text[..^2];
        return text;
    }

    public bool HasSelectedText
        => !Selection.IsEmpty;

    public int LineCount()
    {
        string text = HasSelectedText ? Selection.Text : GetPlainText();
        if (string.IsNullOrEmpty(text))
            return 0;
        return text.Split(Environment.NewLine).Length;
    }

    public int WordCount()
    {
        string text = HasSelectedText ? Selection.Text : GetPlainText();
        if (string.IsNullOrEmpty(text))
            return 0;
        return text.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length;
    }

    public int CharCount()
    {
        string text = HasSelectedText ? Selection.Text : GetPlainText();
        if (string.IsNullOrEmpty(text))
            return 0;
        return text.Length - text.Count(c => c == '\n' || c == '\r');
    }

    public string GetCurrentLineText()
    {
        TextPointer lineStart = CaretPosition.GetLineStartPosition(0);
        TextPointer? lineEnd = CaretPosition.GetLineStartPosition(1);
        if (lineStart == null)
            return "";
        TextRange lineRange = new(lineStart, lineEnd ?? Document.ContentEnd);
        return lineRange.Text.TrimEnd('\r', '\n');
    }

    #endregion

    #region RTF Serialization

    private static void OnRtfContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteTextBoxControl control && !control._isUpdatingRtfContent)
            control.LoadRtfContent((string)e.NewValue);
    }

    private void LoadRtfContent(string rtf)
    {
        _isUpdatingRtfContent = true;
        try
        {
            TextRange range = new(Document.ContentStart, Document.ContentEnd);
            if (!string.IsNullOrEmpty(rtf) && rtf.TrimStart().StartsWith(@"{\rtf"))
            {
                using MemoryStream stream = new(Encoding.UTF8.GetBytes(rtf));
                range.Load(stream, DataFormats.Rtf);
            }
            else
            {
                range.Text = rtf ?? "";
            }
        }
        finally
        {
            _isUpdatingRtfContent = false;
        }
    }

    private string SerializeRtfContent()
    {
        TextRange range = new(Document.ContentStart, Document.ContentEnd);
        using MemoryStream stream = new();
        range.Save(stream, DataFormats.Rtf);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    #endregion

    #region Formatting

    public void ApplyFontFamily(string fontFamily)
        => Selection.ApplyPropertyValue(TextElement.FontFamilyProperty, new FontFamily(fontFamily));

    public void ApplyFontSize(double size)
        => Selection.ApplyPropertyValue(TextElement.FontSizeProperty, size);

    public void ApplyForeground(Color color)
        => Selection.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(color));

    // A null colour clears any existing highlight (the "None" choice in the menu).
    public void ApplyHighlight(Color? color)
        => Selection.ApplyPropertyValue(
            TextElement.BackgroundProperty,
            color is Color c ? new SolidColorBrush(c) : null);

    public void InsertHyperlink(Uri navigateUri)
    {
        if (Selection.IsEmpty)
        {
            // No selection: drop the URL itself in as the clickable text at the caret.
            Hyperlink link = new(new Run(navigateUri.ToString()))
            {
                NavigateUri = navigateUri
            };
            if (CaretPosition.Paragraph is Paragraph paragraph)
                paragraph.Inlines.Add(link);
        }
        else
        {
            // Wrap the current selection. The two-pointer constructor re-parents the selected
            // inlines under the new Hyperlink, preserving their text and formatting.
            _ = new Hyperlink(Selection.Start, Selection.End)
            {
                NavigateUri = navigateUri
            };
        }
    }

    private void OnHyperlinkRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // No registered handler for the scheme, or shell refused; ignore silently.
        }
        e.Handled = true;
    }

    public void ToggleBold()
    {
        object weight = Selection.GetPropertyValue(TextElement.FontWeightProperty);
        FontWeight newWeight = weight is FontWeight fw && fw == FontWeights.Bold
            ? FontWeights.Normal
            : FontWeights.Bold;
        Selection.ApplyPropertyValue(TextElement.FontWeightProperty, newWeight);
    }

    public void ToggleItalic()
    {
        object style = Selection.GetPropertyValue(TextElement.FontStyleProperty);
        FontStyle newStyle = style is FontStyle fs && fs == FontStyles.Italic
            ? FontStyles.Normal
            : FontStyles.Italic;
        Selection.ApplyPropertyValue(TextElement.FontStyleProperty, newStyle);
    }

    public void ToggleUnderline()
    {
        object decorations = Selection.GetPropertyValue(Inline.TextDecorationsProperty);
        if (decorations is TextDecorationCollection tdc && tdc.Count > 0)
            Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, new TextDecorationCollection());
        else
            Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Underline);
    }

    public void ClearFormatting()
        => Selection.ClearAllProperties();

    public void ApplyAlignment(TextAlignment alignment)
    {
        TextPointer start = Selection.Start;
        TextPointer end = Selection.IsEmpty ? Selection.Start : Selection.End;

        Block? startBlock = start.Paragraph ?? (Block?)start.GetAdjacentElement(LogicalDirection.Forward);
        Block? endBlock = end.Paragraph ?? (Block?)end.GetAdjacentElement(LogicalDirection.Backward);

        if (startBlock == null)
            return;

        Block? block = startBlock;
        while (block != null)
        {
            block.TextAlignment = alignment;
            if (block == endBlock)
                break;
            block = block.NextBlock;
        }
    }

    public void ApplyList(TextMarkerStyle markerStyle)
    {
        TextPointer start = Selection.Start;
        TextPointer end = Selection.IsEmpty ? Selection.Start : Selection.End;

        Paragraph? startParagraph = start.Paragraph;
        Paragraph? endParagraph = end.Paragraph;

        if (startParagraph == null)
            return;

        // If removing list (None), unwrap ListItems back to document-level paragraphs
        if (markerStyle == TextMarkerStyle.None)
        {
            // When paragraphs are inside ListItems, walk via ListItem siblings
            ListItem? startItem = startParagraph.Parent as ListItem;
            ListItem? endItem = endParagraph?.Parent as ListItem;

            if (startItem == null)
                return;

            List<ListItem> itemsToUnwrap = [];
            ListItem? currentItem = startItem;
            while (currentItem != null)
            {
                itemsToUnwrap.Add(currentItem);
                if (currentItem == endItem)
                    break;
                currentItem = currentItem.NextListItem;
            }

            foreach (ListItem item in itemsToUnwrap)
            {
                if (item.Parent is List parentList)
                {
                    List<Paragraph> paragraphs = [.. item.Blocks.OfType<Paragraph>()];
                    Block insertAfter = parentList;
                    foreach (Paragraph p in paragraphs)
                    {
                        item.Blocks.Remove(p);
                        Document.Blocks.InsertAfter(insertAfter, p);
                        insertAfter = p;
                    }
                    parentList.ListItems.Remove(item);
                    if (!parentList.ListItems.Any())
                        Document.Blocks.Remove(parentList);
                }
            }
            return;
        }

        // Check if already in a list — change marker style on all affected lists
        if (startParagraph.Parent is ListItem)
        {
            HashSet<List> listsToUpdate = [];
            ListItem? startItem = startParagraph.Parent as ListItem;
            ListItem? endItem = endParagraph?.Parent as ListItem;
            ListItem? currentItem = startItem;
            while (currentItem != null)
            {
                if (currentItem.Parent is List parentList)
                    listsToUpdate.Add(parentList);
                if (currentItem == endItem)
                    break;
                currentItem = currentItem.NextListItem;
            }
            foreach (List list in listsToUpdate)
            {
                list.MarkerStyle = markerStyle;
            }
            return;
        }

        // Collect paragraphs to wrap — traverse as Block to handle non-Paragraph blocks
        List<Paragraph> toWrap = [];
        Block? block = startParagraph;
        while (block != null)
        {
            if (block is Paragraph p)
                toWrap.Add(p);
            if (block == endParagraph)
                break;
            block = block.NextBlock;
        }

        if (toWrap.Count == 0)
            return;

        // Insert new List before the first paragraph
        List newList = new() { MarkerStyle = markerStyle };
        Document.Blocks.InsertBefore(toWrap[0], newList);

        foreach (Paragraph p in toWrap)
        {
            Document.Blocks.Remove(p);
            ListItem listItem = new(p);
            newList.ListItems.Add(listItem);
        }
    }

    /// <summary>
    /// Converts every paragraph touched by the selection into a checklist item, or back
    /// into a plain paragraph when they are already checklist items. Mirrors the toggle
    /// behaviour of the bullet/numbered list menu entries.
    /// </summary>
    public void ApplyChecklist()
    {
        List<Paragraph> paragraphs = GetSelectedParagraphs();
        if (paragraphs.Count == 0)
            return;

        // Toggle semantics: if every touched line is already a checklist item, strip them.
        bool removing = paragraphs.All(ChecklistHelper.IsChecklistItem);

        // One undo unit for the whole gesture. This also coalesces TextChanged, so the note is
        // re-serialized to RTF once instead of once per line edit.
        BeginChange();
        try
        {
            foreach (Paragraph paragraph in paragraphs)
            {
                if (removing)
                    RemoveChecklistItem(paragraph);
                else if (!ChecklistHelper.IsChecklistItem(paragraph))
                    MakeChecklistItem(paragraph);
            }
        }
        finally
        {
            EndChange();
        }
    }

    /// <summary>
    /// Checks or unchecks the line under the caret (Ctrl+K, and the "Toggle check" menu entry).
    /// A line that is not yet a checklist item becomes one and is checked in a single step, so the
    /// shortcut always does something visible rather than silently ignoring the keypress.
    /// </summary>
    public void ToggleChecklistItemAtCaret()
    {
        if (IsReadOnly || CaretPosition.Paragraph is not Paragraph paragraph)
            return;

        if (ChecklistHelper.IsChecklistItem(paragraph))
        {
            ToggleChecklistItem(paragraph);
            return;
        }

        // Blank lines have nothing to check off; converting one would leave a stray empty item.
        if (new TextRange(paragraph.ContentStart, paragraph.ContentEnd).IsEmpty)
            return;

        // Convert and check as one gesture. Change blocks are reference-counted, so nesting the
        // inner block opened by ToggleChecklistItem keeps this a single undo unit.
        BeginChange();
        try
        {
            MakeChecklistItem(paragraph);
            ToggleChecklistItem(paragraph);
        }
        finally
        {
            EndChange();
        }
    }

    /// <summary>
    /// Flips a checklist item between checked and unchecked, swapping the glyph and
    /// applying or removing the struck-and-dimmed appearance.
    /// </summary>
    private void ToggleChecklistItem(Paragraph paragraph)
    {
        TextPointer? glyphStart = ChecklistHelper.FindGlyphPosition(paragraph);
        if (glyphStart is null)
            return;

        int glyphLength = ChecklistHelper.GlyphLength(glyphStart);
        TextPointer? glyphEnd = glyphStart.GetPositionAtOffset(glyphLength, LogicalDirection.Forward);
        if (glyphEnd is null)
            return;

        char currentGlyph = ChecklistHelper.IsChecked(paragraph)
            ? ChecklistHelper.CheckedGlyph
            : ChecklistHelper.UncheckedGlyph;
        char nextGlyph = ChecklistHelper.Toggle(currentGlyph);

        // One undo unit: a single click or Ctrl+K must reverse in a single Ctrl+Z, never leaving
        // the glyph swapped but the strikethrough half-applied.
        BeginChange();
        try
        {
            // Replace just the glyph. Assigning TextRange.Text collapses the range's runs, so the
            // surrounding item text is untouched.
            new TextRange(glyphStart, glyphEnd).Text = ChecklistHelper.GlyphText(nextGlyph);

            ChecklistHelper.ApplyCheckedAppearance(paragraph, nextGlyph == ChecklistHelper.CheckedGlyph);
        }
        finally
        {
            EndChange();
        }
    }

    /// <summary>
    /// Prefixes a paragraph with the unchecked glyph, leaving its text formatting alone.
    ///
    /// Deliberately calls PinGlyphFont rather than ApplyCheckedAppearance(false). The latter also
    /// runs ClearInherited over Foreground and TextDecorations across every inline, which strips the
    /// author's own colour and underline from a line that was never checked in the first place.
    /// Converting a line to a checklist item must only add a box; it must inherit the line's font,
    /// size, weight and colour untouched.
    /// </summary>
    private static void MakeChecklistItem(Paragraph paragraph)
    {
        paragraph.ContentStart.InsertTextInRun(ChecklistHelper.UncheckedPrefix);
        ChecklistHelper.PinGlyphFont(paragraph);
    }

    /// <summary>Strips the leading glyph and separator, and clears any checked appearance.</summary>
    private static void RemoveChecklistItem(Paragraph paragraph)
    {
        TextPointer? glyphStart = ChecklistHelper.FindGlyphPosition(paragraph);
        if (glyphStart is null)
            return;

        // Clear the struck/dimmed look before removing the glyph, while the paragraph
        // still reports as a checklist item.
        ChecklistHelper.ApplyCheckedAppearance(paragraph, isChecked: false);

        // Re-find: ApplyCheckedAppearance can re-split runs and invalidate the pointer.
        glyphStart = ChecklistHelper.FindGlyphPosition(paragraph);
        if (glyphStart is null)
            return;

        int removeLength = ChecklistHelper.GlyphLength(glyphStart);

        // Swallow one following separator space so the text does not keep a stray indent.
        TextPointer? afterGlyph = glyphStart.GetPositionAtOffset(removeLength, LogicalDirection.Forward);
        if (afterGlyph is not null)
        {
            string following = afterGlyph.GetTextInRun(LogicalDirection.Forward);
            if (following.Length > 0 && following[0] == ChecklistHelper.GlyphSeparator)
                removeLength++;
        }

        TextPointer? removeEnd = glyphStart.GetPositionAtOffset(removeLength, LogicalDirection.Forward);
        if (removeEnd is not null)
            new TextRange(glyphStart, removeEnd).Text = string.Empty;
    }

    /// <summary>
    /// Every paragraph the selection touches, in document order, including paragraphs nested
    /// inside ListItems. An empty selection yields the single paragraph under the caret.
    /// </summary>
    private List<Paragraph> GetSelectedParagraphs()
    {
        TextPointer start = Selection.Start;
        TextPointer end = Selection.IsEmpty ? Selection.Start : Selection.End;

        Paragraph? startParagraph = start.Paragraph;
        Paragraph? endParagraph = end.Paragraph ?? startParagraph;

        if (startParagraph is null)
            return [];

        // Walk top-level blocks rather than paragraph-to-paragraph. A selection can span loose
        // paragraphs and Lists in any order, and a Paragraph's NextBlock never crosses out of the
        // ListItem that contains it, so a paragraph-typed walk stops at the first List it meets.
        Block? startBlock = TopLevelBlockOf(startParagraph);
        Block? endBlock = TopLevelBlockOf(endParagraph);

        List<Paragraph> paragraphs = [];
        bool started = false;
        bool finished = false;

        Block? block = startBlock;
        while (block is not null && !finished)
        {
            CollectParagraphs(block, startParagraph, endParagraph, paragraphs, ref started, ref finished);
            if (block == endBlock)
                break;
            block = block.NextBlock;
        }

        return paragraphs;
    }

    /// <summary>The document-level block containing <paramref name="element"/>.</summary>
    private Block? TopLevelBlockOf(TextElement? element)
    {
        DependencyObject? current = element;
        while (current is TextElement textElement)
        {
            if (textElement is Block candidate && ReferenceEquals(textElement.Parent, Document))
                return candidate;
            current = textElement.Parent;
        }
        return null;
    }

    /// <summary>
    /// Depth-first collection of the paragraphs between <paramref name="startParagraph"/> and
    /// <paramref name="endParagraph"/> inclusive, descending into Lists so nested items are found.
    /// </summary>
    private static void CollectParagraphs(
        Block block,
        Paragraph startParagraph,
        Paragraph endParagraph,
        List<Paragraph> paragraphs,
        ref bool started,
        ref bool finished)
    {
        if (finished)
            return;

        if (block is Paragraph paragraph)
        {
            if (!started && paragraph == startParagraph)
                started = true;

            if (started)
            {
                paragraphs.Add(paragraph);
                if (paragraph == endParagraph)
                    finished = true;
            }
            return;
        }

        if (block is List list)
        {
            foreach (ListItem item in list.ListItems)
            {
                foreach (Block childBlock in item.Blocks)
                {
                    CollectParagraphs(childBlock, startParagraph, endParagraph, paragraphs, ref started, ref finished);
                    if (finished)
                        return;
                }
            }
        }
    }

    public void ApplyTabSpacing(double spacing)
    {
        if (spacing <= 0)
            return;

        foreach (Block block in Document.Blocks)
        {
            ApplyTabSpacingToBlock(block, spacing);
        }
    }

    private static void ApplyTabSpacingToBlock(Block block, double spacing)
    {
        if (block is Paragraph paragraph)
        {
            // Recalculate margin based on new tab spacing.
            // If the paragraph has left margin, re-snap it to the new spacing grid.
            double currentLeft = paragraph.Margin.Left;
            if (currentLeft > 0)
            {
                // Keep the indent but round to new tab spacing
                double newLeft = Math.Round(currentLeft / spacing) * spacing;
                if (newLeft < spacing)
                    newLeft = 0;
                paragraph.Margin = new Thickness(newLeft, paragraph.Margin.Top, paragraph.Margin.Right, paragraph.Margin.Bottom);
            }
        }
        else if (block is List list)
        {
            list.MarkerOffset = spacing;
            list.Padding = new Thickness(spacing, 0, 0, 0);
            foreach (ListItem item in list.ListItems)
            {
                foreach (Block childBlock in item.Blocks)
                {
                    ApplyTabSpacingToBlock(childBlock, spacing);
                }
            }
        }
    }

    public void ApplyCaseTransform(CaseTransform caseType)
    {
        TextInfo textInfo = CultureInfo.CurrentCulture.TextInfo;

        if (!Selection.IsEmpty)
        {
            string text = Selection.Text;
            if (string.IsNullOrEmpty(text))
                return;

            Selection.Text = TransformCase(text, caseType, textInfo);
            return;
        }

        // When no selection, iterate through Run inlines to preserve RTF formatting
        foreach (Block block in Document.Blocks)
        {
            TransformBlockCase(block, caseType, textInfo);
        }
    }

    private static string TransformCase(string text, CaseTransform caseType, TextInfo textInfo)
    {
        return caseType switch
        {
            CaseTransform.Lower => textInfo.ToLower(text),
            CaseTransform.Upper => textInfo.ToUpper(text),
            CaseTransform.Title => textInfo.ToTitleCase(textInfo.ToLower(text)),
            _ => text
        };
    }

    private static void TransformBlockCase(Block block, CaseTransform caseType, TextInfo textInfo)
    {
        if (block is Paragraph paragraph)
        {
            foreach (Inline inline in paragraph.Inlines)
            {
                if (inline is Run run)
                    run.Text = TransformCase(run.Text, caseType, textInfo);
            }
        }
        else if (block is List list)
        {
            foreach (ListItem item in list.ListItems)
            {
                foreach (Block childBlock in item.Blocks)
                {
                    TransformBlockCase(childBlock, caseType, textInfo);
                }
            }
        }
    }

    #endregion

    #region Copy / Paste

    private static bool IsShiftPressed(bool exclusive = false)
        => exclusive ? (Keyboard.Modifiers == ModifierKeys.Shift) : Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

    private void OnCopyExecuted(object sender, ExecutedRoutedEventArgs e)
        => Copy();
    private void OnCutExecuted(object sender, ExecutedRoutedEventArgs e)
        => Cut();
    private void OnPasteExecuted(object sender, ExecutedRoutedEventArgs e)
        => Paste();

    private new void Copy()
        => CopyText();

    private new void Cut()
        => CopyText(true);

    private void CopyText(bool cut = false)
    {
        string copiedText;

        if (IsShiftPressed())
            copiedText = GetTextForCopyAction(CopyAltAction, CopyAltFallbackAction, TrimTextOnAltCopy, TrimTextOnAltFallbackCopy, cut);
        else
            copiedText = GetTextForCopyAction(CopyAction, CopyFallbackAction, TrimTextOnCopy, TrimTextOnFallbackCopy, cut);

        if (string.IsNullOrEmpty(copiedText))
            return;

        try
        {
            Clipboard.SetDataObject(copiedText);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // Clipboard locked by another application; ignore silently
        }
    }

    private string GetTextForCopyAction(CopyAction action, CopyFallbackAction fallbackAction, bool trim, bool fallbackTrim, bool cut)
    {
        string text;

        if (!HasSelectedText)
        {
            text = fallbackAction switch
            {
                CopyFallbackAction.CopyLine => HandleLineCopyOrCut(fallbackTrim, cut),
                CopyFallbackAction.CopyNote => HandleNoteCopyOrCut(fallbackTrim, cut),
                _ => string.Empty
            };
            return text;
        }

        text = action switch
        {
            CopyAction.CopySelected => HandleSelectedTextCopyOrCut(trim, cut),
            CopyAction.CopyLine => HandleLineCopyOrCut(trim, cut),
            CopyAction.CopyAll => HandleNoteCopyOrCut(trim, cut),
            _ => string.Empty
        };

        return text;
    }

    private string HandleSelectedTextCopyOrCut(bool trim, bool cut)
    {
        string text = Selection.Text;

        if (trim)
            text = text.Trim();

        if (cut)
            Selection.Text = string.Empty;

        return text;
    }

    private string HandleLineCopyOrCut(bool trim, bool cut)
    {
        TextPointer lineStart = CaretPosition.GetLineStartPosition(0);
        TextPointer? lineEnd = CaretPosition.GetLineStartPosition(1);
        if (lineStart == null)
            return string.Empty;

        TextRange lineRange = new(lineStart, lineEnd ?? Document.ContentEnd);
        string lineText = lineRange.Text;
        string text = trim ? lineText.Trim() : lineText;

        if (cut)
            lineRange.Text = string.Empty;

        return text;
    }

    private string HandleNoteCopyOrCut(bool trim, bool cut)
    {
        string text = GetPlainText();
        if (trim)
            text = text.Trim();

        if (cut)
        {
            SelectAll();
            Selection.Text = string.Empty;
        }

        return text;
    }

    private new void Paste()
    {
        bool trimText;
        PasteAction action;
        if (IsShiftPressed())
        {
            trimText = TrimTextOnAltPaste;
            action = PasteAltAction;
        }
        else
        {
            trimText = TrimTextOnPaste;
            action = PasteAction;
        }

        if (action == PasteAction.None || !Clipboard.ContainsText())
            return;

        string clipboardString;
        try
        {
            clipboardString = Clipboard.GetText();
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            return;
        }

        if (trimText)
            clipboardString = clipboardString.Trim();

        if (string.IsNullOrEmpty(clipboardString))
            return;

        if (ConvertIndentation)
        {
            string spaces = string.Empty.PadLeft(TabWidth, ' ');
            if (TabSpaces)
                clipboardString = clipboardString.Replace("\t", spaces);
            else
                clipboardString = clipboardString.Replace(spaces, "\t");
        }

        switch (action)
        {
            case PasteAction.Paste:
                Selection.Text = clipboardString;
                CaretPosition = Selection.End;
                break;
            case PasteAction.PasteAndReplaceAll:
                SelectAll();
                Selection.Text = clipboardString;
                CaretPosition = Selection.End;
                break;
            case PasteAction.PasteAtEnd:
                CaretPosition = Document.ContentEnd;
                Selection.Text = clipboardString;
                CaretPosition = Selection.End;
                break;
        }
    }

    #endregion

    #region Event Handlers

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isUpdatingRtfContent)
        {
            _isUpdatingRtfContent = true;
            try
            {
                RtfContent = SerializeRtfContent();
            }
            finally
            {
                _isUpdatingRtfContent = false;
            }
        }
    }

    private void OnSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (AutoCopy && HasSelectedText)
            Copy();
    }

    private void OnPreviewDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void OnPreviewDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            return;

        string filePath = files[0];

        try
        {
            FileInfo fileInfo = new(filePath);
            if (!fileInfo.Exists || fileInfo.Length > 10 * 1024 * 1024)
                return;

            string fileText = File.ReadAllText(filePath);
            Document.Blocks.Clear();
            Document.Blocks.Add(new Paragraph(new Run(fileText)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Silently ignore inaccessible files
        }
    }

    private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (HasSelectedText && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
            Copy();
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle && MiddleClickPaste)
            Paste();
    }

    /// <summary>
    /// Toggles a checklist item when the click lands on its ballot-box glyph. Clicks anywhere
    /// else on the line fall through so the caret can be placed and the text edited normally.
    /// Checked items stay in the note; only their appearance changes.
    /// </summary>
    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsReadOnly)
            return;

        // Let modified clicks through: Shift extends a selection onto this line, and Ctrl+click
        // is a selection gesture too. Swallowing them would make a checklist line unselectable.
        if (Keyboard.Modifiers != ModifierKeys.None)
            return;

        // A double-click is a word-select gesture. Without this the first click of the pair
        // toggles, the second toggles back, and the selection never happens.
        if (e.ClickCount != 1)
            return;

        // snapToText:false so a click in the margin or past the end of a line returns null
        // instead of snapping onto the nearest character, which would toggle by accident.
        TextPointer? clickPosition = GetPositionFromPoint(e.GetPosition(this), snapToText: false);
        if (clickPosition?.Paragraph is not Paragraph paragraph)
            return;

        TextPointer? glyphStart = ChecklistHelper.FindGlyphPosition(paragraph);
        if (glyphStart is null)
            return;

        // Only the glyph's own characters are a hit target.
        int offsetFromGlyph = glyphStart.GetOffsetToPosition(clickPosition);
        if (offsetFromGlyph < 0 || offsetFromGlyph >= ChecklistHelper.GlyphLength(glyphStart))
            return;

        ToggleChecklistItem(paragraph);

        // Swallow the click so the caret does not land inside the glyph we just replaced.
        e.Handled = true;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab)
        {
            e.Handled = HandleTabPressed();
        }
        else if (e.Key == Key.Return && !IsReadOnly && HandleReturnOnChecklistItem())
        {
            e.Handled = true;
        }
        else if (e.Key == Key.Return && AutoIndent)
        {
            string lineText = GetCurrentLineText();
            string whitespace = new([.. lineText.TakeWhile(char.IsWhiteSpace)]);

            if (HasSelectedText)
                Selection.Text = "";

            EditingCommands.EnterParagraphBreak.Execute(null, this);

            if (whitespace.Length > 0)
                CaretPosition.InsertTextInRun(whitespace);

            e.Handled = true;
        }
    }

    private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        _contextMenu.Update();
    }

    /// <summary>
    /// Continues a checklist when Return is pressed on one of its items. Returns true when the
    /// key was consumed.
    ///
    /// Pressing Return on an item starts a fresh unchecked item. The new item always begins with
    /// default formatting, so the struck, dimmed appearance of a completed task never bleeds onto
    /// the next one. Pressing Return on an item that has no text ends the checklist instead, which
    /// is how the list is exited without reaching for the menu.
    /// </summary>
    private bool HandleReturnOnChecklistItem()
    {
        if (HasSelectedText)
            return false;

        if (CaretPosition.Paragraph is not Paragraph paragraph || !ChecklistHelper.IsChecklistItem(paragraph))
            return false;

        // An item whose only content is the glyph and its separator is "empty". Pressing Return on
        // it exits the checklist: strip the glyph, then fall through (return false) so the keypress
        // still produces the new line the user asked for.
        if (IsChecklistItemEmpty(paragraph))
        {
            BeginChange();
            try
            {
                RemoveChecklistItem(paragraph);
            }
            finally
            {
                EndChange();
            }
            return false;
        }

        // Whether the line we are splitting was checked decides how much formatting the new item may
        // inherit. Read it before the break, while `paragraph` is still the source item.
        bool splittingCheckedItem = ChecklistHelper.IsChecked(paragraph);

        EditingCommands.EnterParagraphBreak.Execute(null, this);

        if (CaretPosition.Paragraph is not Paragraph newParagraph)
            return true;

        // The break inherits the previous line's character formatting. Strip the struck/dimmed look
        // only when the source item was checked, so a completed task does not hand its strikethrough
        // and grey to the new item. Clearing unconditionally would also discard the author's own
        // colour and underline on an ordinary unchecked line.
        if (splittingCheckedItem)
            ChecklistHelper.ApplyCheckedAppearance(newParagraph, isChecked: false);

        newParagraph.ContentStart.InsertTextInRun(ChecklistHelper.UncheckedPrefix);
        ChecklistHelper.PinGlyphFont(newParagraph);

        // Park the caret after the glyph and its separator, ready to type.
        TextPointer? glyphStart = ChecklistHelper.FindGlyphPosition(newParagraph);
        if (glyphStart is not null)
        {
            int skip = ChecklistHelper.GlyphLength(glyphStart) + 1; // glyph + separator
            CaretPosition = glyphStart.GetPositionAtOffset(skip, LogicalDirection.Forward) ?? CaretPosition;
        }

        return true;
    }

    /// <summary>True when a checklist item carries no text beyond its glyph and separator.</summary>
    private static bool IsChecklistItemEmpty(Paragraph paragraph)
    {
        string text = new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text;
        string withoutGlyph = text
            .Replace(ChecklistHelper.CheckedGlyph.ToString(), string.Empty)
            .Replace(ChecklistHelper.UncheckedGlyph.ToString(), string.Empty)
            .Replace(ChecklistHelper.TextPresentationSelector.ToString(), string.Empty);
        return string.IsNullOrWhiteSpace(withoutGlyph);
    }

    #endregion

    #region Tab Handling

    private bool HandleTabPressed()
    {
        if ((!HasSelectedText || !Selection.Text.Contains(Environment.NewLine)) && !IsShiftPressed() && TabSpaces)
        {
            // Single line tab: insert spaces
            TextPointer? lineStart = CaretPosition.GetLineStartPosition(0);
            string textBeforeCaret = (lineStart != null)
                ? new TextRange(lineStart, CaretPosition).Text
                : "";

            int lineCaretIndex = textBeforeCaret.Length;
            int spaceCount = TabWidth - (lineCaretIndex % TabWidth);
            if (spaceCount == 0) spaceCount = TabWidth;
            string spaces = "".PadLeft(spaceCount, ' ');

            Selection.Text = spaces;
            return true;
        }
        else if (!HasSelectedText && IsShiftPressed(true))
        {
            // Shift+Tab: remove preceding whitespace
            TextPointer? lineStart = CaretPosition.GetLineStartPosition(0);
            if (lineStart == null)
                return true;

            string textBeforeCaret = new TextRange(lineStart, CaretPosition).Text;
            int tabLength = 0;

            if (textBeforeCaret.Length > 0 && textBeforeCaret[^1] == '\t')
            {
                tabLength = 1;
            }
            else if (textBeforeCaret.Length > 0 && textBeforeCaret[^1] == ' ')
            {
                for (int i = textBeforeCaret.Length - 1; i >= 0 && textBeforeCaret[i] == ' ' && tabLength < TabWidth; i--)
                    tabLength++;
            }

            if (tabLength > 0)
            {
                // Select the whitespace before caret and delete
                TextRange deleteRange = new(lineStart, CaretPosition);
                string lineText = deleteRange.Text;
                if (lineText.Length >= tabLength)
                {
                    string newText = lineText[..^tabLength];
                    deleteRange.Text = newText;
                }
            }

            return true;
        }
        else if (HasSelectedText && Selection.Text.Contains(Environment.NewLine))
        {
            // Multi-line indent/dedent
            string indentation = TabSpaces ? "".PadLeft(TabWidth, ' ') : "\t";
            string[] lines = Selection.Text.Split(Environment.NewLine);

            for (int i = 0; i < lines.Length; i++)
            {
                if (IsShiftPressed(true))
                {
                    if (lines[i].Length > 0)
                    {
                        if (lines[i][0] == '\t')
                            lines[i] = lines[i][1..];
                        else if (lines[i][0] == ' ')
                        {
                            int concurrentSpaces = 0;
                            foreach (char character in lines[i])
                            {
                                if (character != ' ')
                                    break;
                                concurrentSpaces++;
                            }

                            int toRemove = Math.Min(concurrentSpaces, TabWidth);
                            lines[i] = lines[i][toRemove..];
                        }
                    }
                }
                else
                {
                    lines[i] = $"{indentation}{lines[i]}";
                }
            }

            Selection.Text = string.Join(Environment.NewLine, lines);
            return true;
        }

        return false;
    }

    #endregion

    #region Caret Updates

    static NoteTextBoxControl()
    {
        ForegroundProperty.OverrideMetadata(
            typeof(NoteTextBoxControl),
            new FrameworkPropertyMetadata(OnForegroundChanged)
        );
    }

    private static void OnForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteTextBoxControl control && control.CaretColour == CaretColour.Default)
            control.UpdateCaretAppearance();
    }

    private static void OnCaretPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteTextBoxControl control)
            control.UpdateCaretAppearance();
    }

    private void UpdateCaretAppearance()
    {
        Color caretColor = GetCaretColor();
        double thickness = CaretThickness;

        if (double.IsNaN(thickness) || double.IsInfinity(thickness) || thickness < 1.0)
            thickness = 2.0;
        else if (thickness > 10.0)
            thickness = 10.0;

        if (thickness <= 1.0)
        {
            SolidColorBrush solidBrush = new(caretColor);
            solidBrush.Freeze();
            CaretBrush = solidBrush;
            return;
        }

        // Use a DrawingBrush to simulate a wider caret.
        // The caret is rendered as a 1px-wide region by WPF;
        // a DrawingBrush that fills a rectangle wider than 1px
        // and uses Viewport/TileMode to stretch gives the visual
        // effect of a thicker caret.
        DrawingBrush brush = new()
        {
            Stretch = Stretch.None,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top,
            Viewbox = new Rect(0, 0, thickness, 1),
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, thickness, 1),
            ViewportUnits = BrushMappingMode.Absolute,
            TileMode = TileMode.Tile,
            Drawing = new GeometryDrawing
            {
                Brush = new SolidColorBrush(caretColor),
                Geometry = new RectangleGeometry(new Rect(0, 0, thickness, 1))
            }
        };
        brush.Freeze();
        CaretBrush = brush;
    }

    private Color GetCaretColor()
    {
        if (CaretColour == CaretColour.Default)
        {
            if (Foreground is SolidColorBrush foregroundBrush)
                return foregroundBrush.Color;
            return Colors.Black;
        }

        return CaretColour switch
        {
            CaretColour.Black => Colors.Black,
            CaretColour.White => Colors.White,
            CaretColour.Red => Colors.Red,
            CaretColour.Blue => Colors.Blue,
            CaretColour.Green => Colors.Green,
            CaretColour.Orange => Colors.Orange,
            CaretColour.Purple => Colors.Purple,
            CaretColour.Brown => Colors.Brown,
            CaretColour.Gray => Colors.Gray,
            _ => Colors.Black
        };
    }

    #endregion

    #region Font / Wrap Updates

    private static void OnFontPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteTextBoxControl control)
            control.UpdateFont();
    }

    private void UpdateFont()
    {
        FontFamily newFont = new(UseMonoFont ? MonoFontFamily : StandardFontFamily);
        FontFamily = newFont;
        if (Document != null)
            Document.FontFamily = newFont;
    }

    private static void OnWrapTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteTextBoxControl control)
            control.UpdateTextWrapping();
    }

    private void UpdateTextWrapping()
    {
        if (Document == null)
            return;

        if (WrapText)
            Document.PageWidth = double.NaN;
        else
            Document.PageWidth = double.MaxValue;
    }

    #endregion

    #region Utility

    private void ClearDocument()
    {
        Document.Blocks.Clear();
    }

    private void SetReadOnly(bool isReadOnly)
    {
        IsReadOnly = isReadOnly;
    }

    #endregion
}
