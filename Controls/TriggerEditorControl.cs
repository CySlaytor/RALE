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
        // Handles all DataGridView interactions and display logic
        private readonly ConditionGridManager _gridManager;
        // Handles local Undo/Redo. Changed from string to TriggerEditorState to track Generator Rules
        private readonly HistoryManager<TriggerEditorState> _history = new();

        // -- State --
        private bool _isProgrammaticallyChanging; // Lock to prevent event loops
        private string _triggerText = ""; // The current raw logic string
        private bool _isLeaderboardMode;
        private bool _showDecimal = false;
        private bool _showAliases = false;
        private IAssetItem? _boundAsset; // The Asset object currently being edited

        // -- Data Models --
        private List<AchievementConditionGroup> _activeGroups = new();
        // Logic segment map for Leaderboards (Start, Cancel, Submit, Value)
        private Dictionary<string, List<AchievementConditionGroup>> _leaderboardSegments = new();
        // Stores Generator rules (Copies, Strides) keyed by Group Index
        private Dictionary<int, GeneratorDefinition> _generatorRules = new();

        // Current list of validation issues for highlighting
        private List<ValidationIssue> _currentIssues = new();

        // Cache for Code Notes for Tooltips/Aliases
        // Key: Normalized Address "0x1234", Value: Parsed Node Tree
        private Dictionary<string, List<NoteNode>> _notesLookup = new();

        public char GroupSeparator { get; set; } = 'S'; // 'S' for achievements, '::' handled internally for LBs

        // -- Events --
        public event EventHandler? TriggerTextChanged;
        public event EventHandler? AchievementDataChanged;
        public event EventHandler<string>? StatusUpdateRequested;
        public event EventHandler? AssetResetRequested;
        public event EventHandler? AddFromCodeNoteRequested;
        public event EventHandler<string>? AddressSelected;
        public event EventHandler<PointerChainArgs>? PointerChainSelected;

        // New event to broadcast validation results to main form
        public event EventHandler<List<ValidationIssue>>? ValidationCompleted;

        // Public property for the Logic String (Two-way binding)
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string TriggerText
        {
            get => _triggerText;
            set
            {
                if (_triggerText != value && !_history.IsUndoingRedoing)
                {
                    PushUndoState();
                    _triggerText = value;
                    UpdateUIFromText();
                }
                else if (_history.IsUndoingRedoing)
                {
                    _triggerText = value;
                    UpdateUIFromText();
                }
            }
        }

        public TriggerEditorControl()
        {
            InitializeComponent();
            _gridManager = new ConditionGridManager(conditionGrid);
            InitializeEvents();
            InitializeGrids();
            InitializeLeaderboardControls();
            SetStatus("Ready");
        }

        // Helper to capture current full state
        private void PushUndoState()
        {
            var state = new TriggerEditorState
            {
                LogicString = _triggerText,
                GeneratorRules = _generatorRules // Wrapper clones this internally
            };
            _history.PushState(state.Clone());
        }

        // Helper to restore state
        private void RestoreState(TriggerEditorState state)
        {
            _triggerText = state.LogicString;
            _generatorRules = state.Clone().GeneratorRules; // Restore dictionary
        }

        private void InitializeEvents()
        {
            // Bridge Manager events to Control events
            _gridManager.LogicChanged += GridManager_LogicChanged;
            _gridManager.StatusUpdateRequested += (s, msg) => SetStatus(msg);
            _gridManager.AddressSelected += (s, addr) => AddressSelected?.Invoke(this, addr);
            _gridManager.PointerChainSelected += (s, args) => PointerChainSelected?.Invoke(this, args);

            // Hook Paste for Smart Paste Logic
            _gridManager.PasteRequested += GridManager_PasteRequested;

            // Input field wiring
            txtTitle.TextChanged += TxtTitle_TextChanged;
            txtDescription.TextChanged += TxtDescription_TextChanged;
            cmbPoints.SelectedIndexChanged += CmbPoints_Changed;
            cmbPoints.TextChanged += CmbPoints_Changed;
            cmbType.SelectedIndexChanged += CmbType_SelectedIndexChanged;
            chkShowDecimal.CheckedChanged += ChkShowDecimal_CheckedChanged;
            chkAliasMode.CheckedChanged += ChkAliasMode_CheckedChanged;

            conditionGrid.ContextMenuStrip.Opening += ConditionGrid_ContextMenuStrip_Opening;

            // Handle Tools Menu Opening to toggle Bake option
            toolsContextMenu.Opening += ToolsContextMenu_Opening;
        }

        private void InitializeGrids()
        {
            // Setup columns manually to ensure correct naming for GridManager
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

        // Leaderboards use a specific segment selector (Start/Cancel/Submit/Value) instead of points/badges
        private void InitializeLeaderboardControls()
        {
            lvLeaderboardComponents.Items.Clear();
            lvLeaderboardComponents.Items.Add(new ListViewItem("Start"));
            lvLeaderboardComponents.Items.Add(new ListViewItem("Cancel"));
            lvLeaderboardComponents.Items.Add(new ListViewItem("Submit"));
            lvLeaderboardComponents.Items.Add(new ListViewItem("Value"));

            lvLeaderboardComponents.SelectedIndexChanged += (s, e) =>
            {
                if (!_isProgrammaticallyChanging && lvLeaderboardComponents.SelectedItems.Count > 0)
                    RefreshLeaderboardGroupsList();
            };

            if (lvLeaderboardComponents.Items.Count > 0)
                lvLeaderboardComponents.Items[0].Selected = true;
        }

        // Updates the local note cache for Alias/Tooltip lookup
        public void SetNotes(List<CodeNote> notes)
        {
            _notesLookup.Clear();
            // Clear range cache in manager
            _gridManager.RangeCache.Clear();

            if (notes == null) return;

            foreach (var note in notes)
            {
                // Use LogicFormatter to get canonical key (e.g. "0x1234" instead of "0x1234")
                string key = LogicFormatter.NormalizeAddress(note.Address);

                // Parse note for tree structure
                var nodes = PointerTreeParser.ParseNoteText(note.Note);

                // Add to dictionary (latest note wins if duplicate)
                _notesLookup[key] = nodes;

                // Populate Range Cache for struct-like notes (Size > 1)
                // We assume the root node has the total size if parsed correctly
                if (nodes.Count > 0)
                {
                    var root = nodes[0]; // Usually index 0 is root or default
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

            // Push map to Grid Manager for Tooltips
            _gridManager.NotesLookup = _notesLookup;

            // Refresh grid to update aliases if enabled
            RefreshCurrentGridDisplay();
        }

        private void GridManager_LogicChanged(object? sender, EventArgs e)
        {
            if (!this.IsHandleCreated) return;

            // Use BeginInvoke to avoid reentrancy bugs (modifying grid rows during CellEndEdit)
            this.BeginInvoke(new Action(() =>
            {
                PushUndoState();
                BuildTriggerAndNotify();
                RunValidation();
            }));
        }

        private void RunValidation()
        {
            // Run full validation on all groups, passing the bound asset for context
            _currentIssues = LogicValidator.ValidateAsset(_activeGroups, _boundAsset);

            // Broadcast to listeners (e.g. CodeReviewControl)
            ValidationCompleted?.Invoke(this, _currentIssues);

            if (_currentIssues.Any(i => i.Severity >= FeedbackSeverity.Error))
                SetStatus("❌ Error(s) found in logic.");
            else if (_currentIssues.Any(i => i.Severity >= FeedbackSeverity.Warning))
                SetStatus("⚠️ Warning(s) found in logic.");
            else
                SetStatus("Ready");

            // Refresh grid to show highlights
            RefreshCurrentGridDisplay();
        }

        // Public method for external controls (CodeReviewer) to select a specific condition
        public void SelectCondition(AchievementCondition? target)
        {
            if (target == null) return;

            // 1. Find which group contains this condition
            int groupIndex = -1;
            for (int i = 0; i < _activeGroups.Count; i++)
            {
                if (_activeGroups[i].Conditions.Contains(target))
                {
                    groupIndex = i;
                    break;
                }
            }

            if (groupIndex != -1)
            {
                // 2. Switch to that group if not active
                if (groupsList.SelectedIndices.Count == 0 || groupsList.SelectedIndices[0] != groupIndex)
                {
                    if (groupIndex < groupsList.Items.Count)
                    {
                        groupsList.Items[groupIndex].Selected = true;
                        // Ensure the list scrolls to show it
                        groupsList.EnsureVisible(groupIndex);
                    }
                }

                // 3. Find the row in the grid
                foreach (DataGridViewRow row in conditionGrid.Rows)
                {
                    if (row.Tag == target)
                    {
                        conditionGrid.ClearSelection();
                        row.Selected = true;
                        conditionGrid.FirstDisplayedScrollingRowIndex = row.Index;
                        break;
                    }
                }
            }
        }

        private void GridManager_PasteRequested(object? sender, string clipboardText)
        {
            if (string.IsNullOrWhiteSpace(clipboardText)) return;

            // Smart Paste Check: Is this a full achievement string?
            if (char.IsDigit(clipboardText[0]) && clipboardText.Contains(":\"") && clipboardText.Count(c => c == ':') > 5)
            {
                var result = MessageBox.Show("Detected full achievement data in clipboard.\nDo you want to update Title, Points, and Description as well?",
                    "Smart Paste", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

                if (result == DialogResult.Cancel) return;

                if (result == DialogResult.Yes)
                {
                    ParseAndApplySmartPaste(clipboardText);
                    return; // Handled completely
                }
            }

            // Fallback to normal logic paste
            PushUndoState();
            _gridManager.PasteLogic();
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

                // Simple CSV-like parse for colons respecting quotes
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
                    // Apply Logic
                    this.TriggerText = parts[0];

                    // Apply Metadata (suppressing events if needed, but we want propagation)
                    txtTitle.Text = parts[1];
                    txtDescription.Text = parts[2];

                    if (parts.Count > 5 && !string.IsNullOrEmpty(parts[5])) // Type
                    {
                        // Convert raw type to UI
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

        private void NotifyAssetDataChanged(Action<IAssetItem> updateAction)
        {
            if (_boundAsset != null)
            {
                updateAction(_boundAsset);
                AchievementDataChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void SetStatus(string msg)
        {
            if (lblEditorStatus != null)
                lblEditorStatus.Text = msg;
            StatusUpdateRequested?.Invoke(this, msg);
        }

        // Resets the UI to empty state
        public void Clear()
        {
            _boundAsset = null;
            _triggerText = "";
            _generatorRules.Clear();
            txtTitle.Text = "";
            txtDescription.Text = "";
            cmbPoints.SelectedIndex = -1;
            cmbType.SelectedIndex = -1;
            lblAuthor.Text = "";
            pbBadge.Image = null;
            _activeGroups.Clear();
            _leaderboardSegments.Clear();
            groupsList.Items.Clear();
            conditionGrid.Rows.Clear();
            _history.Clear();
            _currentIssues.Clear();

            cmbPoints.Visible = true;
            cmbType.Visible = true;
            lblAuthor.Visible = true;
            lblAssetState.Visible = false;
            pbBadge.Visible = true;
            lvLeaderboardComponents.Visible = false;
            btnReset.Visible = false;
            SetStatus("Ready");

            // Clear reviewer list
            ValidationCompleted?.Invoke(this, new List<ValidationIssue>());
        }

        // Loads an asset into the editor
        public async void BindAchievement(IAssetItem asset, string setType = "Core")
        {
            _boundAsset = asset;
            _isLeaderboardMode = asset.IsLeaderboard;
            _generatorRules.Clear();

            // We DO NOT clear groupsList.SelectedItems here.
            // RefreshGroupsListUI (called by UpdateUIFromText) captures the current selection 
            // and attempts to restore it. This preserves the "Alt 2" selection when switching assets.

            // Directly update backing field and force UI refresh.
            // This ensures the UI updates even if the logic string is identical to the previous asset,
            // and avoids pushing an Undo state for the file load operation.
            _triggerText = asset.Logic;
            UpdateUIFromText();

            _history.Clear();

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

            // Set Asset State Label
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
            lvLeaderboardComponents.Visible = false;

            txtTitle.Left = 75;
            txtTitle.Width = panelHeader.Width - 75 - 10;
            txtDescription.Left = 75;
            txtDescription.Width = panelHeader.Width - 75 - 10;

            // Reset labels positions for Achievement mode
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

            if (!string.IsNullOrEmpty(displayType))
                cmbType.SelectedItem = displayType;
            else
                cmbType.Text = displayType;
        }

        private void SetupLeaderboardUI()
        {
            cmbPoints.Visible = false;
            cmbType.Visible = false;
            pbBadge.Visible = false;

            // Show Author/State now
            lblAuthor.Visible = true;
            lblAssetState.Visible = true;

            lvLeaderboardComponents.Visible = true;

            lvLeaderboardComponents.Location = new Point(5, 5);
            // With wider ListView (80 vs 64), textLeft shifts.
            int textLeft = lvLeaderboardComponents.Right + 10;

            txtTitle.Left = textLeft;
            txtTitle.Width = panelHeader.Width - textLeft - 10;
            txtDescription.Left = textLeft;
            txtDescription.Width = panelHeader.Width - textLeft - 10;

            // Align Author and State for Leaderboard mode
            lblAuthor.Location = new Point(textLeft, 63);
            lblAuthor.Text = "Author: Unknown";
            lblAssetState.Location = new Point(textLeft + 150, 63);

            if (lvLeaderboardComponents.SelectedItems.Count == 0 && lvLeaderboardComponents.Items.Count > 0)
                lvLeaderboardComponents.Items[0].Selected = true;
        }

        public void Undo()
        {
            var current = new TriggerEditorState { LogicString = _triggerText, GeneratorRules = _generatorRules };
            var previous = _history.Undo(current);

            RestoreState(previous);
            UpdateUIFromText();
            TriggerTextChanged?.Invoke(this, EventArgs.Empty);
            SetStatus("Undone.");
        }

        public void Redo()
        {
            var current = new TriggerEditorState { LogicString = _triggerText, GeneratorRules = _generatorRules };
            var next = _history.Redo(current);

            RestoreState(next);
            UpdateUIFromText();
            TriggerTextChanged?.Invoke(this, EventArgs.Empty);
            SetStatus("Redone.");
        }

        // -- UI Event Handlers (Named to match Designer) --

        private void btnReset_Click(object? sender, EventArgs e) => AssetResetRequested?.Invoke(this, EventArgs.Empty);
        private void btnAddNote_Click(object? sender, EventArgs e) => AddFromCodeNoteRequested?.Invoke(this, EventArgs.Empty);

        private void btnCopyDef_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_triggerText)) return;
            Clipboard.SetText(_triggerText);
            SetStatus("Full definition copied to clipboard.");
        }

        private void TxtTitle_TextChanged(object? sender, EventArgs e) => NotifyAssetDataChanged(a => a.Title = txtTitle.Text);
        private void TxtDescription_TextChanged(object? sender, EventArgs e) => NotifyAssetDataChanged(a => a.Description = txtDescription.Text);
        private void CmbPoints_Changed(object? sender, EventArgs e) => NotifyAssetDataChanged(a => { if (a is RaAchievement r && int.TryParse(cmbPoints.Text, out int p)) r.Points = p; });

        private void ChkShowDecimal_CheckedChanged(object? sender, EventArgs e)
        {
            _showDecimal = chkShowDecimal.Checked;
            // Sync manager so edits work correctly
            _gridManager.ShowDecimal = _showDecimal;
            RefreshCurrentGridDisplay();
        }

        private void ChkAliasMode_CheckedChanged(object? sender, EventArgs e)
        {
            _showAliases = chkAliasMode.Checked;
            _gridManager.ShowAliases = _showAliases;
            RefreshCurrentGridDisplay();
        }

        private void CmbType_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_boundAsset is RaAchievement cheevo)
            {
                string display = cmbType.SelectedItem?.ToString() ?? "";
                cheevo.Type = display switch
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
            // Only allow generator if we are editing an asset
            createGeneratorToolStripMenuItem.Enabled = _activeGroups.Count > 0;

            // Only allow bake if the current group is a generator master
            bool canBake = false;
            if (conditionGrid.Tag is AchievementConditionGroup group)
            {
                int index = _activeGroups.IndexOf(group);
                if (index != -1 && _generatorRules.ContainsKey(index))
                    canBake = true;
            }
            commitGeneratorToolStripMenuItem.Enabled = canBake;
        }

        // Proxy calls to Grid Manager
        private void moveUpToolStripMenuItem_Click(object? sender, EventArgs e) { PushUndoState(); _gridManager.MoveSelected(-1); }
        private void moveDownToolStripMenuItem_Click(object? sender, EventArgs e) { PushUndoState(); _gridManager.MoveSelected(1); }
        private void duplicateToolStripMenuItem_Click(object? sender, EventArgs e) { PushUndoState(); _gridManager.DuplicateSelected(); }
        private void deleteToolStripMenuItem_Click(object? sender, EventArgs e) { PushUndoState(); _gridManager.DeleteSelected(); }
        private void addConditionToolStripMenuItem_Click(object? sender, EventArgs e) { PushUndoState(); _gridManager.AddCondition(); }
        private void copyRowToolStripMenuItem_Click(object? sender, EventArgs e) => _gridManager.CopySelectedRows();
        private void copyLogicToolStripMenuItem_Click(object? sender, EventArgs e) => _gridManager.CopySelectedLogic();
        private void pasteToolStripMenuItem_Click(object? sender, EventArgs e) { if (Clipboard.ContainsText()) GridManager_PasteRequested(sender, Clipboard.GetText()); }

        private void addGroupToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            PushUndoState();
            string newName = _activeGroups.Count == 0 ? "Core" : (GroupSeparator == 'S' ? "Alt " + _activeGroups.Count : "Group " + (_activeGroups.Count + 1));
            _activeGroups.Add(new AchievementConditionGroup { GroupName = newName });
            RefreshGroupsListUI();
            // Select new group
            if (groupsList.Items.Count > 0)
                groupsList.Items[groupsList.Items.Count - 1].Selected = true;
            BuildTriggerAndNotify();
        }

        private void removeGroupToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            if (groupsList.SelectedItems.Count == 0) return;
            int idx = groupsList.SelectedItems[0].Index;

            if (_activeGroups.Count <= 1) return;

            // Block deleting generated groups as they are managed by the master
            foreach (var kvp in _generatorRules)
            {
                if (idx > kvp.Key && idx <= kvp.Key + kvp.Value.Copies)
                {
                    MessageBox.Show("Cannot manually delete a generated group. Modify the Generator on the Master Group instead.",
                        "Action Blocked", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            PushUndoState();
            if (_generatorRules.ContainsKey(idx)) _generatorRules.Remove(idx);

            _activeGroups.RemoveAt(idx);

            // Renumber Alt groups (Alt 1, Alt 2...)
            for (int i = 1; i < _activeGroups.Count; i++)
            {
                if (_activeGroups[i].GroupName.StartsWith("Alt "))
                    _activeGroups[i].GroupName = $"Alt {i}";
            }

            RefreshGroupsListUI();

            if (idx >= _activeGroups.Count) idx = _activeGroups.Count - 1;
            if (idx >= 0) groupsList.Items[idx].Selected = true;

            BuildTriggerAndNotify();
        }

        private void removeAllAltGroupsToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            if (_activeGroups.Count <= 1) return;

            if (MessageBox.Show("Are you sure you want to remove all Alt groups?", "Confirm", MessageBoxButtons.YesNo) != DialogResult.Yes)
                return;

            PushUndoState();
            _generatorRules.Clear();

            var first = _activeGroups[0];
            _activeGroups.Clear();
            _activeGroups.Add(first);

            if (!_isLeaderboardMode && first.GroupName != "Core")
                first.GroupName = "Core";

            RefreshGroupsListUI();
            BuildTriggerAndNotify();
        }

        private void createGeneratorToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            var group = conditionGrid.Tag as AchievementConditionGroup;
            if (group == null) return;

            int groupIndex = _activeGroups.IndexOf(group);
            if (groupIndex == -1) return;

            // --- Updated Logic: Flattened Hierarchy with Context-Aware Notes ---

            var rootItems = new List<GeneratorConfigForm.GeneratorItemViewModel>();
            int autoStride = 0;
            int detectedCopies = 1;
            Dictionary<long, int> sizeCounts = new Dictionary<long, int>();

            GeneratorConfigForm.GeneratorItemViewModel? activeChainParent = null;
            NoteNode? activeNoteNode = null; // Tracks the pointer tree context

            // Local function now returns the Found Node to persist context
            (GeneratorConfigForm.GeneratorItemViewModel Item, NoteNode? FoundNode) GetOrCreateNode(string addr, bool isPointerBase, GeneratorConfigForm.GeneratorItemViewModel? parent, NoteNode? contextNode)
            {
                string normAddr = LogicFormatter.NormalizeAddress(addr);
                string desc = "";
                NoteNode? currentNote = null;
                bool foundAsOffset = false;

                // 1. Try Offset Context Lookup (if parent context exists)
                if (contextNode != null)
                {
                    long offsetVal = long.TryParse(normAddr.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long v) ? v : -1;
                    if (offsetVal != -1)
                    {
                        foreach (var n in contextNode.Children)
                        {
                            // Clean node offset "+0x10" -> 0x10. Handle negative offsets.
                            long nodeOff = long.TryParse(n.Offset.Replace("0x", "").Replace("+", "").Replace("-", "").Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long no) ? no : -99;
                            if (n.Offset.Contains("-")) nodeOff = -nodeOff;

                            // Match logic: Logic Offset matches Note Offset range
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

                // 2. Try Base Lookup (if not found as offset)
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

                            // Try Stride Detection
                            if (autoStride == 0 && root.Size > 1) autoStride = (int)root.Size;
                            if (root.Size > 1) { if (!sizeCounts.ContainsKey(root.Size)) sizeCounts[root.Size] = 0; sizeCounts[root.Size]++; }

                            // Create Synthetic Context for Base
                            var syntheticContext = new NoteNode();
                            foreach (var n in nodes)
                            {
                                if (n.IndentLevel != -2 && (n.Parent == null || n.Parent.IndentLevel == -1))
                                {
                                    syntheticContext.Children.Add(n);
                                }
                            }
                            currentNote = syntheticContext;
                        }
                    }
                }

                // Create ViewModel
                var item = new GeneratorConfigForm.GeneratorItemViewModel
                {
                    Address = addr.StartsWith("0x") ? addr : "0x" + addr,
                    Description = desc
                };

                // Add to hierarchy
                if (parent != null) parent.Children.Add(item);
                else rootItems.Add(item);

                return (item, currentNote);
            }

            // Scan conditions to build hierarchy
            foreach (var cond in group.Conditions)
            {
                if (cond.Flag == "Add Address")
                {
                    if (RaLogicSyntax.MemoryTypes.Contains(cond.LeftOperand.Type))
                    {
                        var val = cond.LeftOperand.Value;
                        // Pass activeChainParent. If null, this becomes root. If not null, this becomes child of root.
                        var result = GetOrCreateNode(val, isPointerBase: true, parent: activeChainParent, contextNode: activeNoteNode);

                        // If we didn't have a parent, this node IS the parent for the rest of the chain
                        if (activeChainParent == null)
                        {
                            activeChainParent = result.Item;
                        }
                        // Else: activeChainParent stays the same (the root), effectively flattening the UI hierarchy

                        // Context must advance deeply to support finding next offset descriptions
                        activeNoteNode = result.FoundNode;
                    }
                }
                else
                {
                    // Comparisons
                    if (RaLogicSyntax.MemoryTypes.Contains(cond.LeftOperand.Type))
                    {
                        GetOrCreateNode(cond.LeftOperand.Value, isPointerBase: false, parent: activeChainParent, contextNode: activeNoteNode);
                    }
                    if (RaLogicSyntax.MemoryTypes.Contains(cond.RightOperand.Type))
                    {
                        GetOrCreateNode(cond.RightOperand.Value, isPointerBase: false, parent: null, contextNode: null);
                    }

                    activeChainParent = null;
                    activeNoteNode = null;
                }
            }

            // Apply Smart Padding
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
            _generatorRules.TryGetValue(groupIndex, out existingDef);

            if (existingDef == null && detectedCopies > 1)
                existingDef = new GeneratorDefinition { Copies = detectedCopies };

            using var form = new GeneratorConfigForm(rootItems, autoStride, existingDef);

            if (form.ShowDialog() == DialogResult.OK)
            {
                PushUndoState();

                if (groupIndex < _activeGroups.Count - 1)
                {
                    var result = MessageBox.Show(
                        "There are existing Alt groups after this group. Do you want to delete them to make room for the generator?",
                        "Overwrite Confirmation",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Warning);

                    if (result == DialogResult.Cancel) return;

                    if (result == DialogResult.Yes)
                    {
                        while (_activeGroups.Count > groupIndex + 1)
                        {
                            _activeGroups.RemoveAt(_activeGroups.Count - 1);
                        }
                        var keysToRemove = _generatorRules.Keys.Where(k => k > groupIndex).ToList();
                        foreach (var k in keysToRemove) _generatorRules.Remove(k);
                    }
                }

                if (!_generatorRules.ContainsKey(groupIndex))
                    _generatorRules[groupIndex] = new GeneratorDefinition();

                var def = _generatorRules[groupIndex];
                def.Copies = form.Copies;
                def.Rules.Clear();

                foreach (string addr in form.SelectedAddresses)
                {
                    def.Rules.Add(new GeneratorRule
                    {
                        SourceAddress = addr,
                        Stride = form.Stride
                    });
                }

                LogicGeneratorService.ApplyGeneratorRules(_activeGroups, _generatorRules);
                RefreshGroupsListUI();
                BuildTriggerAndNotify();
            }
        }

        // New helper for padding
        private void ApplySmartPadding(List<GeneratorConfigForm.GeneratorItemViewModel> roots)
        {
            foreach (var root in roots)
            {
                if (root.Children.Count == 0) continue;

                // Calculate max length of hex part for children
                int maxLen = 0;
                foreach (var child in root.Children)
                {
                    string hex = child.Address.StartsWith("0x") ? child.Address.Substring(2) : child.Address;
                    if (hex.Length > maxLen) maxLen = hex.Length;
                }

                // Apply padding if maxLen > 1
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

        // New context menu action to "Bake" the generator
        private void commitGeneratorToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            // Find active group index
            if (conditionGrid.Tag is not AchievementConditionGroup group) return;
            int index = _activeGroups.IndexOf(group);

            if (index != -1 && _generatorRules.ContainsKey(index))
            {
                PushUndoState();
                _generatorRules.Remove(index);

                // Rename logic to clean up "Alt 1 (+1)" to sequential "Alt 2"
                if (!_isLeaderboardMode)
                {
                    for (int i = 1; i < _activeGroups.Count; i++)
                    {
                        _activeGroups[i].GroupName = $"Alt {i}";
                    }
                }

                RefreshGroupsListUI(); // Refreshes list to remove (Auto) text and show new names
                SetStatus("Generator committed. Groups are now independent and renamed.");
            }
        }

        public void AddConditionFromNote(string logicString, string type, string size)
        {
            if (string.IsNullOrEmpty(logicString)) return;

            PushUndoState();

            var groups = AchievementParser.ParseAchievementTrigger(logicString, GroupSeparator, false);

            if (groups.Count > 0 && groups[0].Conditions.Count > 0)
            {
                var conditions = groups[0].Conditions;

                // Inject missing size for pointers
                foreach (var cond in conditions)
                {
                    if (cond.Flag == "Add Address" && string.IsNullOrEmpty(cond.LeftOperand.Size))
                        cond.LeftOperand.Size = "32-bit";
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
                    lastCond.RightOperand = new Operand { Type = "Value", Value = type == "Float" ? "0.0" : "0" };
                }

                if (conditionGrid.Tag is AchievementConditionGroup activeGroup)
                {
                    // Logic for insertion point
                    int insertIndex = activeGroup.Conditions.Count;
                    if (conditionGrid.SelectedRows.Count > 0)
                    {
                        // Insert after the last selected row
                        insertIndex = conditionGrid.SelectedRows.Cast<DataGridViewRow>().Max(r => r.Index) + 1;
                    }

                    // Insert conditions
                    activeGroup.Conditions.InsertRange(insertIndex, conditions);

                    RefreshCurrentGridDisplay();

                    // Restore Selection (highlight new items)
                    conditionGrid.ClearSelection();

                    if (insertIndex < conditionGrid.Rows.Count)
                    {
                        // Scroll to/Focus first new row
                        conditionGrid.CurrentCell = conditionGrid.Rows[insertIndex].Cells[0];
                    }

                    for (int i = 0; i < conditions.Count; i++)
                    {
                        int rowIdx = insertIndex + i;
                        if (rowIdx < conditionGrid.Rows.Count)
                        {
                            conditionGrid.Rows[rowIdx].Selected = true;
                        }
                    }

                    BuildTriggerAndNotify();
                }
            }
            SetStatus("Condition added from note.");
        }

        private string ValidateMemorySize(string inputSize)
        {
            if (string.IsNullOrEmpty(inputSize)) return "8-bit";
            if (inputSize.Equals("Flaot", StringComparison.OrdinalIgnoreCase)) return "Float";
            return RaLogicSyntax.MemorySizes.Contains(inputSize) ? inputSize : "8-bit";
        }

        private void UpdateUIFromText()
        {
            _isProgrammaticallyChanging = true;
            if (_isLeaderboardMode)
            {
                _leaderboardSegments = ParseLeaderboardIntoSegments(_triggerText);
                RefreshLeaderboardGroupsList();
            }
            else
            {
                _activeGroups = AchievementParser.ParseAchievementTrigger(_triggerText, GroupSeparator, false);
                if (!_activeGroups.Any())
                    _activeGroups.Add(new AchievementConditionGroup { GroupName = "Core" });

                RefreshGroupsListUI();
            }
            _isProgrammaticallyChanging = false;
        }

        private Dictionary<string, List<AchievementConditionGroup>> ParseLeaderboardIntoSegments(string logic)
        {
            var segments = new Dictionary<string, List<AchievementConditionGroup>>
            {
                { "Start", new() }, { "Cancel", new() }, { "Submit", new() }, { "Value", new() }
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
                if (segments[key].Count == 0)
                    segments[key].Add(new AchievementConditionGroup { GroupName = "Core" });
            }
            return segments;
        }

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

        private void RefreshGroupsListUI()
        {
            int savedIndex = groupsList.SelectedIndices.Count > 0 ? groupsList.SelectedIndices[0] : 0;

            groupsList.BeginUpdate();
            groupsList.Items.Clear();

            HashSet<int> generatedIndices = new HashSet<int>();
            foreach (var kvp in _generatorRules)
            {
                for (int i = 1; i <= kvp.Value.Copies; i++)
                    generatedIndices.Add(kvp.Key + i);
            }

            for (int i = 0; i < _activeGroups.Count; i++)
            {
                var item = new ListViewItem(_activeGroups[i].GroupName);
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
            groupsList.EndUpdate();

            // Reselect previous group
            if (groupsList.Items.Count > 0)
            {
                if (savedIndex >= groupsList.Items.Count) savedIndex = groupsList.Items.Count - 1;

                bool wasChanging = _isProgrammaticallyChanging;
                _isProgrammaticallyChanging = true; // Suppress spurious reload

                groupsList.Items[savedIndex].Selected = true;

                // Ensure context matches (Tag update)
                var group = _activeGroups[savedIndex];
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

        private void GroupsList_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_isProgrammaticallyChanging || groupsList.SelectedItems.Count == 0) return;

            int index = groupsList.SelectedItems[0].Index;
            if (index >= 0 && index < _activeGroups.Count)
            {
                var group = _activeGroups[index];
                conditionGrid.Tag = group;
                RefreshCurrentGridDisplay();
            }
        }

        private void RefreshCurrentGridDisplay()
        {
            if (conditionGrid.Tag is AchievementConditionGroup group)
            {
                // Capture State
                int savedScroll = conditionGrid.FirstDisplayedScrollingRowIndex;
                List<int> selectedIndices = new List<int>();
                foreach (DataGridViewRow row in conditionGrid.SelectedRows)
                {
                    selectedIndices.Add(row.Index);
                }

                int focusedRowIndex = -1;
                int focusedColIndex = 0;
                if (conditionGrid.CurrentCell != null)
                {
                    focusedRowIndex = conditionGrid.CurrentCell.RowIndex;
                    focusedColIndex = conditionGrid.CurrentCell.ColumnIndex;
                }

                int index = _activeGroups.IndexOf(group);
                bool isGenerated = false;
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

                // Passed _showDecimal, _showAliases, Notes Lookup AND Current Issues for highlighting
                // Also passing RangeCache for Struct logic
                ConditionGridHelper.PopulateGridRows(conditionGrid, group, isGenerated, _showDecimal, _showAliases, _notesLookup, _gridManager.RangeCache, _currentIssues);

                // Restore State
                if (conditionGrid.Rows.Count > 0)
                {
                    // Restore Focus FIRST (prevents clearing selection later)
                    if (focusedRowIndex >= 0 && focusedRowIndex < conditionGrid.Rows.Count)
                    {
                        try { conditionGrid.CurrentCell = conditionGrid.Rows[focusedRowIndex].Cells[focusedColIndex]; } catch { }
                    }

                    // Restore Selection
                    conditionGrid.ClearSelection(); // Clear the selection made by CurrentCell assignment if any
                    foreach (int idx in selectedIndices)
                    {
                        if (idx >= 0 && idx < conditionGrid.Rows.Count)
                        {
                            conditionGrid.Rows[idx].Selected = true;
                        }
                    }

                    // Restore Scroll
                    if (savedScroll >= 0 && savedScroll < conditionGrid.Rows.Count)
                    {
                        try { conditionGrid.FirstDisplayedScrollingRowIndex = savedScroll; } catch { }
                    }
                }
            }
        }

        private void BuildTriggerAndNotify()
        {
            if (_isProgrammaticallyChanging) return;

            if (conditionGrid.Tag is AchievementConditionGroup activeGroup)
            {
                LogicGeneratorService.ApplyGeneratorRules(_activeGroups, _generatorRules);
                RefreshGroupsListUI();
            }

            var sb = new StringBuilder();
            if (_isLeaderboardMode)
            {
                sb.Append("STA:").Append(RebuildSegment(_leaderboardSegments.GetValueOrDefault("Start"))).Append("::");
                sb.Append("CAN:").Append(RebuildSegment(_leaderboardSegments.GetValueOrDefault("Cancel"))).Append("::");
                sb.Append("SUB:").Append(RebuildSegment(_leaderboardSegments.GetValueOrDefault("Submit"))).Append("::");
                sb.Append("VAL:").Append(RebuildSegment(_leaderboardSegments.GetValueOrDefault("Value")));
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

        private string RebuildSegment(List<AchievementConditionGroup>? groups)
        {
            if (groups == null || groups.Count == 0) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < groups.Count; i++)
            {
                if (i > 0) sb.Append(GroupSeparator);
                var group = groups[i];
                for (int c = 0; c < group.Conditions.Count; c++)
                {
                    if (c > 0) sb.Append('_');
                    sb.Append(LogicFormatter.ConditionToString(group.Conditions[c]));
                }
            }
            return sb.ToString();
        }

        // -- Snippet and Tools Events --

        private void btnTools_Click(object? sender, EventArgs e)
        {
            toolsContextMenu.Show(btnTools, 0, btnTools.Height);
        }

        private void pointerCalculatorToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            using (var form = new PointerCalculatorForm())
            {
                form.ShowDialog();
            }
        }

        private void saveAsSnippetToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            if (conditionGrid.SelectedRows.Count == 0) return;

            // Generate Logic String for selection
            var sb = new StringBuilder();
            foreach (DataGridViewRow row in conditionGrid.SelectedRows.Cast<DataGridViewRow>().OrderBy(r => r.Index))
            {
                if (row.Tag is AchievementCondition cond)
                {
                    if (sb.Length > 0) sb.Append('_');
                    sb.Append(LogicFormatter.ConditionToString(cond));
                }
            }
            string logic = sb.ToString();

            // Simple input dialog for name (using standard InputBox style or custom if preferred, simplified here)
            string name = Microsoft.VisualBasic.Interaction.InputBox("Enter name for this snippet:", "Save Snippet", "My Snippet");
            if (!string.IsNullOrWhiteSpace(name))
            {
                SnippetManager.SaveSnippet(name, logic);
                MessageBox.Show("Snippet saved!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void insertSnippetToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            if (conditionGrid.ReadOnly) return;

            using (var form = new SnippetSelectForm())
            {
                if (form.ShowDialog() == DialogResult.OK && form.SelectedSnippet != null)
                {
                    // Copy to clipboard internally to reuse PasteLogic
                    string oldClip = Clipboard.ContainsText() ? Clipboard.GetText() : "";
                    Clipboard.SetText(form.SelectedSnippet.LogicCode);

                    PushUndoState();
                    _gridManager.PasteLogic();

                    // Restore clipboard if desired, or leave it for repeated pasting
                    // Clipboard.SetText(oldClip);
                }
            }
        }
    }
}