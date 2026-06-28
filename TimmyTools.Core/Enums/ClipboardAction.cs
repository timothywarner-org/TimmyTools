namespace TimmyTools.Core.Enums;

public enum ClipboardAction
{
    // A new clip was captured and persisted.
    Captured,
    // An existing entry was deleted, or the history was cleared.
    Removed,
    // An entry's pinned state changed.
    PinChanged
}
