# RA Logic Editor (Qt Edition)

RA Logic Editor is a development tool that helps RetroAchievements developers compile, modify, and manage achievement and leaderboard logic. This repository is the **C++17 / Qt 6 port** of the original C#/.NET WinForms application ([RALogicEditor](https://github.com/CySlaytor/RALogicEditor)), rebuilt natively on the Qt Widgets framework with feature parity.

## Key Features

- **Interactive Trigger Editor** — grid-based logic editing with drag reordering, copy/paste, bulk property editing, code note aliases in cells, collapse chains for `Add Address` blocks, and owner-drawn row headers that highlight active logic chains.
- **Smart Code Note Editor** — hierarchical pointer parsing into tree structures, hierarchy fixing/formatting, deep search across nested notes, and safe node deletion that prunes orphaned dependencies.
- **Automation Generators** — logic multiplier (create generator), batch note multiplier, multi-region support tool with `refer to $0x...` resolution and regional Pause If locks, and a logic optimizer for script size reduction.
- **Code Review & Live Validation** — static analysis for missing Pause If resets, stale pointer reads, size mismatches, and undocumented addresses, with one-click navigation to the offending row.
- **Rich Presence tooling** — full Rich (Presence) script parsing, editing, and validation.
- **Theming** — light and dark application themes with Qt stylesheet support.

## File Resolution & Project Loading

The editor follows standard RetroAchievements naming conventions based on the **Game ID**. Opening a primary file automatically loads adjacent files in the same directory:

- **Server Data:** `<GameID>.json` (Core and Unofficial assets)
- **Local Edits:** `<GameID>-User.txt` (prioritized and highlighted)
- **Code Notes:** `<GameID>-Notes.json`
- **Rich Presence:** `<GameID>-Rich.txt`

## Building from Source

### Prerequisites

- Windows 10 or newer
- Qt 6.11.2 (or later Qt 6.x) with the `msvc2022_64` kit, including the **Qt Concurrent** and **Qt Svg** modules
- CMake 3.20 or newer
- Visual Studio 2022 (MSVC v143) or Build Tools for Visual Studio 2022

### Build steps

```bat
cmake -B build -G "Visual Studio 17 2022" -A x64 -DCMAKE_PREFIX_PATH="C:/Qt/6.11.2/msvc2022_64"
cmake --build build --config Release
```

The build runs `windeployqt` automatically as a post-build step and deploys Qt plugins into a `plugins/` directory next to the executable.

Continuous integration builds Windows x64 artifacts with GitHub Actions (`.github/workflows/build.yml`).

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

## License

Released under the MIT License — see [LICENSE.txt](LICENSE.txt).
