# GEMINI.md

This file provides foundational guidance for Gemini CLI when working in the Timmy Tools repository.

## Project Overview

**Timmy Tools** (formerly PinnyNotes) is a Windows-only WPF desktop utility designed for technical trainers. It features persistent rich-text sticky notes, an NTP-synced atomic clock, and a configurable break timer.

- **Main Technologies:** .NET 10.0, WPF, SQLite (`Microsoft.Data.Sqlite`), Microsoft DI.
- **Key Dependencies:** `H.NotifyIcon.Wpf` (system tray), `Microsoft.Extensions.DependencyInjection`.
- **Target Platform:** Windows only (WPF limitation).

## Architecture

The project follows the **MVVM (Model-View-ViewModel)** pattern with a **pub/sub messaging** layer for decoupled communication.

### Project Structure
- **TimmyTools.Core:** Data layer. Contains SQLite repositories, DTOs (immutable records), migrations, and enums.
- **TimmyTools.WpfUi:** UI layer. Contains Views (XAML), ViewModels, Models (`INotifyPropertyChanged`), Services, and Win32 Interop.
- **TimmyTools.Setup:** Visual Studio Installer project (`.vdproj`) for MSI generation (requires Visual Studio).

### Key Architectural Patterns
- **Messaging:** `MessengerService` facilitates communication between ViewModels and Services using typed message records (e.g., `NoteActionMessage`).
- **Dependency Injection:** Services are registered in `App.xaml.cs` (Singletons for repositories/services, Transients for windows/ViewModels).
- **Settings Pipeline:** Settings are loaded from SQLite via `SettingsService` into observable models (`ApplicationSettingsModel`, etc.), bound to the UI, and saved on exit.
- **Note Lifecycle:** Managed by `WindowService`. Notes auto-save every 5 seconds via `DispatcherTimer`.

## Development Conventions

### Coding Style (Enforced via .editorconfig)
- **Explicit Types:** Use explicit types instead of `var` (`csharp_style_var_*: false`).
- **Brace Style:** Allman style (braces on new lines).
- **Naming:** PascalCase for types and members; `I` prefix for interfaces.
- **Spelling:** British English (`Colour`, `Initialise`, `Initialiser`) is preferred for code and internal documentation.
- **Namespaces:** File-scoped namespaces are preferred.
- **Indentation:** 4 spaces, CRLF line endings.

### Implementation Details
- **DTOs:** Use C# `record` types for immutability in the data layer.
- **Models:** Inherit from `BaseModel` and use `SetProperty<T>()` to trigger `INotifyPropertyChanged` and track the `IsSaved` state.
- **Win32 Interop:** P/Invoke wrappers in `Interop/` handle advanced window behavior (multi-monitor, always-on-top, taskbar visibility).
- **Portable Mode:** Triggered by the presence of a `portable.txt` file next to the executable.

## Building and Running

### Build Commands
```bash
# Build the entire solution
dotnet build TimmyTools.sln

# Build a specific project (e.g., UI)
dotnet build TimmyTools.WpfUi

# Build release configuration
dotnet build TimmyTools.sln -c Release
```

### Run Commands
```bash
# Run the application
dotnet run --project TimmyTools.WpfUi
```

### Testing
- **TODO:** There are currently no automated tests in the repository. New features should ideally include unit tests for logic in `TimmyTools.Core` or ViewModels in `TimmyTools.WpfUi`.

## Usage & Tooling
- **Database:** SQLite DB located at `%APPDATA%/Timmy Tools/timmy_tools.sqlite` or local folder in portable/debug mode.
- **Single Instance:** Managed via a named Mutex and `EventWaitHandle`.
- **Tools System:** Text transformation tools (e.g., Case, Alignment) inherit from `BaseTool` and are managed by `NoteTextBoxContextMenu`.
