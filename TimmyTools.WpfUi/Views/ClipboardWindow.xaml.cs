using System.Windows;

using TimmyTools.WpfUi.ViewModels;

namespace TimmyTools.WpfUi.Views;

public partial class ClipboardWindow : Window
{
    public ClipboardWindow(ClipboardViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();

        Closed += (s, e) =>
        {
            if (DataContext is IDisposable disposable)
                disposable.Dispose();
        };
    }
}
