using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

using TimmyTools.Core.DataTransferObjects;
using TimmyTools.Core.Enums;
using TimmyTools.Core.Repositories;
using TimmyTools.WpfUi.Commands;
using TimmyTools.WpfUi.Helpers;
using TimmyTools.WpfUi.Messages;
using TimmyTools.WpfUi.Models;
using TimmyTools.WpfUi.Services;

namespace TimmyTools.WpfUi.ViewModels;

// Drives the Clipboard window. Structurally a sibling of BreakTimerViewModel: a
// self-contained INotifyPropertyChanged view model (not BaseViewModel) because it
// owns a tool window. Holds the history collection, a live text filter, the
// current-clip readout, and the restore/pin/delete/clear/always-on-top commands.
// Subscribes to ClipboardActionMessage so a capture from the monitor service shows
// up immediately without a reload.
public class ClipboardViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ClipboardRepository _clipboardRepository;
    private readonly MessengerService _messengerService;
    private readonly ClipboardSettingsModel _clipboardSettings;

    private readonly ObservableCollection<ClipboardEntryModel> _history = [];

    private string _filter = "";
    private string _currentClipPreview = "";

    public ClipboardViewModel(
        ClipboardRepository clipboardRepository,
        MessengerService messengerService,
        SettingsService settingsService
    )
    {
        _clipboardRepository = clipboardRepository;
        _messengerService = messengerService;
        _clipboardSettings = settingsService.ClipboardSettings;

        HistoryView = CollectionViewSource.GetDefaultView(_history);
        HistoryView.Filter = MatchesFilter;

        RestoreEntryCommand = new RelayCommand<ClipboardEntryModel>(RestoreEntry);
        PinEntryCommand = new RelayCommand<ClipboardEntryModel>(entry => _ = TogglePin(entry));
        DeleteEntryCommand = new RelayCommand<ClipboardEntryModel>(entry => _ = DeleteEntry(entry));
        ClearHistoryCommand = new RelayCommand(() => _ = ClearHistory());
        ToggleAlwaysOnTopCommand = new RelayCommand(ToggleAlwaysOnTop);

        _messengerService.Subscribe<ClipboardActionMessage>(OnClipboardActionMessage);

        _ = LoadHistory();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICollectionView HistoryView { get; }

    public string Filter
    {
        get => _filter;
        set
        {
            if (SetProperty(ref _filter, value))
                HistoryView.Refresh();
        }
    }

    // Top-of-window readout of what is on the clipboard right now. This is the
    // headline answer to "did my copy register?" and stays current as captures
    // arrive.
    public string CurrentClipPreview
    {
        get => _currentClipPreview;
        private set => SetProperty(ref _currentClipPreview, value);
    }

    public bool HasCurrentClip => !string.IsNullOrEmpty(_currentClipPreview);

    public bool IsHistoryEmpty => _history.Count == 0;

    public ClipboardSettingsModel ClipboardSettings => _clipboardSettings;

    public ICommand RestoreEntryCommand { get; }
    public ICommand PinEntryCommand { get; }
    public ICommand DeleteEntryCommand { get; }
    public ICommand ClearHistoryCommand { get; }
    public ICommand ToggleAlwaysOnTopCommand { get; }

    private async Task LoadHistory()
    {
        try
        {
            IEnumerable<ClipboardEntryDto> entries = await _clipboardRepository.GetRecent(_clipboardSettings.HistoryLimit);

            _history.Clear();
            foreach (ClipboardEntryDto dto in entries)
                _history.Add(new ClipboardEntryModel(dto));

            // Seed the live readout from the actual OS clipboard, which is the
            // ground truth — it reflects a clip copied before Timmy Tools started or
            // before any history exists. Fall back to the newest history row.
            string? liveClip = ReadCurrentClipboardPreview();
            if (!string.IsNullOrEmpty(liveClip))
                CurrentClipPreview = liveClip;
            else if (_history.Count > 0)
                CurrentClipPreview = _history[0].Preview;

            OnPropertyChanged(nameof(IsHistoryEmpty));
            OnPropertyChanged(nameof(HasCurrentClip));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load clipboard history: {ex.Message}");
        }
    }

    private void OnClipboardActionMessage(ClipboardActionMessage message)
    {
        // Marshal to the UI thread: the monitor's capture runs on the dispatcher,
        // but defensive dispatch keeps the collection safe if that ever changes.
        Application.Current?.Dispatcher.Invoke(() =>
        {
            switch (message.Action)
            {
                case ClipboardAction.Captured when message.Entry is not null:
                    ClipboardEntryModel model = new(message.Entry);
                    _history.Insert(0, model);
                    CurrentClipPreview = model.Preview;
                    OnPropertyChanged(nameof(IsHistoryEmpty));
                    OnPropertyChanged(nameof(HasCurrentClip));
                    break;
            }
        });
    }

    // Click to restore: put the original full payload back on the clipboard. The
    // monitor's dedupe guard keeps this from creating a duplicate history row.
    private void RestoreEntry(ClipboardEntryModel? entry)
    {
        if (entry is null)
            return;

        try
        {
            Clipboard.SetText(entry.Content);
            CurrentClipPreview = entry.Preview;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to restore clipboard entry: {ex.Message}");
        }
    }

    private async Task TogglePin(ClipboardEntryModel? entry)
    {
        if (entry is null)
            return;

        try
        {
            bool newState = !entry.Pinned;
            await _clipboardRepository.SetPinned(entry.Id, newState);
            entry.Pinned = newState;
            HistoryView.Refresh();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to toggle pin: {ex.Message}");
        }
    }

    private async Task DeleteEntry(ClipboardEntryModel? entry)
    {
        if (entry is null)
            return;

        try
        {
            await _clipboardRepository.Delete(entry.Id);
            _history.Remove(entry);
            OnPropertyChanged(nameof(IsHistoryEmpty));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to delete clipboard entry: {ex.Message}");
        }
    }

    // Clear all respects pins: pinned items are an explicit keep and survive.
    private async Task ClearHistory()
    {
        try
        {
            await _clipboardRepository.ClearUnpinned();

            for (int i = _history.Count - 1; i >= 0; i--)
            {
                if (!_history[i].Pinned)
                    _history.RemoveAt(i);
            }

            _messengerService.Publish(new ClipboardActionMessage(ClipboardAction.Removed, null));
            OnPropertyChanged(nameof(IsHistoryEmpty));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to clear clipboard history: {ex.Message}");
        }
    }

    private void ToggleAlwaysOnTop()
    {
        _clipboardSettings.AlwaysOnTop = !_clipboardSettings.AlwaysOnTop;
    }

    // Reads the OS clipboard's current text as a one-line preview, or null when the
    // clipboard holds no text or is transiently locked by another process.
    private static string? ReadCurrentClipboardPreview()
    {
        try
        {
            if (!Clipboard.ContainsText())
                return null;

            string text = Clipboard.GetText();
            return string.IsNullOrEmpty(text) ? null : ClipboardPreview.Build(text);
        }
        catch
        {
            return null;
        }
    }

    private bool MatchesFilter(object item)
    {
        if (string.IsNullOrWhiteSpace(_filter))
            return true;

        if (item is not ClipboardEntryModel entry)
            return false;

        return entry.Preview.Contains(_filter, StringComparison.OrdinalIgnoreCase)
            || entry.Content.Contains(_filter, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _messengerService.Unsubscribe<ClipboardActionMessage>(OnClipboardActionMessage);
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
