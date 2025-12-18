using RALogicEditor.Controls;
using RALogicEditor.Definitions;
using RALogicEditor.Forms;
using RALogicEditor.Models;
using RALogicEditor.Parsers;
using RALogicEditor.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace RALogicEditor.Helpers
{
    // Manages the interaction logic for the Condition Grid.
    // Handles events, input validation, context menus, and clipboard operations.
    public class ConditionGridManager
    {
        private readonly DoubleBufferedDataGridView _grid;

        // Context menus for different column types
        private readonly ContextMenuStrip _flagContextMenu = new();
        private readonly ContextMenuStrip _typeContextMenu = new();
        private readonly ContextMenuStrip _sizeContextMenu = new();
        private readonly ContextMenuStrip _cmpContextMenu = new();
        private readonly ContextMenuStrip _cmpArithmeticContextMenu = new();
        private readonly ContextMenuStrip _cmpStrictContextMenu = new();

        public event EventHandler? LogicChanged;
        public event EventHandler<string>? StatusUpdateRequested;
        public event EventHandler<string>? AddressSelected;
        public event EventHandler<PointerChainArgs>? PointerChainSelected;

        public event EventHandler<string>? PasteRequested;

        // Property to track display preference (Hex vs Decimal)
        public bool ShowDecimal { get; set; } = false;
        // Property to track Alias display
        public bool ShowAliases { get; set; } = false;
        // Property to track Chain Collapse state
        public bool CollapseChains { get; set; } = false;

        // Dictionary to lookup parsed notes by address
        // Key: Normalized Address "0x1234", Value: List of NoteNodes (Tree)
        public Dictionary<string, List<NoteNode>> NotesLookup { get; set; } = new();

        // Cache for range-based notes (e.g. Structs covering [176 bytes])
        // Tuple: Start Address, End Address, Note Node
        public List<(long Start, long End, NoteNode Node)> RangeCache { get; set; } = new();

        public ConditionGridManager(DoubleBufferedDataGridView grid)
        {
            _grid = grid;
            InitializeContextMenus();
            WireUpGridEvents();
        }

        private void WireUpGridEvents()
        {
            _grid.CellValueChanged += Grid_CellValueChanged;
            _grid.CellMouseDown += Grid_CellMouseDown;
            _grid.CellDoubleClick += Grid_CellDoubleClick;
            _grid.EditingControlShowing += Grid_EditingControlShowing;
            _grid.CellEndEdit += Grid_CellEndEdit;
            _grid.KeyDown += Grid_KeyDown;

            // Wiring up Tooltips
            _grid.CellToolTipTextNeeded += Grid_CellToolTipTextNeeded;
        }

        // Dynamically creates context menus based on definitions in RaLogicSyntax
        private void InitializeContextMenus()
        {
            CreateMenu(_flagContextMenu, RaLogicSyntax.Flags);
            CreateMenu(_typeContextMenu, RaLogicSyntax.OperandTypes);
            CreateMenu(_sizeContextMenu, RaLogicSyntax.MemorySizes);
            CreateMenu(_cmpContextMenu, RaLogicSyntax.ComparisonOperators);
            CreateMenu(_cmpArithmeticContextMenu, RaLogicSyntax.ArithmeticOperators);
            // Strict operators exclude "None"
            var strictOps = RaLogicSyntax.ComparisonOperators.Where(op => op != RaLogicSyntax.NO_OPERATOR_TEXT).ToArray();
            CreateMenu(_cmpStrictContextMenu, strictOps);
        }

        private void CreateMenu(ContextMenuStrip menu, string[] items)
        {
            foreach (var item in items)
            {
                // Escape ampersands for menu items
                var text = item.Replace("&", "&&");
                var menuItem = new ToolStripMenuItem(text, null, OnContextMenuItemClick) { Tag = item };
                menu.Items.Add(menuItem);
            }
        }

        // Handles keyboard shortcuts for Copy/Paste and Enter key navigation
        private void Grid_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && _grid.CurrentCell != null)
            {
                // Enter on an address cell triggers the "Follow Address" logic
                HandleAddressSelection(_grid.CurrentCell.RowIndex, _grid.CurrentCell.ColumnIndex);
                e.Handled = true;
            }

            // QoL: Ctrl+A to Select All
            if (e.Control && e.KeyCode == Keys.A)
            {
                _grid.SelectAll();
                e.Handled = true;
                return;
            }

            // QoL: Alt+Up/Down for moving rows (Mirrors Context Menu)
            if (e.Alt && e.KeyCode == Keys.Up)
            {
                MoveSelected(-1);
                e.Handled = true;
                return;
            }
            if (e.Alt && e.KeyCode == Keys.Down)
            {
                MoveSelected(1);
                e.Handled = true;
                return;
            }

            // Prevent modification in ReadOnly mode, but allow Copy
            if (_grid.ReadOnly && e.KeyCode != Keys.C) { if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.V || e.KeyCode == Keys.X) { e.Handled = true; return; } }

            // MODIFIED: Swapped Copy Shortcuts
            if (e.Control && e.KeyCode == Keys.C && !e.Shift)
            {
                CopySelectedLogic();
                e.Handled = true;
            }
            else if (e.Control && e.Shift && e.KeyCode == Keys.C)
            {
                CopySelectedRows();
                e.Handled = true;
            }
            // NEW: Cut support (Ctrl+X)
            else if (e.Control && e.KeyCode == Keys.X)
            {
                CutSelected();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.V)
            {
                if (Clipboard.ContainsText())
                    PasteRequested?.Invoke(this, Clipboard.GetText());
                e.Handled = true;
            }
        }

        // Handles Right-Click context menus
        private void Grid_CellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                // Ensure the row is selected on right-click
                // Only clear selection if the target row is NOT already part of the selection
                if (!_grid.Rows[e.RowIndex].Selected)
                {
                    _grid.ClearSelection();
                    _grid.Rows[e.RowIndex].Selected = true;
                }

                if (_grid.ReadOnly) return;

                var colName = _grid.Columns[e.ColumnIndex].Name;

                // Show appropriate menu based on column type
                if (colName == "ColFlag") _flagContextMenu.Show(Cursor.Position);
                else if (colName == "ColLType" || colName == "ColRType") _typeContextMenu.Show(Cursor.Position);
                else if (colName == "ColLSize" || colName == "ColRSize") _sizeContextMenu.Show(Cursor.Position);
                else if (colName == "ColCmp")
                {
                    // Show specific operator menus based on the current Flag (e.g., Arithmetic vs Comparison)
                    var flag = _grid.Rows[e.RowIndex].Cells["ColFlag"].Value?.ToString() ?? "";
                    if (RaLogicSyntax.ArithmeticFlags.Contains(flag)) _cmpArithmeticContextMenu.Show(Cursor.Position);
                    else if (RaLogicSyntax.StrictComparisonFlags.Contains(flag)) _cmpStrictContextMenu.Show(Cursor.Position);
                    else _cmpContextMenu.Show(Cursor.Position);
                }
            }
        }

        // Double click acts as a right click for context menus
        private void Grid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0) Grid_CellMouseDown(sender, new DataGridViewCellMouseEventArgs(e.ColumnIndex, e.RowIndex, 0, 0, new MouseEventArgs(MouseButtons.Right, 1, 0, 0, 0)));
        }

        // Provides tooltip text for memory addresses
        private void Grid_CellToolTipTextNeeded(object? sender, DataGridViewCellToolTipTextNeededEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                var colName = _grid.Columns[e.ColumnIndex].Name;
                if (colName == "ColLValue" || colName == "ColRValue")
                {
                    // Get the raw object value to check type, or check the Type column
                    var row = _grid.Rows[e.RowIndex];
                    if (row.Tag is AchievementCondition cond)
                    {
                        Operand op = colName == "ColLValue" ? cond.LeftOperand : cond.RightOperand;
                        if (RaLogicSyntax.MemoryTypes.Contains(op.Type))
                        {
                            string addr = op.Value.Trim();
                            if (!addr.StartsWith("0x")) addr = "0x" + addr;

                            if (NotesLookup.TryGetValue(addr, out var nodes) && nodes.Count > 0)
                            {
                                // Find the root node (indent -2) or fallback to first
                                var root = nodes.FirstOrDefault(n => n.IndentLevel == -2) ?? nodes[0];
                                string desc = root.Description;
                                if (string.IsNullOrEmpty(desc)) desc = root.Content.Split('\n')[0];

                                // Strip tags for tooltip as well
                                e.ToolTipText = Regex.Replace(desc, @"\[.*?\]", "").Trim();
                            }
                            else
                            {
                                // Fallback to Range Cache for large structs/arrays
                                string cleanAddr = addr.Replace("0x", "").Trim();
                                if (long.TryParse(cleanAddr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long targetVal))
                                {
                                    var match = RangeCache.FirstOrDefault(r => targetVal >= r.Start && targetVal < r.End);
                                    if (match.Node != null)
                                    {
                                        string desc = match.Node.Description;
                                        if (string.IsNullOrEmpty(desc)) desc = match.Node.Content.Split('\n')[0];
                                        desc = Regex.Replace(desc, @"\[.*?\]", "").Trim();

                                        long delta = targetVal - match.Start;
                                        if (delta > 0) desc += $" +0x{delta:X}";

                                        e.ToolTipText = desc;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // Configures the TextBox control when editing a cell (Input Validation)
        private void Grid_EditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (e.Control is TextBox editingControl && _grid.CurrentCell != null)
            {
                editingControl.CharacterCasing = CharacterCasing.Lower;
                editingControl.KeyPress -= EditingControl_KeyPress;
                editingControl.KeyPress += EditingControl_KeyPress;

                string colName = _grid.Columns[_grid.CurrentCell.ColumnIndex].Name;
                string type = colName == "ColLValue" ? (_grid.Rows[_grid.CurrentCell.RowIndex].Cells["ColLType"].Value?.ToString() ?? "") : (_grid.Rows[_grid.CurrentCell.RowIndex].Cells["ColRType"].Value?.ToString() ?? "");

                if (type == "Float") editingControl.MaxLength = 32;
                else if (type == "Value") editingControl.MaxLength = 15;
                else
                {
                    // Hex addresses limit
                    editingControl.MaxLength = 8;
                    // Auto-strip 0x prefix for easier editing
                    if (editingControl.Text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                        editingControl.Text = editingControl.Text.Substring(2);
                }
            }
        }

        // Enforces Hexadecimal or Floating point character rules
        private void EditingControl_KeyPress(object? sender, KeyPressEventArgs e)
        {
            if (char.IsControl(e.KeyChar)) return;
            bool allowFloat = false;
            if (_grid.CurrentCell != null)
            {
                string colName = _grid.Columns[_grid.CurrentCell.ColumnIndex].Name;
                string type = colName == "ColLValue" ? (_grid.Rows[_grid.CurrentCell.RowIndex].Cells["ColLType"].Value?.ToString() ?? "") : (_grid.Rows[_grid.CurrentCell.RowIndex].Cells["ColRType"].Value?.ToString() ?? "");
                allowFloat = type == "Float";
            }

            // Allow Hex chars
            bool isHex = char.IsDigit(e.KeyChar) || (e.KeyChar >= 'a' && e.KeyChar <= 'f') || (e.KeyChar >= 'A' && e.KeyChar <= 'F') || e.KeyChar == 'x' || e.KeyChar == 'X';
            if (isHex) return;

            // Allow Float chars
            if (allowFloat && (e.KeyChar == '.' || e.KeyChar == '-' || e.KeyChar == ',')) return;

            e.Handled = true; // Block invalid input
        }

        // Updates the Model object (AchievementCondition) when cell values change
        private void Grid_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = _grid.Rows[e.RowIndex];
            if (row.Tag is not AchievementCondition cond) return;
            var cell = row.Cells[e.ColumnIndex];
            string value = cell.Value?.ToString() ?? "";

            // Parse input string into Model
            if (cell.OwningColumn.Name == "ColLValue") cond.LeftOperand.Value = LogicFormatter.ParseAndFormatValue(value, cond.LeftOperand.Type, cond.LeftOperand.Size);
            else if (cell.OwningColumn.Name == "ColRValue") cond.RightOperand.Value = LogicFormatter.ParseAndFormatValue(value, cond.RightOperand.Type, LogicFormatter.GetRightOperandSizeReference(cond));
            else if (cell.OwningColumn.Name == "ColHits") { var match = Regex.Match(value, @"\d+"); cond.RequiredHits = match.Success ? uint.Parse(match.Value) : 0; }
        }

        // Called when editing finishes; refreshes the visual row state
        private void Grid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var row = _grid.Rows[e.RowIndex];
            if (row.Tag is AchievementCondition cond && _grid.Tag is AchievementConditionGroup group)
            {
                ApplyConditionLogicRules(cond);

                // FIX: Get correct Model Index, not visual Row Index, for alias resolution
                // When chains are collapsed, row.Index != Model Index
                int modelIndex = group.Conditions.IndexOf(cond);

                // Refresh visuals (applying ShowDecimal and ShowAliases if active)
                ConditionGridRenderer.SetRowValues(row, cond, _grid.ReadOnly, ShowDecimal, ShowAliases, NotesLookup, RangeCache, group, modelIndex);
                LogicChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        // Handles selection from Context Menus
        private void OnContextMenuItemClick(object? sender, EventArgs e)
        {
            if (_grid.ReadOnly || sender is not ToolStripMenuItem item) return;
            if (_grid.Tag is not AchievementConditionGroup group) return;

            string val = item.Tag as string ?? "";
            if (val == RaLogicSyntax.NO_FLAG_TEXT || val == RaLogicSyntax.NO_OPERATOR_TEXT) val = "";

            bool changed = false;
            foreach (DataGridViewRow row in _grid.SelectedRows)
            {
                if (row.Tag is AchievementCondition cond)
                {
                    if (_grid.CurrentCell == null) continue;
                    string colName = _grid.Columns[_grid.CurrentCell.ColumnIndex].Name;

                    // Apply selected menu item to the model
                    if (colName == "ColFlag") { string old = cond.Flag; cond.Flag = val; HandleFlagChange(cond, old); }
                    else if (colName == "ColLType") HandleTypeChange(cond.LeftOperand, val, cond.LeftOperand.Size);
                    else if (colName == "ColLSize") cond.LeftOperand.Size = val;
                    else if (colName == "ColCmp") { string old = cond.Operator; cond.Operator = val; HandleOperatorChange(cond, old); }
                    else if (colName == "ColRType") HandleTypeChange(cond.RightOperand, val, cond.LeftOperand.Size, cond.LeftOperand); // Pass LeftOperand for auto-copy
                    else if (colName == "ColRSize") cond.RightOperand.Size = val;

                    ApplyConditionLogicRules(cond);

                    // FIX: Get correct Model Index, not visual Row Index, for alias resolution
                    int modelIndex = group.Conditions.IndexOf(cond);

                    // Updated to use ConditionGridRenderer
                    ConditionGridRenderer.SetRowValues(row, cond, _grid.ReadOnly, ShowDecimal, ShowAliases, NotesLookup, RangeCache, group, modelIndex);
                    changed = true;
                }
            }
            if (changed) { LogicChanged?.Invoke(this, EventArgs.Empty); StatusUpdateRequested?.Invoke(this, "Updated condition property."); }
        }

        // Auto-formats value when Type changes (e.g., reset Float to 1.0 or Value to 0)
        private void HandleTypeChange(Operand op, string newType, string refSize, Operand? copyFrom = null)
        {
            string oldType = op.Type;
            if (oldType == newType) return;

            // QoL: If switching Right Operand to Delta or Prior, default to copying the Left Operand (address/size)
            if (copyFrom != null && (newType == "Delta" || newType == "Prior"))
            {
                op.Value = copyFrom.Value;
                op.Size = copyFrom.Size;
                op.Type = newType;
                return;
            }

            if (newType == "Recall") { op.Value = ""; op.Type = newType; return; }
            if (string.IsNullOrEmpty(op.Value) || oldType == "Recall")
            {
                if (newType == "Float") op.Value = "1.0";
                else op.Value = "0x" + 1.ToString("x" + LogicFormatter.GetPaddingForSize(refSize));
            }
            else
            {
                var integerLike = new HashSet<string> { "Mem", "Value", "Delta", "Prior", "BCD", "Inverted" };
                // Convert Integer to Float presentation
                if (integerLike.Contains(oldType) && newType == "Float") { if (uint.TryParse(op.Value.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hexVal)) op.Value = BitConverter.ToSingle(BitConverter.GetBytes(hexVal), 0).ToString("G9", CultureInfo.InvariantCulture); }
                // Convert Float to Integer presentation
                else if (oldType == "Float" && integerLike.Contains(newType)) { string safeVal = op.Value.Replace(',', '.'); if (float.TryParse(safeVal, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatVal)) op.Value = "0x" + BitConverter.ToUInt32(BitConverter.GetBytes(floatVal), 0).ToString("x8"); }
            }
            op.Type = newType;
        }

        // Resets operands when Flag changes (e.g. AddSource shouldn't have Comparison)
        private void HandleFlagChange(AchievementCondition cond, string oldFlag)
        {
            bool wasA = RaLogicSyntax.ArithmeticFlags.Contains(oldFlag);
            bool isA = RaLogicSyntax.ArithmeticFlags.Contains(cond.Flag);
            if (wasA && !isA) { cond.Operator = "="; cond.RightOperand = new Operand { Type = "Value", Value = "0x1" }; }
            else if (!wasA && isA) { cond.Operator = ""; cond.RightOperand = new Operand(); }
        }

        // Resets right operand when Operator changes (None vs =)
        private void HandleOperatorChange(AchievementCondition cond, string oldOp)
        {
            bool wasE = string.IsNullOrEmpty(oldOp);
            bool isE = string.IsNullOrEmpty(cond.Operator);
            if (wasE && !isE) cond.RightOperand = new Operand { Type = "Value", Value = "0x1" };
            else if (!wasE && isE) cond.RightOperand = new Operand();
        }

        // Enforces syntax rules (e.g. Implicit sizes, required comparisons)
        private void ApplyConditionLogicRules(AchievementCondition cond)
        {
            if (cond.LeftOperand.Type is "Value" or "Float" or "Recall") cond.LeftOperand.Size = "";
            else if (RaLogicSyntax.MemoryTypes.Contains(cond.LeftOperand.Type) && string.IsNullOrEmpty(cond.LeftOperand.Size)) cond.LeftOperand.Size = "32-bit";

            if (cond.RightOperand.Type is "Value" or "Float" or "Recall") cond.RightOperand.Size = "";
            else if (RaLogicSyntax.MemoryTypes.Contains(cond.RightOperand.Type) && string.IsNullOrEmpty(cond.RightOperand.Size)) cond.RightOperand.Size = "32-bit";

            if (cond.RightOperand.Type == "Value") cond.RightOperand.Size = "";

            if (RaLogicSyntax.StrictComparisonFlags.Contains(cond.Flag))
            {
                if (!RaLogicSyntax.ComparisonOperators.Contains(cond.Operator) || string.IsNullOrEmpty(cond.Operator)) { cond.Operator = "="; cond.RightOperand = new Operand { Type = "Value", Value = "0x1" }; }
            }
            else if (RaLogicSyntax.ArithmeticFlags.Contains(cond.Flag))
            {
                if (RaLogicSyntax.ComparisonOperators.Contains(cond.Operator)) { cond.Operator = ""; cond.RightOperand = new Operand(); }
            }
        }

        // Retrieves the effective list of conditions to operate on.
        // If CollapseChains is true, this includes the hidden 'Add Address' rows preceding the selected items.
        private List<AchievementCondition> GetEffectiveSelection()
        {
            if (_grid.Tag is not AchievementConditionGroup group) return new List<AchievementCondition>();

            // Get currently selected objects from grid based on Tag
            var selected = _grid.SelectedRows.Cast<DataGridViewRow>()
                .Select(r => r.Tag as AchievementCondition)
                .Where(c => c != null)
                .OrderBy(c => group.Conditions.IndexOf(c!))
                .Cast<AchievementCondition>()
                .ToList();

            // If chains are not collapsed, what you see is what you selected.
            if (!CollapseChains) return selected;

            var expanded = new List<AchievementCondition>();
            var processed = new HashSet<AchievementCondition>();

            foreach (var cond in selected)
            {
                int idx = group.Conditions.IndexOf(cond);
                if (idx == -1) continue;

                // Backtrack to find the hidden "Add Address" chain
                var chain = new List<AchievementCondition>();
                int scan = idx - 1;
                while (scan >= 0)
                {
                    var prev = group.Conditions[scan];
                    if (prev.Flag == "Add Address")
                    {
                        chain.Insert(0, prev);
                        scan--;
                    }
                    else
                    {
                        break;
                    }
                }

                // Add chain items first
                foreach (var c in chain)
                {
                    if (processed.Add(c)) expanded.Add(c);
                }

                // Add the selected item
                if (processed.Add(cond)) expanded.Add(cond);
            }

            return expanded;
        }

        // Copies selected rows with Fixed-Width spacing to ensure visual alignment in text editors
        // Replaces simple TSV to satisfy QoL request for "pure line up"
        public void CopySelectedRows()
        {
            if (_grid.SelectedRows.Count == 0) return;

            // Get rows in visual order
            var rows = _grid.SelectedRows.Cast<DataGridViewRow>().OrderBy(r => r.Index).ToList();
            int colCount = _grid.Columns.Count;

            // Calculate max width for each column to align them
            int[] colWidths = new int[colCount];
            for (int c = 0; c < colCount; c++)
            {
                int max = 0;
                foreach (var row in rows)
                {
                    string txt = row.Cells[c].FormattedValue?.ToString() ?? "";
                    if (txt.Length > max) max = txt.Length;
                }
                colWidths[c] = max;
            }

            var sb = new StringBuilder();
            foreach (var row in rows)
            {
                for (int c = 0; c < colCount; c++)
                {
                    string txt = row.Cells[c].FormattedValue?.ToString() ?? "";
                    // Add padding (2 spaces buffer) to align columns, except the last one
                    if (c < colCount - 1)
                        sb.Append(txt.PadRight(colWidths[c] + 2));
                    else
                        sb.Append(txt);
                }
                sb.AppendLine();
            }

            Clipboard.SetText(sb.ToString());
            StatusUpdateRequested?.Invoke(this, "Rows copied to clipboard (aligned).");
        }

        // Copies logic as RA Logic String (e.g. 0x123=1) to Clipboard
        // Updated to include hidden chains if collapsed.
        public void CopySelectedLogic()
        {
            var conditionsToCopy = GetEffectiveSelection();
            if (conditionsToCopy.Count == 0) return;

            var sb = new StringBuilder();
            foreach (var cond in conditionsToCopy)
            {
                if (sb.Length > 0) sb.Append('_');
                sb.Append(LogicFormatter.ConditionToString(cond));
            }
            Clipboard.SetText(sb.ToString());
            StatusUpdateRequested?.Invoke(this, "Logic string copied to clipboard.");
        }

        // Cuts selected logic (Copy Logic + Delete Rows)
        public void CutSelected()
        {
            CopySelectedLogic();
            DeleteSelected();
            StatusUpdateRequested?.Invoke(this, "Logic cut to clipboard.");
        }

        // Restores selection based on Model Tags
        // FIXED: Sets CurrentCell BEFORE selecting rows to prevent selection clear
        private void RestoreSelection(List<AchievementCondition> selectedConditions)
        {
            if (selectedConditions == null || selectedConditions.Count == 0) return;

            var set = new HashSet<AchievementCondition>(selectedConditions);
            int firstFoundIndex = -1;
            var rowsToSelect = new List<int>();

            // Identify rows to select
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.Tag is AchievementCondition cond && set.Contains(cond))
                {
                    rowsToSelect.Add(row.Index);
                    if (firstFoundIndex == -1) firstFoundIndex = row.Index;
                }
            }

            if (rowsToSelect.Count == 0) return;

            // 1. Set CurrentCell (Focus) FIRST to prevent it from clearing selection later
            if (firstFoundIndex >= 0 && firstFoundIndex < _grid.Rows.Count)
            {
                try
                {
                    // Only change if not already in selection to prevent jumping focus
                    if (_grid.CurrentCell == null || !rowsToSelect.Contains(_grid.CurrentCell.RowIndex))
                        _grid.CurrentCell = _grid.Rows[firstFoundIndex].Cells[0];
                }
                catch { }
            }

            // 2. Clear and Re-apply Selection
            _grid.ClearSelection();
            foreach (int idx in rowsToSelect)
            {
                if (idx < _grid.Rows.Count)
                    _grid.Rows[idx].Selected = true;
            }

            // 3. Ensure Visibility (Smart Scroll)
            if (firstFoundIndex >= 0)
            {
                int firstVisible = _grid.FirstDisplayedScrollingRowIndex;
                int displayed = _grid.DisplayedRowCount(false);

                // Only scroll if out of view to avoid jitter
                if (firstFoundIndex < firstVisible || firstFoundIndex >= firstVisible + displayed)
                {
                    try
                    {
                        _grid.FirstDisplayedScrollingRowIndex = Math.Max(0, firstFoundIndex - 2);
                    }
                    catch { }
                }
            }
        }

        // Pastes logic string from Clipboard into Grid
        public void PasteLogic()
        {
            if (!Clipboard.ContainsText() || _grid.ReadOnly || _grid.Tag is not AchievementConditionGroup group) return;
            string txt = Clipboard.GetText().Trim().Replace("\r", "").Replace("\n", "");
            if (string.IsNullOrWhiteSpace(txt)) return;

            int insertIdx = group.Conditions.Count;
            if (_grid.SelectedRows.Count > 0)
            {
                // Find insertion point relative to the last selected visual row's model index
                var lastRow = _grid.SelectedRows.Cast<DataGridViewRow>().OrderByDescending(r => r.Index).First();
                if (lastRow.Tag is AchievementCondition c)
                {
                    insertIdx = group.Conditions.IndexOf(c) + 1;
                }
            }

            var conds = AchievementParser.ParseAchievementTrigger(txt, 'S', false).SelectMany(g => g.Conditions).ToList();

            group.Conditions.InsertRange(insertIdx, conds);

            // Updated to use ConditionGridRenderer
            ConditionGridRenderer.PopulateGridRows(_grid, group, _grid.ReadOnly, ShowDecimal, ShowAliases, NotesLookup, RangeCache, null, CollapseChains);

            RestoreSelection(conds);

            LogicChanged?.Invoke(this, EventArgs.Empty);
            StatusUpdateRequested?.Invoke(this, $"Logic pasted ({conds.Count} lines).");
        }

        public void DuplicateSelected()
        {
            if (_grid.ReadOnly || _grid.Tag is not AchievementConditionGroup group) return;

            // Use GetEffectiveSelection to include hidden chains
            var originalConditions = GetEffectiveSelection();
            if (originalConditions.Count == 0) return;

            // Find insert position: After the last item in the EFFECTIVE selection list
            var lastCondition = originalConditions.Last();
            int insertIdx = group.Conditions.IndexOf(lastCondition) + 1;

            var clones = originalConditions.Select(c => c.Clone()).ToList();

            group.Conditions.InsertRange(insertIdx, clones);

            // Updated to use ConditionGridRenderer
            ConditionGridRenderer.PopulateGridRows(_grid, group, _grid.ReadOnly, ShowDecimal, ShowAliases, NotesLookup, RangeCache, null, CollapseChains);

            RestoreSelection(clones);

            LogicChanged?.Invoke(this, EventArgs.Empty);
            StatusUpdateRequested?.Invoke(this, $"Conditions duplicated ({clones.Count} lines).");
        }

        public void DeleteSelected()
        {
            if (_grid.ReadOnly || _grid.Tag is not AchievementConditionGroup group) return;

            // Use GetEffectiveSelection to delete hidden chains along with the leaf
            var toDelete = GetEffectiveSelection();
            if (toDelete.Count == 0) return;

            // Determine selection restoration target (index of first deleted item)
            int firstSelIndex = -1;
            if (toDelete.Count > 0)
            {
                firstSelIndex = group.Conditions.IndexOf(toDelete[0]);
            }

            foreach (var cond in toDelete)
            {
                group.Conditions.Remove(cond);
            }

            // Updated to use ConditionGridRenderer
            ConditionGridRenderer.PopulateGridRows(_grid, group, _grid.ReadOnly, ShowDecimal, ShowAliases, NotesLookup, RangeCache, null, CollapseChains);

            if (firstSelIndex != -1)
            {
                if (firstSelIndex >= group.Conditions.Count) firstSelIndex = group.Conditions.Count - 1;
                if (firstSelIndex >= 0)
                {
                    RestoreSelection(new List<AchievementCondition> { group.Conditions[firstSelIndex] });
                }
            }

            LogicChanged?.Invoke(this, EventArgs.Empty);
            StatusUpdateRequested?.Invoke(this, "Conditions deleted.");
        }

        public void MoveSelected(int direction)
        {
            if (_grid.ReadOnly || _grid.Tag is not AchievementConditionGroup group) return;
            if (direction == 0) return;

            var selectedConditions = GetEffectiveSelection();
            if (selectedConditions.Count == 0) return;

            // Strategy: Reorder the list of conditions
            // 1. Extract all conditions into a list of "Moveable Units".
            //    If CollapseChains is ON: A Unit is a full Pointer Chain (AddAddress...Leaf).
            //    If CollapseChains is OFF: A Unit is a single Condition.

            var units = new List<List<AchievementCondition>>();

            if (CollapseChains)
            {
                var currentUnit = new List<AchievementCondition>();
                foreach (var cond in group.Conditions)
                {
                    currentUnit.Add(cond);
                    // A unit ends if it's NOT an AddAddress (it's a leaf/logic gate/etc)
                    // OR if it's the last item
                    if (cond.Flag != "Add Address")
                    {
                        units.Add(new List<AchievementCondition>(currentUnit));
                        currentUnit.Clear();
                    }
                }
                // Handle trailing AddAddress (incomplete chain)
                if (currentUnit.Count > 0) units.Add(currentUnit);
            }
            else
            {
                // Each condition is a unit
                foreach (var cond in group.Conditions) units.Add(new List<AchievementCondition> { cond });
            }

            // 2. Identify which Units are selected
            // A unit is selected if ANY of its conditions are in 'selectedConditions'
            var selectedUnitsIndices = new HashSet<int>();
            var selectedSet = new HashSet<AchievementCondition>(selectedConditions);

            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].Any(c => selectedSet.Contains(c)))
                    selectedUnitsIndices.Add(i);
            }

            // 3. Perform Move on Units
            var sortedIndices = selectedUnitsIndices.OrderBy(i => i).ToList();

            // Bounds check
            if (direction < 0 && sortedIndices.Min() == 0) return; // Top
            if (direction > 0 && sortedIndices.Max() == units.Count - 1) return; // Bottom

            var newUnits = new List<List<AchievementCondition>>(units);

            if (direction < 0) // Up
            {
                foreach (var idx in sortedIndices)
                {
                    var unit = newUnits[idx];
                    newUnits.RemoveAt(idx);
                    newUnits.Insert(idx - 1, unit);
                }
            }
            else // Down
            {
                // Iterate reverse to avoid index shifting issues
                for (int i = sortedIndices.Count - 1; i >= 0; i--)
                {
                    var idx = sortedIndices[i];
                    var unit = newUnits[idx];
                    newUnits.RemoveAt(idx);
                    newUnits.Insert(idx + 1, unit);
                }
            }

            // 4. Flatten and Apply
            group.Conditions.Clear();
            foreach (var unit in newUnits)
            {
                group.Conditions.AddRange(unit);
            }

            // 5. Refresh
            ConditionGridRenderer.PopulateGridRows(_grid, group, _grid.ReadOnly, ShowDecimal, ShowAliases, NotesLookup, RangeCache, null, CollapseChains);
            RestoreSelection(selectedConditions);

            LogicChanged?.Invoke(this, EventArgs.Empty);
            StatusUpdateRequested?.Invoke(this, "Conditions moved.");
        }

        public void AddCondition()
        {
            if (_grid.ReadOnly || _grid.Tag is not AchievementConditionGroup group) return;

            var newCond = new AchievementCondition { LeftOperand = { Type = "Value", Value = "0" }, Operator = "=", RightOperand = { Type = "Value", Value = "1" } };

            int insertIdx = group.Conditions.Count;
            if (_grid.SelectedRows.Count > 0)
            {
                var lastRow = _grid.SelectedRows.Cast<DataGridViewRow>().OrderByDescending(r => r.Index).First();
                if (lastRow.Tag is AchievementCondition c)
                {
                    insertIdx = group.Conditions.IndexOf(c) + 1;
                }
            }

            group.Conditions.Insert(insertIdx, newCond);

            // Updated to use ConditionGridRenderer
            ConditionGridRenderer.PopulateGridRows(_grid, group, _grid.ReadOnly, ShowDecimal, ShowAliases, NotesLookup, RangeCache, null, CollapseChains);

            RestoreSelection(new List<AchievementCondition> { newCond });

            LogicChanged?.Invoke(this, EventArgs.Empty);
        }

        // Detects if a cell contains a memory address and attempts to resolve pointer chains
        private void HandleAddressSelection(int rowIndex, int colIndex)
        {
            if (colIndex != 4 && colIndex != 8) return;

            // Get underlying value directly from Model tag, ignoring Visual Cell value (alias)
            string val = "";
            if (_grid.Rows[rowIndex].Tag is AchievementCondition cond)
            {
                val = colIndex == 4 ? cond.LeftOperand.Value : cond.RightOperand.Value;
            }
            else
            {
                // Fallback to cell value if tag missing (shouldn't happen)
                val = _grid.Rows[rowIndex].Cells[colIndex].Value?.ToString() ?? "";
            }

            if (!val.StartsWith("0x") || !long.TryParse(val.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long currentOffset)) return;

            List<long> offsets = new List<long>();
            string baseAddress = val;
            var collected = new Stack<long>();
            collected.Push(currentOffset);

            // Pointer Logic Scan
            if (_grid.Tag is not AchievementConditionGroup group) return;
            if (_grid.Rows[rowIndex].Tag is not AchievementCondition currentCond) return;

            int modelIndex = group.Conditions.IndexOf(currentCond);
            int scanIndex = modelIndex - 1;
            bool chainFound = false;

            // Scan backwards in MODEL
            while (scanIndex >= 0)
            {
                var prevCond = group.Conditions[scanIndex];

                // If we hit a non-AddAddress line, chain breaks
                if (prevCond.Flag != "Add Address") break;

                string memVal = prevCond.LeftOperand.Value;
                if (memVal.StartsWith("0x") && long.TryParse(memVal.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long addr))
                {
                    collected.Push(addr);
                    scanIndex--;
                    chainFound = true;
                }
                else break;
            }

            if (chainFound)
            {
                long baseAddrLong = collected.Pop();
                baseAddress = "0x" + baseAddrLong.ToString("x");
                offsets = collected.ToList();
                PointerChainSelected?.Invoke(this, new PointerChainArgs(baseAddress, offsets));
            }
            else AddressSelected?.Invoke(this, val);
        }
    }
}