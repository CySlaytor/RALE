#nullable enable
using RALogicEditor.Definitions;
using RALogicEditor.Forms;
using RALogicEditor.Helpers;
using RALogicEditor.Models;
using RALogicEditor.Parsers;
using RALogicEditor.Services;
using RALogicEditor.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace RALogicEditor.Controls
{
    public partial class TriggerEditorControl : UserControl
    {
        // -- ViewModel Handles State and Parsing --
        // This moves business logic out of the View, enabling better testing and history management
        private readonly TriggerViewModel _viewModel;

        // Handles all DataGridView interactions (Sorting, Context Menus, Edit Controls)
        private readonly ConditionGridManager _gridManager;

        // -- View State --
        private bool _isProgrammaticallyChanging; // Lock to prevent event loops when updating UI from Model
        private bool _showDecimal = false;
        private bool _showAliases = false;
        private bool _collapseChains = false;

        // Current list of validation issues for highlighting specific rows
        private List<ValidationIssue> _currentIssues = new();

        // Cache for Code Notes for Tooltips/Aliases
        // Key: Normalized Address (e.g., "0x1234"), Value: Parsed Pointer Tree
        private Dictionary<string, List<NoteNode>> _notesLookup = new();
        private List<CodeNote> _rawNotes = new(); // Keep raw notes for Multi-Region tool

        // -- Events --
        public event EventHandler? TriggerTextChanged;
        public event EventHandler? AchievementDataChanged;
        public event EventHandler<string>? StatusUpdateRequested;
        public event EventHandler? AssetResetRequested;
        public event EventHandler? AddFromCodeNoteRequested;
        public event EventHandler<string>? AddressSelected;
        public event EventHandler<PointerChainArgs>? PointerChainSelected;
        public event EventHandler<List<ValidationIssue>>? ValidationCompleted;

        // NEW: Event to request Multi-Region Batch Processing in MainForm
        public event EventHandler<MultiRegionForm>? MultiRegionLogicRequested;

        // Public property for the Logic String (Two-way binding via ViewModel)
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string TriggerText
        {
            get => _viewModel.BoundAsset?.Logic ?? "";
            set
            {
                // This setter is primarily used when the Parent (MainForm) pushes a string change manually.
                // In MVVM this is less common, but we maintain compatibility with legacy calls.
                if (_viewModel.BoundAsset != null && _viewModel.BoundAsset.Logic != value)
                {
                    _viewModel.BoundAsset.Logic = value;
                    // Force VM to reload state from this new string
                    _viewModel.LoadAsset(_viewModel.BoundAsset);
                }
            }
        }

        public TriggerEditorControl()
        {
            InitializeComponent();

            // Manual Dependency Injection (Service Location)
            // In a full framework, this would come from an IoC container.
            ILogicValidator validator = new LogicValidator();
            ILogicGenerator generator = new LogicGeneratorService();
            _viewModel = new TriggerViewModel(validator, generator);

            _gridManager = new ConditionGridManager(conditionGrid);
            InitializeEvents();
            InitializeGrids();

            SetStatus("Ready");

            // Wire up ViewModel events to react to internal logic changes
            _viewModel.LogicChanged += ViewModel_LogicChanged;
            _viewModel.SegmentsChanged += ViewModel_SegmentsChanged;
        }

        // Added helper method to bridge VM/Manager status to Parent Form
        private void SetStatus(string msg)
        {
            if (lblEditorStatus != null)
                lblEditorStatus.Text = msg;
            StatusUpdateRequested?.Invoke(this, msg);
        }

        private void InitializeEvents()
        {
            // Bridge Manager events to Control events
            _gridManager.LogicChanged += GridManager_LogicChanged;
            _gridManager.StatusUpdateRequested += (s, msg) => SetStatus(msg);
            _gridManager.AddressSelected += (s, addr) => AddressSelected?.Invoke(this, addr);
            _gridManager.PointerChainSelected += (s, args) => PointerChainSelected?.Invoke(this, args);
            _gridManager.PasteRequested += GridManager_PasteRequested;

            // Input field wiring
            txtTitle.TextChanged += TxtTitle_TextChanged;
            txtDescription.TextChanged += TxtDescription_TextChanged;
            cmbPoints.SelectedIndexChanged += CmbPoints_Changed;
            cmbPoints.TextChanged += CmbPoints_Changed;
            cmbType.SelectedIndexChanged += CmbType_SelectedIndexChanged;
            chkShowDecimal.CheckedChanged += ChkShowDecimal_CheckedChanged;
            chkAliasMode.CheckedChanged += ChkAliasMode_CheckedChanged;
            chkCollapseChains.CheckedChanged += ChkCollapseChains_CheckedChanged;

            conditionGrid.ContextMenuStrip.Opening += ConditionGrid_ContextMenuStrip_Opening;
            toolsContextMenu.Opening += ToolsContextMenu_Opening;
        }

        private void InitializeGrids()
        {
            // Setup columns manually to ensure correct naming for GridManager references
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

            groupsList.SelectedIndexChanged += GroupsList_SelectedIndexChanged;
            groupsList.Resize += (s, e) =>
            {
                if (groupsList.Columns.Count > 0)
                    groupsList.Columns[0].Width = groupsList.ClientSize.Width - 2;
            };
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

        public void SetNotes(List<CodeNote> notes)
        {
            _rawNotes = notes ?? new List<CodeNote>(); // Store for Multi-Region
            _notesLookup.Clear();
            _gridManager.RangeCache.Clear();
            if (notes == null) return;

            // Instantiate Parser locally (Step 2: Interface Extraction)
            // This helper is used purely for caching note trees for tooltips/aliases
            var parser = new PointerTreeParser();

            foreach (var note in notes)
            {
                string key = LogicFormatter.NormalizeAddress(note.Address);
                var nodes = parser.ParseNoteText(note.Note);
                _notesLookup[key] = nodes;

                // Pre-cache struct ranges for faster lookup
                if (nodes.Count > 0)
                {
                    var root = nodes[0];
                    if (root.Size > 1)
                    {
                        if (long.TryParse(key.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long startAddr))
                        {
                            long endAddr = startAddr + root.Size;
                            _gridManager.RangeCache.Add((startAddr, endAddr, root));
                        }
                    }
                }
            }
            _gridManager.NotesLookup = _notesLookup;

            // Pass references to ViewModel so validation can use them
            _viewModel.NotesLookup = _notesLookup;
            _viewModel.RangeCache = _gridManager.RangeCache;

            RefreshCurrentGridDisplay();
        }

        #region ViewModel Events

        private void ViewModel_SegmentsChanged(object? sender, EventArgs e)
        {
            // Re-populate the Segment Selector (ListView for Leaderboards)
            // This handles hiding/showing the "Start/Cancel/Submit" list if switching between Achievement/Leaderboard
            lvLeaderboardComponents.Items.Clear();
            var segments = _viewModel.AssetStrategy.GetSegmentNames();

            if (segments.Length > 1)
            {
                foreach (var seg in segments)
                    lvLeaderboardComponents.Items.Add(new ListViewItem(seg));

                lvLeaderboardComponents.Visible = true;
                if (lvLeaderboardComponents.Items.Count > 0)
                {
                    _isProgrammaticallyChanging = true;
                    lvLeaderboardComponents.Items[0].Selected = true;
                    _isProgrammaticallyChanging = false;
                }

                // Wire up selection event if not already
                lvLeaderboardComponents.SelectedIndexChanged -= LvLeaderboardComponents_SelectedIndexChanged;
                lvLeaderboardComponents.SelectedIndexChanged += LvLeaderboardComponents_SelectedIndexChanged;
            }
            else
            {
                lvLeaderboardComponents.Visible = false;
            }

            RefreshGroupsListUI();
        }

        private void LvLeaderboardComponents_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_isProgrammaticallyChanging) return;
            if (lvLeaderboardComponents.SelectedItems.Count > 0)
            {
                string seg = lvLeaderboardComponents.SelectedItems[0].Text;
                _viewModel.SelectSegment(seg);
                RefreshGroupsListUI();
            }
        }

        private void ViewModel_LogicChanged(object? sender, EventArgs e)
        {
            // Logic string has updated (via undo/redo or external edit)
            TriggerTextChanged?.Invoke(this, EventArgs.Empty);

            _isProgrammaticallyChanging = true;
            RefreshGroupsListUI();
            _isProgrammaticallyChanging = false;

            RunValidation();
        }

        private void GridManager_LogicChanged(object? sender, EventArgs e)
        {
            if (!this.IsHandleCreated) return;
            this.BeginInvoke(new Action(() =>
            {
                // Grid logic changed visually -> Commit to ViewModel
                // NOTE: We do NOT call PushUndoState here because UpdateLogic(true) handles it.
                // This prevents the "double undo" bug.
                _viewModel.UpdateLogic(true);
                RunValidation();
            }));
        }

        #endregion

        // Loads an asset into the editor
        public async void BindAchievement(IAssetItem asset, string setType = "Core")
        {
            // ViewModel handles strategy and parsing
            _viewModel.LoadAsset(asset);

            txtTitle.Text = asset.Title;
            txtDescription.Text = asset.Description;

            if (asset is RaAchievement cheevo)
            {
                SetupAchievementUI(cheevo);
                pbBadge.Image = await ImageCache.GetBadgeAsync(cheevo.BadgeName);
            }
            else if (asset is RaLeaderboard lb)
            {
                SetupLeaderboardUI();
            }

            if (asset.State == AssetState.Local)
            {
                lblAssetState.Text = "Local";
                lblAssetState.ForeColor = Color.Green;
            }
            else
            {
                lblAssetState.Text = setType;
                lblAssetState.ForeColor = Color.DimGray;
            }

            btnReset.Visible = asset.State != AssetState.Local;
            SetStatus("Asset loaded.");
            RunValidation();
        }

        private void SetupAchievementUI(RaAchievement cheevo)
        {
            cmbPoints.Visible = true;
            cmbType.Visible = true;
            lblAuthor.Visible = true;
            lblAssetState.Visible = true;
            pbBadge.Visible = true;

            txtTitle.Left = 75;
            txtTitle.Width = panelHeader.Width - 75 - 10;
            txtDescription.Left = 75;
            txtDescription.Width = panelHeader.Width - 75 - 10;

            lblAuthor.Location = new Point(232, 63);
            lblAssetState.Location = new Point(330, 63);

            cmbPoints.Text = cheevo.Points.ToString();
            lblAuthor.Text = $"Author: {cheevo.Author}";

            string displayType = cheevo.Type switch
            {
                "progression" => "Progression",
                "win_condition" => "Win",
                "missable" => "Missable",
                _ => ""
            };

            if (!string.IsNullOrEmpty(displayType)) cmbType.SelectedItem = displayType;
            else cmbType.Text = displayType;
        }

        private void SetupLeaderboardUI()
        {
            cmbPoints.Visible = false;
            cmbType.Visible = false;
            pbBadge.Visible = false;
            lblAuthor.Visible = true;
            lblAssetState.Visible = true;

            int textLeft = 110; // Approx width of LV + margin
            txtTitle.Left = textLeft;
            txtTitle.Width = panelHeader.Width - textLeft - 10;
            txtDescription.Left = textLeft;
            txtDescription.Width = panelHeader.Width - textLeft - 10;

            lblAuthor.Location = new Point(textLeft, 63);
            lblAuthor.Text = "Author: Unknown";
            lblAssetState.Location = new Point(textLeft + 150, 63);
        }

        public void Clear()
        {
            _viewModel.LoadAsset(new RaAchievement()); // Dummy clear
            txtTitle.Text = "";
            txtDescription.Text = "";
            cmbPoints.SelectedIndex = -1;
            cmbType.SelectedIndex = -1;
            lblAuthor.Text = "";
            pbBadge.Image = null;
            groupsList.Items.Clear();
            conditionGrid.Rows.Clear();
            _currentIssues.Clear();

            cmbPoints.Visible = true;
            cmbType.Visible = true;
            lblAuthor.Visible = true;
            lblAssetState.Visible = false;
            pbBadge.Visible = true;
            btnReset.Visible = false;
            SetStatus("Ready");
            ValidationCompleted?.Invoke(this, new List<ValidationIssue>());
        }

        public void Undo() { _viewModel.Undo(); SetStatus("Undone."); }
        public void Redo() { _viewModel.Redo(); SetStatus("Redone."); }

        private void RefreshGroupsListUI()
        {
            int savedIndex = groupsList.SelectedIndices.Count > 0 ? groupsList.SelectedIndices[0] : 0;
            var activeGroups = _viewModel.ActiveGroups;
            var generatorRules = _viewModel.GeneratorRules;

            groupsList.BeginUpdate();
            groupsList.Items.Clear();

            HashSet<int> generatedIndices = new HashSet<int>();
            foreach (var kvp in generatorRules)
            {
                // If it's an Array type, it modifies the group in place, it doesn't create new Alts
                // So we only skip indices for Normal mode
                if (!kvp.Value.IsArray)
                {
                    for (int i = 1; i <= kvp.Value.Copies; i++)
                        generatedIndices.Add(kvp.Key + i);
                }
            }

            for (int i = 0; i < activeGroups.Count; i++)
            {
                var item = new ListViewItem(activeGroups[i].GroupName);
                if (generatedIndices.Contains(i))
                {
                    item.ForeColor = Color.Gray;
                    item.Font = new Font(groupsList.Font, FontStyle.Italic);
                    item.Text += " (Auto)";
                }
                else if (generatorRules.ContainsKey(i))
                {
                    item.ForeColor = Color.Blue;
                    item.Font = new Font(groupsList.Font, FontStyle.Bold);
                    item.Text += " *";

                    // Indicate Array mode in list
                    if (generatorRules[i].IsArray)
                        item.Text += " [Array]";
                }
                groupsList.Items.Add(item);
            }
            groupsList.EndUpdate();

            if (groupsList.Items.Count > 0)
            {
                if (savedIndex >= groupsList.Items.Count) savedIndex = groupsList.Items.Count - 1;

                bool wasChanging = _isProgrammaticallyChanging;
                _isProgrammaticallyChanging = true;
                groupsList.Items[savedIndex].Selected = true;

                // Sync grid tag
                var group = activeGroups[savedIndex];
                bool groupChanged = (conditionGrid.Tag != group);
                conditionGrid.Tag = group;

                if (wasChanging || groupChanged)
                {
                    RefreshCurrentGridDisplay();
                }
                _isProgrammaticallyChanging = wasChanging;
            }
            else
            {
                conditionGrid.Rows.Clear();
            }
        }

        private void RefreshCurrentGridDisplay()
        {
            if (conditionGrid.Tag is AchievementConditionGroup group)
            {
                int savedScroll = conditionGrid.FirstDisplayedScrollingRowIndex;
                var selectedIndices = conditionGrid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Index).ToList();
                int focusedRow = conditionGrid.CurrentCell?.RowIndex ?? -1;
                int focusedCol = conditionGrid.CurrentCell?.ColumnIndex ?? 0;

                int index = _viewModel.ActiveGroups.IndexOf(group);
                bool isGenerated = false;

                foreach (var kvp in _viewModel.GeneratorRules)
                {
                    if (!kvp.Value.IsArray && index > kvp.Key && index <= kvp.Key + kvp.Value.Copies)
                    {
                        isGenerated = true;
                        break;
                    }
                }

                // Use Renderer
                ConditionGridRenderer.PopulateGridRows(
                    conditionGrid,
                    group,
                    isGenerated,
                    _showDecimal,
                    _showAliases,
                    _notesLookup,
                    _gridManager.RangeCache,
                    _currentIssues,
                    _collapseChains); // Pass collapse flag

                if (conditionGrid.Rows.Count > 0)
                {
                    if (focusedRow >= 0 && focusedRow < conditionGrid.Rows.Count)
                        try { conditionGrid.CurrentCell = conditionGrid.Rows[focusedRow].Cells[focusedCol]; } catch { }

                    conditionGrid.ClearSelection();
                    // Only restore selections if they are still visible.
                    // Since indices shift when rows are hidden, index-based restoration is unsafe.
                    // GridManager handles robust selection restoration, so we rely on it if possible,
                    // or do basic index restoration here if safe.
                    foreach (int idx in selectedIndices)
                        if (idx >= 0 && idx < conditionGrid.Rows.Count) conditionGrid.Rows[idx].Selected = true;

                    if (savedScroll >= 0 && savedScroll < conditionGrid.Rows.Count)
                        try { conditionGrid.FirstDisplayedScrollingRowIndex = savedScroll; } catch { }
                }
            }
        }

        private void RunValidation()
        {
            // Use the ViewModel to run validation (which uses the injected service)
            _currentIssues = _viewModel.ValidateCurrentLogic();

            ValidationCompleted?.Invoke(this, _currentIssues);

            if (_currentIssues.Any(i => i.Severity >= FeedbackSeverity.Error)) SetStatus("❌ Error(s) found in logic.");
            else if (_currentIssues.Any(i => i.Severity >= FeedbackSeverity.Warning)) SetStatus("⚠️ Warning(s) found in logic.");
            else SetStatus("Ready");

            RefreshCurrentGridDisplay();
        }

        // -- Event Handlers --

        private void GroupsList_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_isProgrammaticallyChanging || groupsList.SelectedItems.Count == 0) return;
            int index = groupsList.SelectedItems[0].Index;
            var groups = _viewModel.ActiveGroups;
            if (index >= 0 && index < groups.Count)
            {
                conditionGrid.Tag = groups[index];
                RefreshCurrentGridDisplay();
            }
        }

        private void btnReset_Click(object? sender, EventArgs e) => AssetResetRequested?.Invoke(this, EventArgs.Empty);
        private void btnAddNote_Click(object? sender, EventArgs e) => AddFromCodeNoteRequested?.Invoke(this, EventArgs.Empty);
        private void btnCopyDef_Click(object? sender, EventArgs e) { if (_viewModel.BoundAsset != null) Clipboard.SetText(_viewModel.BoundAsset.Logic); SetStatus("Full definition copied."); }

        private void TxtTitle_TextChanged(object? sender, EventArgs e) { if (_viewModel.BoundAsset != null) { _viewModel.BoundAsset.Title = txtTitle.Text; AchievementDataChanged?.Invoke(this, EventArgs.Empty); } }
        private void TxtDescription_TextChanged(object? sender, EventArgs e) { if (_viewModel.BoundAsset != null) { _viewModel.BoundAsset.Description = txtDescription.Text; AchievementDataChanged?.Invoke(this, EventArgs.Empty); } }
        private void CmbPoints_Changed(object? sender, EventArgs e) { if (_viewModel.BoundAsset is RaAchievement r && int.TryParse(cmbPoints.Text, out int p)) { r.Points = p; AchievementDataChanged?.Invoke(this, EventArgs.Empty); } }

        private void ChkShowDecimal_CheckedChanged(object? sender, EventArgs e) { _showDecimal = chkShowDecimal.Checked; _gridManager.ShowDecimal = _showDecimal; RefreshCurrentGridDisplay(); }
        private void ChkAliasMode_CheckedChanged(object? sender, EventArgs e) { _showAliases = chkAliasMode.Checked; _gridManager.ShowAliases = _showAliases; RefreshCurrentGridDisplay(); }

        private void ChkCollapseChains_CheckedChanged(object? sender, EventArgs e)
        {
            _collapseChains = chkCollapseChains.Checked;
            _gridManager.CollapseChains = _collapseChains; // Notify manager so it knows view is filtered
            RefreshCurrentGridDisplay();
        }

        private void CmbType_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_viewModel.BoundAsset is RaAchievement cheevo)
            {
                cheevo.Type = cmbType.SelectedItem?.ToString() switch
                {
                    "Progression" => "progression",
                    "Win" => "win_condition",
                    "Missable" => "missable",
                    _ => ""
                };
                AchievementDataChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ConditionGrid_ContextMenuStrip_Opening(object? sender, CancelEventArgs e)
        {
            bool any = conditionGrid.SelectedRows.Count > 0;
            moveUpToolStripMenuItem.Enabled = any;
            moveDownToolStripMenuItem.Enabled = any;
            deleteToolStripMenuItem.Enabled = any;
            duplicateToolStripMenuItem.Enabled = any;
            copyRowToolStripMenuItem.Enabled = any;
            copyLogicToolStripMenuItem.Enabled = any;
            pasteToolStripMenuItem.Enabled = Clipboard.ContainsText();
            saveAsSnippetToolStripMenuItem.Enabled = any;
        }

        private void ToolsContextMenu_Opening(object? sender, CancelEventArgs e)
        {
            createGeneratorToolStripMenuItem.Enabled = _viewModel.ActiveGroups.Count > 0;
            bool canBake = false;
            if (conditionGrid.Tag is AchievementConditionGroup group)
            {
                int index = _viewModel.ActiveGroups.IndexOf(group);
                if (index != -1 && _viewModel.GeneratorRules.ContainsKey(index)) canBake = true;
            }
            commitGeneratorToolStripMenuItem.Enabled = canBake;

            // Enable Multi-Region if active groups exist and notes are loaded
            multiRegionToolStripMenuItem.Enabled = _viewModel.ActiveGroups.Count > 0 && _rawNotes.Count > 0;
        }

        // Context Menu Actions - Delegated to ViewModel or GridManager
        private void moveUpToolStripMenuItem_Click(object? sender, EventArgs e) { _viewModel.PushUndoState(); _gridManager.MoveSelected(-1); }
        private void moveDownToolStripMenuItem_Click(object? sender, EventArgs e) { _viewModel.PushUndoState(); _gridManager.MoveSelected(1); }
        private void duplicateToolStripMenuItem_Click(object? sender, EventArgs e) { _viewModel.PushUndoState(); _gridManager.DuplicateSelected(); }
        private void deleteToolStripMenuItem_Click(object? sender, EventArgs e) { _viewModel.PushUndoState(); _gridManager.DeleteSelected(); }
        private void addConditionToolStripMenuItem_Click(object? sender, EventArgs e) { _viewModel.PushUndoState(); _gridManager.AddCondition(); }

        private void copyRowToolStripMenuItem_Click(object? sender, EventArgs e) => _gridManager.CopySelectedRows();
        private void copyLogicToolStripMenuItem_Click(object? sender, EventArgs e) => _gridManager.CopySelectedLogic();
        private void pasteToolStripMenuItem_Click(object? sender, EventArgs e) { if (Clipboard.ContainsText()) GridManager_PasteRequested(sender, Clipboard.GetText()); }

        private void addGroupToolStripMenuItem_Click(object? sender, EventArgs e) => _viewModel.AddGroup();

        private void removeGroupToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            if (groupsList.SelectedItems.Count == 0) return;
            _viewModel.RemoveGroup(groupsList.SelectedItems[0].Index);
        }

        private void removeAllAltGroupsToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            if (MessageBox.Show("Remove all Alt groups?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
                _viewModel.RemoveAllAltGroups();
        }

        // Restored Generator Logic (Tree Building)
        // This logic constructs the tree view model for the generator dialog based on the current group's conditions.
        private void createGeneratorToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            var group = conditionGrid.Tag as AchievementConditionGroup;
            if (group == null) return;

            int groupIndex = _viewModel.ActiveGroups.IndexOf(group);
            if (groupIndex == -1) return;

            var rootItems = new List<GeneratorConfigForm.GeneratorItemViewModel>();
            int autoStride = 0;
            int detectedCopies = 1;
            Dictionary<long, int> sizeCounts = new Dictionary<long, int>();

            GeneratorConfigForm.GeneratorItemViewModel? activeChainParent = null;
            NoteNode? activeNoteNode = null;

            // Helper to build node hierarchy
            // Re-added _gridManager.RangeCache usage here for lookup fallback
            (GeneratorConfigForm.GeneratorItemViewModel Item, NoteNode? FoundNode) GetOrCreateNode(string addr, bool isPointerBase, GeneratorConfigForm.GeneratorItemViewModel? parent, NoteNode? contextNode)
            {
                string normAddr = LogicFormatter.NormalizeAddress(addr);
                string desc = "";
                NoteNode? currentNote = null;
                bool foundAsOffset = false;

                // 1. Try Offset Context Lookup
                if (contextNode != null)
                {
                    long offsetVal = long.TryParse(normAddr.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long v) ? v : -1;
                    if (offsetVal != -1)
                    {
                        foreach (var n in contextNode.Children)
                        {
                            long nodeOff = long.TryParse(n.Offset.Replace("0x", "").Replace("+", "").Replace("-", "").Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long no) ? no : -99;
                            if (n.Offset.Contains("-")) nodeOff = -nodeOff;

                            if (nodeOff != -99 && offsetVal >= nodeOff && offsetVal < nodeOff + n.Size)
                            {
                                desc = n.Description;
                                currentNote = n;
                                foundAsOffset = true;
                                break;
                            }
                        }
                    }
                }

                // 2. Try Base Lookup
                if (!foundAsOffset && (isPointerBase || parent == null))
                {
                    if (_notesLookup.TryGetValue(normAddr, out var nodes))
                    {
                        var root = nodes.FirstOrDefault();
                        if (root != null)
                        {
                            desc = root.Description;
                            if (string.IsNullOrEmpty(desc) || desc.Equals("Full Note", StringComparison.OrdinalIgnoreCase))
                                desc = root.Content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";

                            if (autoStride == 0 && root.Size > 1) autoStride = (int)root.Size;
                            if (root.Size > 1) { if (!sizeCounts.ContainsKey(root.Size)) sizeCounts[root.Size] = 0; sizeCounts[root.Size]++; }

                            var syntheticContext = new NoteNode();
                            foreach (var n in nodes)
                            {
                                if (n.IndentLevel != -2 && (n.Parent == null || n.Parent.IndentLevel == -1))
                                    syntheticContext.Children.Add(n);
                            }
                            currentNote = syntheticContext;
                        }
                    }
                    // 2a. Try Range Cache Lookup (FIX: Re-added to support struct ranges like [176 bytes])
                    else if (_gridManager.RangeCache != null)
                    {
                        long targetVal = long.TryParse(normAddr.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long tv) ? tv : -1;
                        if (targetVal != -1)
                        {
                            var match = _gridManager.RangeCache.FirstOrDefault(r => targetVal >= r.Start && targetVal < r.End);
                            if (match.Node != null)
                            {
                                desc = match.Node.Description;
                                if (string.IsNullOrEmpty(desc) || desc.Equals("Full Note")) desc = match.Node.Content.Split('\n')[0];

                                // Auto-detect stride from range size
                                if (autoStride == 0 && match.Node.Size > 1) autoStride = (int)match.Node.Size;
                                if (match.Node.Size > 1)
                                {
                                    if (!sizeCounts.ContainsKey(match.Node.Size)) sizeCounts[match.Node.Size] = 0;
                                    sizeCounts[match.Node.Size]++;
                                }
                            }
                        }
                    }
                }

                var item = new GeneratorConfigForm.GeneratorItemViewModel
                {
                    Address = addr.StartsWith("0x") ? addr : "0x" + addr,
                    Description = desc
                };

                if (parent != null) parent.Children.Add(item);
                else rootItems.Add(item);

                return (item, currentNote);
            }

            // Build Tree from Conditions
            foreach (var cond in group.Conditions)
            {
                if (cond.Flag == "Add Address")
                {
                    if (RaLogicSyntax.MemoryTypes.Contains(cond.LeftOperand.Type))
                    {
                        var val = cond.LeftOperand.Value;
                        var result = GetOrCreateNode(val, isPointerBase: true, parent: activeChainParent, contextNode: activeNoteNode);
                        if (activeChainParent == null) activeChainParent = result.Item;
                        activeNoteNode = result.FoundNode;
                    }
                }
                else
                {
                    if (RaLogicSyntax.MemoryTypes.Contains(cond.LeftOperand.Type))
                        GetOrCreateNode(cond.LeftOperand.Value, isPointerBase: false, parent: activeChainParent, contextNode: activeNoteNode);
                    if (RaLogicSyntax.MemoryTypes.Contains(cond.RightOperand.Type))
                        GetOrCreateNode(cond.RightOperand.Value, isPointerBase: false, parent: null, contextNode: null);

                    activeChainParent = null;
                    activeNoteNode = null;
                }
            }

            ApplySmartPadding(rootItems);

            // Stride Heuristic
            if (autoStride > 0)
            {
                int count = _gridManager.RangeCache.Count(x => x.Node.Size == autoStride);
                if (count > 1) detectedCopies = count - 1;
            }

            if (rootItems.Count == 0)
            {
                MessageBox.Show("No memory addresses found in this group to generate from.", "Generator", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            GeneratorDefinition? existingDef;
            _viewModel.GeneratorRules.TryGetValue(groupIndex, out existingDef);

            if (existingDef == null && detectedCopies > 1)
                existingDef = new GeneratorDefinition { Copies = detectedCopies };

            using var form = new GeneratorConfigForm(rootItems, autoStride, existingDef);

            if (form.ShowDialog() == DialogResult.OK)
            {
                _viewModel.PushUndoState();

                // If NOT an Array generator, confirm overwrite of existing Alts
                if (!form.IsArray && groupIndex < _viewModel.ActiveGroups.Count - 1)
                {
                    var result = MessageBox.Show(
                        "There are existing Alt groups after this group. Do you want to delete them to make room for the generator?",
                        "Overwrite Confirmation",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Warning);

                    if (result == DialogResult.Cancel) return;

                    if (result == DialogResult.Yes)
                    {
                        // Manual list modification is safe here because UpdateLogic will re-sync everything
                        while (_viewModel.ActiveGroups.Count > groupIndex + 1)
                        {
                            _viewModel.ActiveGroups.RemoveAt(_viewModel.ActiveGroups.Count - 1);
                        }
                        // Clean rules for deleted groups
                        var keysToRemove = _viewModel.GeneratorRules.Keys.Where(k => k > groupIndex).ToList();
                        foreach (var k in keysToRemove) _viewModel.GeneratorRules.Remove(k);
                    }
                }

                if (!_viewModel.GeneratorRules.ContainsKey(groupIndex))
                    _viewModel.GeneratorRules[groupIndex] = new GeneratorDefinition();

                var def = _viewModel.GeneratorRules[groupIndex];
                def.Copies = form.Copies;
                def.IsArray = form.IsArray;

                // IMPORTANT: If we are in Array mode, we treat the CURRENT conditions in the group
                // as the "Template". We save this count so future updates know how much to keep vs clear.
                if (def.IsArray)
                {
                    def.TemplateConditionCount = group.Conditions.Count;
                }

                def.Rules.Clear();

                foreach (string addr in form.SelectedAddresses)
                {
                    def.Rules.Add(new GeneratorRule
                    {
                        SourceAddress = addr,
                        Stride = form.Stride
                    });
                }

                _viewModel.UpdateLogic(false);
                RefreshGroupsListUI();
            }
        }

        private void ApplySmartPadding(List<GeneratorConfigForm.GeneratorItemViewModel> roots)
        {
            foreach (var root in roots)
            {
                if (root.Children.Count == 0) continue;
                int maxLen = 0;
                foreach (var child in root.Children)
                {
                    string hex = child.Address.StartsWith("0x") ? child.Address.Substring(2) : child.Address;
                    if (hex.Length > maxLen) maxLen = hex.Length;
                }
                if (maxLen > 1)
                {
                    foreach (var child in root.Children)
                    {
                        string hex = child.Address.StartsWith("0x") ? child.Address.Substring(2) : child.Address;
                        child.Address = "0x" + hex.PadLeft(maxLen, '0');
                    }
                }
            }
        }

        private void commitGeneratorToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            var group = conditionGrid.Tag as AchievementConditionGroup;
            if (group == null) return;
            int index = _viewModel.ActiveGroups.IndexOf(group);
            if (index != -1 && _viewModel.GeneratorRules.ContainsKey(index))
            {
                _viewModel.PushUndoState();
                _viewModel.GeneratorRules.Remove(index);
                // Renaming alts
                if (!_viewModel.AssetStrategy.IsLeaderboard)
                {
                    for (int i = 1; i < _viewModel.ActiveGroups.Count; i++)
                        _viewModel.ActiveGroups[i].GroupName = $"Alt {i}";
                }
                _viewModel.UpdateLogic(false);
                SetStatus("Generator baked.");
            }
        }

        private void btnTools_Click(object? sender, EventArgs e) => toolsContextMenu.Show(btnTools, 0, btnTools.Height);

        private void saveAsSnippetToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            _gridManager.CopySelectedLogic();
        }

        private void insertSnippetToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            using (var form = new SnippetSelectForm()) { if (form.ShowDialog() == DialogResult.OK) GridManager_PasteRequested(sender, form.SelectedSnippet.LogicCode); }
        }

        private void GridManager_PasteRequested(object? sender, string clipboardText)
        {
            // Smart Paste Logic
            if (char.IsDigit(clipboardText[0]) && clipboardText.Contains(":\"") && clipboardText.Count(c => c == ':') > 5)
            {
                if (MessageBox.Show("Apply Smart Paste?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    ParseAndApplySmartPaste(clipboardText);
                    return;
                }
            }
            _viewModel.PushUndoState();
            _gridManager.PasteLogic();
            _viewModel.UpdateLogic(false);
        }

        private void ParseAndApplySmartPaste(string raw)
        {
            try
            {
                // ID:"Logic":Title:Desc...
                int firstColon = raw.IndexOf(':');
                if (firstColon == -1) return;

                string content = raw.Substring(firstColon + 1);
                var parts = new List<string>();

                bool inQuote = false;
                int start = 0;
                for (int i = 0; i < content.Length; i++)
                {
                    if (content[i] == '"') inQuote = !inQuote;
                    else if (content[i] == ':' && !inQuote)
                    {
                        string val = content.Substring(start, i - start);
                        if (val.StartsWith("\"") && val.EndsWith("\"")) val = val.Substring(1, val.Length - 2).Replace("\\\"", "\"");
                        parts.Add(val);
                        start = i + 1;
                    }
                }
                if (start <= content.Length)
                {
                    string val = content.Substring(start);
                    if (val.StartsWith("\"") && val.EndsWith("\"")) val = val.Substring(1, val.Length - 2).Replace("\\\"", "\"");
                    parts.Add(val);
                }

                if (parts.Count >= 3)
                {
                    this.TriggerText = parts[0];
                    txtTitle.Text = parts[1];
                    txtDescription.Text = parts[2];

                    if (parts.Count > 5 && !string.IsNullOrEmpty(parts[5]))
                    {
                        string t = parts[5].ToLower();
                        if (t.Contains("prog")) cmbType.Text = "Progression";
                        else if (t.Contains("win")) cmbType.Text = "Win";
                        else if (t.Contains("miss")) cmbType.Text = "Missable";
                        else cmbType.SelectedItem = null;
                    }

                    if (parts.Count > 7 && int.TryParse(parts[7], out int pts))
                    {
                        cmbPoints.Text = pts.ToString();
                    }
                    SetStatus("Smart Paste applied successfully.");
                }
            }
            catch
            {
                MessageBox.Show("Failed to parse smart paste data.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void AddConditionFromNote(string logicString, string type, string size)
        {
            if (string.IsNullOrEmpty(logicString)) return;

            _viewModel.PushUndoState();

            // Parse the full logic chain from the note generator
            var groups = AchievementParser.ParseAchievementTrigger(logicString, 'S', false);

            if (groups.Count > 0 && groups[0].Conditions.Count > 0)
            {
                var conditions = groups[0].Conditions;

                // 1. Fix up intermediate pointer sizes
                foreach (var cond in conditions)
                {
                    if (cond.Flag == "Add Address" && string.IsNullOrEmpty(cond.LeftOperand.Size))
                        cond.LeftOperand.Size = "32-bit";
                }

                // 2. Configure the final condition (the actual variable)
                var lastCond = conditions.Last();
                if (lastCond.LeftOperand.Type == "Mem")
                {
                    lastCond.LeftOperand.Type = type;
                    lastCond.LeftOperand.Size = ValidateMemorySize(size);
                }

                // 3. Ensure a default comparison exists
                if (string.IsNullOrEmpty(lastCond.Operator))
                {
                    lastCond.Operator = "=";
                    lastCond.RightOperand = new Operand { Type = "Value", Value = type == "Float" ? "0.0" : "0" };
                }

                // 4. Insert into active group
                if (conditionGrid.Tag is AchievementConditionGroup activeGroup)
                {
                    int insertIndex = activeGroup.Conditions.Count;
                    if (conditionGrid.SelectedRows.Count > 0)
                    {
                        // Robust insert position: Use the last selected row to determine where to insert.
                        // If filtered, grid.SelectedRows contains rows. We must get their model object to find true index.
                        // BUT, for simple addition, finding the model index of the last selected row is safe.
                        var lastRow = conditionGrid.SelectedRows.Cast<DataGridViewRow>().OrderByDescending(r => r.Index).First();
                        if (lastRow.Tag is AchievementCondition c)
                        {
                            insertIndex = activeGroup.Conditions.IndexOf(c) + 1;
                        }
                    }

                    activeGroup.Conditions.InsertRange(insertIndex, conditions);

                    _viewModel.UpdateLogic(false); // Logic updated
                    RefreshCurrentGridDisplay();

                    // Restore selection to the new items
                    // We need to find the new items in the grid.
                    // Since we just inserted them, and Refresh repopulates grid...
                    // We can iterate grid rows and look for the inserted objects.
                    // This is robust even if some are hidden (though new items shouldn't be AddAddress usually unless pasted)
                    // Note: AddConditionFromNote adds AddAddress chains potentially.
                    // If collapsed, only the leaf is shown. We should select the leaf.

                    var insertedSet = new HashSet<AchievementCondition>(conditions);
                    conditionGrid.ClearSelection();
                    bool firstFound = false;
                    foreach (DataGridViewRow row in conditionGrid.Rows)
                    {
                        if (row.Tag is AchievementCondition c && insertedSet.Contains(c))
                        {
                            row.Selected = true;
                            if (!firstFound)
                            {
                                conditionGrid.CurrentCell = row.Cells[0];
                                firstFound = true;
                            }
                        }
                    }
                }
            }
        }

        private string ValidateMemorySize(string inputSize)
        {
            if (string.IsNullOrEmpty(inputSize)) return "8-bit";
            if (inputSize.Equals("Float", StringComparison.OrdinalIgnoreCase)) return "Float";
            return RaLogicSyntax.MemorySizes.Contains(inputSize) ? inputSize : "8-bit";
        }

        public void SelectCondition(AchievementCondition? target)
        {
            if (target == null) return;

            // Find group index
            int groupIndex = -1;

            // We must search _viewModel.ActiveGroups because that's what populate groupsList
            for (int i = 0; i < _viewModel.ActiveGroups.Count; i++)
            {
                var group = _viewModel.ActiveGroups[i];
                if (group.Conditions.Contains(target))
                {
                    groupIndex = i;
                    break;
                }
            }

            // Fallback: match by ID + properties if object reference differs
            if (groupIndex == -1)
            {
                for (int i = 0; i < _viewModel.ActiveGroups.Count; i++)
                {
                    var group = _viewModel.ActiveGroups[i];
                    for (int j = 0; j < group.Conditions.Count; j++)
                    {
                        var c = group.Conditions[j];
                        if (c.ID == target.ID && c.Flag == target.Flag &&
                            c.LeftOperand.Value == target.LeftOperand.Value)
                        {
                            groupIndex = i;
                            // Update target ref so we can find it in grid later
                            target = c;
                            break;
                        }
                    }
                    if (groupIndex != -1) break;
                }
            }

            if (groupIndex != -1)
            {
                // 1. Select group in list (triggers grid refresh via event)
                if (groupIndex >= 0 && groupIndex < groupsList.Items.Count)
                {
                    bool wasChanging = _isProgrammaticallyChanging;
                    _isProgrammaticallyChanging = true;

                    if (!groupsList.Items[groupIndex].Selected)
                    {
                        groupsList.Items[groupIndex].Selected = true;
                        conditionGrid.Tag = _viewModel.ActiveGroups[groupIndex];
                        RefreshCurrentGridDisplay();
                    }
                    else
                    {
                        conditionGrid.Tag = _viewModel.ActiveGroups[groupIndex];
                        RefreshCurrentGridDisplay();
                    }

                    groupsList.EnsureVisible(groupIndex);
                    _isProgrammaticallyChanging = wasChanging;
                }

                // 2. Highlight specific row by TAG match
                // Iterate rows to find the one holding the target object.
                // This works regardless of hidden rows (if hidden, it won't be selected, which is correct behavior).
                foreach (DataGridViewRow row in conditionGrid.Rows)
                {
                    if (row.Tag == target)
                    {
                        conditionGrid.ClearSelection();
                        row.Selected = true;
                        if (row.Index >= 0)
                        {
                            conditionGrid.FirstDisplayedScrollingRowIndex = row.Index;
                            conditionGrid.CurrentCell = row.Cells[0];
                        }
                        break;
                    }
                }
            }
        }

        // NEW: Event Handler for the "Explain Logic" tool menu item
        private void explainLogicToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            if (_viewModel.BoundAsset == null) return;

            var form = new LogicExplainerForm();
            var allGroups = new List<AchievementConditionGroup>();

            // Gather all groups from all segments to show the full logic
            foreach (var segName in _viewModel.AssetStrategy.GetSegmentNames())
            {
                if (!_viewModel.AssetStrategy.IsLeaderboard)
                {
                    allGroups.AddRange(_viewModel.ActiveGroups);
                    break;
                }
                else
                {
                    // For Leaderboards, showing only the active tab (e.g. Start) is safer for now
                    allGroups.AddRange(_viewModel.ActiveGroups);
                    break;
                }
            }

            form.DisplayLogic(_viewModel.BoundAsset, allGroups, _notesLookup, _gridManager.RangeCache);
            form.Show();
        }

        // NEW: Multi-Region Support Menu Handler (Passes logic to MainForm)
        private void multiRegionToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            using (var form = new MultiRegionForm(_rawNotes))
            {
                if (form.ShowDialog() != DialogResult.OK) return;

                // Fire event to Main Form to handle batch processing on all assets
                MultiRegionLogicRequested?.Invoke(this, form);
            }
        }
    }
}