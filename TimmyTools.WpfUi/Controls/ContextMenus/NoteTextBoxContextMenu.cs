using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

using TimmyTools.Core.Enums;
using TimmyTools.WpfUi.Commands;
using TimmyTools.WpfUi.Views;

namespace TimmyTools.WpfUi.Controls.ContextMenus;

public class NoteTextBoxContextMenu : ContextMenu
{
    private readonly NoteTextBoxControl _noteTextBox;

    private readonly MenuItem _undoMenuItem;
    private readonly MenuItem _redoMenuItem;
    private readonly MenuItem _copyMenuItem;
    private readonly MenuItem _cutMenuItem;
    private readonly MenuItem _pasteMenuItem;
    private readonly MenuItem _selectAllMenuItem;
    private readonly MenuItem _insertLinkMenuItem;
    private readonly MenuItem _fontMenuItem;
    private readonly MenuItem _sizeMenuItem;
    private readonly MenuItem _styleMenuItem;
    private readonly MenuItem _caseMenuItem;
    private readonly MenuItem _highlightMenuItem;
    private readonly MenuItem _paragraphMenuItem;
    private readonly MenuItem _countsMenuItem;
    private readonly MenuItem _lineCountMenuItem;
    private readonly MenuItem _wordCountMenuItem;
    private readonly MenuItem _charCountMenuItem;
    private readonly MenuItem _lockedMenuItem;

    private readonly List<Control> _spellingErrorMenuItems = [];

    public NoteTextBoxContextMenu(NoteTextBoxControl noteTextBox)
    {
        _noteTextBox = noteTextBox;

        _undoMenuItem = new()
        {
            Header = "Undo",
            InputGestureText = "Ctrl+Z",
            Command = ApplicationCommands.Undo,
            CommandTarget = _noteTextBox
        };
        _redoMenuItem = new()
        {
            Header = "Redo",
            InputGestureText = "Ctrl+Shift+Z",
            Command = ApplicationCommands.Redo,
            CommandTarget = _noteTextBox
        };

        _copyMenuItem = new()
        {
            Header = "Copy",
            Command = _noteTextBox.CopyCommand,
            InputGestureText = "Ctrl+C"
        };
        _cutMenuItem = new()
        {
            Header = "Cut",
            Command = _noteTextBox.CutCommand,
            InputGestureText = "Ctrl+X"
        };
        _pasteMenuItem = new()
        {
            Header = "Paste",
            Command = _noteTextBox.PasteCommand,
            InputGestureText = "Ctrl+V"
        };

        _selectAllMenuItem = new()
        {
            Header = "Select All",
            InputGestureText = "Ctrl+A",
            Command = new RelayCommand(_noteTextBox.SelectAll)
        };

        _insertLinkMenuItem = new()
        {
            Header = "Insert link...",
            Command = new RelayCommand(InsertLinkPrompt)
        };

        _fontMenuItem = BuildFontMenu();
        _sizeMenuItem = BuildSizeMenu();
        _styleMenuItem = BuildStyleMenu();
        _caseMenuItem = BuildCaseMenu();
        _highlightMenuItem = BuildHighlightMenu();
        _paragraphMenuItem = BuildParagraphMenu();

        _countsMenuItem = new() { Header = "Counts" };
        _lineCountMenuItem = new() { IsEnabled = false };
        _wordCountMenuItem = new() { IsEnabled = false };
        _charCountMenuItem = new() { IsEnabled = false };

        _lockedMenuItem = new()
        {
            Header = "Locked",
            IsCheckable = true,
            Command = _noteTextBox.SetReadOnlyCommand,
            CommandParameter = true
        };
        _lockedMenuItem.SetBinding(
            MenuItem.CommandParameterProperty,
            new Binding(nameof(MenuItem.IsChecked))
            {
                Source = _lockedMenuItem
            }
        );

        Populate();
    }

    public void Update()
    {
        bool hasText = (_noteTextBox.GetPlainText().Length > 0);

        UpdateSpellingErrorMenuItems();

        _undoMenuItem.IsEnabled = _noteTextBox.CanUndo;
        _redoMenuItem.IsEnabled = _noteTextBox.CanRedo;

        _copyMenuItem.IsEnabled = _noteTextBox.HasSelectedText;
        _cutMenuItem.IsEnabled = _noteTextBox.HasSelectedText;
        _pasteMenuItem.IsEnabled = Clipboard.ContainsText();

        _selectAllMenuItem.IsEnabled = hasText;

        _lockedMenuItem.IsChecked = _noteTextBox.IsReadOnly;

        _lineCountMenuItem.Header = $"Lines: {_noteTextBox.LineCount()}";
        _wordCountMenuItem.Header = $"Words: {_noteTextBox.WordCount()}";
        _charCountMenuItem.Header = $"Chars: {_noteTextBox.CharCount()}";
    }

    private void Populate()
    {
        Items.Add(_undoMenuItem);
        Items.Add(_redoMenuItem);

        Items.Add(new Separator());

        Items.Add(_cutMenuItem);
        Items.Add(_copyMenuItem);
        Items.Add(_pasteMenuItem);

        Items.Add(new Separator());

        Items.Add(_selectAllMenuItem);

        Items.Add(new Separator());

        Items.Add(_insertLinkMenuItem);

        Items.Add(new Separator());

        Items.Add(_fontMenuItem);
        Items.Add(_sizeMenuItem);
        Items.Add(_styleMenuItem);
        Items.Add(_caseMenuItem);
        Items.Add(_highlightMenuItem);
        Items.Add(_paragraphMenuItem);

        Items.Add(new Separator());

        _countsMenuItem.Items.Add(_lineCountMenuItem);
        _countsMenuItem.Items.Add(_wordCountMenuItem);
        _countsMenuItem.Items.Add(_charCountMenuItem);
        Items.Add(_countsMenuItem);

        Items.Add(_lockedMenuItem);
    }

    #region Submenu Builders

    private MenuItem BuildFontMenu()
    {
        MenuItem fontMenu = new() { Header = "Font" };
        // Family names below match exactly what is installed on Tim's machine (verified via
        // InstalledFontCollection). WPF silently falls back to a default when a name is missing,
        // so entries for fonts absent on a given box are harmless, just inert.
        string[] fonts = [
            "Segoe UI", "Arial", "Calibri", "Consolas", "Courier New", "Times New Roman",
            "JetBrains Mono", "JetBrainsMono NF", "Operator Mono", "Poppins", "Tekton Pro"
        ];
        foreach (string font in fonts)
        {
            fontMenu.Items.Add(new MenuItem
            {
                Header = font,
                Command = new RelayCommand(() => _noteTextBox.ApplyFontFamily(font))
            });
        }

        // Text colours live inside the Font menu, after the families, matching the Stickies layout.
        fontMenu.Items.Add(new Separator());
        (string Name, Color Color)[] colors = [
            ("Black", Colors.Black),
            ("Red", Colors.Red),
            ("Blue", Colors.Blue),
            ("Green", Colors.Green),
            ("Orange", Colors.Orange),
            ("Purple", Colors.Purple),
            ("Brown", Colors.Brown),
            ("Gray", Colors.Gray)
        ];
        foreach ((string name, Color color) in colors)
        {
            fontMenu.Items.Add(new MenuItem
            {
                Header = name,
                Icon = MakeSwatch(color),
                Command = new RelayCommand(() => _noteTextBox.ApplyForeground(color))
            });
        }
        return fontMenu;
    }

    private MenuItem BuildHighlightMenu()
    {
        MenuItem highlightMenu = new() { Header = "Highlight" };

        highlightMenu.Items.Add(new MenuItem
        {
            Header = "None",
            Command = new RelayCommand(() => _noteTextBox.ApplyHighlight(null))
        });
        highlightMenu.Items.Add(new Separator());

        // Sourced from the app's own note-colour palette (DefaultTheme.cs light tints) so highlights
        // stay on-brand and readable behind dark text.
        (string Name, string Hex)[] highlights = [
            ("Yellow", "#FFFCDD"),
            ("Orange", "#FEE8B9"),
            ("Red", "#FFC4C6"),
            ("Pink", "#EBBFE3"),
            ("Purple", "#D0CEF3"),
            ("Blue", "#B3D9EC"),
            ("Aqua", "#C0E2E1"),
            ("Green", "#E3EBC6")
        ];
        foreach ((string name, string hex) in highlights)
        {
            Color color = (Color)ColorConverter.ConvertFromString(hex);
            highlightMenu.Items.Add(new MenuItem
            {
                Header = name,
                Icon = MakeSwatch(color),
                Command = new RelayCommand(() => _noteTextBox.ApplyHighlight(color))
            });
        }
        return highlightMenu;
    }

    private static Rectangle MakeSwatch(Color color)
        => new()
        {
            Width = 12,
            Height = 12,
            Fill = new SolidColorBrush(color),
            Stroke = Brushes.Gray,
            StrokeThickness = 1
        };

    private void InsertLinkPrompt()
    {
        InsertLinkDialog dialog = new()
        {
            Owner = Window.GetWindow(_noteTextBox)
        };
        if (dialog.ShowDialog() != true)
            return;

        if (Uri.TryCreate(dialog.LinkUrl, UriKind.Absolute, out Uri? uri))
            _noteTextBox.InsertHyperlink(uri);
    }

    private MenuItem BuildSizeMenu()
    {
        MenuItem sizeMenu = new() { Header = "Size" };
        double[] sizes = [8, 10, 12, 14, 16, 18, 20, 24, 28, 36, 48, 56, 64, 72, 84];
        foreach (double size in sizes)
        {
            sizeMenu.Items.Add(new MenuItem
            {
                Header = size.ToString(),
                Command = new RelayCommand(() => _noteTextBox.ApplyFontSize(size))
            });
        }
        return sizeMenu;
    }

    private MenuItem BuildStyleMenu()
    {
        MenuItem styleMenu = new() { Header = "Style" };
        styleMenu.Items.Add(new MenuItem
        {
            Header = "Bold",
            InputGestureText = "Ctrl+B",
            Command = new RelayCommand(() => _noteTextBox.ToggleBold())
        });
        styleMenu.Items.Add(new MenuItem
        {
            Header = "Italic",
            InputGestureText = "Ctrl+I",
            Command = new RelayCommand(() => _noteTextBox.ToggleItalic())
        });
        styleMenu.Items.Add(new MenuItem
        {
            Header = "Underline",
            InputGestureText = "Ctrl+U",
            Command = new RelayCommand(() => _noteTextBox.ToggleUnderline())
        });
        styleMenu.Items.Add(new Separator());
        styleMenu.Items.Add(new MenuItem
        {
            Header = "Clear Formatting",
            Command = new RelayCommand(() => _noteTextBox.ClearFormatting())
        });
        return styleMenu;
    }

    private MenuItem BuildCaseMenu()
    {
        MenuItem caseMenu = new() { Header = "Case" };
        caseMenu.Items.Add(new MenuItem
        {
            Header = "Lower",
            Command = new RelayCommand(() => _noteTextBox.ApplyCaseTransform(CaseTransform.Lower))
        });
        caseMenu.Items.Add(new MenuItem
        {
            Header = "Upper",
            Command = new RelayCommand(() => _noteTextBox.ApplyCaseTransform(CaseTransform.Upper))
        });
        caseMenu.Items.Add(new MenuItem
        {
            Header = "Title",
            Command = new RelayCommand(() => _noteTextBox.ApplyCaseTransform(CaseTransform.Title))
        });
        return caseMenu;
    }

    private MenuItem BuildParagraphMenu()
    {
        MenuItem paragraphMenu = new() { Header = "Paragraph" };

        // Alignment
        paragraphMenu.Items.Add(new MenuItem
        {
            Header = "Left",
            Command = new RelayCommand(() => _noteTextBox.ApplyAlignment(TextAlignment.Left))
        });
        paragraphMenu.Items.Add(new MenuItem
        {
            Header = "Center",
            Command = new RelayCommand(() => _noteTextBox.ApplyAlignment(TextAlignment.Center))
        });
        paragraphMenu.Items.Add(new MenuItem
        {
            Header = "Right",
            Command = new RelayCommand(() => _noteTextBox.ApplyAlignment(TextAlignment.Right))
        });
        paragraphMenu.Items.Add(new MenuItem
        {
            Header = "Justified",
            Command = new RelayCommand(() => _noteTextBox.ApplyAlignment(TextAlignment.Justify))
        });

        paragraphMenu.Items.Add(new Separator());

        // List styles
        paragraphMenu.Items.Add(new MenuItem
        {
            Header = "None",
            Command = new RelayCommand(() => _noteTextBox.ApplyList(TextMarkerStyle.None))
        });
        paragraphMenu.Items.Add(new MenuItem
        {
            Header = "Bullets",
            Command = new RelayCommand(() => _noteTextBox.ApplyList(TextMarkerStyle.Disc))
        });
        paragraphMenu.Items.Add(new MenuItem
        {
            Header = "Numbered",
            Command = new RelayCommand(() => _noteTextBox.ApplyList(TextMarkerStyle.Decimal))
        });
        paragraphMenu.Items.Add(new MenuItem
        {
            Header = "Lettered",
            Command = new RelayCommand(() => _noteTextBox.ApplyList(TextMarkerStyle.LowerLatin))
        });
        // Converts the selected lines to/from checklist items. Checking an item off is a
        // separate action: click its box, or press Ctrl+K on the line.
        paragraphMenu.Items.Add(new MenuItem
        {
            Header = "Checklist",
            Command = new RelayCommand(_noteTextBox.ApplyChecklist)
        });
        paragraphMenu.Items.Add(new MenuItem
        {
            Header = "Toggle check",
            InputGestureText = "Ctrl+K",
            Command = new RelayCommand(_noteTextBox.ToggleChecklistItemAtCaret)
        });

        paragraphMenu.Items.Add(new Separator());

        // Tab spacing
        double[] tabSpacings = [40, 80, 120, 240];
        foreach (double spacing in tabSpacings)
        {
            paragraphMenu.Items.Add(new MenuItem
            {
                Header = $"Tab spacing: {spacing}",
                Command = new RelayCommand(() => _noteTextBox.ApplyTabSpacing(spacing))
            });
        }

        return paragraphMenu;
    }

    #endregion

    #region Spelling Error Items

    private void UpdateSpellingErrorMenuItems()
    {
        foreach (Control spellingErrorMenuItem in _spellingErrorMenuItems)
            Items.Remove(spellingErrorMenuItem);

        _spellingErrorMenuItems.Clear();

        SpellingError? spellingError = _noteTextBox.GetSpellingError(_noteTextBox.CaretPosition);
        if (spellingError != null)
        {
            if (!spellingError.Suggestions.Any())
                _spellingErrorMenuItems.Add(
                    new MenuItem()
                    {
                        Header = "(no spelling suggestions)",
                        IsEnabled = false
                    }
                );
            else
                foreach (string spellingSuggestion in spellingError.Suggestions)
                {
                    _spellingErrorMenuItems.Add(
                        new MenuItem()
                        {
                            Header = spellingSuggestion,
                            FontWeight = FontWeights.Bold,
                            Command = EditingCommands.CorrectSpellingError,
                            CommandParameter = spellingSuggestion,
                            CommandTarget = _noteTextBox
                        }
                    );
                }
            _spellingErrorMenuItems.Add(new Separator());
        }

        _spellingErrorMenuItems.Reverse();
        foreach (Control spellingErrorMenuItem in _spellingErrorMenuItems)
            Items.Insert(0, spellingErrorMenuItem);
    }

    #endregion
}
