# RA Logic Editor (Qt Edition)

RA Logic Editor is a development tool for RetroAchievements developers to compile, modify, and manage achievement and leaderboard logic.

This repository contains the **C++17 / Qt 6 port** of the original C#/.NET WinForms application, rebuilt natively using the Qt Widgets framework with feature parity.

## Key Features

- **Interactive Trigger Editor:** Grid-based logic editing with drag reordering, copy/paste, bulk property editing, code note aliases in cells, collapsible chains for `Add Address` blocks, and owner-drawn row headers that highlight active logic chains.
- **Smart Code Note Editor:** Hierarchical pointer parsing into tree structures, hierarchy fixing and formatting, deep search across nested notes, and safe node deletion that removes orphaned dependencies.
- **Automation Generators:** Logic multiplier, batch note multiplier, multi-region support with `refer to $0x...` resolution and regional Pause If locks, and a logic optimizer for reducing script size.
- **Code Review and Live Validation:** Static analysis for missing Pause If resets, stale pointer reads, size mismatches, and undocumented addresses, with navigation to the offending row.
- **Rich Presence Tooling:** Rich Presence script parsing, editing, and validation.
- **Theming:** Light and dark application themes using Qt stylesheets.

## File Resolution and Project Loading

The editor follows standard RetroAchievements naming conventions based on the **Game ID**. Opening a primary file automatically loads adjacent files from the same directory:

- **Server Data:** `<GameID>.json` (Core and Unofficial assets).
- **Local Edits:** `<GameID>-User.txt` (prioritized and highlighted).
- **Code Notes:** `<GameID>-Notes.json`.
- **Rich Presence:** `<GameID>-Rich.txt`.

## Building from Source

### Prerequisites

- **Operating System:** Windows 10 or newer.
- **Qt:** Qt 6.11.2 or later Qt 6.x with the `msvc2022_64` kit, including the **Qt Concurrent** and **Qt Svg** modules.
- **CMake:** Version 3.20 or newer.
- **Compiler:** Visual Studio 2022 (MSVC v143) or Visual Studio 2022 Build Tools.

### Build Steps

```
cmake -B build -G "Visual Studio 17 2022" -A x64 -DCMAKE_PREFIX_PATH="C:/Qt/6.11.2/msvc2022_64"
cmake --build build --config Release
```

The build automatically runs `windeployqt` as a post-build step and deploys the required Qt plugins into a `plugins/` directory alongside the executable.

Windows x64 builds are also produced through GitHub Actions using `.github/workflows/build.yml`.

## Project Layout

```
src/
  Common/        App settings, formatting helpers, image cache
  Definitions/   Memory sizes, syntax and region definitions
  Logic/         Core code note logic
  Models/        Data models for assets, notes, and projects
  Parsers/       Achievement, Rich Presence, and pointer tree parsers
  Services/      Validation, generation, optimization, persistence
  UI/            Main window, dialogs, models, widgets, and styles
  Utilities/     Logic formatting
resources/       Icons, Windows resource file, Qt resource collection
```
