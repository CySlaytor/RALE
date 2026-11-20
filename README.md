# RetroAchievements Logic Editor (RALE)

![Language](https://img.shields.io/badge/Language-C%2B%2B17-blue)
![Framework](https://img.shields.io/badge/Framework-Qt6-green)
![Platform](https://img.shields.io/badge/Platform-Windows-lightgray)

A native C++ achievement logic authoring, code note management, and quality-assurance utility built with Qt6, designed for RetroAchievements developers and code review workflows.

Originally developed in C# as **RALogicEditor**, the project has been rebranded as **RALE** and rebuilt using the **C++17 / Qt 6** framework with a renewed architecture and interface.

---

## Features

- **Interactive Trigger Editor:**
  - Grid-based authoring for both **Achievements** (`Core` and `Alt` groups) and **Leaderboards** (`Start`, `Cancel`, `Submit`, `Value`).
  - Owner-drawn vertical chain connector headers highlighting `Add Address`, `Add Source`/`Sub Source`, `AndNext`/`OrNext`, `ResetNextIf`, and `Add Hits`/`Sub Hits` blocks.
  - Toggleable **Collapse Chains** (hides intermediate `Add Address` rows), **Show Aliases** (resolves memory addresses and pointer chains against Code Notes), and **Show Decimal** views.
  - Pattern-based row selection extension (**Extend Selection by Pattern**), bulk column property editing, Smart Paste, and multi-row drag/keyboard reordering (`Alt+Up` / `Alt+Down`).

- **Smart Code Note Editor:**
  - Parses hierarchical pointer chains (`+0x`, `-0x`), array offsets (`|0x`, `|+0x`, `|-0x`), and Unicode box-drawing trees (`├─`, `└─`, `│`).
  - Supports `refer to $0x...` note redirection, virtual/indirect offset editing, intelligent orphan-aware node deletion, and **Fix Hierarchy** normalization.
  - **Batch Stride Note Cloner (`Clone Stride...`)** accessible from both the Code Note Editor and the **Tools** menu for duplicating repeating structures (e.g., Level 1..N tables, character slots) with clipboard name pasting.

- **Automation & Multi-Region Tooling:**
  - **Logic Generators:** Expand template groups into sequential `Alt` groups or in-group arrays with configurable stride and copy counts, or bake them back into static conditions.
  - **Multi-Region Support:** Automatically detects regions from Code Notes (`USA`, `Europe`, `Japan`, `Asia`), maps address ranges and redirects, applies regional `Pause If` locks, updates Rich Presence display conditions, and supports reverting back to a single base region.
  - **Logic Optimizer:** Strips redundant leading hex zeros to shrink oversized scripts under the 65,535-character limit, or pads values back to 8-character hex for readability.

- **Code Review & Live Validation:**
  - Static analysis across all assets for incomplete chains, `Pause If` hit locks missing resets, stale `Add Address` (`Delta`/`Prior`) reads, comparison size mismatches, missing Deltas, database length limits, and undocumented memory / broken pointer chains.
  - Double-clicking or activating any reviewer issue jumps directly to the offending asset and condition row.

- **Diffing, Rich Presence & Theming:**
  - Side-by-side **Asset Diff Viewer** (`Server` vs `Local`) and **Rich Presence Diff Viewer** with backup/revert support.
  - Light and Dark themes with customizable Code Note formatting standards and live preview in **Settings**, plus built-in GitHub release update checking.

---

## Project File Resolution

RALE follows standard RetroAchievements `RACache` naming conventions based on the **Game ID**. Opening a primary file automatically loads adjacent files from the same directory:

- **Server Data:** `<GameID>.json` (`Core` and `Unofficial` achievements and leaderboards)
- **Local Edits:** `<GameID>-User.txt` (prioritized and color-coded as `Modified` or `Local`, with automatic timestamped backups on save)
- **Code Notes:** `<GameID>-Notes.json` (merged with local `N0:` overrides from `-User.txt`)
- **Rich Presence:** `<GameID>-Rich.txt`

---

## Keyboard Shortcuts

### Asset List & General
| Shortcut | Action |
| :--- | :--- |
| `Ctrl+O` | Open project file |
| `Ctrl+S` | Save local edits to `-User.txt` |
| `Ctrl+Z` / `Ctrl+Y` | Context-aware Undo / Redo |
| `Ctrl+,` | Open Settings |
| `F8` | Toggle Logic Reviewer panel |
| `Enter` | Load selected asset into Trigger Editor |
| `Ctrl+D` | Duplicate selected asset(s) |
| `Alt+Up` / `Alt+Down` | Move selected Local asset(s) up / down |
| `Delete` | Delete selected Local asset(s) |

### Trigger Editor Grid
| Shortcut | Action |
| :--- | :--- |
| `Enter` | Jump to address / pointer chain in Code Note Editor |
| `Alt+Up` / `Alt+Down` | Move selected condition(s) or pointer chain up / down |
| `Ctrl+C` / `Ctrl+Shift+C` | Copy RA Logic / Copy aligned table row(s) |
| `Ctrl+X` / `Ctrl+V` | Cut / Paste Logic (with Smart Paste detection) |
| `Ctrl+D` | Duplicate selected condition(s) |
| `Delete` | Delete selected condition(s) |

---

## Building from Source

### Prerequisites
- **Operating System:** Windows 10 or newer
- **CMake:** Version 3.20 or newer
- **Qt:** Qt 6.x (`Core`, `Widgets`, `Svg`, `Network`) with the `msvc2022_64` kit
- **Compiler:** Visual Studio 2022 (MSVC v143) or Visual Studio 2022 Build Tools

### Build Steps (Windows)
```
git clone https://github.com/CySlaytor/RALE.git
cd RALE
cmake -B build -G "Visual Studio 17 2022" -A x64 -DCMAKE_PREFIX_PATH="C:/Qt/6.11.2/msvc2022_64"
cmake --build build --config Release
```

The build automatically runs `windeployqt` as a post-build step and deploys the required Qt plugins into a `plugins/` directory alongside `RALE.exe`.