#nullable enable
using RALogicEditor.Forms;
using RALogicEditor.Models;
using RALogicEditor.Parsers;
using RARPEditor.Helpers;
using RARPEditor.Models;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RALogicEditor.Controls
{
    // CLASS 1: The Main UserControl
    // Must be first for Visual Studio Designer support.
    public partial class TriggerEditorControl : UserControl
    {
        // -- State Variables --
        private bool _isProgrammaticallyChanging;
        private string _triggerText = "";

        // Core data models
        private List<AchievementConditionGroup> _activeGroups = new();
        private Dictionary<string, List<AchievementConditionGroup>> _leaderboardSegments = new();

        // UI Settings
        private bool _showDecimal = false;
        private IAssetItem? _boundAsset;
        private bool _isLeaderboardMode;

        // Undo/Redo
        private Stack<string> _undoStack = new();
        private Stack<string> _redoStack = new();
        private bool _isUndoingRedoing;

        // Grid Tracking
        private DoubleBufferedDataGridView? _activeGrid;
        private int _activeColIndex = -1;
        private int _activeRowIndex = -1;

        // -- Logic Generator State --
        private class GeneratorDefinition
        {
            public int Copies { get; set; }
            public List<GeneratorRule> Rules { get; set; } = new();
        }

        private class GeneratorRule
        {
            public string SourceAddress { get; set; } = "";
            public int Stride { get; set; }
        }

        private Dictionary<int, GeneratorDefinition> _generatorRules = new();

        // -- Events --
        public event EventHandler? TriggerTextChanged;
        public event EventHandler? AchievementDataChanged;
        public event EventHandler<string>? StatusUpdateRequested;
        public event EventHandler? AssetResetRequested;
        public event EventHandler? AddFromCodeNoteRequested;

        // Bridge events
        public event EventHandler<string>? AddressSelected;
        public event EventHandler<AddressTooltipEventArgs>? AddressTooltipNeeded;
        public event EventHandler<PointerChainArgs>? PointerChainSelected;

        public char GroupSeparator { get; set; } = 'S';

        #region Constants & Menus
        private const string NO_FLAG_TEXT = "(No Flag)";
        private const string NO_OPERATOR_TEXT = "(None)";

        private static readonly string[] Flags = { NO_FLAG_TEXT, "Reset If", "Pause If", "Add Source", "Sub Source", "Add Hits", "Sub Hits", "Add Address", "AndNext", "OrNext", "Measured", "MeasuredIf", "Measured%", "Trigger", "ResetNextIf", "Remember" };
        private static readonly string[] OperandTypes = { "Mem", "Value", "Delta", "Prior", "BCD", "Inverted", "Float", "Recall" };
        private static readonly string[] MemorySizes = { "", "8-bit", "16-bit", "24-bit", "32-bit", "Bit0", "Bit1", "Bit2", "Bit3", "Bit4", "Bit5", "Bit6", "Bit7", "Lower4", "Upper4", "BitCount", "16-bit BE", "24-bit BE", "32-bit BE", "Float", "Float BE", "Double32", "Double32 BE", "MBF32", "MBF32 LE" };
        private static readonly string[] ComparisonOperators = { NO_OPERATOR_TEXT, "=", "!=", "<", "<=", ">", ">=" };
        private static readonly string[] ArithmeticOperators = { NO_OPERATOR_TEXT, "&", "^", "+", "-", "*", "/", "%" };

        private static readonly HashSet<string> ArithmeticFlags = new() { "Add Source", "Sub Source", "Add Address", "Remember" };
        private static readonly HashSet<string> StrictComparisonFlags = new() { "Reset If", "Pause If", "Add Hits", "Sub Hits", "AndNext", "OrNext", "MeasuredIf", "Trigger", "ResetNextIf" };
        private static readonly HashSet<string> FlagsWithoutHits = new() { "Add Source", "Sub Source", "Add Address", "AndNext", "OrNext" };
        private static readonly HashSet<string> MemoryTypes = new() { "Mem", "Delta", "Prior", "BCD", "Inverted" };

        private static readonly Dictionary<string, char> ReverseFlagMap = new() { { "Reset If", 'R' }, { "Pause If", 'P' }, { "Add Source", 'A' }, { "Sub Source", 'B' }, { "Add Hits", 'C' }, { "Sub Hits", 'D' }, { "Add Address", 'I' }, { "AndNext", 'N' }, { "OrNext", 'O' }, { "Measured", 'M' }, { "MeasuredIf", 'Q' }, { "Measured%", 'G' }, { "Trigger", 'T' }, { "ResetNextIf", 'Z' }, { "Remember", 'K' } };
        private static readonly Dictionary<string, char> ReverseSizeMap = new() { { "", ' ' }, { "16-bit", ' ' }, { "8-bit", 'H' }, { "32-bit", 'X' }, { "24-bit", 'W' }, { "Bit0", 'M' }, { "Bit1", 'N' }, { "Bit2", 'O' }, { "Bit3", 'P' }, { "Bit4", 'Q' }, { "Bit5", 'R' }, { "Bit6", 'S' }, { "Bit7", 'T' }, { "Lower4", 'L' }, { "Upper4", 'U' }, { "BitCount", 'K' }, { "32-bit BE", 'G' }, { "16-bit BE", 'I' }, { "24-bit BE", 'J' } };
        private static readonly Dictionary<string, char> ReverseFloatSizeMap = new() { { "Float", 'F' }, { "Float BE", 'B' }, { "Double32", 'H' }, { "Double32 BE", 'I' }, { "MBF32", 'M' }, { "MBF32 LE", 'L' } };
        private static readonly Dictionary<char, string> FloatSizeMap = new() { { 'F', "Float" }, { 'B', "Float BE" }, { 'H', "Double32" }, { 'I', "Double32 BE" }, { 'M', "MBF32" }, { 'L', "MBF32 LE" } };

        private readonly ContextMenuStrip _flagContextMenu = new();
        private readonly ContextMenuStrip _typeContextMenu = new();
        private readonly ContextMenuStrip _sizeContextMenu = new();
        private readonly ContextMenuStrip _cmpContextMenu = new();
        private readonly ContextMenuStrip _cmpArithmeticContextMenu = new();
        private readonly ContextMenuStrip _cmpStrictContextMenu = new();
        #endregion

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string TriggerText
        {
            get => _triggerText;
            set
            {
                if (_triggerText != value && !_isUndoingRedoing)
                {
                    PushUndoState();
                    _triggerText = value;
                    UpdateUIFromText();
                }
                else if (_isUndoingRedoing)
                {
                    _triggerText = value;
                    UpdateUIFromText();
                }
            }
        }

        public TriggerEditorControl()
        {
            InitializeComponent();
            InitializeGrids();
            CreateEditingContextMenus();
            InitializeHeaderEvents();
            InitializeLeaderboardControls();

            chkShowDecimal.CheckedChanged += (s, e) =>
            {
                _showDecimal = chkShowDecimal.Checked;
                RefreshCurrentGridDisplay();
            };

            SetStatus("Ready");
        }

        private void SetStatus(string message)
        {
            if (lblEditorStatus != null)
                lblEditorStatus.Text = message;
        }

        // Setup Leaderboard UI
        private void InitializeLeaderboardControls()
        {
            lvLeaderboardComponents.Items.Clear();
            lvLeaderboardComponents.Items.Add(new ListViewItem("Start"));
            lvLeaderboardComponents.Items.Add(new ListViewItem("Cancel"));
            lvLeaderboardComponents.Items.Add(new ListViewItem("Submit"));
            lvLeaderboardComponents.Items.Add(new ListViewItem("Value"));

            if (lvLeaderboardComponents.Items.Count > 0)
                lvLeaderboardComponents.Items[0].Selected = true;

            lvLeaderboardComponents.SelectedIndexChanged += LvLeaderboardComponents_SelectedIndexChanged;
        }

        // Leaderboard Segment Click Handler
        private void LvLeaderboardComponents_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_isProgrammaticallyChanging) return;
            if (lvLeaderboardComponents.SelectedItems.Count > 0)
            {
                RefreshLeaderboardGroupsList();
            }
        }

        // Loads the specific leaderboard logic (Start/Cancel/Submit/Value) into the active groups
        private void RefreshLeaderboardGroupsList()
        {
            if (lvLeaderboardComponents.SelectedItems.Count == 0) return;

            string key = lvLeaderboardComponents.SelectedItems[0].Text;

            if (_leaderboardSegments.TryGetValue(key, out var segGroups))
            {
                _activeGroups = segGroups;
                RefreshGroupsListUI();
            }
        }

        // Setup Grid
        private void InitializeGrids()
        {
            AddGridColumn(conditionGrid, "ColID", "ID", 35, true);
            AddGridColumn(conditionGrid, "ColFlag", "Flag", 80, true);
            AddGridColumn(conditionGrid, "ColLType", "Type", 50, true);
            AddGridColumn(conditionGrid, "ColLSize", "Size", 60, true);
            AddGridColumn(conditionGrid, "ColLValue", "Memory", 100, false);
            AddGridColumn(conditionGrid, "ColCmp", "Cmp", 40, true);
            AddGridColumn(conditionGrid, "ColRType", "Type", 50, true);
            AddGridColumn(conditionGrid, "ColRSize", "Size", 60, true);
            AddGridColumn(conditionGrid, "ColRValue", "Mem/Val", 100, false);
            AddGridColumn(conditionGrid, "ColHits", "Hits", 50, false);

            conditionGrid.CellValueChanged += Grid_CellValueChanged;
            conditionGrid.CellMouseDown += Grid_CellMouseDown;
            conditionGrid.CellDoubleClick += Grid_CellDoubleClick;
            conditionGrid.EditingControlShowing += Grid_EditingControlShowing;
            conditionGrid.CellEndEdit += Grid_CellEndEdit;
            conditionGrid.KeyDown += Grid_KeyDown;
            conditionGrid.CellEnter += Grid_CellEnter;

            conditionGrid.ContextMenuStrip.Opening += (s, e) =>
            {
                bool any = conditionGrid.SelectedRows.Count > 0;
                moveUpToolStripMenuItem.Enabled = any;
                moveDownToolStripMenuItem.Enabled = any;
                deleteToolStripMenuItem.Enabled = any;
                duplicateToolStripMenuItem.Enabled = any;
                copyRowToolStripMenuItem.Enabled = any;
                copyLogicToolStripMenuItem.Enabled = any;
                pasteToolStripMenuItem.Enabled = Clipboard.ContainsText();

                createGeneratorToolStripMenuItem.Enabled = any;
                if (any && conditionGrid.SelectedRows.Count == 1)
                {
                    var row = conditionGrid.SelectedRows[0];
                    string lVal = row.Cells["ColLValue"].Value?.ToString() ?? "";
                    string rVal = row.Cells["ColRValue"].Value?.ToString() ?? "";
                    createGeneratorToolStripMenuItem.Enabled = lVal.StartsWith("0x") || rVal.StartsWith("0x");
                }
            };

            groupsList.SelectedIndexChanged += GroupsList_SelectedIndexChanged;

            if (groupsList.Columns.Count > 0)
                groupsList.Columns[0].Width = groupsList.ClientSize.Width - 2;

            groupsList.Resize += (s, e) =>
            {
                if (groupsList.Columns.Count > 0)
                    groupsList.Columns[0].Width = groupsList.ClientSize.Width - 2;
            };
        }

        private void RefreshCurrentGridDisplay()
        {
            if (conditionGrid.Tag is AchievementConditionGroup group)
            {
                bool isGenerated = false;
                int index = _activeGroups.IndexOf(group);
                if (index >= 0)
                {
                    foreach (var kvp in _generatorRules)
                    {
                        if (index > kvp.Key && index <= kvp.Key + kvp.Value.Copies)
                        {
                            isGenerated = true;
                            break;
                        }
                    }
                }
                PopulateGridRows(conditionGrid, group, isGenerated);
            }
        }

        private void Grid_CellEnter(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
        }

        private void Grid_KeyDown(object? sender, KeyEventArgs e)
        {
            _activeGrid = sender as DoubleBufferedDataGridView;

            if (e.KeyCode == Keys.Enter && _activeGrid != null && _activeGrid.CurrentCell != null)
            {
                HandleAddressSelection(_activeGrid.CurrentCell.RowIndex, _activeGrid.CurrentCell.ColumnIndex);
                e.Handled = true;
            }

            if (_activeGrid != null && _activeGrid.ReadOnly && e.KeyCode != Keys.C)
            {
                if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.V) { e.Handled = true; return; }
            }

            if (e.Control && e.KeyCode == Keys.V) { PasteLogic(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.C) { CopySelectedRows(); e.Handled = true; }
            else if (e.Control && e.Shift && e.KeyCode == Keys.C) { CopySelectedLogic(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.D) { DuplicateSelectedItems(sender, e); e.Handled = true; }
            else if (e.Alt && e.KeyCode == Keys.Up) { MoveSelectedItems(-1); e.Handled = true; }
            else if (e.Alt && e.KeyCode == Keys.Down) { MoveSelectedItems(1); e.Handled = true; }
            else if (e.KeyCode == Keys.Delete) { DeleteSelected(sender, e); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.Z) { Undo(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.Y) { Redo(); e.Handled = true; }
        }

        private void HandleAddressSelection(int rowIndex, int colIndex)
        {
            if (_activeGrid == null) return;
            if (colIndex != 4 && colIndex != 8) return;

            string val = _activeGrid.Rows[rowIndex].Cells[colIndex].Value?.ToString() ?? "";
            if (!val.StartsWith("0x")) return;

            if (!long.TryParse(val.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long currentOffset))
                return;

            List<long> offsets = new List<long>();
            string baseAddress = val;

            var collected = new Stack<long>();
            collected.Push(currentOffset);

            int scanIndex = rowIndex - 1;
            bool chainFound = false;

            while (scanIndex >= 0)
            {
                var row = _activeGrid.Rows[scanIndex];
                string flag = row.Cells["ColFlag"].Value?.ToString() ?? "";
                string memVal = row.Cells["ColLValue"].Value?.ToString() ?? "";

                if (flag == "Add Address" && memVal.StartsWith("0x") &&
                    long.TryParse(memVal.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long addr))
                {
                    collected.Push(addr);
                    scanIndex--;
                    chainFound = true;
                }
                else
                {
                    break;
                }
            }

            if (chainFound)
            {
                long baseAddrLong = collected.Pop();
                baseAddress = "0x" + baseAddrLong.ToString("x");
                offsets = collected.ToList();
                PointerChainSelected?.Invoke(this, new PointerChainArgs(baseAddress, offsets));
            }
            else
            {
                AddressSelected?.Invoke(this, val);
            }
        }

        private void InitializeHeaderEvents()
        {
            txtTitle.TextChanged += (s, e) =>
            {
                if (_boundAsset != null)
                {
                    _boundAsset.Title = txtTitle.Text;
                    AchievementDataChanged?.Invoke(this, EventArgs.Empty);
                }
            };
            txtDescription.TextChanged += (s, e) =>
            {
                if (_boundAsset != null)
                {
                    _boundAsset.Description = txtDescription.Text;
                    AchievementDataChanged?.Invoke(this, EventArgs.Empty);
                }
            };

            cmbPoints.SelectedIndexChanged += (s, e) =>
            {
                if (_boundAsset is RaAchievement cheevo && int.TryParse(cmbPoints.Text, out int p))
                {
                    cheevo.Points = p;
                    AchievementDataChanged?.Invoke(this, EventArgs.Empty);
                }
            };
            cmbPoints.TextChanged += (s, e) =>
            {
                if (_boundAsset is RaAchievement cheevo && int.TryParse(cmbPoints.Text, out int p))
                {
                    cheevo.Points = p;
                    AchievementDataChanged?.Invoke(this, EventArgs.Empty);
                }
            };

            cmbType.SelectedIndexChanged += (s, e) =>
            {
                if (_boundAsset is RaAchievement cheevo)
                {
                    string display = cmbType.SelectedItem?.ToString() ?? "";
                    string backend = display switch
                    {
                        "Progression" => "progression",
                        "Win" => "win_condition",
                        "Missable" => "missable",
                        _ => ""
                    };
                    cheevo.Type = backend;
                    AchievementDataChanged?.Invoke(this, EventArgs.Empty);
                }
            };
        }

        public void Clear()
        {
            _boundAsset = null;
            _triggerText = "";
            _generatorRules.Clear();

            txtTitle.Text = "";
            txtDescription.Text = "";
            cmbPoints.SelectedIndex = -1;
            cmbPoints.Text = "";
            cmbType.SelectedIndex = -1;
            lblAuthor.Text = "";
            pbBadge.Image = null;

            _activeGroups.Clear();
            _leaderboardSegments.Clear();
            groupsList.Items.Clear();
            conditionGrid.Rows.Clear();

            _undoStack.Clear();
            _redoStack.Clear();

            cmbPoints.Visible = true;
            cmbType.Visible = true;
            lblAuthor.Visible = true;
            pbBadge.Visible = true;
            lvLeaderboardComponents.Visible = false;
            btnReset.Visible = false;

            SetStatus("Ready");
        }

        public async void BindAchievement(IAssetItem asset, string setType = "Core")
        {
            _boundAsset = asset;
            _isLeaderboardMode = asset.IsLeaderboard;
            _generatorRules.Clear();
            this.TriggerText = asset.Logic;
            _undoStack.Clear();
            _redoStack.Clear();

            txtTitle.Text = asset.Title;
            txtDescription.Text = asset.Description;

            if (asset is RaAchievement cheevo)
            {
                cmbPoints.Visible = true;
                cmbType.Visible = true;
                lblAuthor.Visible = true;
                pbBadge.Visible = true;
                lvLeaderboardComponents.Visible = false;

                txtTitle.Left = 75;
                txtDescription.Left = 75;
                txtTitle.Width = panelHeader.Width - txtTitle.Left - 165;
                txtDescription.Width = panelHeader.Width - txtDescription.Left - 165;

                cmbPoints.SelectedItem = cheevo.Points.ToString();
                if (cmbPoints.SelectedIndex == -1) cmbPoints.Text = cheevo.Points.ToString();

                lblAuthor.Text = $"Author: {cheevo.Author}";

                string displayType = cheevo.Type switch
                {
                    "progression" => "Progression",
                    "win_condition" => "Win",
                    "missable" => "Missable",
                    _ => ""
                };

                if (cmbType.Items.Contains(displayType)) cmbType.SelectedItem = displayType;
                else cmbType.Text = displayType;

                pbBadge.Image = null;
                var img = await ImageCache.GetBadgeAsync(cheevo.BadgeName);

                if (_boundAsset == asset)
                    pbBadge.Image = img;
            }
            else if (asset is RaLeaderboard lb)
            {
                cmbPoints.Visible = false;
                cmbType.Visible = false;
                lblAuthor.Visible = false;
                pbBadge.Visible = false;

                lvLeaderboardComponents.Visible = true;
                lvLeaderboardComponents.Location = new Point(5, 5);

                int textLeft = lvLeaderboardComponents.Right + 10;
                txtTitle.Left = textLeft;
                txtDescription.Left = textLeft;
                txtTitle.Width = panelHeader.Width - textLeft - 165;
                txtDescription.Width = panelHeader.Width - textLeft - 165;

                if (lvLeaderboardComponents.SelectedItems.Count == 0 && lvLeaderboardComponents.Items.Count > 0)
                    lvLeaderboardComponents.Items[0].Selected = true;
            }

            btnReset.Visible = asset.State != AssetState.Local;
            SetStatus("Asset loaded.");
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            AssetResetRequested?.Invoke(this, EventArgs.Empty);
            SetStatus("Asset reset to server state.");
        }

        private void btnAddNote_Click(object sender, EventArgs e)
        {
            AddFromCodeNoteRequested?.Invoke(this, EventArgs.Empty);
            SetStatus("Condition added from note.");
        }

        public void AddConditionFromNote(string logicString, string type, string size)
        {
            if (string.IsNullOrEmpty(logicString)) return;

            PushUndoState();

            var groups = AchievementParser.ParseAchievementTrigger(logicString, GroupSeparator, false);

            if (groups.Count > 0 && groups[0].Conditions.Count > 0)
            {
                var conditions = groups[0].Conditions;

                foreach (var cond in conditions)
                {
                    if (cond.Flag == "Add Address")
                    {
                        if (string.IsNullOrEmpty(cond.LeftOperand.Size))
                        {
                            cond.LeftOperand.Size = "32-bit";
                        }
                    }
                }

                var lastCond = conditions.Last();
                if (lastCond.LeftOperand.Type == "Mem")
                {
                    lastCond.LeftOperand.Type = type;
                    lastCond.LeftOperand.Size = ValidateMemorySize(size);
                }

                if (string.IsNullOrEmpty(lastCond.Operator))
                {
                    lastCond.Operator = "=";
                    lastCond.RightOperand = new Operand { Type = "Value", Value = "0" };
                    if (size == "Float") lastCond.RightOperand.Value = "0.0";
                }

                if (conditionGrid.Tag is AchievementConditionGroup activeGroup)
                {
                    activeGroup.Conditions.AddRange(conditions);
                    PopulateGridRows(conditionGrid, activeGroup);
                    BuildTriggerAndNotify();
                    ApplyGeneratorRules(activeGroup);

                    conditionGrid.ClearSelection();
                    int startIdx = conditionGrid.Rows.Count - conditions.Count;
                    if (startIdx >= 0 && startIdx < conditionGrid.Rows.Count)
                    {
                        for (int i = startIdx; i < conditionGrid.Rows.Count; i++)
                            conditionGrid.Rows[i].Selected = true;

                        conditionGrid.FirstDisplayedScrollingRowIndex = startIdx;
                    }
                }
            }
            SetStatus("Logic injected from Code Note.");
        }

        private string ValidateMemorySize(string inputSize)
        {
            if (string.IsNullOrEmpty(inputSize)) return "8-bit";
            if (inputSize.Equals("Flaot", StringComparison.OrdinalIgnoreCase)) return "Float";
            foreach (var validSize in MemorySizes)
            {
                if (validSize.Equals(inputSize, StringComparison.OrdinalIgnoreCase))
                    return validSize;
            }
            return "8-bit";
        }

        private void CreateEditingContextMenus()
        {
            CreateMenu(_flagContextMenu, Flags, OnContextMenuItemClick);
            CreateMenu(_typeContextMenu, OperandTypes, OnContextMenuItemClick);
            CreateMenu(_sizeContextMenu, MemorySizes, OnContextMenuItemClick);
            CreateMenu(_cmpContextMenu, ComparisonOperators, OnContextMenuItemClick);
            CreateMenu(_cmpArithmeticContextMenu, ArithmeticOperators, OnContextMenuItemClick);
            var strictComparisonOperators = ComparisonOperators.Where(op => op != NO_OPERATOR_TEXT).ToArray();
            CreateMenu(_cmpStrictContextMenu, strictComparisonOperators, OnContextMenuItemClick);
        }

        private void CreateMenu(ContextMenuStrip menu, string[] items, EventHandler clickHandler)
        {
            foreach (var item in items)
            {
                var menuItem = new ToolStripMenuItem(item.Replace("&", "&&"), null, clickHandler) { Tag = item };
                menu.Items.Add(menuItem);
            }
        }

        #region Logic Parsing & Grid

        private void UpdateUIFromText()
        {
            int savedGroupIndex = groupsList.SelectedIndices.Count > 0 ? groupsList.SelectedIndices[0] : 0;
            int savedGridScroll = conditionGrid.FirstDisplayedScrollingRowIndex;
            int savedRowIndex = conditionGrid.CurrentCell?.RowIndex ?? -1;
            int savedColIndex = conditionGrid.CurrentCell?.ColumnIndex ?? -1;

            _isProgrammaticallyChanging = true;

            if (_isLeaderboardMode)
            {
                _leaderboardSegments = ParseLeaderboardIntoSegments(_triggerText);
                RefreshLeaderboardGroupsList();
            }
            else
            {
                _activeGroups = AchievementParser.ParseAchievementTrigger(_triggerText, GroupSeparator, false);
                if (!_activeGroups.Any()) _activeGroups.Add(new AchievementConditionGroup { GroupName = "Core" });

                RefreshGroupsListUI();
            }

            if (savedGroupIndex < groupsList.Items.Count)
            {
                groupsList.SelectedIndices.Clear();
                groupsList.Items[savedGroupIndex].Selected = true;
            }
            else if (groupsList.Items.Count > 0)
            {
                groupsList.Items[0].Selected = true;
            }

            addGroupToolStripMenuItem.Enabled = true;
            removeGroupToolStripMenuItem.Enabled = true;

            _isProgrammaticallyChanging = false;

            if (groupsList.SelectedItems.Count > 0)
            {
                GroupsList_SelectedIndexChanged(groupsList, EventArgs.Empty);

                if (savedGridScroll >= 0 && savedGridScroll < conditionGrid.Rows.Count)
                    try { conditionGrid.FirstDisplayedScrollingRowIndex = savedGridScroll; } catch { }

                if (savedRowIndex >= 0 && savedRowIndex < conditionGrid.Rows.Count && savedColIndex >= 0)
                    conditionGrid.CurrentCell = conditionGrid.Rows[savedRowIndex].Cells[savedColIndex];
            }
            else
            {
                conditionGrid.Rows.Clear();
            }
        }

        private Dictionary<string, List<AchievementConditionGroup>> ParseLeaderboardIntoSegments(string logic)
        {
            var segments = new Dictionary<string, List<AchievementConditionGroup>>
            {
                { "Start", new List<AchievementConditionGroup>() },
                { "Cancel", new List<AchievementConditionGroup>() },
                { "Submit", new List<AchievementConditionGroup>() },
                { "Value", new List<AchievementConditionGroup>() }
            };

            if (string.IsNullOrEmpty(logic)) return segments;

            string[] parts = Regex.Split(logic, "::");

            foreach (var part in parts)
            {
                string typeKey = "";
                string cleanLogic = part;

                if (part.StartsWith("STA:")) { typeKey = "Start"; cleanLogic = part.Substring(4); }
                else if (part.StartsWith("CAN:")) { typeKey = "Cancel"; cleanLogic = part.Substring(4); }
                else if (part.StartsWith("SUB:")) { typeKey = "Submit"; cleanLogic = part.Substring(4); }
                else if (part.StartsWith("VAL:")) { typeKey = "Value"; cleanLogic = part.Substring(4); }

                if (!string.IsNullOrEmpty(typeKey))
                {
                    var groups = AchievementParser.ParseAchievementTrigger(cleanLogic, 'S', false);
                    if (!groups.Any()) groups.Add(new AchievementConditionGroup { GroupName = "Core" });
                    segments[typeKey] = groups;
                }
            }

            foreach (var key in segments.Keys)
            {
                if (segments[key].Count == 0) segments[key].Add(new AchievementConditionGroup { GroupName = "Core" });
            }

            return segments;
        }

        private void RefreshGroupsListUI()
        {
            groupsList.BeginUpdate();
            groupsList.Items.Clear();

            HashSet<int> generatedIndices = new HashSet<int>();
            foreach (var kvp in _generatorRules)
            {
                int masterIndex = kvp.Key;
                int copies = kvp.Value.Copies;
                for (int i = 1; i <= copies; i++)
                {
                    generatedIndices.Add(masterIndex + i);
                }
            }

            for (int i = 0; i < _activeGroups.Count; i++)
            {
                var group = _activeGroups[i];
                var item = new ListViewItem(group.GroupName);

                if (generatedIndices.Contains(i))
                {
                    item.ForeColor = Color.Gray;
                    item.Font = new Font(groupsList.Font, FontStyle.Italic);
                    item.Text += " (Auto)";
                }
                else if (_generatorRules.ContainsKey(i))
                {
                    item.ForeColor = Color.Blue;
                    item.Font = new Font(groupsList.Font, FontStyle.Bold);
                    item.Text += " *";
                }
                groupsList.Items.Add(item);
            }

            if (groupsList.Items.Count > 0) groupsList.Items[0].Selected = true;
            else conditionGrid.Rows.Clear();
            groupsList.EndUpdate();
        }

        private void GroupsList_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_isProgrammaticallyChanging) return;
            if (groupsList.SelectedItems.Count == 0) return;

            int index = groupsList.SelectedItems[0].Index;
            if (index >= 0 && index < _activeGroups.Count)
            {
                var group = _activeGroups[index];
                conditionGrid.Tag = group;

                bool isGenerated = false;
                foreach (var kvp in _generatorRules)
                {
                    if (index > kvp.Key && index <= kvp.Key + kvp.Value.Copies)
                    {
                        isGenerated = true;
                        break;
                    }
                }

                PopulateGridRows(conditionGrid, group, isGenerated);
            }
        }

        private void AddGridColumn(DataGridView grid, string name, string header, int width, bool readOnly)
        {
            var col = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = width,
                ReadOnly = readOnly,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            if (name == "ColFlag" || name == "ColLValue" || name == "ColRValue")
                col.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns.Add(col);
        }

        private void PopulateGridRows(DoubleBufferedDataGridView grid, AchievementConditionGroup group, bool readOnlyMode = false)
        {
            grid.Rows.Clear();
            if (group == null || group.Conditions == null) return;

            grid.ReadOnly = readOnlyMode;
            grid.DefaultCellStyle.BackColor = readOnlyMode ? SystemColors.Control : SystemColors.Window;
            grid.DefaultCellStyle.ForeColor = readOnlyMode ? SystemColors.GrayText : SystemColors.ControlText;

            for (int i = 0; i < group.Conditions.Count; i++)
            {
                var cond = group.Conditions[i];
                cond.ID = i + 1;
                var row = new DataGridViewRow();
                row.CreateCells(grid);
                row.Tag = cond;
                SetRowValues(row, cond, readOnlyMode);
                grid.Rows.Add(row);
            }
        }

        private void SetRowValues(DataGridViewRow row, AchievementCondition cond, bool readOnlyMode = false)
        {
            row.Cells[0].Value = cond.ID;
            row.Cells[1].Value = cond.Flag;
            row.Cells[2].Value = cond.LeftOperand.Type;
            row.Cells[3].Value = cond.LeftOperand.Size;
            row.Cells[4].Value = FormatDisplayValue(cond.LeftOperand);
            row.Cells[5].Value = cond.Operator;
            row.Cells[6].Value = cond.RightOperand.Type;
            row.Cells[7].Value = cond.RightOperand.Size;
            row.Cells[8].Value = FormatDisplayValue(cond.RightOperand, GetRightOperandSizeReference(cond));

            var lSizeCell = row.Cells[3];
            var lValueCell = row.Cells[4];
            bool leftIsRecall = cond.LeftOperand.Type == "Recall";

            Color editableColor = readOnlyMode ? SystemColors.Control : SystemColors.Window;
            Color staticColor = SystemColors.ControlLight;

            if (cond.LeftOperand.Type is "Float" or "Value" or "Recall")
            {
                lSizeCell.Style.BackColor = staticColor;
                lSizeCell.ToolTipText = "";
                if (leftIsRecall)
                {
                    lValueCell.ReadOnly = true;
                    lValueCell.Style.BackColor = staticColor;
                    row.Cells[4].Value = "";
                }
                else
                {
                    lValueCell.ReadOnly = readOnlyMode;
                    lValueCell.Style.BackColor = editableColor;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(cond.LeftOperand.Size))
                {
                    lSizeCell.Style.BackColor = readOnlyMode ? Color.FromArgb(240, 240, 220) : Color.LemonChiffon;
                    lSizeCell.ToolTipText = "Implicit 32-bit (Please specify size explicitly)";
                }
                else
                {
                    lSizeCell.Style.BackColor = editableColor;
                    lSizeCell.ToolTipText = "";
                }

                lValueCell.ReadOnly = readOnlyMode;
                lValueCell.Style.BackColor = editableColor;
            }

            var rTypeCell = row.Cells[6];
            var rSizeCell = row.Cells[7];
            var rValueCell = row.Cells[8];
            bool rightIsRecall = cond.RightOperand.Type == "Recall";

            if (string.IsNullOrEmpty(cond.Operator))
            {
                rTypeCell.Style.BackColor = staticColor;
                rSizeCell.Style.BackColor = staticColor;
                rValueCell.ReadOnly = true;
                rValueCell.Style.BackColor = staticColor;
                row.Cells[6].Value = "";
                row.Cells[7].Value = "";
                row.Cells[8].Value = "";
            }
            else
            {
                rTypeCell.Style.BackColor = editableColor;

                if (rightIsRecall)
                {
                    rSizeCell.Style.BackColor = staticColor;
                    rSizeCell.ToolTipText = "";
                    rValueCell.ReadOnly = true;
                    rValueCell.Style.BackColor = staticColor;
                    row.Cells[8].Value = "";
                }
                else if (cond.RightOperand.Type is "Float" or "Value")
                {
                    rValueCell.ReadOnly = readOnlyMode;
                    rValueCell.Style.BackColor = editableColor;
                    rSizeCell.Style.BackColor = staticColor;
                    rSizeCell.ToolTipText = "";
                }
                else
                {
                    if (string.IsNullOrEmpty(cond.RightOperand.Size))
                    {
                        if (MemoryTypes.Contains(cond.RightOperand.Type))
                        {
                            rSizeCell.Style.BackColor = readOnlyMode ? Color.FromArgb(240, 240, 220) : Color.LemonChiffon;
                            rSizeCell.ToolTipText = "Implicit 32-bit (Please specify size explicitly)";
                        }
                        else
                        {
                            rSizeCell.Style.BackColor = editableColor;
                            rSizeCell.ToolTipText = "";
                        }
                    }
                    else
                    {
                        rSizeCell.Style.BackColor = editableColor;
                        rSizeCell.ToolTipText = "";
                    }

                    rValueCell.ReadOnly = readOnlyMode;
                    rValueCell.Style.BackColor = editableColor;
                }
            }

            if (FlagsWithoutHits.Contains(cond.Flag))
            {
                cond.RequiredHits = 0;
                row.Cells[9].Value = "";
                row.Cells[9].ReadOnly = true;
                row.Cells[9].Style.BackColor = staticColor;
            }
            else
            {
                row.Cells[9].Value = cond.RequiredHits > 0 ? cond.RequiredHits.ToString() : "";
                row.Cells[9].ReadOnly = readOnlyMode;
                row.Cells[9].Style.BackColor = editableColor;
            }
        }
        #endregion

        #region Formatting & Helpers
        private string GetRightOperandSizeReference(AchievementCondition cond)
        {
            if (ArithmeticOperators.Contains(cond.Operator) && cond.Operator != NO_OPERATOR_TEXT) return "32-bit";
            return cond.LeftOperand.Size;
        }

        private string FormatDisplayValue(Operand operand, string sizeReference = "")
        {
            if (string.IsNullOrEmpty(operand.Value)) return "";

            if (long.TryParse(operand.Value.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long val))
            {
                if (operand.Type == "Value")
                {
                    // Apply Show Decimal setting only for "Value" type
                    if (_showDecimal) return val.ToString();

                    int padding = GetPaddingForSize(sizeReference);
                    long mask = GetMaskForSize(sizeReference);
                    return "0x" + (val & mask).ToString($"x{padding}");
                }
                else if (operand.Type != "Float")
                {
                    return "0x" + val.ToString("x8");
                }
            }
            if (operand.Type == "Float")
            {
                string normalized = operand.Value.Replace(',', '.');
                if (decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal decVal) && decVal % 1 == 0 && !normalized.Contains('.'))
                {
                    return normalized + ".0";
                }
                return normalized;
            }
            return operand.Value;
        }

        private int GetPaddingForSize(string size)
        {
            if (size.Contains("8-bit") || size.Contains("Bit") || size.Contains("4")) return 2;
            if (size.Contains("16-bit")) return 4;
            if (size.Contains("24-bit")) return 6;
            return 8;
        }

        private long GetMaskForSize(string size)
        {
            if (size.Contains("8-bit") || size.Contains("Bit") || size.Contains("4")) return 0xFF;
            if (size.Contains("16-bit")) return 0xFFFF;
            if (size.Contains("24-bit")) return 0xFFFFFF;
            return -1;
        }

        private string ParseAndFormatValue(string input, string operandType, string size)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";

            if (operandType == "Float")
            {
                return input.Replace(',', '.');
            }

            if (operandType == "Mem" && ReverseFloatSizeMap.ContainsKey(size))
            {
                string safeInput = input.Replace(',', '.');
                if (float.TryParse(safeInput, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatVal))
                {
                    byte[] bytes = BitConverter.GetBytes(floatVal);
                    if (size.EndsWith("BE")) Array.Reverse(bytes);
                    return "0x" + BitConverter.ToUInt32(bytes, 0).ToString("x8");
                }
                if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && uint.TryParse(input.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hexVal))
                    return "0x" + hexVal.ToString("x8");
                return input;
            }

            long val;
            int padding;
            if (operandType == "Mem" || operandType == "Delta" || operandType == "Prior" || operandType == "BCD" || operandType == "Inverted")
            {
                string hexInput = input.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? input.Substring(2) : input;
                if (!long.TryParse(hexInput, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out val)) return input;
                padding = 8;
            }
            else if (operandType == "Value")
            {
                if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    if (!long.TryParse(input.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out val)) return input;
                }
                else
                {
                    if (!long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out val)) return input;
                }
                padding = GetPaddingForSize(size);
            }
            else return input;

            return "0x" + val.ToString($"x{padding}");
        }
        #endregion

        #region Grid Interaction (Events)
        private void Grid_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (_isProgrammaticallyChanging || e.RowIndex < 0) return;
            var grid = sender as DoubleBufferedDataGridView;
            if (grid == null) return;
            var row = grid.Rows[e.RowIndex];
            if (row.Tag is not AchievementCondition cond) return;
            var cell = row.Cells[e.ColumnIndex];
            string value = cell.Value?.ToString() ?? "";

            if (cell.OwningColumn.Name == "ColLValue")
                cond.LeftOperand.Value = ParseAndFormatValue(value, cond.LeftOperand.Type, cond.LeftOperand.Size);
            else if (cell.OwningColumn.Name == "ColRValue")
                cond.RightOperand.Value = ParseAndFormatValue(value, cond.RightOperand.Type, GetRightOperandSizeReference(cond));
            else if (cell.OwningColumn.Name == "ColHits")
            {
                var match = Regex.Match(value, @"\d+");
                cond.RequiredHits = match.Success ? uint.Parse(match.Value) : 0;
            }
        }

        private void Grid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var grid = sender as DoubleBufferedDataGridView;
            if (grid == null) return;
            PushUndoState();
            var row = grid.Rows[e.RowIndex];
            if (row.Tag is AchievementCondition cond)
            {
                ApplyConditionLogicRules(cond);
                SetRowValues(row, cond);
                BuildTriggerAndNotify();

                if (grid.Tag is AchievementConditionGroup group)
                    ApplyGeneratorRules(group);
            }
        }

        private void Grid_EditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (e.Control is TextBox editingControl)
            {
                var grid = sender as DataGridView;
                if (grid?.CurrentCell == null) return;

                editingControl.CharacterCasing = CharacterCasing.Lower;

                editingControl.TextChanged -= EditingControl_TextChanged;
                editingControl.TextChanged += EditingControl_TextChanged;
                editingControl.KeyPress -= EditingControl_KeyPress;
                editingControl.KeyPress += EditingControl_KeyPress;

                string colName = grid.Columns[grid.CurrentCell.ColumnIndex].Name;
                string type = "";
                if (colName == "ColLValue") type = grid.Rows[grid.CurrentCell.RowIndex].Cells["ColLType"].Value?.ToString() ?? "";
                else if (colName == "ColRValue") type = grid.Rows[grid.CurrentCell.RowIndex].Cells["ColRType"].Value?.ToString() ?? "";

                if (type == "Float")
                {
                    editingControl.MaxLength = 32;
                }
                else if (type == "Value")
                {
                    editingControl.MaxLength = 15;
                }
                else
                {
                    editingControl.MaxLength = 8;
                    if (editingControl.Text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        editingControl.Text = editingControl.Text.Substring(2);
                    }
                }
            }
        }

        private void EditingControl_KeyPress(object? sender, KeyPressEventArgs e)
        {
            if (char.IsControl(e.KeyChar)) return;

            bool allowFloat = false;

            if (conditionGrid.CurrentCell != null)
            {
                var row = conditionGrid.Rows[conditionGrid.CurrentCell.RowIndex];
                string colName = conditionGrid.Columns[conditionGrid.CurrentCell.ColumnIndex].Name;

                if (colName == "ColLValue")
                {
                    string type = row.Cells["ColLType"].Value?.ToString() ?? "";
                    allowFloat = type == "Float";
                }
                else if (colName == "ColRValue")
                {
                    string type = row.Cells["ColRType"].Value?.ToString() ?? "";
                    allowFloat = type == "Float";
                }
            }

            bool isHex = char.IsDigit(e.KeyChar) ||
                         (e.KeyChar >= 'a' && e.KeyChar <= 'f') ||
                         (e.KeyChar >= 'A' && e.KeyChar <= 'F') ||
                         e.KeyChar == 'x' || e.KeyChar == 'X';

            if (isHex) return;

            if (allowFloat && (e.KeyChar == '.' || e.KeyChar == '-' || e.KeyChar == ',')) return;

            e.Handled = true;
        }

        private void EditingControl_TextChanged(object? sender, EventArgs e)
        {
            if (_activeGrid != null && _activeGrid.CurrentCell != null && sender is TextBox tb)
                _activeGrid.CurrentCell.Value = tb.Text;
        }

        private void Grid_CellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                _activeGrid = sender as DoubleBufferedDataGridView;
                _activeRowIndex = e.RowIndex;
                _activeColIndex = e.ColumnIndex;
                if (_activeGrid == null) return;
                if (!_activeGrid.Rows[e.RowIndex].Selected)
                {
                    _activeGrid.ClearSelection();
                    _activeGrid.Rows[e.RowIndex].Selected = true;
                }

                if (_activeGrid.ReadOnly) return;

                var colName = _activeGrid.Columns[e.ColumnIndex].Name;
                if (colName == "ColFlag") _flagContextMenu.Show(Cursor.Position);
                else if (colName == "ColLType" || colName == "ColRType") _typeContextMenu.Show(Cursor.Position);
                else if (colName == "ColLSize" || colName == "ColRSize") _sizeContextMenu.Show(Cursor.Position);
                else if (colName == "ColCmp")
                {
                    var flag = _activeGrid.Rows[e.RowIndex].Cells["ColFlag"].Value?.ToString() ?? "";
                    if (ArithmeticFlags.Contains(flag)) _cmpArithmeticContextMenu.Show(Cursor.Position);
                    else if (StrictComparisonFlags.Contains(flag)) _cmpStrictContextMenu.Show(Cursor.Position);
                    else _cmpContextMenu.Show(Cursor.Position);
                }
            }
            else if (e.Button == MouseButtons.Left)
            {
                _activeGrid = sender as DoubleBufferedDataGridView;
            }
        }
        private void Grid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
                Grid_CellMouseDown(sender, new DataGridViewCellMouseEventArgs(e.ColumnIndex, e.RowIndex, 0, 0, new MouseEventArgs(MouseButtons.Right, 1, 0, 0, 0)));
        }

        private void OnContextMenuItemClick(object? sender, EventArgs e)
        {
            if (_activeGrid == null || sender is not ToolStripMenuItem item) return;
            if (_activeGrid.ReadOnly) return;

            DoubleBufferedDataGridView grid = _activeGrid!;
            PushUndoState();
            string val = item.Tag as string ?? "";
            if (val == NO_FLAG_TEXT || val == NO_OPERATOR_TEXT) val = "";

            AchievementConditionGroup? currentGroup = grid.Tag as AchievementConditionGroup;

            foreach (DataGridViewRow row in grid.SelectedRows)
            {
                if (row.Tag is AchievementCondition cond)
                {
                    string colName = grid.Columns[_activeColIndex].Name;
                    if (colName == "ColFlag")
                    {
                        string old = cond.Flag;
                        cond.Flag = val;
                        HandleFlagChange(cond, old);
                    }
                    else if (colName == "ColLType") HandleTypeChange(cond.LeftOperand, val, cond.LeftOperand.Size);
                    else if (colName == "ColLSize") cond.LeftOperand.Size = val;
                    else if (colName == "ColCmp")
                    {
                        string old = cond.Operator;
                        cond.Operator = val;
                        HandleOperatorChange(cond, old);
                    }
                    else if (colName == "ColRType") HandleTypeChange(cond.RightOperand, val, cond.LeftOperand.Size);
                    else if (colName == "ColRSize") cond.RightOperand.Size = val;
                    ApplyConditionLogicRules(cond);
                    SetRowValues(row, cond);
                }
            }
            BuildTriggerAndNotify();

            if (currentGroup != null)
                ApplyGeneratorRules(currentGroup);

            SetStatus("Updated condition property.");
        }
        #endregion

        #region Logic Rules
        private void HandleTypeChange(Operand op, string newType, string refSize)
        {
            string oldType = op.Type;
            if (oldType == newType) return;
            if (newType == "Recall") { op.Value = ""; op.Type = newType; return; }
            if (string.IsNullOrEmpty(op.Value) || oldType == "Recall")
            {
                if (newType == "Float") op.Value = "1.0";
                else op.Value = "0x" + 1.ToString("x" + GetPaddingForSize(refSize));
            }
            else
            {
                var integerLike = new HashSet<string> { "Mem", "Value", "Delta", "Prior", "BCD", "Inverted" };
                if (integerLike.Contains(oldType) && newType == "Float")
                {
                    if (uint.TryParse(op.Value.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint hexVal))
                        op.Value = BitConverter.ToSingle(BitConverter.GetBytes(hexVal), 0).ToString("G9", CultureInfo.InvariantCulture);
                }
                else if (oldType == "Float" && integerLike.Contains(newType))
                {
                    string safeVal = op.Value.Replace(',', '.');
                    if (float.TryParse(safeVal, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatVal))
                        op.Value = "0x" + BitConverter.ToUInt32(BitConverter.GetBytes(floatVal), 0).ToString("x8");
                }
            }
            op.Type = newType;
        }
        private void HandleFlagChange(AchievementCondition cond, string oldFlag)
        {
            bool wasA = ArithmeticFlags.Contains(oldFlag);
            bool isA = ArithmeticFlags.Contains(cond.Flag);
            if (wasA && !isA)
            {
                cond.Operator = "=";
                cond.RightOperand = new Operand { Type = "Value", Value = "0x1" };
            }
            else if (!wasA && isA)
            {
                cond.Operator = "";
                cond.RightOperand = new Operand();
            }
        }
        private void HandleOperatorChange(AchievementCondition cond, string oldOp)
        {
            bool wasE = string.IsNullOrEmpty(oldOp);
            bool isE = string.IsNullOrEmpty(cond.Operator);
            if (wasE && !isE) cond.RightOperand = new Operand { Type = "Value", Value = "0x1" };
            else if (!wasE && isE) cond.RightOperand = new Operand();
        }
        private void ApplyConditionLogicRules(AchievementCondition cond)
        {
            ApplyOperandSizeRules(cond.LeftOperand);
            ApplyOperandSizeRules(cond.RightOperand);
            if (cond.RightOperand.Type == "Value") cond.RightOperand.Size = "";
            if (StrictComparisonFlags.Contains(cond.Flag))
            {
                if (!ComparisonOperators.Contains(cond.Operator) || string.IsNullOrEmpty(cond.Operator))
                {
                    cond.Operator = "=";
                    cond.RightOperand = new Operand { Type = "Value", Value = "0x1" };
                }
            }
            else if (ArithmeticFlags.Contains(cond.Flag))
            {
                if (ComparisonOperators.Contains(cond.Operator))
                {
                    cond.Operator = "";
                    cond.RightOperand = new Operand();
                }
            }
        }
        private void ApplyOperandSizeRules(Operand op)
        {
            if (op.Type is "Value" or "Float" or "Recall") op.Size = "";
            else if (MemoryTypes.Contains(op.Type) && string.IsNullOrEmpty(op.Size)) op.Size = "32-bit";
        }
        #endregion

        #region Undo/Redo/Actions
        private void PushUndoState()
        {
            if (_undoStack.Count > 0 && _undoStack.Peek() == _triggerText) return;
            _undoStack.Push(_triggerText);
            _redoStack.Clear();
        }
        public void Undo()
        {
            if (_undoStack.Count == 0) return;
            _isUndoingRedoing = true;
            _redoStack.Push(_triggerText);
            _triggerText = _undoStack.Pop();
            UpdateUIFromText();
            TriggerTextChanged?.Invoke(this, EventArgs.Empty);
            _isUndoingRedoing = false;
            SetStatus("Undone last action.");
        }
        public void Redo()
        {
            if (_redoStack.Count == 0) return;
            _isUndoingRedoing = true;
            _undoStack.Push(_triggerText);
            _triggerText = _redoStack.Pop();
            UpdateUIFromText();
            TriggerTextChanged?.Invoke(this, EventArgs.Empty);
            _isUndoingRedoing = false;
            SetStatus("Redone last action.");
        }

        // Actions
        private void CopySelectedRows()
        {
            if (_activeGrid == null || _activeGrid.SelectedRows.Count == 0) return;
            var sb = new StringBuilder();
            foreach (DataGridViewRow row in _activeGrid.SelectedRows.Cast<DataGridViewRow>().OrderBy(r => r.Index))
            {
                var cells = row.Cells.Cast<DataGridViewCell>().Select(c => c.FormattedValue?.ToString() ?? "");
                sb.AppendLine(string.Join("\t", cells));
            }
            Clipboard.SetText(sb.ToString());
            StatusUpdateRequested?.Invoke(this, "Rows copied.");
            SetStatus("Rows copied to clipboard.");
        }
        private void CopySelectedLogic()
        {
            if (_activeGrid == null || _activeGrid.SelectedRows.Count == 0) return;
            var sb = new StringBuilder();
            foreach (DataGridViewRow row in _activeGrid.SelectedRows.Cast<DataGridViewRow>().OrderBy(r => r.Index))
            {
                if (row.Tag is AchievementCondition cond)
                {
                    if (sb.Length > 0) sb.Append('_');
                    sb.Append(ConditionToString(cond));
                }
            }
            Clipboard.SetText(sb.ToString());
            StatusUpdateRequested?.Invoke(this, "Logic copied.");
            SetStatus("Logic string copied to clipboard.");
        }
        private void PasteLogic()
        {
            if (!Clipboard.ContainsText() || _activeGrid == null) return;
            if (_activeGrid.ReadOnly) return;

            string txt = Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(txt)) return;

            txt = txt.Trim().Replace("\r", "").Replace("\n", "");

            PushUndoState();

            if (_activeGrid.Tag is not AchievementConditionGroup group) return;

            int idx = _activeGrid.SelectedRows.Count > 0
                ? _activeGrid.SelectedRows.Cast<DataGridViewRow>().Max(r => r.Index) + 1
                : _activeGrid.Rows.Count;

            var conds = AchievementParser.ParseAchievementTrigger(txt, GroupSeparator, _isLeaderboardMode)
                .SelectMany(g => g.Conditions).ToList();

            group.Conditions.InsertRange(idx, conds);
            PopulateGridRows(_activeGrid, group);

            // Selection Logic
            _activeGrid.ClearSelection();

            if (conds.Count > 0 && idx < _activeGrid.Rows.Count)
            {
                _activeGrid.CurrentCell = _activeGrid.Rows[idx].Cells[0];

                for (int i = 0; i < conds.Count; i++)
                {
                    if (idx + i < _activeGrid.Rows.Count)
                        _activeGrid.Rows[idx + i].Selected = true;
                }
            }

            BuildTriggerAndNotify();
            ApplyGeneratorRules(group);

            StatusUpdateRequested?.Invoke(this, "Pasted logic.");
            SetStatus("Logic pasted from clipboard.");
        }

        private void DuplicateSelectedItems(object? sender, EventArgs e)
        {
            if (_activeGrid == null) return;
            if (_activeGrid.ReadOnly) return;

            PushUndoState();
            var group = _activeGrid.Tag as AchievementConditionGroup;
            if (group == null || _activeGrid.SelectedRows.Count == 0) return;

            var rows = _activeGrid.SelectedRows.Cast<DataGridViewRow>().OrderBy(r => r.Index).ToList();
            int idx = rows.Max(r => r.Index) + 1;
            var clones = rows.Select(r => r.Tag).OfType<AchievementCondition>().Select(c => c.Clone()).ToList();

            group.Conditions.InsertRange(idx, clones);
            PopulateGridRows(_activeGrid, group);

            // Selection Logic
            _activeGrid.ClearSelection();

            if (idx < _activeGrid.Rows.Count)
            {
                _activeGrid.CurrentCell = _activeGrid.Rows[idx].Cells[0];
            }

            for (int i = 0; i < clones.Count; i++)
            {
                if (idx + i < _activeGrid.Rows.Count)
                {
                    _activeGrid.Rows[idx + i].Selected = true;
                }
            }

            BuildTriggerAndNotify();
            ApplyGeneratorRules(group);
            SetStatus("Conditions duplicated.");
        }

        private void DeleteSelected(object? sender, EventArgs e)
        {
            if (_activeGrid == null) return;
            if (_activeGrid.ReadOnly) return;

            var group = _activeGrid.Tag as AchievementConditionGroup;
            if (group == null) return;

            PushUndoState();

            var rowsToDelete = _activeGrid.SelectedRows.Cast<DataGridViewRow>().ToList();

            foreach (var row in rowsToDelete)
            {
                if (row.IsNewRow) continue;

                if (row.Tag is AchievementCondition cond)
                {
                    group.Conditions.Remove(cond);
                }

                _activeGrid.Rows.Remove(row);
            }

            BuildTriggerAndNotify();
            ApplyGeneratorRules(group);
            SetStatus("Conditions deleted.");
        }
        private void MoveSelectedItems(int dist)
        {
            if (_activeGrid == null) return;
            if (_activeGrid.ReadOnly) return;

            var group = _activeGrid.Tag as AchievementConditionGroup;
            if (group == null) return;
            PushUndoState();
            var indices = _activeGrid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Index).OrderBy(i => i).ToList();
            if (dist < 0 && indices.Min() == 0) return;
            if (dist > 0 && indices.Max() == _activeGrid.Rows.Count - 1) return;
            if (dist > 0) indices.Reverse();
            foreach (var idx in indices)
            {
                var item = group.Conditions[idx];
                group.Conditions.RemoveAt(idx);
                group.Conditions.Insert(idx + dist, item);
            }
            PopulateGridRows(_activeGrid, group);
            var newSel = indices.Select(i => i + dist).ToList();
            _activeGrid.ClearSelection();
            foreach (var i in newSel) _activeGrid.Rows[i].Selected = true;
            BuildTriggerAndNotify();
            ApplyGeneratorRules(group);
            SetStatus("Conditions moved.");
        }

        private void AddGroupAction(object? sender, EventArgs e)
        {
            PushUndoState();
            string newName = _activeGroups.Count == 0 ? "Core" : "Alt " + _activeGroups.Count;
            if (GroupSeparator != 'S') newName = "Group " + (_activeGroups.Count + 1);
            var newGroup = new AchievementConditionGroup { GroupName = newName };
            _activeGroups.Add(newGroup);

            var item = new ListViewItem(newName);
            groupsList.Items.Add(item);
            groupsList.SelectedItems.Clear();
            item.Selected = true;

            BuildTriggerAndNotify();
            SetStatus("Group added.");
        }

        private void removeGroupToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            if (groupsList.SelectedItems.Count == 0) return;
            int idx = groupsList.SelectedItems[0].Index;

            if (_activeGroups.Count <= 1) return;

            foreach (var kvp in _generatorRules)
            {
                if (idx > kvp.Key && idx <= kvp.Key + kvp.Value.Copies)
                {
                    MessageBox.Show("Cannot manually delete a generated group. Modify the Generator on the Master Group instead.", "Action Blocked", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            if (_generatorRules.ContainsKey(idx)) _generatorRules.Remove(idx);

            PushUndoState();
            _activeGroups.RemoveAt(idx);
            groupsList.Items.RemoveAt(idx);

            for (int i = 1; i < _activeGroups.Count; i++)
            {
                if (_activeGroups[i].GroupName.StartsWith("Alt "))
                {
                    _activeGroups[i].GroupName = $"Alt {i}";
                    groupsList.Items[i].Text = _activeGroups[i].GroupName;
                }
            }

            if (idx >= _activeGroups.Count) idx = _activeGroups.Count - 1;
            if (idx >= 0) groupsList.Items[idx].Selected = true;

            BuildTriggerAndNotify();
            SetStatus("Group removed.");
        }

        private void removeAllAltGroupsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_activeGroups.Count <= 1) return;

            if (_generatorRules.Count > 0)
            {
                var result = MessageBox.Show("This will delete all Alt groups, including those managed by Generators. Are you sure?", "Confirm Delete All", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result != DialogResult.Yes) return;
            }
            else
            {
                var result = MessageBox.Show("Are you sure you want to remove all Alt groups? This cannot be easily undone without Undo.", "Confirm Delete All", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result != DialogResult.Yes) return;
            }

            PushUndoState();
            _generatorRules.Clear();

            var first = _activeGroups[0];
            _activeGroups.Clear();
            _activeGroups.Add(first);

            if (_isLeaderboardMode == false && first.GroupName != "Core") first.GroupName = "Core";

            RefreshGroupsListUI();
            BuildTriggerAndNotify();
            SetStatus("All Alt groups removed.");
        }

        private void moveUpToolStripMenuItem_Click(object sender, EventArgs e) => MoveSelectedItems(-1);
        private void moveDownToolStripMenuItem_Click(object sender, EventArgs e) => MoveSelectedItems(1);
        private void duplicateToolStripMenuItem_Click(object sender, EventArgs e) => DuplicateSelectedItems(sender, e);
        private void copyRowToolStripMenuItem_Click(object sender, EventArgs e) => CopySelectedRows();
        private void copyLogicToolStripMenuItem_Click(object sender, EventArgs e) => CopySelectedLogic();
        private void pasteToolStripMenuItem_Click(object sender, EventArgs e) => PasteLogic();
        private void deleteToolStripMenuItem_Click(object sender, EventArgs e) => DeleteSelected(sender, e);
        private void addConditionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_activeGrid == null) return;
            if (_activeGrid.ReadOnly) return;

            PushUndoState();
            var cond = new AchievementCondition
            {
                LeftOperand = { Type = "Value", Value = "0" },
                Operator = "=",
                RightOperand = { Type = "Value", Value = "1" }
            };
            var group = _activeGrid.Tag as AchievementConditionGroup;

            if (group != null)
            {
                group.Conditions.Add(cond);
                PopulateGridRows(_activeGrid!, group);
                BuildTriggerAndNotify();
                ApplyGeneratorRules(group);
            }
            SetStatus("Condition added: 0=1");
        }
        private void addGroupToolStripMenuItem_Click(object sender, EventArgs e) => AddGroupAction(sender, e);
        #endregion

        #region Generator Logic
        private void createGeneratorToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (conditionGrid.SelectedRows.Count != 1) return;
            var group = conditionGrid.Tag as AchievementConditionGroup;
            if (group == null) return;

            int groupIndex = _activeGroups.IndexOf(group);
            if (groupIndex == -1) return;

            var row = conditionGrid.SelectedRows[0];
            string lVal = row.Cells["ColLValue"].Value?.ToString() ?? "";
            string rVal = row.Cells["ColRValue"].Value?.ToString() ?? "";

            string targetAddress = lVal.StartsWith("0x") ? lVal : (rVal.StartsWith("0x") ? rVal : "");
            if (string.IsNullOrEmpty(targetAddress)) return;

            using var form = new GeneratorConfigForm(targetAddress);
            if (form.ShowDialog() == DialogResult.OK)
            {
                PushUndoState();

                if (!_generatorRules.ContainsKey(groupIndex))
                {
                    _generatorRules[groupIndex] = new GeneratorDefinition
                    {
                        Copies = form.Copies,
                        Rules = new List<GeneratorRule>()
                    };
                }
                else
                {
                    _generatorRules[groupIndex].Copies = form.Copies;
                }

                var def = _generatorRules[groupIndex];
                var existingRule = def.Rules.FirstOrDefault(r => r.SourceAddress == targetAddress);
                if (existingRule != null)
                {
                    existingRule.Stride = form.Stride;
                }
                else
                {
                    def.Rules.Add(new GeneratorRule { SourceAddress = targetAddress, Stride = form.Stride });
                }

                ApplyGeneratorRules(group);
                RefreshGroupsListUI();
            }
        }

        private void ApplyGeneratorRules(AchievementConditionGroup masterGroup)
        {
            int masterIndex = _activeGroups.IndexOf(masterGroup);
            if (masterIndex == -1 || !_generatorRules.ContainsKey(masterIndex)) return;

            var def = _generatorRules[masterIndex];

            while (_activeGroups.Count <= masterIndex + def.Copies)
            {
                _activeGroups.Insert(masterIndex + 1, new AchievementConditionGroup { GroupName = "Temp" });
            }

            for (int i = 1; i <= def.Copies; i++)
            {
                int targetGroupIndex = masterIndex + i;
                var targetGroup = _activeGroups[targetGroupIndex];

                string baseName = masterGroup.GroupName;
                if (baseName.Contains(" #"))
                {
                    baseName = Regex.Replace(baseName, @" #\d+", "");
                }
                targetGroup.GroupName = $"{baseName} (+{i})";

                targetGroup.Conditions.Clear();
                foreach (var cond in masterGroup.Conditions)
                {
                    var newCond = cond.Clone();

                    foreach (var rule in def.Rules)
                    {
                        ApplyOffsetToOperand(newCond.LeftOperand, rule.SourceAddress, rule.Stride, i);
                        ApplyOffsetToOperand(newCond.RightOperand, rule.SourceAddress, rule.Stride, i);
                    }

                    targetGroup.Conditions.Add(newCond);
                }
            }

            RefreshGroupsListUI();
        }

        private void ApplyOffsetToOperand(Operand op, string targetAddress, int stride, int multiplier)
        {
            if (op == null || string.IsNullOrEmpty(op.Value)) return;
            if (op.Type == "Recall") return;

            string cleanTarget = targetAddress.StartsWith("0x") ? targetAddress.Substring(2) : targetAddress;
            string cleanOp = op.Value.StartsWith("0x") ? op.Value.Substring(2) : op.Value;

            if (cleanOp.Equals(cleanTarget, StringComparison.OrdinalIgnoreCase))
            {
                if (long.TryParse(cleanOp, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long baseVal))
                {
                    long offset = (long)stride * multiplier;
                    long newVal = baseVal + offset;

                    int padding = cleanOp.Length;
                    op.Value = "0x" + newVal.ToString($"X{padding}");
                }
            }
        }
        #endregion

        #region String Utils
        public static string ConditionToString(AchievementCondition cond)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(cond.Flag) && ReverseFlagMap.TryGetValue(cond.Flag, out char f)) sb.Append(f).Append(':');
            sb.Append(OperandToString(cond.LeftOperand));
            sb.Append(cond.Operator);
            sb.Append(OperandToString(cond.RightOperand));
            if (cond.RequiredHits > 0) sb.Append('.').Append(cond.RequiredHits).Append('.');
            return sb.ToString();
        }
        private static string OperandToString(Operand op)
        {
            if (op == null || string.IsNullOrEmpty(op.Type)) return "";
            if (op.Type == "Recall") return "{recall}";
            var sb = new StringBuilder();
            string core = op.Type;
            bool pfx = false;
            switch (op.Type)
            {
                case "Delta": sb.Append('d'); pfx = true; break;
                case "Prior": sb.Append('p'); pfx = true; break;
                case "BCD": sb.Append('b'); pfx = true; break;
                case "Inverted": sb.Append('~'); pfx = true; break;
                case "Float": return "f" + op.Value;
            }
            if (pfx) core = "Mem";
            if (core == "Mem")
            {
                if (ReverseFloatSizeMap.TryGetValue(op.Size, out char fs))
                {
                    sb.Append("f").Append(fs);
                    sb.Append(op.Value.Replace("0x", ""));
                }
                else
                {
                    sb.Append("0x");
                    if (ReverseSizeMap.TryGetValue(op.Size, out char s) && s != ' ') sb.Append(char.ToUpper(s));
                    sb.Append(op.Value.Replace("0x", ""));
                }
            }
            else if (core == "Value")
            {
                if (op.Value.StartsWith("0x") && long.TryParse(op.Value.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long v)) return v.ToString();
                return op.Value;
            }
            else sb.Append(op.Value);
            return sb.ToString();
        }
        private void BuildTriggerAndNotify()
        {
            if (_isProgrammaticallyChanging) return;

            var sb = new StringBuilder();

            if (_isLeaderboardMode)
            {
                var sta = RebuildSegment(_leaderboardSegments.ContainsKey("Start") ? _leaderboardSegments["Start"] : new List<AchievementConditionGroup>());
                var can = RebuildSegment(_leaderboardSegments.ContainsKey("Cancel") ? _leaderboardSegments["Cancel"] : new List<AchievementConditionGroup>());
                var sub = RebuildSegment(_leaderboardSegments.ContainsKey("Submit") ? _leaderboardSegments["Submit"] : new List<AchievementConditionGroup>());
                var val = RebuildSegment(_leaderboardSegments.ContainsKey("Value") ? _leaderboardSegments["Value"] : new List<AchievementConditionGroup>());

                sb.Append("STA:").Append(sta).Append("::");
                sb.Append("CAN:").Append(can).Append("::");
                sb.Append("SUB:").Append(sub).Append("::");
                sb.Append("VAL:").Append(val);
            }
            else
            {
                sb.Append(RebuildSegment(_activeGroups));
            }

            string n = sb.ToString();
            if (_triggerText != n)
            {
                _triggerText = n;
                TriggerTextChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private string RebuildSegment(List<AchievementConditionGroup> groups)
        {
            if (groups == null || groups.Count == 0) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < groups.Count; i++)
            {
                if (i > 0) sb.Append(GroupSeparator);
                sb.Append(BuildGroupLogic(groups[i]));
            }
            return sb.ToString();
        }

        private string BuildGroupLogic(AchievementConditionGroup group)
        {
            var sb = new StringBuilder();
            for (int c = 0; c < group.Conditions.Count; c++)
            {
                if (c > 0) sb.Append('_');
                sb.Append(ConditionToString(group.Conditions[c]));
            }
            return sb.ToString();
        }
        #endregion
    }

    // -- HELPER CLASSES --

    public class AddressTooltipEventArgs : EventArgs
    {
        public string Address { get; }
        public string TooltipText { get; set; } = "";
        public AddressTooltipEventArgs(string address) { Address = address; }
    }

    public class PointerChainArgs : EventArgs
    {
        public string BaseAddress { get; set; }
        public List<long> Offsets { get; set; }

        public PointerChainArgs(string baseAddr, List<long> offsets)
        {
            BaseAddress = baseAddr;
            Offsets = offsets;
        }
    }
}