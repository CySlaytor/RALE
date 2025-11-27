#nullable enable
using RALogicEditor.Models;
using RALogicEditor.Parsers; // Required for Parsing Clipboard Logic
using RALogicEditor.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace RALogicEditor.Forms
{
    public partial class MultiRegionForm : Form
    {
        private List<CodeNote> _allNotes;
        public Dictionary<string, string> AddressMap { get; private set; } = new();

        // Storing Result Logic Lists directly instead of strings
        public List<AchievementCondition> RegionAConditions { get; private set; } = new();
        public List<AchievementCondition> RegionBConditions { get; private set; } = new();

        public MultiRegionForm(List<CodeNote> notes)
        {
            InitializeComponent();
            _allNotes = notes;

            // Enable Double Buffering for smoother grid rendering
            EnableDoubleBuffering(dgvMapping);
            EnableDoubleBuffering(dgvRegionA);
            EnableDoubleBuffering(dgvRegionB);

            // Wire up event handlers for grids
            InitializeGridHandlers(dgvRegionA);
            InitializeGridHandlers(dgvRegionB);

            // Wire up menu items
            pasteLogicToolStripMenuItem.Click += (s, e) => PasteToActiveGrid();
            deleteRowToolStripMenuItem.Click += (s, e) => DeleteFromActiveGrid();
            copyLogicToolStripMenuItem.Click += (s, e) => CopyFromActiveGrid();

            ApplyTheme();
            ScanForMappings();
        }

        private void EnableDoubleBuffering(Control control)
        {
            typeof(Control).InvokeMember("DoubleBuffered",
                BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                null, control, new object[] { true });
        }

        private void InitializeGridHandlers(DataGridView grid)
        {
            grid.KeyDown += Grid_KeyDown;
            // Set the clicked grid as 'active' for context menu reference
            grid.MouseDown += (s, e) => {
                if (s is DataGridView g && e.Button == MouseButtons.Right)
                {
                    // Select row on right click if valid
                    var h = g.HitTest(e.X, e.Y);
                    if (h.RowIndex >= 0)
                    {
                        if (!g.Rows[h.RowIndex].Selected)
                        {
                            g.ClearSelection();
                            g.Rows[h.RowIndex].Selected = true;
                        }
                    }
                    ctxGridActions.Tag = g; // Store ref to grid in context menu
                }
            };
            grid.GotFocus += (s, e) => ctxGridActions.Tag = s; // Ensure focus tracks active grid
        }

        private void ApplyTheme()
        {
            if (SystemColors.Window.R < 128)
            {
                this.BackColor = Color.FromArgb(45, 45, 48);
                this.ForeColor = Color.White;

                StyleGrid(dgvMapping);
                StyleGrid(dgvRegionA);
                StyleGrid(dgvRegionB);

                grpRegionA.ForeColor = Color.White;
                grpRegionB.ForeColor = Color.White;
            }
        }

        private void StyleGrid(DataGridView dgv)
        {
            dgv.BackgroundColor = Color.FromArgb(30, 30, 30);
            dgv.DefaultCellStyle.BackColor = Color.FromArgb(30, 30, 30);
            dgv.DefaultCellStyle.ForeColor = Color.White;
            dgv.GridColor = Color.FromArgb(60, 60, 60);
            dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 45, 48);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgv.EnableHeadersVisualStyles = false;
        }

        private void ScanForMappings()
        {
            dgvMapping.Rows.Clear();
            var regex = new Regex(@"refer to \$0x([0-9a-f]+)", RegexOptions.IgnoreCase);

            foreach (var note in _allNotes)
            {
                var match = regex.Match(note.Note);
                if (match.Success)
                {
                    // Clean Address Strings (remove 0x)
                    string targetAddrHex = match.Groups[1].Value;
                    string sourceAddrHex = note.Address.Replace("0x", "");

                    // Parse to numeric to handle leading zero mismatch (00541004 vs 541004)
                    bool validTarget = long.TryParse(targetAddrHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long targetVal);
                    bool validSource = long.TryParse(sourceAddrHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long sourceVal);

                    if (validTarget && validSource)
                    {
                        // Verify target exists by numeric comparison
                        bool targetExists = _allNotes.Any(n =>
                        {
                            string nHex = n.Address.Replace("0x", "");
                            return long.TryParse(nHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long nVal) && nVal == targetVal;
                        });

                        int rowIdx = dgvMapping.Rows.Add(targetAddrHex, sourceAddrHex, note.Note.Split('\n')[0]);

                        if (!targetExists)
                        {
                            dgvMapping.Rows[rowIdx].DefaultCellStyle.BackColor = Color.Maroon; // Darker red for dark mode friendly
                            dgvMapping.Rows[rowIdx].DefaultCellStyle.ForeColor = Color.White;
                            dgvMapping.Rows[rowIdx].Cells[2].Value += " (Target not found!)";
                        }
                    }
                }
            }
        }

        // --- GRID INTERACTION LOGIC (Paste/Delete) ---

        private void Grid_KeyDown(object? sender, KeyEventArgs e)
        {
            if (sender is not DataGridView grid) return;

            if (e.Control && e.KeyCode == Keys.V)
            {
                PasteToGrid(grid);
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.C)
            {
                CopyFromGrid(grid); // Native copy works, but let's be explicit
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back)
            {
                DeleteFromGrid(grid);
                e.Handled = true;
            }
        }

        private void PasteToActiveGrid()
        {
            if (ctxGridActions.Tag is DataGridView grid)
                PasteToGrid(grid);
        }

        private void DeleteFromActiveGrid()
        {
            if (ctxGridActions.Tag is DataGridView grid)
                DeleteFromGrid(grid);
        }

        private void CopyFromActiveGrid()
        {
            if (ctxGridActions.Tag is DataGridView grid)
                CopyFromGrid(grid);
        }

        private void CopyFromGrid(DataGridView grid)
        {
            if (grid.GetCellCount(DataGridViewElementStates.Selected) > 0)
            {
                try { Clipboard.SetDataObject(grid.GetClipboardContent()); }
                catch { /* Ignore clipboard locks */ }
            }
        }

        private void DeleteFromGrid(DataGridView grid)
        {
            if (grid.SelectedRows.Count > 0 && !grid.ReadOnly)
            {
                foreach (DataGridViewRow row in grid.SelectedRows)
                {
                    if (!row.IsNewRow) grid.Rows.Remove(row);
                }
            }
        }

        private void PasteToGrid(DataGridView grid)
        {
            if (!Clipboard.ContainsText()) return;
            string text = Clipboard.GetText().Trim();
            if (string.IsNullOrEmpty(text)) return;

            // 1. Try Parse as RA Logic String (e.g. 0x1234=1)
            // Note: AchievementParser assumes full achievement string usually, but handles fragments too.
            // We pass 'S' separator, but for simple lines it works.
            List<AchievementCondition>? conds = null;
            try
            {
                var groups = AchievementParser.ParseAchievementTrigger(text, 'S', false);
                if (groups.Count > 0) conds = groups[0].Conditions;
            }
            catch { }

            if (conds != null && conds.Count > 0)
            {
                // Populate from Logic Objects
                foreach (var c in conds)
                {
                    grid.Rows.Add(
                        c.LeftOperand.Type, c.LeftOperand.Size, c.LeftOperand.Value,
                        c.Operator,
                        c.RightOperand.Type, c.RightOperand.Size, c.RightOperand.Value
                    );
                }
            }
            else
            {
                // 2. Fallback: Parse as TSV (Tab Separated Values) for spreadsheet pasting
                string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var cells = line.Split('\t');
                    if (cells.Length >= 3) // Minimum viable cols
                    {
                        // Mapping raw text to columns. If fewer cols than grid, fill what we have.
                        int idx = grid.Rows.Add();
                        var row = grid.Rows[idx];
                        for (int i = 0; i < cells.Length && i < grid.Columns.Count; i++)
                        {
                            row.Cells[i].Value = cells[i].Trim();
                        }
                    }
                }
            }
        }

        // --- END GRID INTERACTION ---

        private List<AchievementCondition> ExtractLogicFromGrid(DataGridView grid)
        {
            var list = new List<AchievementCondition>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;

                var cond = new AchievementCondition();

                // Flag is implicitly "Pause If" for the caller to handle, but grid has no Flag col
                cond.LeftOperand.Type = row.Cells[0].Value?.ToString() ?? "Mem";
                cond.LeftOperand.Size = row.Cells[1].Value?.ToString() ?? "32-bit";
                cond.LeftOperand.Value = row.Cells[2].Value?.ToString() ?? "0";

                cond.Operator = row.Cells[3].Value?.ToString() ?? "=";

                cond.RightOperand.Type = row.Cells[4].Value?.ToString() ?? "Value";
                cond.RightOperand.Size = row.Cells[5].Value?.ToString() ?? "";
                cond.RightOperand.Value = row.Cells[6].Value?.ToString() ?? "0";

                list.Add(cond);
            }
            return list;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            // Build Map: Target (USA) -> Source (PAL)
            AddressMap.Clear();
            foreach (DataGridViewRow row in dgvMapping.Rows)
            {
                if (row.IsNewRow) continue;
                string usaStr = row.Cells[0].Value?.ToString()?.Trim() ?? "";
                string palStr = row.Cells[1].Value?.ToString()?.Trim() ?? "";

                if (long.TryParse(usaStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long usaVal) &&
                    long.TryParse(palStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long palVal))
                {
                    // Map by Normalized Hex String (Lowercase, no 0x, no leading zeros)
                    AddressMap[usaVal.ToString("x")] = palVal.ToString("x");
                }
            }

            RegionAConditions = ExtractLogicFromGrid(dgvRegionA);
            RegionBConditions = ExtractLogicFromGrid(dgvRegionB);

            if (RegionAConditions.Count == 0 || RegionBConditions.Count == 0)
            {
                MessageBox.Show("Please define Region Lock logic for both regions using the grids at the bottom.", "Missing Logic", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}