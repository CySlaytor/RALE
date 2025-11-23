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
        // Handles local Undo/Redo for text edits inside this control
        private readonly HistoryManager<string> _history = new();

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
                    _history.PushState(_triggerText);
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
            if (notes == null) return;

            foreach (var note in notes)
            {
                // Use LogicFormatter to get canonical key (e.g. "0x1234" instead of "0x1234")
                string key = LogicFormatter.NormalizeAddress(note.Address);

                // Parse note for tree structure
                var nodes = PointerTreeParser.ParseNoteText(note.Note);

                // Add to dictionary (latest note wins if duplicate)
                _notesLookup[key] = nodes;
            }

            // Push map to Grid Manager for Tooltips
            _gridManager.NotesLookup = _notesLookup;

            // Refresh grid to update aliases if enabled
            RefreshCurrentGridDisplay();
        }

        private void GridManager_LogicChanged(object? sender, EventArgs e)
        {
            _history.PushState(_triggerText);
            BuildTriggerAndNotify();
            RunValidation();
        }

        private void RunValidation()
        {
            // Only validate current group to save perf
            if (conditionGrid.Tag is AchievementConditionGroup group)
            {
                var warnings = LogicValidator.ValidateGroup(group);
                if (warnings.Count > 0)
                {
                    SetStatus("⚠️ Warning: " + warnings[0]);
                    // Could update UI color here if desired
                }
                else
                {
                    SetStatus("Ready");
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
            _history.PushState(_triggerText);
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

            cmbPoints.Visible = true;
            cmbType.Visible = true;
            lblAuthor.Visible = true;
            lblAssetState.Visible = false;
            pbBadge.Visible = true;
            lvLeaderboardComponents.Visible = false;
            btnReset.Visible = false;
            SetStatus("Ready");
        }

        // Loads an asset into the editor
        public async void BindAchievement(IAssetItem asset, string setType = "Core")
        {
            _boundAsset = asset;
            _isLeaderboardMode = asset.IsLeaderboard;
            _generatorRules.Clear();
            this.TriggerText = asset.Logic;
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
            lblAssetState.Location = new Point(350, 63);

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
            _triggerText = _history.Undo(_triggerText);
            UpdateUIFromText();
            TriggerTextChanged?.Invoke(this, EventArgs.Empty);
            SetStatus("Undone.");
        }

        public void Redo()
        {
            _triggerText = _history.Redo(_triggerText);
            UpdateUIFromText();
            TriggerTextChanged?.Invoke(this, EventArgs.Empty);
            SetStatus("Redone.");
        }

        // -- UI Event Handlers (Named to match Designer) --

        private void btnReset_Click(object? sender, EventArgs e) => AssetResetRequested?.Invoke(this, EventArgs.Empty);
        private void btnAddNote_Click(object? sender, EventArgs e) => AddFromCodeNoteRequested?.Invoke(this, EventArgs.Empty);

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
            createGeneratorToolStripMenuItem.Enabled = any && conditionGrid.SelectedRows.Count == 1;
            saveAsSnippetToolStripMenuItem.Enabled = any;
        }

        // Proxy calls to Grid Manager
        private void moveUpToolStripMenuItem_Click(object? sender, EventArgs e) { _history.PushState(_triggerText); _gridManager.MoveSelected(-1); }
        private void moveDownToolStripMenuItem_Click(object? sender, EventArgs e) { _history.PushState(_triggerText); _gridManager.MoveSelected(1); }
        private void duplicateToolStripMenuItem_Click(object? sender, EventArgs e) { _history.PushState(_triggerText); _gridManager.DuplicateSelected(); }
        private void deleteToolStripMenuItem_Click(object? sender, EventArgs e) { _history.PushState(_triggerText); _gridManager.DeleteSelected(); }
        private void addConditionToolStripMenuItem_Click(object? sender, EventArgs e) { _history.PushState(_triggerText); _gridManager.AddCondition(); }
        private void copyRowToolStripMenuItem_Click(object? sender, EventArgs e) => _gridManager.CopySelectedRows();
        private void copyLogicToolStripMenuItem_Click(object? sender, EventArgs e) => _gridManager.CopySelectedLogic();
        private void pasteToolStripMenuItem_Click(object? sender, EventArgs e) { if (Clipboard.ContainsText()) GridManager_PasteRequested(sender, Clipboard.GetText()); }

        private void addGroupToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            _history.PushState(_triggerText);
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

            _history.PushState(_triggerText);
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

            _history.PushState(_triggerText);
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
            if (conditionGrid.SelectedRows.Count != 1) return;
            var group = conditionGrid.Tag as AchievementConditionGroup;
            if (group == null) return;

            int groupIndex = _activeGroups.IndexOf(group);
            if (groupIndex == -1) return;

            var row = conditionGrid.SelectedRows[0];
            string target = "";
            string lVal = row.Cells["ColLValue"].Value?.ToString() ?? "";
            string rVal = row.Cells["ColRValue"].Value?.ToString() ?? "";

            if (lVal.StartsWith("0x") || lVal.StartsWith("[")) // Handle alias bracket start
            {
                // In Alias mode, we must retrieve raw value from tag
                if (row.Tag is AchievementCondition cond) target = cond.LeftOperand.Value;
                else target = lVal;
            }
            else if (rVal.StartsWith("0x") || rVal.StartsWith("["))
            {
                if (row.Tag is AchievementCondition cond) target = cond.RightOperand.Value;
                else target = rVal;
            }

            if (string.IsNullOrEmpty(target)) return;

            using var form = new GeneratorConfigForm(target);
            if (form.ShowDialog() == DialogResult.OK)
            {
                _history.PushState(_triggerText);

                if (!_generatorRules.ContainsKey(groupIndex))
                    _generatorRules[groupIndex] = new GeneratorDefinition { Copies = form.Copies };
                else
                    _generatorRules[groupIndex].Copies = form.Copies;

                var def = _generatorRules[groupIndex];
                var existing = def.Rules.FirstOrDefault(r => r.SourceAddress == target);

                if (existing != null) existing.Stride = form.Stride;
                else def.Rules.Add(new GeneratorRule { SourceAddress = target, Stride = form.Stride });

                LogicGeneratorService.ApplyGeneratorRules(_activeGroups, _generatorRules);
                RefreshGroupsListUI();
                BuildTriggerAndNotify();
            }
        }

        public void AddConditionFromNote(string logicString, string type, string size)
        {
            if (string.IsNullOrEmpty(logicString)) return;

            _history.PushState(_triggerText);

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

            if (groupsList.Items.Count > 0)
                groupsList.Items[0].Selected = true;
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
                // Passed _showDecimal and _showAliases and the Notes Lookup
                ConditionGridHelper.PopulateGridRows(conditionGrid, group, isGenerated, _showDecimal, _showAliases, _notesLookup);

                // Restore State
                if (conditionGrid.Rows.Count > 0)
                {
                    // Restore Selection
                    conditionGrid.ClearSelection();
                    foreach (int idx in selectedIndices)
                    {
                        if (idx >= 0 && idx < conditionGrid.Rows.Count)
                        {
                            conditionGrid.Rows[idx].Selected = true;
                        }
                    }

                    // Restore Focus
                    if (focusedRowIndex >= 0 && focusedRowIndex < conditionGrid.Rows.Count)
                    {
                        conditionGrid.CurrentCell = conditionGrid.Rows[focusedRowIndex].Cells[focusedColIndex];
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

                    _history.PushState(_triggerText);
                    _gridManager.PasteLogic();

                    // Restore clipboard if desired, or leave it for repeated pasting
                    // Clipboard.SetText(oldClip);
                }
            }
        }
    }
}