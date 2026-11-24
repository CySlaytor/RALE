# RetroAchievements Logic Editor v2.0.0 - Release Notes

After quite some time working on RALE, I'm happy to share version 2.0.0.

This release is a complete rewrite of the original C# application (`RALogicEditor`) in C++ and Qt 6. Alongside the rewrite, I've spent a lot of time revisiting existing features, fixing issues, and adding tools that I hope will make working with achievement logic and code notes a little easier.

There's quite a bit to cover, so here's what's changed.

---

## What's New

### 1. Interface, Settings & General Improvements

- **Light and Dark Themes**: RALE now supports both Light and Dark themes, which can be changed from **Settings** (`Ctrl+,`). Icons, gridlines, checkboxes, and tooltips have also been adjusted to work with both themes.
- **Settings & Layout Persistence**: Settings now have their own dedicated window. You can configure themes and Code Note formatting rules, with a live preview and a reference guide for QA formatting. Window sizes, splitter positions, filters, and dialog positions are also remembered between sessions.
- **Update Checker**: RALE can now check GitHub for new releases on startup. You can also check manually through **Help → Check for Updates...**, where you can update, skip a release, or be reminded later.

### 2. Trigger & Leaderboard Logic Editor

- **Reset and Revert to Server**: These are now separate actions. Reset discards changes made since the last load or save, while Revert to Server restores the original JSON received from the server and removes local overrides. This distinction is available in both the Trigger Editor and asset list.
- **Undo / Redo Improvements**: Undo and Redo (`Ctrl+Z` / `Ctrl+Y`) now cover more than just trigger conditions. Achievement and leaderboard metadata (Title, Description, Points, Type, Format, and Lower is Better) are included, along with active groups and operations performed by the logic generators.
- **Leaderboard Editing**: Improved handling of `$`-separated Value groups, as well as Start, Cancel, and Submit segments. RALE also remembers which segment you were editing when switching between leaderboards.
- **Extend Selection by Pattern**: The pattern selection tool now handles repeating blocks across multiple rows, selections with gaps, and sequential `Bit0`–`Bit7` flags, including cases where they wrap across byte boundaries.
- **Logic Generator Improvements**:
  - Added an option to **Duplicate only selected address(es)** when generating arrays. This lets you work with selected conditions without duplicating every address, while keeping existing `Add Address` chains intact.
  - Added **Duplicate associated Code Notes**. This can duplicate standalone notes as well as entries inside pointer chains. There's also an editable preview before applying the changes, support for custom title templates, and the ability to paste names from the clipboard. Targets that already have notes are skipped.
- **Bulk Column Editing**: You can now right-click cells in the `Flag`, `Type`, `Size`, and `Cmp` columns to apply changes to multiple selected rows.

### 3. Code Note Editor & Clone Structure Stride

- **Clone Structure Stride (`Clone Stride...`)**: The old v1.0.0 note multipliers have been replaced with a batch note generator, available next to the `Address:` dropdown and through the **Tools** menu. It supports `{INDEX}`, `{INDEX:02}`, `{INDEX:03}`, `{OFFSET}`, and `{NAME}` tokens, automatic byte-stride detection (including `NxM bytes` arrays), inline editing, and pasting name lists from the clipboard.
- **Array & Console-Aware Pointers**:
  - Array element offsets (`|0x`, `|+0x`, `|-0x`) and `NxM bytes` definitions are now parsed. Array templates can also be inherited across sibling pointers and slots.
  - Pointer prefixes and bitmasks are now generated according to the game's Console ID. Supported platforms include GameCube, Wii, N64, PS1, PS2, PSP, Nintendo DS, Dreamcast, and GBA.
- **Fix Hierarchy Improvements**: The hierarchy fixer now handles pointer (`+`) and Unicode (`├─`, `└─`, `│`) indentation, dot-indented value enumerations, root headers, and configurable delimiters (`=`, `:`, `|`). It can also populate intermediate pointer tags when needed.
- **Note-Level Reset and Revert**: Individual code notes now have their own Reset and Revert to Server actions, accessible through buttons and the context menu. Intelligent Delete has also been improved to better handle nested and array nodes.

### 4. Code Review & QA (`F8`)

A considerable amount of work went into expanding the code review tool. The goal was to catch more common issues during development and make it easier to navigate and address them.

- **Issue Categories**: Findings can now be filtered by *Show All Issues*, *Logic Warnings & Errors*, *Missing Code Notes*, *Code Notes*, *Presentation & Writing*, and *Set Design*.
- **Additional Logic Checks**:
  - Checks for `Hits Without Reset`, `Useless AndNext`, `Useless PauseIf` / `Pausing Measured`, `Useless ResetIf`, `ResetIf Hitcount = 1`, `Useless ResetNextIf`, `AddHits Without Target`, `Negative Offset`, `Questionable Use of Prior`, `Pointer Comparison`, `Type Mismatch`, `One-Condition Achievement`, and more.
  - Improved `Delta` checks across Core, Alt, and Leaderboard segments.
- **Code Note Checks**:
  - Checks for console-specific memory regions, including Echo RAM, Mirror RAM, Kernel RAM, BIOS/ROM, and Stack ranges, with suggested alternative addresses where applicable.
  - Reports missing note sizes, hexadecimal enumerations without prefixes, and enumerated values that exceed the declared bit size of a note.
- **Presentation & Writing Checks**:
  - Checks achievement titles against Chicago Manual of Style (CMOS) Title Case rules.
  - Flags smart quotes, emojis, foreign and special characters, and brackets in descriptions.
  - Title and Description fields also receive color-coded highlights to make these findings easier to spot.
- **Set Design Checks**:
  - Reports missing Progression/Win typing and duplicate achievement or leaderboard titles and descriptions.
- **Suggested Fixes**: Some findings now include a lightbulb (`💡`) indicating that an automatic fix is available. Selecting **Suggested Fix...** opens a before/after preview before applying it. Current fixes include `PauseIf`/`ResetIf` inversion, removing redundant `ResetIf` hit counts, removing unnecessary `AndNext` flags, applying CMOS Title Case, and normalizing smart quotes.
- **References & Navigation**: The `Link` entry in the `Ref` column opens the relevant RetroAchievements documentation. Double-clicking an issue takes you directly to the affected asset, segment, group, condition, metadata field, or code note address.

### 5. Multi-Region, Optimizer & Diff Tools

- **Multi-Region Mapping**: Address swapping now preserves `Add Address` pointer offsets. The tool also recognizes `NxM bytes` and bit-size range annotations. Preferences for `Generate as PauseLocks` and `Update Rich Presence` are remembered, and group ordering is preserved.
- **Diff Viewers**: Asset Diff and Rich Presence Diff now have synchronized horizontal and vertical scrolling between the Server/Original and Local/Current panels.
- **Logic Optimizer**: Improved integration with project Undo/Redo, along with more accurate asset sync state updates when stripping or padding hexadecimal values.

---

## Compatibility & Migration Notes

- **Existing Projects**: RALE remains compatible with existing `RACache` files, including `<GameID>.json`, `<GameID>-User.txt`, `<GameID>-Notes.json`, and `<GameID>-Rich.txt`. UTF-8 BOM handling and special-character escaping when saving and reloading `-User.txt` have also been improved.
- **Settings**: Configuration is now stored in `settings.ini` and `Metadata/layout.ini`, replacing the `settings.json` format used by v1.0.0.
- **Snippet Library**: The standalone Snippet Library from v1.0.0 has been removed. Its functionality is now covered by direct logic copy/paste, pattern selection, and the template generators.

---

That's pretty much everything for v2.0.0. There's been a lot of work behind this release, and I hope some of these additions will prove useful to fellow developers working on their sets.

As always, feedback, bug reports, and suggestions are welcome.