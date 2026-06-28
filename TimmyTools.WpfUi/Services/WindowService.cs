using Microsoft.Extensions.DependencyInjection;

using TimmyTools.Core.Enums;
using TimmyTools.Core.Repositories;
using TimmyTools.WpfUi.Helpers;
using TimmyTools.WpfUi.Messages;
using TimmyTools.WpfUi.Models;
using TimmyTools.WpfUi.ViewModels;
using TimmyTools.WpfUi.Views;

namespace TimmyTools.WpfUi.Services;

public class WindowService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly MessengerService _messengerService;
    private readonly AppMetadataService _appMetadataService;
    private readonly SettingsService _settingsService;
    private readonly NoteRepository _noteRepository;
    private readonly ThemeService _themeService;

    private readonly Dictionary<int, NoteWindow> _openNoteWindows = [];
    private SettingsWindow? _settingsWindow;
    private ManagementWindow? _managementWindow;
    private BreakTimerWindow? _breakTimerWindow;
    private AtomicClockWindow? _atomicClockWindow;
    private ClipboardWindow? _clipboardWindow;

    public WindowService(
        IServiceProvider serviceProvider,
        MessengerService messengerService,
        AppMetadataService appMetadataService,
        SettingsService settingsService,
        NoteRepository noteRepository,
        ThemeService themeService
    )
    {
        _serviceProvider = serviceProvider;
        _messengerService = messengerService;
        _appMetadataService = appMetadataService;
        _settingsService = settingsService;
        _noteRepository = noteRepository;
        _themeService = themeService;

        _messengerService.Subscribe<ApplicationActionMessage>(OnApplicationActionMessage);

        _messengerService.Subscribe<OpenNoteWindowMessage>(OnOpenNoteWindowMessage);
        _messengerService.Subscribe<MultipleNoteWindowActionMessage>(OnMultipleNoteWindowActionMessage);
        _messengerService.Subscribe<NoteActionMessage>(OnNoteActionMessage);

        _messengerService.Subscribe<OpenSettingsWindowMessage>(OnOpenSettingsWindowMessage);

        _messengerService.Subscribe<OpenManagementWindowMessage>(OnOpenManagementWindowMessage);

        _messengerService.Subscribe<OpenBreakTimerWindowMessage>(OnOpenBreakTimerWindowMessage);

        _messengerService.Subscribe<OpenAtomicClockWindowMessage>(OnOpenAtomicClockWindowMessage);

        _messengerService.Subscribe<OpenClipboardWindowMessage>(OnOpenClipboardWindowMessage);
    }

    public async Task SaveAllOpenNotes()
    {
        foreach (KeyValuePair<int, NoteWindow> entry in _openNoteWindows)
        {
            try
            {
                await entry.Value.ViewModel.SaveNote();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save note {entry.Key} on exit: {ex.Message}");
            }
        }
    }

    private void OnApplicationActionMessage(ApplicationActionMessage message)
    {
        // TO DO: Add settings for this behaviour.
        switch (message.Action)
        {
            case ApplicationAction.Start:
                OpenManagementWindow();
                break;
            case ApplicationAction.NewInstance:
                _ = OpenNoteWindow();
                break;
        }
    }

    private void OnOpenNoteWindowMessage(OpenNoteWindowMessage message)
    {
        nint? managementWindowHandle = null;
        if (_managementWindow is not null && message.isManagementWindowParent)
            managementWindowHandle = ScreenHelper.GetWindowHandle(_managementWindow);

        _ = OpenNoteWindow(message.NoteId, message.ParentNote, managementWindowHandle);
    }

    private void OnMultipleNoteWindowActionMessage(MultipleNoteWindowActionMessage message)
    {
        foreach (int noteId in message.NoteIds)
        {
            switch (message.Action)
            {
                case NoteWindowAction.Open:
                    _ = OpenNoteWindow(noteId);
                    break;
                case NoteWindowAction.Close:
                    CloseNoteWindow(noteId);
                    break;
            }
        }
    }

    private void OnNoteActionMessage(NoteActionMessage message)
    {
        switch (message.Action)
        {
            case NoteAction.Closed:
                _openNoteWindows.Remove(message.NoteDto.Id);
                break;
        }
    }

    private void OnOpenSettingsWindowMessage(OpenSettingsWindowMessage message)
    {
        if (_settingsWindow is null || !_settingsWindow.IsLoaded)
        {
            _settingsWindow = _serviceProvider.GetRequiredService<SettingsWindow>();
            _settingsWindow.Closed += (s, e) => _settingsWindow = null;
        }

        _settingsWindow.Owner = message.Owner;
        _settingsWindow.SelectTab(message.TabIndex);

        if (!_settingsWindow.IsVisible)
            _settingsWindow.Show();

        _settingsWindow.Activate();
    }

    private void OnOpenManagementWindowMessage(OpenManagementWindowMessage message)
        => OpenManagementWindow();

    private void OnOpenBreakTimerWindowMessage(OpenBreakTimerWindowMessage message)
    {
        if (_breakTimerWindow is null || !_breakTimerWindow.IsLoaded)
        {
            _breakTimerWindow = _serviceProvider.GetRequiredService<BreakTimerWindow>();
            _breakTimerWindow.Closed += (s, e) => _breakTimerWindow = null;
        }

        if (!_breakTimerWindow.IsVisible)
            _breakTimerWindow.Show();

        _breakTimerWindow.Activate();
    }

    private void OnOpenAtomicClockWindowMessage(OpenAtomicClockWindowMessage message)
    {
        if (_atomicClockWindow is null || !_atomicClockWindow.IsLoaded)
        {
            _atomicClockWindow = _serviceProvider.GetRequiredService<AtomicClockWindow>();
            _atomicClockWindow.Closed += (s, e) => _atomicClockWindow = null;
        }

        if (!_atomicClockWindow.IsVisible)
            _atomicClockWindow.Show();

        _atomicClockWindow.Activate();
    }

    private void OnOpenClipboardWindowMessage(OpenClipboardWindowMessage message)
    {
        if (_clipboardWindow is null || !_clipboardWindow.IsLoaded)
        {
            _clipboardWindow = _serviceProvider.GetRequiredService<ClipboardWindow>();
            _clipboardWindow.Closed += (s, e) => _clipboardWindow = null;
        }

        if (!_clipboardWindow.IsVisible)
            _clipboardWindow.Show();

        _clipboardWindow.Activate();
    }

    private async Task OpenNoteWindow(int? noteId = null, NoteModel? parentNote = null, nint? managementWindowHandle = null)
    {
        try
        {
            NoteWindow window;

            if (noteId is not null && _openNoteWindows.TryGetValue((int)noteId, out NoteWindow? existingWindow))
            {
                window = existingWindow;
                window.Activate();
            }
            else
            {
                NoteViewModel viewModel = new(_noteRepository, _appMetadataService, _settingsService, _messengerService, _themeService);
                await viewModel.Initialize(noteId, parentNote, managementWindowHandle);
                window = new(_settingsService, _messengerService, _themeService, viewModel);

                window.Show();

                _openNoteWindows[viewModel.Note.Id] = window;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open note window: {ex.Message}");
        }
    }

    private void CloseNoteWindow(int noteId)
    {
        if (_openNoteWindows.TryGetValue(noteId, out NoteWindow? window) && window.IsLoaded)
        {
            window.Close();
            _openNoteWindows.Remove(noteId);
        }
    }

    private void OpenManagementWindow()
    {
        if (_managementWindow is null || !_managementWindow.IsLoaded)
        {
            _managementWindow = _serviceProvider.GetRequiredService<ManagementWindow>();
            _managementWindow.Closed += (s, e) => _managementWindow = null;
        }

        if (!_managementWindow.IsVisible)
            _managementWindow.Show();

        _managementWindow.Activate();
    }
}
