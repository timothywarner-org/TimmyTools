using System.Windows;
using System.Windows.Input;

using TimmyTools.WpfUi.Services;
using TimmyTools.WpfUi.ViewModels;

namespace TimmyTools.WpfUi.Views;

public partial class BreakTimerWindow : Window
{
    private readonly SettingsService _settingsService;

    public BreakTimerWindow(BreakTimerViewModel viewModel, SettingsService settingsService)
    {
        _settingsService = settingsService;
        DataContext = viewModel;
        InitializeComponent();

        Closed += (s, e) =>
        {
            if (DataContext is IDisposable disposable)
                disposable.Dispose();
        };
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        QuickEditPopup.IsOpen = !QuickEditPopup.IsOpen;
    }

    private void QuickEditPopup_Opened(object sender, EventArgs e)
    {
        ClassTitleTextBox.Focus();
        ClassTitleTextBox.SelectAll();
    }

    private async void QuickEditPopup_Closed(object sender, EventArgs e)
    {
        try
        {
            await _settingsService.Save();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to persist break timer settings: {ex.Message}");
        }
    }

    private void QuickEditTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Escape)
        {
            QuickEditPopup.IsOpen = false;
            e.Handled = true;
        }
    }
}
