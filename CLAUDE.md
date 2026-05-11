# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Timmy Tools is a Windows-only WPF desktop utility built as a teaching sidecar for technical training. It bundles three integrated tools accessible from a single tray icon and from each note's title bar: **sticky notes** (rich-text, auto-save, normal z-order), an **NTP-synced atomic clock**, and a **break timer** with class-tracking fields.

The repo folder is named `PinnyNotes` for historical reasons (the project was forked from [63BeetleSmurf/PinnyNotes](https://github.com/63BeetleSmurf/PinnyNotes)) but everything inside the folder is named TimmyTools/Timmy Tools. Don't rename the folder.

## Build & Run

```bash
dotnet build TimmyTools.sln                    # debug build
dotnet build TimmyTools.sln -c Release         # release build (use this — Tim runs from bin/Release)
dotnet run --project TimmyTools.WpfUi          # run from source
```

Target: **.NET 10.0** (`net10.0` for Core, `net10.0-windows` for WpfUi). The solution has three projects but only two build via `dotnet`: **TimmyTools.Core** (class library) and **TimmyTools.WpfUi** (WPF exe, output assembly is `Timmy Tools.exe`). **TimmyTools.Setup** is a Visual Studio Installer project (`.vdproj`) and does not build from CLI — it requires Visual Studio with the Installer Projects extension.

If `dotnet build` fails with `MSB3027 / MSB3026` file-lock errors on `TimmyTools.Core.dll`, the running `Timmy Tools.exe` is holding the DLL. Close the app and rebuild. There are no automated tests in the repo.

## Architecture

**MVVM with pub/sub messaging.** Views bind to ViewModels, ViewModels communicate via `MessengerService` (typed message records under `Messages/`), and all services are wired up via `Microsoft.Extensions.DependencyInjection` in `App.xaml.cs`.

### Project layout

- **TimmyTools.Core** — Data layer. SQLite via `Microsoft.Data.Sqlite`, repository pattern, immutable `record` DTOs, enums, and sequential schema migrations.
- **TimmyTools.WpfUi** — UI layer. Views (XAML + code-behind), ViewModels, observable Models (`INotifyPropertyChanged`), Services, Commands, Controls, Helpers, Interop (Win32 P/Invoke), Themes.

### Three windowed tools, one tray icon

- `NoteWindow` — the sticky note. Uses standard WPF z-order — no `HWND_TOPMOST` intervention. Notes stack like any ordinary window: clicking another app sends the note behind it, clicking the note brings it forward. `Window_Activated` / `Window_Deactivated` (NoteWindow.xaml.cs:138-166) only update `Note.IsFocused` (read by `UpdateOpacity` for the "Opaque when focused" setting), title-bar visibility, and the deactivate-time save. Title bar buttons launch the other two tools. **Do not reintroduce `HWND_TOPMOST` tied to focus** — the prior focus-tied state machine had unfixable races and was removed; see memory `notes-topmost-when-focused.md`. If a future "pin this note" feature is requested, gate it on an explicit per-note `NoteSettings.AlwaysOnTop` boolean, not on focus.
- `AtomicClockWindow` / `NtpService` — NTP v4 (RFC 1305) client with four fallback servers (time.nist.gov, pool.ntp.org, time.google.com, time.windows.com) and a 10-minute re-sync interval.
- `BreakTimerWindow` — countdown with presets, custom durations, and "Class" / "Next Up" fields.

### Note lifecycle

`WindowService` creates a `NoteWindow` → `NoteViewModel.Initialize()` creates or loads a `NoteModel` → auto-save runs every 2s via `DispatcherTimer` and on deactivate → `CloseNote()` saves, or deletes the row if the note is empty. State changes publish `NoteActionMessage` via `MessengerService`. `WindowService` tracks open windows by `NoteId` to prevent duplicates and manages singleton Settings/Management windows.

The RTF binding (`NoteTextBoxControl.RtfContentProperty`) uses `UpdateSourceTrigger=LostFocus` to keep selection stable during typing — which means periodic 2s saves would miss in-progress edits. `NoteViewModel.FlushPendingEdits` (an `Action` callback set by `NoteWindow.xaml.cs:53-57`) calls `BindingExpression.UpdateSource()` on the RTF binding before every save in `SaveNote()`, so auto-save captures the current text even while the textbox still has focus. Don't switch the binding to `PropertyChanged` — it makes selection jump during typing.

Auto-resize (vertical) is guarded by `_isInitialLoadComplete` (NoteWindow.xaml.cs:32, set in `Window_Loaded` at DispatcherPriority.Loaded) so the first content load doesn't trigger a resize before the window is fully laid out.

### Settings pipeline

Four observable model objects — `ApplicationSettingsModel`, `NoteSettingsModel`, `EditorSettingsModel`, `ToolSettingsModel` — are loaded from SQLite by `SettingsService` on startup and saved on exit. They flow: `SettingsRepository` → immutable `SettingsDataDto` → `SettingsService` → models → `SettingsViewModel` data binding → `SettingsWindow.xaml`. User edits flow back through the same path.

### Database & migrations

- SQLite at `%APPDATA%/Timmy Tools/timmy_tools.sqlite` (installed) or next to the exe (debug, or when a `portable.txt` marker file exists beside the exe).
- **Current schema version: 7.** `DatabaseInitializer` (note: the file is `DatabaseInitializer.cs` but the class is `DatabaseInitialiser` — British spelling on the type) walks sequential migrations from the user's current version up to `SchemaVersion`. Migrations live in `TimmyTools.Core/Migrations/` (`Schema1To2Migration` through `Schema6To7Migration`) and inherit from `_SchemaMigration`. To bump the schema: add a new `SchemaNToN+1Migration`, register it in `DatabaseInitialiser.UpdateDatabase`, and increment `DatabaseInitialiser.SchemaVersion`.
- `DatabaseConfiguration` performs a one-shot data migration from the legacy PinnyNotes layout: renames `%APPDATA%\Pinny Notes` → `%APPDATA%\Timmy Tools`, `pinny_notes.sqlite` → `timmy_tools.sqlite`, and `pinny_notes_backup_*` → `timmy_tools_backup_*`. Don't remove this until the transition window ends.
- `DatabaseBackupService` is a singleton started in `OnStartup` and stopped in `OnExit`.

### Note text-box context menu

The right-click menu inside a note is built procedurally in `Controls/ContextMenus/NoteTextBoxContextMenu.cs` — a single class that wires up Undo/Redo, clipboard ops, Font/Size/Style/Case/Color/Paragraph submenus, counts, and Locked toggle. There is **no `Tools/` folder and no `BaseTool` hierarchy** (an older design that has since been collapsed). To add a new submenu item, edit `NoteTextBoxContextMenu` directly — wire up a `MenuItem`, add it to the appropriate parent in the constructor, and bind it to a `RelayCommand` or a built-in `ApplicationCommands.*` target.

### Service registration (`App.xaml.cs::ConfigureServices`)

- **Singletons:** `DatabaseConfiguration`, `SettingsRepository`, `AppMetadataRepository`, `NoteRepository`, `AppMetadataService`, `SettingsService`, `MessengerService`, `WindowService`, `ThemeService`, `DatabaseBackupService`, `NtpService`
- **Transients:** `NotifyIconService`, `SettingsWindow`/`SettingsViewModel`, `ManagementWindow`/`ManagementViewModel`, `BreakTimerWindow`/`BreakTimerViewModel`, `AtomicClockWindow`/`AtomicClockViewModel`

### Single instance

App uses a named `Mutex` plus an `EventWaitHandle` with separate GUIDs for Debug vs Release (see `App.xaml.cs:26-27`). A second instance signals the existing instance via the wait handle and exits — the running instance receives an `ApplicationActionMessage(ApplicationAction.NewInstance)` and creates a new note.

### Win32 interop

`Interop/` holds P/Invoke wrappers (`User32`, `HWND`, `SWP`, `GWL`, `WS_EX` constants) and `ScreenHelper` for multi-monitor bounds. Currently used for **taskbar / Alt-Tab visibility** (`NoteViewModel.UpdateVisibility` toggles `WS_EX_TOOLWINDOW` via `GetWindowLongPtrW` / `SetWindowLongPtrW`) and **multi-monitor screen-bounds math** (`ScreenHelper` uses `MonitorFromWindow` / `GetMonitorInfoW` for note placement and gravity-based cascading). The `User32.SetWindowPos` P/Invoke and the `HWND.TOPMOST` / `HWND.NOTOPMOST` constants are currently unused — they're kept as a generic wrapper in case a future per-note "pin" feature needs them.

## Coding conventions (.editorconfig-enforced)

- **No `var`** — explicit types required (`csharp_style_var_*: false:warning`).
- **British English spelling** in identifiers and the `.editorconfig` (`Colour`, `Initialise`, `CycleColours`, `DatabaseInitialiser`). The existing public surface uses these; a Tim-specific override in user memory says new code can use American English where it doesn't conflict with existing identifiers, but the existing British-spelled identifiers cannot be renamed without a migration effort.
- **File-scoped namespaces.**
- **Allman braces** (open brace on its own line).
- 4-space indentation, CRLF line endings.
- PascalCase for types/members, `I` prefix for interfaces.
- DTOs are C# `record` types (immutable).
- Models inherit `BaseModel` and use `SetProperty<T>()`, which auto-flips `IsSaved = false`.
- Commands use `RelayCommand` / `RelayCommand<T>` from `Commands/`.

## Window dragging

Notes use a custom title bar (`WindowStyle="None"` + `WindowChrome` with `CaptionHeight="0"`), so OS-managed dragging is disabled. `NoteWindow.xaml.cs::TitleBar_MouseDown` handles the three click cases explicitly: right-click returns early (lets `TitleBarContextMenu` open), double-left-click toggles roll-up via `ToggleRollUp()` and marks the event handled, and single-left-click calls `Window.DragMove()`. Don't replace this with `WindowChrome.IsHitTestVisibleInChrome` — it conflicts with the roll-up logic.
