using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace TimmyTools.WpfUi.Views;

// A brief, non-intrusive on-copy confirmation. Never takes focus (ShowActivated
// false, IsHitTestVisible false), parks at the bottom-right of the work area, and
// auto-dismisses. This is the headline fix for the "did my copy register?" doubt.
public partial class ClipboardToast : Window
{
    private static readonly TimeSpan VisibleDuration = TimeSpan.FromSeconds(2.2);

    private readonly DispatcherTimer _dismissTimer;

    public ClipboardToast(string preview)
    {
        InitializeComponent();

        PreviewText.Text = preview;

        _dismissTimer = new DispatcherTimer { Interval = VisibleDuration };
        _dismissTimer.Tick += OnDismissTimerTick;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Rect workArea = SystemParameters.WorkArea;
        const double margin = 16.0;

        Left = workArea.Right - ActualWidth - margin;
        Top = workArea.Bottom - ActualHeight - margin;

        DoubleAnimation fadeIn = new(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(150)));
        BeginAnimation(OpacityProperty, fadeIn);

        _dismissTimer.Start();
    }

    private void OnDismissTimerTick(object? sender, EventArgs e)
    {
        _dismissTimer.Stop();
        _dismissTimer.Tick -= OnDismissTimerTick;

        DoubleAnimation fadeOut = new(1.0, 0.0, new Duration(TimeSpan.FromMilliseconds(250)));
        fadeOut.Completed += (s, _) => Close();
        BeginAnimation(OpacityProperty, fadeOut);
    }
}
