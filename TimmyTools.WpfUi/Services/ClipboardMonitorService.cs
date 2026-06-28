using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

using TimmyTools.Core.DataTransferObjects;
using TimmyTools.Core.Enums;
using TimmyTools.Core.Repositories;
using TimmyTools.WpfUi.Interop;
using TimmyTools.WpfUi.Interop.Constants;
using TimmyTools.WpfUi.Messages;
using TimmyTools.WpfUi.Models;

namespace TimmyTools.WpfUi.Services;

// Captures clipboard changes for the Clipboard tool. Registers a hidden,
// message-only listener window for WM_CLIPBOARDUPDATE rather than piggybacking on
// a visible tool window, so capture keeps working even when no tool window is
// open. On each change it reads text, fingerprints it, dedupes against the most
// recent capture, persists via ClipboardRepository, enforces the history cap, and
// publishes a ClipboardActionMessage. A CaptureNotified event lets the UI raise a
// non-intrusive on-copy toast without this service taking a dependency on any view.
public class ClipboardMonitorService
{
    // Clipboard format names password managers and browsers set to opt a clip out
    // of history and monitoring. When IgnoreSensitiveClipboard is on, the presence
    // of either format means we skip capture entirely.
    private const string ExcludeFromMonitorFormat = "ExcludeClipboardContentFromMonitorProcessing";
    private const string ExcludeFromHistoryFormat = "CanIncludeInClipboardHistory";

    private const int ClipboardReadRetries = 5;
    private static readonly TimeSpan ClipboardReadRetryDelay = TimeSpan.FromMilliseconds(80);

    // Arbitrary, app-unique id for the toggle hotkey registration.
    private const int ToggleWindowHotkeyId = 0xC11B;

    private readonly ClipboardRepository _clipboardRepository;
    private readonly MessengerService _messengerService;
    private readonly ClipboardSettingsModel _clipboardSettings;

    private HwndSource? _hwndSource;
    private bool _isStarted;
    private bool _hotkeyRegistered;

    // Raised after a successful capture so a UI layer can surface a brief toast.
    // Carries the human-readable preview of what was captured.
    public event Action<string>? CaptureNotified;

    public ClipboardMonitorService(
        ClipboardRepository clipboardRepository,
        MessengerService messengerService,
        SettingsService settingsService
    )
    {
        _clipboardRepository = clipboardRepository;
        _messengerService = messengerService;
        _clipboardSettings = settingsService.ClipboardSettings;
    }

    public void Start()
    {
        if (_isStarted)
            return;

        // A message-only window (HWND_MESSAGE parent) receives messages but is
        // never shown and never appears in the taskbar or Alt-Tab.
        HwndSourceParameters parameters = new("TimmyToolsClipboardListener")
        {
            Width = 0,
            Height = 0,
            ParentWindow = HwndMessage,
            WindowStyle = 0
        };

        _hwndSource = new HwndSource(parameters);
        _hwndSource.AddHook(WndProc);

        if (!User32.AddClipboardFormatListener(_hwndSource.Handle))
            Debug.WriteLine("Failed to register clipboard format listener.");

        RegisterToggleHotkey();

        _isStarted = true;
    }

    // Registers the user's configured toggle chord (default Ctrl+Alt+C). A failed
    // registration (e.g. the chord is already owned by another app) is logged, not
    // fatal: clipboard capture still works without the hotkey.
    private void RegisterToggleHotkey()
    {
        if (_hwndSource is null)
            return;

        if (!TryParseHotkey(_clipboardSettings.GlobalHotkey, out uint modifiers, out uint virtualKey))
        {
            Debug.WriteLine($"Could not parse clipboard hotkey '{_clipboardSettings.GlobalHotkey}'.");
            return;
        }

        _hotkeyRegistered = User32.RegisterHotKey(
            _hwndSource.Handle,
            ToggleWindowHotkeyId,
            modifiers | MOD.NOREPEAT,
            virtualKey
        );

        if (!_hotkeyRegistered)
            Debug.WriteLine($"Failed to register clipboard hotkey '{_clipboardSettings.GlobalHotkey}'.");
    }

    // Parses a "Ctrl+Alt+C" style string into RegisterHotKey modifier flags and a
    // virtual-key code. Returns false for an empty or unrecognised chord.
    private static bool TryParseHotkey(string hotkey, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;

        if (string.IsNullOrWhiteSpace(hotkey))
            return false;

        string[] parts = hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Key parsedKey = Key.None;

        foreach (string part in parts)
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl" or "control":
                    modifiers |= MOD.CONTROL;
                    break;
                case "alt":
                    modifiers |= MOD.ALT;
                    break;
                case "shift":
                    modifiers |= MOD.SHIFT;
                    break;
                case "win" or "windows":
                    modifiers |= MOD.WIN;
                    break;
                default:
                    if (!Enum.TryParse(part, ignoreCase: true, out parsedKey))
                        return false;
                    break;
            }
        }

        if (modifiers == 0 || parsedKey == Key.None)
            return false;

        virtualKey = (uint)KeyInterop.VirtualKeyFromKey(parsedKey);
        return true;
    }

    public void Stop()
    {
        if (!_isStarted)
            return;

        if (_hwndSource is not null)
        {
            if (_hotkeyRegistered)
            {
                User32.UnregisterHotKey(_hwndSource.Handle, ToggleWindowHotkeyId);
                _hotkeyRegistered = false;
            }

            User32.RemoveClipboardFormatListener(_hwndSource.Handle);
            _hwndSource.RemoveHook(WndProc);
            _hwndSource.Dispose();
            _hwndSource = null;
        }

        _isStarted = false;
    }

    // HWND_MESSAGE = (HWND)(-3); creating a child of it yields a message-only window.
    private static nint HwndMessage => -3;

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WM.CLIPBOARDUPDATE)
        {
            // Fire-and-forget: the capture is async and self-contained. Marshalling
            // back to the dispatcher is unnecessary because Clipboard access and the
            // repository calls are all invoked on this UI thread already.
            _ = CaptureCurrentClipboard();
        }
        else if (msg == WM.HOTKEY && wParam.ToInt32() == ToggleWindowHotkeyId)
        {
            // The global chord opens / focuses the clipboard window from anywhere.
            _messengerService.Publish(new OpenClipboardWindowMessage());
            handled = true;
        }

        return nint.Zero;
    }

    private async Task CaptureCurrentClipboard()
    {
        try
        {
            if (_clipboardSettings.IgnoreSensitiveClipboard && IsSensitiveClipboard())
                return;

            string? text = ReadClipboardTextWithRetry();
            if (string.IsNullOrEmpty(text))
                return;

            string hash = ComputeHash(text);

            // Skip a re-copy of the still-current clip so the history is not spammed.
            if (await _clipboardRepository.IsMostRecentHash(hash))
                return;

            ClipboardEntryDto entry = new(
                Id: 0,
                Content: text,
                ContentType: "text",
                Preview: BuildPreview(text),
                CreatedUtc: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Pinned: false,
                Hash: hash
            );

            int newId = await _clipboardRepository.Create(entry);
            await _clipboardRepository.TrimUnpinned(_clipboardSettings.HistoryLimit);

            ClipboardEntryDto savedEntry = entry with { Id = newId };

            _messengerService.Publish(new ClipboardActionMessage(ClipboardAction.Captured, savedEntry));

            if (_clipboardSettings.ShowCopyNotification)
                CaptureNotified?.Invoke(savedEntry.Preview);
        }
        catch (Exception ex)
        {
            // The clipboard is a shared OS resource; a transient failure must never
            // crash the host. Log and move on; the next change raises a fresh event.
            Debug.WriteLine($"Clipboard capture failed: {ex.Message}");
        }
    }

    // Password managers (1Password, KeePass, etc.) tag secret clips with these
    // formats. Their mere presence is the signal; the format payload is irrelevant.
    private static bool IsSensitiveClipboard()
    {
        try
        {
            IDataObject? dataObject = Clipboard.GetDataObject();
            if (dataObject is null)
                return false;

            return dataObject.GetDataPresent(ExcludeFromMonitorFormat)
                || dataObject.GetDataPresent(ExcludeFromHistoryFormat);
        }
        catch
        {
            // If we cannot inspect the formats, fail safe and treat as sensitive so
            // a secret is never captured by accident.
            return true;
        }
    }

    // OpenClipboard can transiently fail while another process holds the clipboard.
    // A short bounded retry smooths over that contention without blocking the UI
    // for long. Returns null when the clip carries no text or stays locked.
    private static string? ReadClipboardTextWithRetry()
    {
        for (int attempt = 0; attempt < ClipboardReadRetries; attempt++)
        {
            try
            {
                if (!Clipboard.ContainsText())
                    return null;

                return Clipboard.GetText();
            }
            catch
            {
                Thread.Sleep(ClipboardReadRetryDelay);
            }
        }

        return null;
    }

    private static string ComputeHash(string text)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }

    // A single-line, length-capped preview for the history list. Collapses newlines
    // and runs of whitespace so multi-line clips render as one tidy row.
    private static string BuildPreview(string text)
    {
        const int maxPreviewLength = 120;

        string collapsed = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        if (collapsed.Length <= maxPreviewLength)
            return collapsed;

        return string.Concat(collapsed.AsSpan(0, maxPreviewLength), "…");
    }
}
