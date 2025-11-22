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
        private IAssetItem? _boundAsset; // The Asset object currently being edited

        // -- Data Models --
        private List<AchievementConditionGroup> _activeGroups = new();
        // Logic segment map for Leaderboards (Start, Cancel, Submit, Value)
        private Dictionary<string, List<AchievementConditionGroup>> _leaderboardSegments = new();
        // Stores Generator rules (Copies, Strides) keyed by Group Index
        private Dictionary<int, GeneratorDefinition> _generatorRules = new();

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

            // Input field wiring
            txtTitle.TextChanged += TxtTitle_TextChanged;
            txtDescription.TextChanged += TxtDescription_TextChanged;
            cmbPoints.SelectedIndexChanged += CmbPoints_Changed;
            cmbPoints.TextChanged += CmbPoints_Changed;
            cmbType.SelectedIndexChanged += CmbType_SelectedIndexChanged;
            chkShowDecimal.CheckedChanged += ChkShowDecimal_CheckedChanged;

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

        private void GridManager_LogicChanged(object? sender, EventArgs e)
        {
            _history.PushState(_triggerText);
            BuildTriggerAndNotify();
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

            btnReset.Visible = asset.State != AssetState.Local;
            SetStatus("Asset loaded.");
        }

        private void SetupAchievementUI(RaAchievement cheevo)
        {
            cmbPoints.Visible = true;
            cmbType.Visible = true;
            lblAuthor.Visible = true;
            pbBadge.Visible = true;
            lvLeaderboardComponents.Visible = false;

            txtTitle.Left = 75;
            txtTitle.Width = panelHeader.Width - 75 - 165;
            txtDescription.Left = 75;
            txtDescription.Width = panelHeader.Width - 75 - 165;

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
            lblAuthor.Visible = false;
            pbBadge.Visible = false;
            lvLeaderboardComponents.Visible = true;

            lvLeaderboardComponents.Location = new Point(5, 5);
            int textLeft = lvLeaderboardComponents.Right + 10;

            txtTitle.Left = textLeft;
            txtTitle.Width = panelHeader.Width - textLeft - 165;
            txtDescription.Left = textLeft;
            txtDescription.Width = panelHeader.Width - textLeft - 165;

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

        private void btnReset_Click(object sender, EventArgs e) => AssetResetRequested?.Invoke(this, EventArgs.Empty);
        private void btnAddNote_Click(object sender, EventArgs e) => AddFromCodeNoteRequested?.Invoke(this, EventArgs.Empty);

        private void TxtTitle_TextChanged(object? sender, EventArgs e) => NotifyAssetDataChanged(a => a.Title = txtTitle.Text);
        private void TxtDescription_TextChanged(object? sender, EventArgs e) => NotifyAssetDataChanged(a => a.Description = txtDescription.Text);
        private void CmbPoints_Changed(object? sender, EventArgs e) => NotifyAssetDataChanged(a => { if (a is RaAchievement r && int.TryParse(cmbPoints.Text, out int p)) r.Points = p; });
        private void ChkShowDecimal_CheckedChanged(object? sender, EventArgs e) { _showDecimal = chkShowDecimal.Checked; RefreshCurrentGridDisplay(); }

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
        }

        // Proxy calls to Grid Manager
        private void moveUpToolStripMenuItem_Click(object sender, EventArgs e) { _history.PushState(_triggerText); _gridManager.MoveSelected(-1); }
        private void moveDownToolStripMenuItem_Click(object sender, EventArgs e) { _history.PushState(_triggerText); _gridManager.MoveSelected(1); }
        private void duplicateToolStripMenuItem_Click(object sender, EventArgs e) { _history.PushState(_triggerText); _gridManager.DuplicateSelected(); }
        private void deleteToolStripMenuItem_Click(object sender, EventArgs e) { _history.PushState(_triggerText); _gridManager.DeleteSelected(); }
        private void addConditionToolStripMenuItem_Click(object sender, EventArgs e) { _history.PushState(_triggerText); _gridManager.AddCondition(); }
        private void copyRowToolStripMenuItem_Click(object sender, EventArgs e) => _gridManager.CopySelectedRows();
        private void copyLogicToolStripMenuItem_Click(object sender, EventArgs e) => _gridManager.CopySelectedLogic();
        private void pasteToolStripMenuItem_Click(object sender, EventArgs e) { _history.PushState(_triggerText); _gridManager.PasteLogic(); }

        private void addGroupToolStripMenuItem_Click(object sender, EventArgs e)
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

        private void removeGroupToolStripMenuItem_Click(object sender, EventArgs e)
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

        private void removeAllAltGroupsToolStripMenuItem_Click(object sender, EventArgs e)
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

        private void createGeneratorToolStripMenuItem_Click(object sender, EventArgs e)
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

            if (lVal.StartsWith("0x")) target = lVal;
            else if (rVal.StartsWith("0x")) target = rVal;

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
                    activeGroup.Conditions.AddRange(conditions);
                    RefreshCurrentGridDisplay();
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
                ConditionGridHelper.PopulateGridRows(conditionGrid, group, isGenerated);
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
    }
}