# RA Logic Editor

RA Logic Editor is a development tool designed to assist RetroAchievements developers in compiling, modifying, and managing achievement and leaderboard logic. By integrating asset configuration, code note visualization, and automated generation tools under a single interface, it aims to streamline development workflows and reduce repetitive manual scripting.
<img width="1281" height="767" alt="thumbnail" src="https://github.com/user-attachments/assets/75e44941-212f-44d4-887e-4f4dd0f3a7f0" />

---

## Key Features

### 🛠️ Interactive Trigger Editor
- **Advanced Grid Controls:** Drag-and-drop-style logic reordering, copy/paste operations, and instant bulk property editing (flags, sizes, types, and hit counts).
- **Visualization Aids:** Display code note aliases directly within logic cells, and use **Collapse Chains** to condense complex `Add Address` blocks into single, readable rows.
- **Double-Buffered Rendering:** Enhanced row-header painting visually highlights active logic chains (pointer chains, arithmetic operations, and hit counts).

### 📝 Smart Code Note Editor
- **Hierarchical Pointer Parsing:** Parses complex, nested code notes into tree structures for easy navigation.
- **Fix Hierarchy Tool:** Ported from [PointerFinder2](https://github.com/CySlaytor/PointerFinder2). It cleans, merges, and formats indentation layouts automatically. Supports custom visual formats, including Unicode tree graphics (`├─`), dot paths (`.`), and plusses (`+`).
- **Intelligent Search:** Searches beyond top-level headers to find deeply nested sub-pointers, specific variables, and raw descriptions.
- **Intelligent Node Deletion:** Safely removes an offset and its orphaned dependencies from a code note without corrupting the broader layout.

### ⚡ Automation & Productivity Generators
- **Create Generator (Logic Multiplier):** Automatically duplicates logic blocks across sequential index offsets or multi-slot arrays (e.g., slot 1 through slot 4) using custom memory strides.
- **Batch Note Multiplier:** Duplicates documented notes across structures with auto-incrementing text labels (e.g., "Player 1" to "Player 4").
- **Multi-Region Support Tool:** Converts base achievements into multi-region variants (such as USA, PAL, and Japan). This tool resolves target addresses via `refer to $0x...` virtual links, updates the Rich Presence script, and inserts regional Pause If locks.
- **Logic Optimizer:** Strips leading zeros or pads addresses to reduce script size and warn users if logic exceeds standard database character limits.

### 🔍 Code Review & Live Validation
- **Static Analysis:** An entry level real-time logic scanner that alerts of potential execution issues, including:
  - **PauseLocks:** Missing resets on Pause If conditions with hit counts.
  - **Stale Pointers:** Hazardous read orders or stale pointer offsets.
  - **Size Mismatches:** Mismatched comparison sizes across memory reads.
  - **Undocumented Addresses:** Highlights memory cells or pointer leaves missing active code notes.
- **One-Click Navigation:** Double-clicking an issue in the reviewer pane directs you to the exact row and condition inside the trigger grid.

---

## File Resolution & Project Loading

The editor relies on standard RetroAchievements file naming conventions based on your **Game ID**. Opening a primary file (such as a server-downloaded `.json` file) will automatically attempt to load all adjacent files in the same directory:

- **Server Data:** `<GameID>.json` (Core and Unofficial assets)
- **Local Edits:** `<GameID>-User.txt` (Local modifications and new assets)
- **Code Notes:** `<GameID>-Notes.json` (Server-backed documentation)
- **Rich Presence:** `<GameID>-Rich.txt` (RP scripts)

Local modifications found in your `-User.txt` file are prioritized and highlighted in orange. Newly created local assets are highlighted in green.

---

## Installation & Requirements

RA Logic Editor is built on **.NET 8.0** and utilizes **Windows Forms** for a clean, responsive desktop experience.

### Prerequisites
* Windows 10 SP1 or newer
* .NET Runtime 8.0 (Desktop Runtime)

### Building from Source
1. Clone the repository:
   ```
   git clone https://github.com/CySlaytor/RALogicEditor.git
   ```
2. Open the solution file `RALogicEditor.sln` in Visual Studio 2022 (or newer).
3. Restore NuGet dependencies and build the project in **Release** mode.

---

## Configuration & Customization

The application automatically stores UI settings in a local `settings.json` file inside the startup directory, preserving configuration choices across sessions:
- Main splitter distances and panel positions.
- Last active filters (Asset Type, Category, Source Mode).
- Trigger editor preferences (decimal representation, aliases, collapsed chains).
- Code note styling (default indentation engine, value padding).

To fully reset the application to its default state, select **Edit -> Clear Data / Reset...** from the main menu. This will clear user settings, backup archives, and the local cache directories.

---

## Contributing

Contributions, bug reports, and feature suggestions are welcome. Please open an issue to discuss proposed modifications before submitting pull requests.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for more details.
