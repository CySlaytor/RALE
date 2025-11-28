#nullable enable
using RALogicEditor.Definitions;
using RALogicEditor.Models;
using RALogicEditor.Parsers;
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

        // Dictionary to track grids per region tab
        private Dictionary<string, DataGridView> _regionGrids = new();

        public MultiRegionForm(List<CodeNote> notes)
        {
            InitializeComponent();
            _allNotes = notes;

            // Enable Double Buffering for smoother grid rendering
            EnableDoubleBuffering(dgvMapping);

            pasteLogicToolStripMenuItem.Click += (s, e) => PasteToActiveGrid();
            copyLogicToolStripMenuItem.Click += (s, e) => CopyFromActiveGrid();
            deleteRowToolStripMenuItem.Click += (s, e) => DeleteFromActiveGrid();

            ApplyTheme();
            InitializeDynamicRegions();
        }

        private void InitializeDynamicRegions()
        {
            // 1. Identify Regions from Notes
            var detectedRegions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool hasUnknownNotes = false;

            foreach (var note in _allNotes)
            {
                string region = RegionDefinitions.DetectRegion(note.Note);
                if (region != "Unknown")
                    detectedRegions.Add(region);
                else
                    hasUnknownNotes = true;
            }

            if (hasUnknownNotes && !detectedRegions.Contains("USA"))
            {
                detectedRegions.Add("USA");
            }

            if (detectedRegions.Count == 0)
            {
                detectedRegions.Add("USA");
            }

            string baseRegion = detectedRegions.Contains("USA") ? "USA" : detectedRegions.First();

            tabControlRegions.TabPages.Clear();
            _regionGrids.Clear();

            var sortedRegions = detectedRegions.OrderBy(r => r == baseRegion ? 0 : 1).ThenBy(r => r).ToList();

            foreach (var region in sortedRegions)
            {
                bool isBase = region == baseRegion;
                CreateRegionTab(region, isBase);
            }

            ScanForMappings(baseRegion);
        }

        private void CreateRegionTab(string regionName, bool isBase)
        {
            string title = isBase ? $"{regionName} (Base)" : regionName;
            TabPage page = new TabPage(title);
            page.Tag = regionName;

            GroupBox grp = new GroupBox();
            grp.Dock = DockStyle.Fill;
            grp.Text = $"{regionName} Lock Logic (Paste logic here)";
            grp.ForeColor = Color.White;

            DataGridView grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            grid.AllowUserToResizeColumns = false;
            grid.AllowUserToResizeRows = false;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            grid.RowHeadersWidth = 25;
            grid.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;

            // Set ReadOnly to true to prevent direct cell editing, but allow Paste/Delete
            grid.ReadOnly = true;

            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Type", Width = 50 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Size", Width = 60 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Memory", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Cmp", Width = 40 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Type", Width = 50 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Size", Width = 60 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Val/Mem", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

            StyleGrid(grid);
            InitializeGridHandlers(grid);
            grid.ContextMenuStrip = ctxGridActions;

            grp.Controls.Add(grid);
            page.Controls.Add(grp);
            tabControlRegions.TabPages.Add(page);

            _regionGrids[regionName] = grid;
        }

        private void ScanForMappings(string baseRegion)
        {
            dgvMapping.Rows.Clear();
            var regex = new Regex(@"refer to \$0x([0-9a-f]+)", RegexOptions.IgnoreCase);

            foreach (var note in _allNotes)
            {
                var match = regex.Match(note.Note);
                if (match.Success)
                {
                    string targetAddrHex = match.Groups[1].Value;
                    string sourceAddrHex = note.Address.Replace("0x", "");

                    string sourceRegion = RegionDefinitions.DetectRegion(note.Note);
                    var targetNote = _allNotes.FirstOrDefault(n => n.Address.EndsWith(targetAddrHex, StringComparison.OrdinalIgnoreCase));
                    string targetRegion = targetNote != null ? RegionDefinitions.DetectRegion(targetNote.Note) : "Unknown";

                    bool sourceIsBaseCandidate = sourceRegion == "Unknown" || sourceRegion == baseRegion;
                    bool targetIsBaseCandidate = targetRegion == "Unknown" || targetRegion == baseRegion;

                    string baseAddr = "";
                    string mappedAddr = "";
                    string mappedRegion = "";

                    if (!sourceIsBaseCandidate && targetIsBaseCandidate)
                    {
                        baseAddr = targetAddrHex;
                        mappedAddr = sourceAddrHex;
                        mappedRegion = sourceRegion;
                    }
                    else if (sourceIsBaseCandidate && !targetIsBaseCandidate)
                    {
                        baseAddr = sourceAddrHex;
                        mappedAddr = targetAddrHex;
                        mappedRegion = targetRegion;
                    }

                    if (!string.IsNullOrEmpty(baseAddr))
                    {
                        dgvMapping.Rows.Add(baseAddr, mappedAddr, mappedRegion, note.Note.Split('\n')[0]);
                    }
                }
            }
        }

        public List<RegionConfig> GetConfiguredRegions()
        {
            var results = new List<RegionConfig>();
            var mappings = new Dictionary<string, Dictionary<string, string>>();

            foreach (DataGridViewRow row in dgvMapping.Rows)
            {
                if (row.IsNewRow) continue;
                string baseAddr = row.Cells[0].Value?.ToString()?.Trim() ?? "";
                string targetAddr = row.Cells[1].Value?.ToString()?.Trim() ?? "";
                string region = row.Cells[2].Value?.ToString()?.Trim() ?? "";

                if (!string.IsNullOrEmpty(region) && !string.IsNullOrEmpty(baseAddr) && !string.IsNullOrEmpty(targetAddr))
                {
                    if (!mappings.ContainsKey(region)) mappings[region] = new Dictionary<string, string>();

                    if (long.TryParse(baseAddr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long bVal) &&
                        long.TryParse(targetAddr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long tVal))
                    {
                        mappings[region][bVal.ToString("x")] = tVal.ToString("x");
                    }
                }
            }

            foreach (var kvp in _regionGrids)
            {
                string regionName = kvp.Key;
                DataGridView grid = kvp.Value;
                bool isBase = !mappings.ContainsKey(regionName);

                foreach (TabPage page in tabControlRegions.TabPages)
                {
                    if ((string)page.Tag == regionName && page.Text.Contains("(Base)"))
                    {
                        isBase = true;
                        break;
                    }
                }

                var config = new RegionConfig
                {
                    RegionName = regionName,
                    IsBase = isBase,
                    LockConditions = ExtractLogicFromGrid(grid)
                };

                if (!isBase && mappings.ContainsKey(regionName))
                {
                    config.AddressMap = mappings[regionName];
                }

                results.Add(config);
            }

            return results;
        }

        private List<AchievementCondition> ExtractLogicFromGrid(DataGridView grid)
        {
            var list = new List<AchievementCondition>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;

                var cond = new AchievementCondition();
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

        // --- Boilerplate UI Logic ---

        private void EnableDoubleBuffering(Control control)
        {
            typeof(Control).InvokeMember("DoubleBuffered",
                BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                null, control, new object[] { true });
        }

        private void InitializeGridHandlers(DataGridView grid)
        {
            grid.KeyDown += Grid_KeyDown;
            grid.MouseDown += (s, e) => {
                if (s is DataGridView g && e.Button == MouseButtons.Right)
                {
                    var h = g.HitTest(e.X, e.Y);
                    if (h.RowIndex >= 0) { if (!g.Rows[h.RowIndex].Selected) { g.ClearSelection(); g.Rows[h.RowIndex].Selected = true; } }
                    // Update the active grid tag for Context Menu actions
                    ctxGridActions.Tag = g;
                }
            };
            grid.GotFocus += (s, e) => ctxGridActions.Tag = s;
        }

        private void ApplyTheme()
        {
            if (SystemColors.Window.R < 128)
            {
                this.BackColor = Color.FromArgb(45, 45, 48);
                this.ForeColor = Color.White;
                StyleGrid(dgvMapping);
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

        private void Grid_KeyDown(object? sender, KeyEventArgs e)
        {
            if (sender is not DataGridView grid) return;
            if (e.Control && e.KeyCode == Keys.V) { PasteToGrid(grid); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.C) { CopyFromGrid(grid); e.Handled = true; }
            else if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back) { DeleteFromGrid(grid); e.Handled = true; }
        }

        // Active Grid Helpers
        private void PasteToActiveGrid()
        {
            if (ctxGridActions.Tag is DataGridView grid) PasteToGrid(grid);
        }

        private void CopyFromActiveGrid()
        {
            if (ctxGridActions.Tag is DataGridView grid) CopyFromGrid(grid);
        }

        private void DeleteFromActiveGrid()
        {
            if (ctxGridActions.Tag is DataGridView grid) DeleteFromGrid(grid);
        }

        private void PasteToGrid(DataGridView grid)
        {
            if (!Clipboard.ContainsText()) return;
            string text = Clipboard.GetText().Trim().Replace("\"", "");
            if (string.IsNullOrEmpty(text)) return;

            List<AchievementCondition> conds = new List<AchievementCondition>();

            // 1. Try Standard Parser (Handles Logic Chains like S:0x..._R:0x...)
            try
            {
                var groups = AchievementParser.ParseAchievementTrigger(text, 'S', false);
                if (groups.Count > 0 && groups[0].Conditions.Count > 0)
                    conds.AddRange(groups[0].Conditions);
            }
            catch { }

            // 2. Fallback: Manual Parsing for Raw/Split Logic Strings
            // Handles simple cases (0x...=1) or multi-line copies without underscores
            if (conds.Count == 0 && (text.Contains("=") || text.Contains(">") || text.Contains("<")))
            {
                // Split by Underscore OR Newline to handle both RA format and Line-by-Line copy
                string[] parts = text.Split(new[] { '_', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string part in parts)
                {
                    // Clean spaces
                    string cleanPart = part.Trim();
                    if (string.IsNullOrEmpty(cleanPart)) continue;

                    var c = ManualParseCondition(cleanPart);
                    if (c != null) conds.Add(c);
                }
            }

            if (conds.Count > 0)
            {
                foreach (var c in conds)
                {
                    grid.Rows.Add(
                        c.LeftOperand.Type,
                        c.LeftOperand.Size,
                        c.LeftOperand.Value,
                        c.Operator,
                        c.RightOperand.Type,
                        c.RightOperand.Size,
                        c.RightOperand.Value
                    );
                }
            }
            else
            {
                // 3. Fallback: TSV (Spreadsheet copy)
                string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                bool added = false;
                foreach (var line in lines)
                {
                    var cells = line.Split('\t');
                    if (cells.Length >= 3)
                    {
                        int idx = grid.Rows.Add();
                        var row = grid.Rows[idx];
                        for (int i = 0; i < cells.Length && i < grid.Columns.Count; i++)
                            row.Cells[i].Value = cells[i].Trim();
                        added = true;
                    }
                }

                if (!added)
                {
                    MessageBox.Show("Could not parse clipboard content. Supported formats:\n1. RA Logic (0xH1234=1)\n2. Raw Logic Lines\n3. TSV (Spreadsheet)", "Paste Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private AchievementCondition? ManualParseCondition(string raw)
        {
            // Simple Regex to capture Left Op Right
            // Group 1: Left Side (e.g. 0xX00541004)
            // Group 2: Operator
            // Group 3: Right Side
            var regex = new Regex(@"^([^=<>!]+)([=<>!]+)([^=<>!]+)$");
            var match = regex.Match(raw);
            if (!match.Success) return null;

            var cond = new AchievementCondition();
            cond.Operator = match.Groups[2].Value;

            ParseOperand(match.Groups[1].Value, cond.LeftOperand);
            ParseOperand(match.Groups[3].Value, cond.RightOperand);

            return cond;
        }

        private void ParseOperand(string raw, Operand op)
        {
            raw = raw.Trim();

            // Check if it looks like memory (0x...)
            if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                string body = raw.Substring(2);
                if (body.Length > 0)
                {
                    char typeChar = body[0];
                    // Handle standard sizes H, X, W or implicit hex
                    if ("HXW ".Contains(char.ToUpper(typeChar)) || char.IsDigit(typeChar))
                    {
                        op.Type = "Mem";
                        switch (char.ToUpper(typeChar))
                        {
                            case 'H': op.Size = "8-bit"; body = body.Substring(1); break;
                            case ' ': op.Size = "16-bit"; body = body.Substring(1); break;
                            case 'W': op.Size = "24-bit"; body = body.Substring(1); break;
                            case 'X': op.Size = "32-bit"; body = body.Substring(1); break;
                            default:
                                // Assume 16-bit if purely hex digits follow 0x (e.g. 0x1234)
                                // RA Standard implies 16-bit if no prefix char is present
                                op.Size = "16-bit";
                                break;
                        }
                        op.Value = "0x" + body;
                        return;
                    }
                }
            }

            // Fallback to Value
            op.Type = "Value";
            op.Size = "";
            op.Value = raw;
        }

        private void CopyFromGrid(DataGridView grid)
        {
            if (grid.GetCellCount(DataGridViewElementStates.Selected) > 0)
                try { Clipboard.SetDataObject(grid.GetClipboardContent()); } catch { }
        }

        private void DeleteFromGrid(DataGridView grid)
        {
            // Allow deletion even if grid is ReadOnly
            if (grid.SelectedRows.Count > 0)
            {
                foreach (DataGridViewRow row in grid.SelectedRows)
                    if (!row.IsNewRow) grid.Rows.Remove(row);
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void btnHelp_Click(object sender, EventArgs e)
        {
            string helpText =
                "How to use Multi-Region Generator:\n\n" +
                "1. Code Note Setup:\n" +
                "   - Identify the primary region's memory addresses (e.g., USA).\n" +
                "   - For other regions (e.g., Europe), create code notes at the corresponding addresses.\n" +
                "   - The note content must include: 'Refer to $0x[BaseAddress]'\n" +
                "   - Example: If USA Health is 0x1234, create a note at 0x5678 (Europe) saying 'Health | Refer to $0x1234'.\n\n" +
                "2. Region Detection:\n" +
                "   - The tool detects regions based on keywords in your notes:\n" +
                "     - USA / NTSC\n" +
                "     - Europe / PAL\n" +
                "     - Japan / NTSC-J\n" +
                "   - If not detected, ensure your notes contain these keywords.\n\n" +
                "3. Lock Logic:\n" +
                "   - Use the tabs below to define specific logic that identifies each region (e.g., checking a Region ID or Serial string).\n" +
                "   - This logic will be added as a 'Pause If' (Lock) to prevent the achievement from triggering in the wrong region.\n" +
                "   - Paste logic using RA format (e.g., 0xH1234=1).";

            MessageBox.Show(helpText, "Multi-Region Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    // Configuration for a specific region within the Multi-Region tool.
    public class RegionConfig
    {
        public string RegionName { get; set; } = "";
        public bool IsBase { get; set; }
        public Dictionary<string, string> AddressMap { get; set; } = new();
        public List<AchievementCondition> LockConditions { get; set; } = new();
    }
}