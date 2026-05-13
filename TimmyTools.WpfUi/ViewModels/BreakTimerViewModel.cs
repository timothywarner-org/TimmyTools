using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;

using TimmyTools.WpfUi.Commands;
using TimmyTools.WpfUi.Models;
using TimmyTools.WpfUi.Services;

namespace TimmyTools.WpfUi.ViewModels;

public class BreakTimerViewModel : INotifyPropertyChanged, IDisposable
{
    private enum TimerState
    {
        Idle,
        Running,
        Paused,
        Completed
    }

    private readonly DispatcherTimer _timer;
    private readonly BreakTimerSettingsModel _breakTimerSettings;

    private TimerState _state = TimerState.Idle;
    private DateTime _endTime;
    private TimeSpan _totalDuration;
    private TimeSpan _remainingOnPause;

    private string _timeRemainingText = "00:00";
    private string _verboseTimeText = "";
    private double _progressFraction;
    private int _customMinutes = 5;

    public BreakTimerViewModel(SettingsService settingsService)
    {
        _breakTimerSettings = settingsService.BreakTimerSettings;
        _breakTimerSettings.PropertyChanged += OnBreakTimerSettingsChanged;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _timer.Tick += Timer_Tick;

        StartPresetCommand = new RelayCommand<string>(StartPreset);
        StartCustomCommand = new RelayCommand(StartCustom, () => CustomMinutes > 0);
        PauseCommand = new RelayCommand(Pause);
        ResumeCommand = new RelayCommand(Resume);
        ResetCommand = new RelayCommand(Reset);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string TimeRemainingText
    {
        get => _timeRemainingText;
        private set => SetProperty(ref _timeRemainingText, value);
    }

    public string VerboseTimeText
    {
        get => _verboseTimeText;
        private set => SetProperty(ref _verboseTimeText, value);
    }

    public double ProgressFraction
    {
        get => _progressFraction;
        private set => SetProperty(ref _progressFraction, value);
    }

    public int CustomMinutes
    {
        get => _customMinutes;
        set
        {
            int clamped = Math.Clamp(value, 1, 999);
            if (SetProperty(ref _customMinutes, clamped))
                ((RelayCommand)StartCustomCommand).RaiseCanExecuteChanged();
        }
    }

    public string ClassTitle => _breakTimerSettings.ClassTitle;

    public string NextUp => _breakTimerSettings.NextUp;

    public BreakTimerSettingsModel BreakTimerSettings => _breakTimerSettings;

    public bool IsIdle => _state == TimerState.Idle;

    public bool IsRunning => _state == TimerState.Running;

    public bool IsPaused => _state == TimerState.Paused;

    public bool IsCompleted => _state == TimerState.Completed;

    public bool IsActive => _state != TimerState.Idle;

    public ICommand StartPresetCommand { get; }
    public ICommand StartCustomCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand ResumeCommand { get; }
    public ICommand ResetCommand { get; }

    private void StartPreset(string minutesText)
    {
        if (int.TryParse(minutesText, out int minutes) && minutes > 0)
            StartTimer(TimeSpan.FromMinutes(minutes));
    }

    private void StartCustom()
    {
        if (CustomMinutes > 0)
            StartTimer(TimeSpan.FromMinutes(CustomMinutes));
    }

    private void StartTimer(TimeSpan duration)
    {
        _totalDuration = duration;
        _endTime = DateTime.Now + duration;

        UpdateDisplay(duration);
        SetState(TimerState.Running);

        _timer.Start();
    }

    private void Pause()
    {
        _timer.Stop();
        _remainingOnPause = _endTime - DateTime.Now;
        if (_remainingOnPause < TimeSpan.Zero)
            _remainingOnPause = TimeSpan.Zero;

        SetState(TimerState.Paused);
    }

    private void Resume()
    {
        _endTime = DateTime.Now + _remainingOnPause;
        SetState(TimerState.Running);
        _timer.Start();
    }

    private void Reset()
    {
        _timer.Stop();
        TimeRemainingText = "00:00";
        VerboseTimeText = "";
        ProgressFraction = 0;
        SetState(TimerState.Idle);
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        TimeSpan remaining = _endTime - DateTime.Now;

        if (remaining <= TimeSpan.Zero)
        {
            _timer.Stop();
            UpdateDisplay(TimeSpan.Zero);
            ProgressFraction = 1.0;
            SetState(TimerState.Completed);
            return;
        }

        UpdateDisplay(remaining);

        double elapsed = _totalDuration.TotalSeconds - remaining.TotalSeconds;
        ProgressFraction = Math.Min(1.0, elapsed / _totalDuration.TotalSeconds);
    }

    private void UpdateDisplay(TimeSpan remaining)
    {
        if (remaining.TotalHours >= 1)
            TimeRemainingText = $"{(int)remaining.TotalHours}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
        else
            TimeRemainingText = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}";

        int displayMinutes = (int)remaining.TotalMinutes;
        int displaySeconds = remaining.Seconds;

        if (remaining.TotalHours >= 1)
        {
            int hours = (int)remaining.TotalHours;
            int minutes = remaining.Minutes;
            VerboseTimeText = $"{hours} {(hours == 1 ? "hour" : "hours")} {minutes} {(minutes == 1 ? "minute" : "minutes")} {displaySeconds} {(displaySeconds == 1 ? "second" : "seconds")}";
        }
        else
        {
            VerboseTimeText = $"{displayMinutes} {(displayMinutes == 1 ? "minute" : "minutes")} {displaySeconds} {(displaySeconds == 1 ? "second" : "seconds")}";
        }
    }

    private void SetState(TimerState state)
    {
        _state = state;
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(IsActive));
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= Timer_Tick;
        _breakTimerSettings.PropertyChanged -= OnBreakTimerSettingsChanged;
    }

    private void OnBreakTimerSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BreakTimerSettingsModel.ClassTitle))
            OnPropertyChanged(nameof(ClassTitle));
        else if (e.PropertyName == nameof(BreakTimerSettingsModel.NextUp))
            OnPropertyChanged(nameof(NextUp));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(storage, value))
            return false;

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
