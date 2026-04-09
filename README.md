# OmniBar

A macOS Spotlight-style launcher for Windows, built with C# and WPF.

---

## Features

| Feature | How it works |
|---------|-------------|
| **Fluid Animations** | High-end macOS-style "pop-in" transitions and smooth layout shifts |
| **Launch apps** | Indexes all Start Menu shortcuts — type the app name and hit Enter |
| **Timer Adder** | Type `timer 5m` to set a countdown in the native Windows Clock app |
| **Calendar Events**| View upcoming schedule directly from the search bar (WinRT integration) |
| **Search files** | Searches Desktop, Documents, Downloads, Pictures, Music, Videos |
| **Calculator** | Type any math expression (`3+2`) — results appear instantly |
| **Clipboard history**| Tracks your last 25 entries — select to copy again |
| **Web search** | Always available at the bottom — opens Google in your browser |

---

## Requirements

- Windows 10 or 11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (or SDK to build)

---

## Build & Run

### Option A — dotnet CLI (recommended)
```
cd OmniBar
dotnet run
```

### Option B — publish a single EXE
```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true
```
Output is in `bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\WinSpotlight.exe`

### Option C — Visual Studio
Open the folder in Visual Studio 2022 (or later), press **F5**.

---

## Usage

| Action | Key / gesture |
|--------|--------------|
| **Open** OmniBar | `Alt + Space` |
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
OmniBar/
├── App.xaml / App.xaml.cs        — startup, system tray
├── MainWindow.xaml / .cs         — Spotlight UI, keyboard navigation
├── Models.cs                     — SearchResult data model
├── HotkeyManager.cs              — global Alt+Space hotkey (Win32)
├── ClipboardManager.cs           — clipboard history watcher (Win32)
├── SearchEngine.cs               — all search providers
└── OmniBar.csproj
```

---

## Add to Windows startup (optional)

1. Press `Win + R` → type `shell:startup` → Enter
2. Create a shortcut to `OmniBar.exe` in that folder

It will start minimised to the system tray on login.
