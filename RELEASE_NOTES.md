# RetroAchievements Logic Editor v2.0.0 - Release Notes

RetroAchievements Logic Editor (RALE) v2.0.0 is a full C++ / Qt 6 rewrite of RALE, providing responsive achievement and leaderboard authoring, live logic validation, hierarchical code note management, multi-region generation, and Rich Presence tooling.

---

## What's New

### 1. Trigger & Leaderboard Logic Editor
- **Full Trigger Authoring**: Edit Core and Alt groups as well as Leaderboard `Start`, `Cancel`, `Submit`, and `Value` segments with undo/redo history.
- **Chain Collapse, Aliases & Decimal Views**: Collapse `Add Address` pointer chains, resolve memory addresses to code note aliases (including `refer to $0x...` redirects), and toggle hex/decimal immediate display.
- **Logic Generators & Pattern Selection**: Expand repeating conditions across alt groups or array blocks, and extend selections by detected stride/bit patterns.
- **Smart Paste & Bulk Column Editing**: Right-click columns (`Flag`, `Type`, `Size`, `Cmp`) to bulk-update selected rows or smart-paste achievement definitions.

### 2. Code Note Editor & Clone Structure Stride
- **Clone Structure Stride (`Clone Stride...`)**: Ported from Pointer Finder 2.0, placed inline next to the `Address:` dropdown to batch-generate sequential code notes (`{INDEX}`, `{INDEX:02}`, `{INDEX:03}`, `{OFFSET}`, `{NAME}`), auto-detect byte strides (`[144 bytes]`, `[30x16 bytes]`), paste name lists from clipboard, and edit titles inline.
- **Fix Hierarchy**: Standardizes pointer indentation (`+` or Unicode `├─`/`└─`/`│`), dot-indented value descriptions, delimiters, and platform pointer templates (`CodeNoteLogic`).
- **Variable Tree & Intelligent Node Deletion**: Browse parsed pointer/array trees and cleanly remove leaf nodes and orphaned parents (`Delete Note (Selected in Tree)`).

### 3. Live Validation & Code Reviewer (`F8`)
- **Automated Logic Checks**: Flags incomplete chains, `PauseLocks` missing resets, stale `Add Address` references, comparison size mismatches, missing deltas, database character limits (`> 65,535`), and undocumented memory addresses.
- **Global Issue Navigation**: Double-click any issue in the Code Review panel to jump directly to the asset and condition row.

### 4. Multi-Region Support, Optimizer & Diff Tools
- **Multi-Region Logic Generator**: Detects region notes, maps address ranges, generates region alt groups / `PauseIf` locks, updates Rich Presence scripts with backups, and supports reverting a single region.
- **Logic Optimizer**: Strips leading zeros (`0x00001234 -> 0x1234`) or pads hex values across assets to stay within server character limits.
- **Asset & RP Diff Viewers**: Side-by-side synchronized diff comparison between server JSON and local `User.txt` edits or Rich Presence scripts.

---

## Packaging & Build Improvements

- Migrated desktop UI to native Qt 6 (`Fusion` + Light/Dark QSS themes and dynamic SVG icon theming).
- Added manual-only `workflow_dispatch` GitHub Actions release packaging with `windeployqt` deployment.
