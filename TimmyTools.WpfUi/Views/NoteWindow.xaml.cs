using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

using TimmyTools.Core.Enums;
using TimmyTools.WpfUi.Helpers;
using TimmyTools.WpfUi.Messages;
using TimmyTools.WpfUi.Models;
using TimmyTools.WpfUi.Services;
using TimmyTools.WpfUi.Themes;
using TimmyTools.WpfUi.ViewModels;

namespace TimmyTools.WpfUi.Views;

public partial class NoteWindow : Window
{
    private readonly NoteSettingsModel _noteSettings;
    private readonly MessengerService _messengerService;
    private readonly ThemeService _themeService;

    private readonly NoteViewModel _viewModel;

    private bool _isRolledUp;
    private double _savedHeight;
    private double _savedMinHeight;
    private bool _isInitialLoadComplete;

    public NoteViewModel ViewModel => _viewModel;

    #region NoteWindow

    public NoteWindow(SettingsService settingsService, MessengerService messengerService, ThemeService themeService, NoteViewModel viewModel)
    {
        _noteSettings = settingsService.NoteSettings;
        _messengerService = messengerService;
        _messengerService.Subscribe<WindowActionMessage>(OnWindowActionMessage);
        _themeService = themeService;

        _viewModel = viewModel;

        DataContext = _viewModel;

        InitializeComponent();

        // Flush the LostFocus-triggered RtfContent binding before each save
        // so the periodic auto-save still captures in-progress edits.
        _viewModel.FlushPendingEdits = () =>
            BindingOperations.GetBindingExpression(
                NoteTextBox,
                Controls.NoteTextBoxControl.RtfContentProperty
            )?.UpdateSource();

        Activated += Window_Activated;
        Closing += Window_Closing;
        Deactivated += Window_Deactivated;
        MouseEnter += Window_MouseEnter;
        MouseLeave += Window_MouseLeave;
        Loaded += Window_Loaded;
        StateChanged += NoteWindow_StateChanged;

        TitleBarGrid.MouseDown += TitleBar_MouseDown;
        NewButton.Click += NewButton_Click;
        AtomicClockButton.Click += AtomicClockButton_Click;
        BreakTimerButton.Click += BreakTimerButton_Click;
        ClipboardButton.Click += ClipboardButton_Click;
        MinimizeButton.Click += MinimizeButton_Click;
        CloseButton.Click += CloseButton_Click;

        PopulateTitleBarContextMenu();
    }

    private void PopulateTitleBarContextMenu()
    {
        int insertIndex = TitleBarContextMenu.Items.IndexOf(ThemeMenuSeparator);
        foreach (ColourScheme colourScheme in _themeService.CurrentTheme.ColourSchemes.Values)
        {
            MenuItem menuItem = new()
            {
                Header = colourScheme.Name,
                Command = _viewModel.ChangeThemeColourCommand,
                CommandParameter = colourScheme.Name,
                Icon = colourScheme.Icon
            };

            TitleBarContextMenu.Items.Insert(insertIndex, menuItem);

            insertIndex++;
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _viewModel.OnWindowLoaded(
            ScreenHelper.GetWindowHandle(this)
        );

        // Subscribe to text changes after initial load so the first
        // content load does not trigger an unwanted resize.
        NoteTextBox.Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Loaded,
            () =>
            {
                _isInitialLoadComplete = true;
                NoteTextBox.TextChanged += NoteTextBox_TextChanged;
            }
        );
    }

    private void NoteWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            if (_noteSettings.MinimizeMode == MinimizeMode.Prevent)
                WindowState = WindowState.Normal;
        }
        else if (WindowState == WindowState.Normal)
        {
            _viewModel.UpdateVisibility();
        }
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        ShowTitleBar();
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!IsActive)
            HideTitleBar();
    }

    private void Window_Activated(object? sender, EventArgs e)
    {
        _viewModel.Note.IsFocused = true;
        _viewModel.UpdateOpacity();
        ShowTitleBar();
    }

    private async void Window_Deactivated(object? sender, EventArgs e)
    {
        if (!_viewModel.Note.IsOpen)
            return;

        _viewModel.Note.IsFocused = false;
        _viewModel.UpdateOpacity();
        HideTitleBar();

        try
        {
            await _viewModel.SaveNote();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save note on deactivate: {ex.Message}");
        }
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        _messengerService.Unsubscribe<WindowActionMessage>(OnWindowActionMessage);
        e.Cancel = await _viewModel.CloseNote();
    }

    private void OnWindowActionMessage(WindowActionMessage message)
    {
        if (message.Action == WindowAction.Activate)
        {
            WindowState = WindowState.Normal;
            Activate();
        }
    }

    #endregion

    #region Auto-Resize

    private void NoteTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isInitialLoadComplete || _isRolledUp)
            return;

        AutoResizeHeight();
    }

    private void AutoResizeHeight()
    {
        NoteTextBox.UpdateLayout();

        ScrollViewer? scrollViewer = FindVisualChild<ScrollViewer>(NoteTextBox);
        if (scrollViewer == null)
            return;

        double contentHeight = scrollViewer.ExtentHeight;
        double titleBarHeight = TitleBarGrid.ActualHeight;
        double borderHeight = BorderThickness.Top + BorderThickness.Bottom;
        double textBoxPadding = NoteTextBox.Padding.Top + NoteTextBox.Padding.Bottom;
        double buffer = 16.0;

        double desiredHeight = contentHeight + titleBarHeight + borderHeight + textBoxPadding + buffer;

        double maxHeight = SystemParameters.WorkArea.Height;
        double clampedHeight = Math.Clamp(desiredHeight, MinHeight, maxHeight);

        Height = clampedHeight;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found)
                return found;
            T? result = FindVisualChild<T>(child);
            if (result != null)
                return result;
        }
        return null;
    }

    #endregion

    #region TitleBar

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
            return;

        if (e.ClickCount >= 2)
        {
            e.Handled = true;
            ToggleRollUp();
            return;
        }

        DragMove();
    }

    private void ToggleRollUp()
    {
        if (_isRolledUp)
        {
            // Restore: re-bind Height to the model, expand body
            NoteBodyGrid.Visibility = Visibility.Visible;
            MinHeight = _savedMinHeight;
            ResizeMode = ResizeMode.CanResize;

            Binding heightBinding = new("Note.Height")
            {
                Mode = BindingMode.TwoWay
            };
            SetBinding(HeightProperty, heightBinding);
            Height = _savedHeight;

            _isRolledUp = false;
        }
        else
        {
            _savedHeight = ActualHeight;
            _savedMinHeight = MinHeight;

            // Detach the two-way Height binding so the collapsed height
            // doesn't flow into Note.Height and get persisted by auto-save.
            BindingOperations.ClearBinding(this, HeightProperty);

            NoteBodyGrid.Visibility = Visibility.Collapsed;

            double titleBarHeight = TitleBarGrid.ActualHeight;
            double borderHeight = BorderThickness.Top + BorderThickness.Bottom;
            double rolledUpHeight = titleBarHeight + borderHeight;

            MinHeight = rolledUpHeight;
            Height = rolledUpHeight;
            ResizeMode = ResizeMode.NoResize;
            _isRolledUp = true;
        }
    }

    private void NewButton_Click(object sender, RoutedEventArgs e)
    {
        _messengerService.Publish(
            new OpenNoteWindowMessage(ParentNote: _viewModel.Note)
        );
    }

    private void AtomicClockButton_Click(object sender, RoutedEventArgs e)
    {
        _messengerService.Publish(new OpenAtomicClockWindowMessage());
    }

    private void BreakTimerButton_Click(object sender, RoutedEventArgs e)
    {
        _messengerService.Publish(new OpenBreakTimerWindowMessage());
    }

    private void ClipboardButton_Click(object sender, RoutedEventArgs e)
    {
        _messengerService.Publish(new OpenClipboardWindowMessage());
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Note.ShowInTaskbar = true;
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.Note.IsOpen = false;
        Close();
    }

    private void HideTitleBar()
    {
        if (_noteSettings.HideTitleBar)
            BeginStoryboard("HideTitleBarAnimation");
    }

    private void ShowTitleBar()
    {
        BeginStoryboard("ShowTitleBarAnimation");
    }

    private void BeginStoryboard(string resourceKey)
    {
        Storyboard storyboard = (Storyboard)FindResource(resourceKey);
        storyboard.Begin();
    }

    private void SaveMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SaveFileDialog saveFileDialog = new()
        {
            Filter = "Rich Text (*.rtf)|*.rtf|Text Documents (*.txt)|*.txt|All Files|*"
        };

        if (saveFileDialog.ShowDialog(this) == false)
            return;

        try
        {
            if (saveFileDialog.FileName.EndsWith(".rtf", StringComparison.OrdinalIgnoreCase))
                File.WriteAllText(saveFileDialog.FileName, NoteTextBox.RtfContent);
            else
                File.WriteAllText(saveFileDialog.FileName, NoteTextBox.GetPlainText());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                $"Failed to save file: {ex.Message}",
                "Save Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private void ResetMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Width = _noteSettings.DefaultWidth;

        if (_isRolledUp)
        {
            _savedHeight = _noteSettings.DefaultHeight;
        }
        else
        {
            Height = _noteSettings.DefaultHeight;
        }
    }

    private void SetTitleMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetTitleDialog dialog = new(_viewModel.Note.Title)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
            _viewModel.Note.Title = dialog.NoteTitle;
    }

    private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _messengerService.Publish(new OpenSettingsWindowMessage(this));
    }

    #endregion
}
