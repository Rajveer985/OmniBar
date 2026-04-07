# WinSpotlight 🔍

A macOS Spotlight-style launcher for Windows, built with C# and WPF.

---

## Features

| Feature | How it works |
|---------|-------------|
| **Launch apps** | Indexes all Start Menu shortcuts — type the app name and hit Enter |
| **Search files** | Searches Desktop, Documents, Downloads, Pictures, Music, Videos |
| **Web search** | Always available at the bottom — opens Google in your default browser |
| **Calculator** | Type any math expression (`3 * (4 + 2)`) — result is copied to clipboard on Enter |
| **Clipboard history** | Tracks your last 25 clipboard entries — select to copy again |

---

## Requirements

- Windows 10 or 11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (or SDK to build)

---

## Build & Run

### Option A — dotnet CLI (recommended)
```
cd WinSpotlight
dotnet run
```

### Option B — publish a single EXE
```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```
Output is in `bin\Release\net8.0-windows\win-x64\publish\WinSpotlight.exe`

### Option C — Visual Studio
Open the folder in Visual Studio 2022 (or later), press **F5**.

---

## Usage

| Action | Key / gesture |
|--------|--------------|
| **Open** WinSpotlight | `Alt + Space` |
| **Navigate** results | `↑` / `↓` arrow keys |
| **Launch** selected | `Enter` or single click |
| **Close** | `Esc` or click elsewhere |
| **System tray** | Right-click the tray icon to open or exit |

> **Tip — Calculator:** Just type an expression like `(100 + 50) * 0.18` and press Enter to copy the result.
>
> **Tip — Clipboard:** Copy several things throughout your day, then open Spotlight to paste any of them back.

---

## Hotkey conflict?

If `Alt+Space` is already bound by another app (e.g. Windows PowerToys), the app automatically falls back to `Alt+\`` (backtick).  
To use a different shortcut, edit `MainWindow.xaml.cs` — look for `VK_SPACE = 0x20` and replace with your preferred [virtual-key code](https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes).

---

## Project structure

```
WinSpotlight/
├── App.xaml / App.xaml.cs        — startup, system tray
├── MainWindow.xaml / .cs         — Spotlight UI, keyboard navigation
├── Models.cs                     — SearchResult data model
├── HotkeyManager.cs              — global Alt+Space hotkey (Win32)
├── ClipboardManager.cs           — clipboard history watcher (Win32)
├── SearchEngine.cs               — all search providers
└── WinSpotlight.csproj
```

---

## Add to Windows startup (optional)

1. Press `Win + R` → type `shell:startup` → Enter
2. Create a shortcut to `WinSpotlight.exe` in that folder

It will start minimised to the system tray on login.
