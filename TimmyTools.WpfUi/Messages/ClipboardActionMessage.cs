using TimmyTools.Core.DataTransferObjects;
using TimmyTools.Core.Enums;

namespace TimmyTools.WpfUi.Messages;

// Published by ClipboardMonitorService when the history changes. Entry is the
// affected item for Captured/PinChanged, and null for a bulk Removed (clear all).
public record ClipboardActionMessage(
    ClipboardAction Action,
    ClipboardEntryDto? Entry
);
