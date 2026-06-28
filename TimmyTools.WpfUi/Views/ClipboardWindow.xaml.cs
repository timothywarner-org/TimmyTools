using System.Windows;
using System.Windows.Media.Animation;

using TimmyTools.WpfUi.ViewModels;

namespace TimmyTools.WpfUi.Views;

public partial class ClipboardWindow : Window
{
    private readonly ClipboardViewModel _viewModel;

    public ClipboardWindow(ClipboardViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        // Flash the readout panel on each capture as the in-window confirmation.
        _viewModel.CapturePulse += OnCapturePulse;

        Closed += (s, e) =>
        {
            _viewModel.CapturePulse -= OnCapturePulse;
            if (DataContext is IDisposable disposable)
                disposable.Dispose();
        };
    }

    private void OnCapturePulse()
    {
        // The pulse can be raised from a capture marshalled onto the dispatcher;
        // begin the storyboard on the UI thread to be safe.
        Dispatcher.Invoke(() =>
        {
            Storyboard storyboard = (Storyboard)FindResource("CapturePulse");
            storyboard.Begin();
        });
    }
}
