#nullable enable
using RALogicEditor.Controls;
using RALogicEditor.Helpers;
using RALogicEditor.Models;
using RALogicEditor.Parsers;
using RALogicEditor.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace RALogicEditor
{
    public partial class MainForm : Form
    {
        // -- Core Services --
        // Handles File I/O, merging logic, and data storage (Server vs Local)
        private readonly ProjectRepository _projectRepo = new();
        // Handles Global Undo/Redo for the asset list (Add/Remove/Move assets)
        private readonly HistoryManager<UserTextParser.UserData> _mainHistory = new();
        // Manages the Recent Files list in the File menu
        private readonly RecentFilesManager _recentFiles = new();

        // -- State Tracking --
        private IAssetItem? _selectedAsset; // The achievement/leaderboard currently being edited
        private bool _isLoading;            // Flag to suppress events during bulk updates
        private string _currentFilePath = ""; // Path to the main loaded file (JSON)
        private string _currentUserFilePath = ""; // Path to the companion User.txt
        private List<IAssetItem> _currentDisplayList = new(); // The filtered list currently shown in the grid

        public MainForm()
        {
            InitializeComponent();

            // -- Wire up Trigger Editor Events --
            // When the logic text changes in the editor, update the local model
            triggerEditor.TriggerTextChanged += TriggerEditor_TriggerTextChanged;
            // When metadata (Points, Title) changes, update the grid row
            triggerEditor.AchievementDataChanged += TriggerEditor_AchievementDataChanged;
            // When user clicks "Reset", revert the item to Server state
            triggerEditor.AssetResetRequested += TriggerEditor_AssetResetRequested;
            // When user wants to inject logic from the Code Note viewer
            triggerEditor.AddFromCodeNoteRequested += TriggerEditor_AddFromCodeNoteRequested;

            // -- Bridge: Trigger Editor -> Code Note Editor --
            // Highlighting memory addresses in the Note Viewer when clicked in the Trigger Grid
            triggerEditor.AddressSelected += (s, addr) => codeNoteEditor.HighlightAddress(addr);
            triggerEditor.PointerChainSelected += (s, e) =>
            {
                codeNoteEditor.HighlightAddress(e.BaseAddress);
                if (e.Offsets.Count > 0) codeNoteEditor.HighlightVariable(e.Offsets);
            };

            // -- Code Note Editor Events --
            codeNoteEditor.ReloadRequested += Reload_Click;

            // -- Main Grid Events --
            achievementGrid.CellDoubleClick += achievementGrid_CellDoubleClick;
            achievementGrid.SelectionChanged += achievementGrid_SelectionChanged;
            achievementGrid.KeyDown += achievementGrid_KeyDown;
            achievementGrid.ContextMenuStrip.Opening += AchievementContextMenu_Opening;

            // -- Toolbar Setup --
            // Ensure "JSON" is the first option as requested
            cmbSource.Items.Clear();
            cmbSource.Items.AddRange(new object[] { "JSON", "Local", "New" });

            cmbAssetType.SelectedIndex = 0; // Default to Achievements
            cmbSource.SelectedIndex = 0;    // Default to JSON view
            cmbAssetType.SelectedIndexChanged += (s, e) => ReloadDataView();
            cmbSource.SelectedIndexChanged += (s, e) => ReloadDataView();

            // -- Menu Item Wiring --
            newAssetToolStripMenuItem.Click += NewAsset_Click;
            cloneAssetToolStripMenuItem.Click += DuplicateAsset_Click;
            deleteAssetToolStripMenuItem.Click += DeleteAsset_Click;
            resetAssetToolStripMenuItem.Click += ResetAsset_Click;
            moveUpAssetToolStripMenuItem.Click += MoveAssetUp_Click;
            moveDownAssetToolStripMenuItem.Click += MoveAssetDown_Click;

            // -- Button Wiring --
            btnAddAsset.Click += NewAsset_Click;
            btnDuplicateAsset.Click += DuplicateAsset_Click;
            btnDeleteAsset.Click += DeleteAsset_Click;

            this.FormClosing += MainForm_FormClosing;

            // Load recent files list on startup
            _recentFiles.Load();
            UpdateRecentMenu();
        }

        private void achievementGrid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0) LoadSelectedAsset();
        }

        private void achievementGrid_SelectionChanged(object? sender, EventArgs e)
        {
            UpdateSelectionUI();
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (!ConfirmDiscardChanges()) e.Cancel = true;
        }

        // Checks if there are unsaved local changes (Assets in "Modified" or "Local" state)
        private bool ConfirmDiscardChanges()
        {
            if (_projectRepo.LocalData == null) return true;
            bool hasChanges = _projectRepo.LocalData.Achievements.Any(a => a.State != AssetState.Synced) ||
                              _projectRepo.LocalData.Leaderboards.Any(l => l.State != AssetState.Synced);

            if (hasChanges)
            {
                var result = MessageBox.Show("You have unsaved changes. Any unsaved local changes will be lost.\n\nContinue?",
                    "Unsaved Changes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                return result == DialogResult.Yes;
            }
            return true;
        }

        // Reloads the current file from disk, discarding unsaved changes
        private void Reload_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFilePath)) return;
            if (!ConfirmDiscardChanges()) return;

            // Remember selected ID to restore after reload
            int selectedId = -1;
            if (achievementGrid.SelectedRows.Count > 0 && achievementGrid.SelectedRows[0].Tag is IAssetItem item)
            {
                selectedId = item.ID;
            }

            LoadFiles(_currentFilePath);

            // Restore selection
            if (selectedId != -1)
            {
                foreach (DataGridViewRow row in achievementGrid.Rows)
                {
                    if (row.Tag is IAssetItem i && i.ID == selectedId)
                    {
                        row.Selected = true;
                        achievementGrid.CurrentCell = row.Cells[0];
                        LoadSelectedAsset();
                        break;
                    }
                }
            }
        }

        // Injection Logic: Gets logic string from CodeNoteEditor and sends it to TriggerEditor
        private void TriggerEditor_AddFromCodeNoteRequested(object? sender, EventArgs e)
        {
            var data = codeNoteEditor.GetSelectedLogic();
            if (!string.IsNullOrEmpty(data.Logic))
            {
                triggerEditor.AddConditionFromNote(data.Logic, data.Type, data.Size);
            }
            else
            {
                MessageBox.Show("No valid address or variable selected in Code Notes.", "Add Note", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        #region Undo/Redo (Global Asset List)
        private void PushMainUndo()
        {
            // Creates a deep snapshot of the entire Local Data structure
            var snapshot = new UserTextParser.UserData();
            if (_projectRepo.LocalData != null)
            {
                foreach (var ach in _projectRepo.LocalData.Achievements) snapshot.Achievements.Add(ach.Clone(false));
                foreach (var lb in _projectRepo.LocalData.Leaderboards) snapshot.Leaderboards.Add(lb.Clone(false));
                foreach (var n in _projectRepo.LocalData.Notes) snapshot.Notes.Add(new CodeNote { Address = n.Address, Note = n.Note, User = n.User });
            }
            _mainHistory.PushState(snapshot);
        }

        private void UndoMain()
        {
            if (_projectRepo.LocalData == null) return;

            var previousState = _mainHistory.Undo(_projectRepo.LocalData);
            // Restore state
            _projectRepo.LocalData.Achievements = previousState.Achievements;
            _projectRepo.LocalData.Leaderboards = previousState.Leaderboards;
            _projectRepo.LocalData.Notes = previousState.Notes;

            ReloadDataView();
            LoadSelectedAsset();
        }

        private void RedoMain()
        {
            if (_projectRepo.LocalData == null) return;

            var nextState = _mainHistory.Redo(_projectRepo.LocalData);
            _projectRepo.LocalData.Achievements = nextState.Achievements;
            _projectRepo.LocalData.Leaderboards = nextState.Leaderboards;
            _projectRepo.LocalData.Notes = nextState.Notes;

            ReloadDataView();
            LoadSelectedAsset();
        }

        // Routes Undo commands to either the Trigger Editor (text editing) or Main Form (asset list)
        private void Undo_Click(object? sender, EventArgs e)
        {
            if (triggerEditor.ContainsFocus) triggerEditor.Undo();
            else UndoMain();
        }

        private void Redo_Click(object? sender, EventArgs e)
        {
            if (triggerEditor.ContainsFocus) triggerEditor.Redo();
            else RedoMain();
        }
        #endregion

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!ConfirmDiscardChanges()) return;

            using var ofd = new OpenFileDialog();
            ofd.Filter = "JSON Files (*.json)|*.json|Text Files (*.txt)|*.txt|All Files (*.*)|*.*";

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                LoadFiles(ofd.FileName);
            }
        }

        private void LoadFiles(string primaryPath)
        {
            ImageCache.Clear(); // Free up memory
            _recentFiles.Add(primaryPath);
            UpdateRecentMenu();

            _currentFilePath = primaryPath;
            _selectedAsset = null;
            achievementGrid.Rows.Clear();
            _mainHistory.Clear();
            triggerEditor.Clear();

            // Delegate heavy lifting to Repository
            _projectRepo.LoadFiles(primaryPath, out _currentUserFilePath);

            if (_projectRepo.ServerData != null)
                this.Text = $"RA Logic Editor - {_projectRepo.CurrentTitle}";

            // Default view to JSON unless specified
            if (cmbSource.SelectedItem?.ToString() != "JSON")
                cmbSource.SelectedItem = "JSON";

            ReloadDataView();
            codeNoteEditor.SetNotes(_projectRepo.LocalData?.Notes ?? new List<CodeNote>());
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_projectRepo.LocalData == null && _projectRepo.ServerData == null) return;

            string savePath = _currentUserFilePath;
            if (string.IsNullOrEmpty(savePath))
            {
                using var sfd = new SaveFileDialog { Filter = "Text Files (*.txt)|*.txt" };
                if (sfd.ShowDialog() != DialogResult.OK) return;
                savePath = sfd.FileName;
            }

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("1.0");
                sb.AppendLine(_projectRepo.ServerData?.Title ?? "Game Title");

                _projectRepo.EnsureLocalDataExists();

                // Get merged lists excluding fully synced items
                var mergedAchievements = _projectRepo.GetMergedList<RaAchievement>(false);
                var mergedLeaderboards = _projectRepo.GetMergedList<RaLeaderboard>(true);

                foreach (var a in mergedAchievements)
                {
                    if (a.State == AssetState.Synced) continue; // Don't save synced items to User.txt
                    sb.AppendLine($"{a.ID}:\"{a.Logic}\":{a.Title}:{a.Description}:::{a.Type}:{a.Author}:{a.Points}:::::{a.BadgeName}");
                }

                foreach (var l in mergedLeaderboards)
                {
                    if (l.State == AssetState.Synced) continue;
                    // Reformat Logic for LB string
                    var parts = l.Logic.Split(new[] { "::" }, StringSplitOptions.None);
                    string pSta = parts.Length > 0 ? parts[0].Replace("STA:", "") : "";
                    string pCan = parts.Length > 1 ? parts[1].Replace("CAN:", "") : "";
                    string pSub = parts.Length > 2 ? parts[2].Replace("SUB:", "") : "";
                    string pVal = parts.Length > 3 ? parts[3].Replace("VAL:", "") : "";
                    sb.AppendLine($"L{l.ID}:\"{pSta}\":\"{pCan}\":\"{pSub}\":\"{pVal}\":{l.Format}:{l.Title}:{l.Description}:{l.LowerIsBetter}");
                }

                // Save notes
                foreach (var n in _projectRepo.LocalData!.Notes)
                {
                    sb.AppendLine($"N0:{n.Address}:\"{n.Note.Replace("\r\n", "\\r\\n")}\"");
                }

                File.WriteAllText(savePath, sb.ToString());
                _currentUserFilePath = savePath;
                MessageBox.Show("Saved to " + savePath);
            }
            catch (Exception ex) { MessageBox.Show($"Error saving: {ex.Message}"); }
        }

        // Refreshes the main grid based on filters
        private void ReloadDataView()
        {
            int savedScroll = achievementGrid.FirstDisplayedScrollingRowIndex;
            int savedId = -1;
            if (achievementGrid.SelectedRows.Count > 0 && achievementGrid.SelectedRows[0].Tag is IAssetItem item)
                savedId = item.ID;

            PopulateList();

            // Restore selection
            if (savedId != -1)
            {
                foreach (DataGridViewRow row in achievementGrid.Rows)
                {
                    if (row.Tag is IAssetItem i && i.ID == savedId)
                    {
                        row.Selected = true;
                        achievementGrid.CurrentCell = row.Cells[0];
                        break;
                    }
                }
            }
            if (savedScroll >= 0 && savedScroll < achievementGrid.Rows.Count)
                try { achievementGrid.FirstDisplayedScrollingRowIndex = savedScroll; } catch { }
        }

        // Populates the grid based on Source Type (JSON, Local, New) and Asset Type
        private void PopulateList()
        {
            achievementGrid.Rows.Clear();
            _currentDisplayList.Clear();

            bool showLeaderboards = cmbAssetType.SelectedIndex == 1;
            string sourceMode = cmbSource.SelectedItem?.ToString() ?? "Local";

            if (sourceMode == "New")
            {
                // Show only items created locally that don't exist on server
                if (_projectRepo.LocalData != null)
                {
                    var list = showLeaderboards
                       ? _projectRepo.LocalData.Leaderboards.Cast<IAssetItem>()
                       : _projectRepo.LocalData.Achievements.Cast<IAssetItem>();

                    foreach (var item in list)
                    {
                        UpdateAssetState(item);
                        if (item.State == AssetState.Local) AddGridRow(item);
                    }
                }
            }
            else
            {
                // JSON or Local view
                var merged = showLeaderboards
                    ? _projectRepo.GetMergedList<RaLeaderboard>(true)
                    : _projectRepo.GetMergedList<RaAchievement>(false).Cast<IAssetItem>();

                foreach (var item in merged)
                {
                    // Keep JSON view clean: Hide purely local items (Green)
                    if (sourceMode == "JSON" && item.State == AssetState.Local) continue;

                    // Keep Local view focused on changes: Hide perfectly synced items
                    if (sourceMode == "Local" && item.State == AssetState.Synced) continue;

                    AddGridRow(item);
                }
            }
        }

        private void AddGridRow(IAssetItem item)
        {
            int idx = achievementGrid.Rows.Add(item.ID, item.Title);
            var row = achievementGrid.Rows[idx];
            row.Tag = item;
            UpdateRowColor(row, item);
            _currentDisplayList.Add(item);
        }

        private void UpdateRowColor(DataGridViewRow row, IAssetItem item)
        {
            if (item.State == AssetState.Modified) row.DefaultCellStyle.ForeColor = Color.Orange;
            else if (item.State == AssetState.Local) row.DefaultCellStyle.ForeColor = Color.Green;
            else row.DefaultCellStyle.ForeColor = Color.Empty;
        }

        // Loads the asset into the Trigger Editor
        private void LoadSelectedAsset()
        {
            if (achievementGrid.SelectedRows.Count > 0)
            {
                var row = achievementGrid.SelectedRows[0];
                if (row.Tag is IAssetItem asset)
                {
                    _isLoading = true;
                    // Create a detached clone if it's synced, so we don't edit the server data directly
                    // until we decide to save (which promotes it to Modified)
                    if (asset.State == AssetState.Synced)
                    {
                        IAssetItem clone;
                        if (asset is RaAchievement ra) clone = ra.Clone(false);
                        else if (asset is RaLeaderboard rl) clone = rl.Clone(false);
                        else return;
                        _selectedAsset = clone;
                        row.Tag = clone;
                    }
                    else
                    {
                        _selectedAsset = asset;
                    }
                    triggerEditor.BindAchievement(_selectedAsset);
                    _isLoading = false;
                }
            }
            UpdateSelectionUI();
        }

        // Compares item against server data to determine Sync state (Synced, Modified, Local)
        private void UpdateAssetState(IAssetItem item)
        {
            if (_projectRepo.ServerData == null) { item.State = AssetState.Local; return; }

            var serverSet = _projectRepo.ServerData.Sets;
            IAssetItem? serverItem = null;

            foreach (var s in serverSet)
            {
                if (item.IsLeaderboard) serverItem = s.Leaderboards.FirstOrDefault(x => x.ID == item.ID);
                else serverItem = s.Achievements.FirstOrDefault(x => x.ID == item.ID);
                if (serverItem != null) break;
            }

            if (serverItem == null) item.State = AssetState.Local;
            else
            {
                bool equal = item.Logic == serverItem.Logic && item.Title == serverItem.Title && item.Description == serverItem.Description;
                if (item is RaAchievement a && serverItem is RaAchievement b)
                {
                    if (a.Points != b.Points || a.Type != b.Type) equal = false;
                }
                item.State = equal ? AssetState.Synced : AssetState.Modified;
            }

            foreach (DataGridViewRow row in achievementGrid.Rows)
            {
                if (row.Tag == item) { UpdateRowColor(row, item); break; }
            }
        }

        // Callback from Trigger Editor: Logic string changed
        private void TriggerEditor_TriggerTextChanged(object? sender, EventArgs e)
        {
            if (_isLoading || _selectedAsset == null) return;
            _projectRepo.EnsureLocalDataExists();
            EnsureAssetIsLocal();
            _selectedAsset.Logic = triggerEditor.TriggerText;
            UpdateAssetState(_selectedAsset);
        }

        // Callback from Trigger Editor: Metadata (Title, Desc) changed
        private void TriggerEditor_AchievementDataChanged(object? sender, EventArgs e)
        {
            if (_isLoading || _selectedAsset == null) return;
            _projectRepo.EnsureLocalDataExists();
            EnsureAssetIsLocal();
            foreach (DataGridViewRow row in achievementGrid.Rows)
            {
                if (row.Tag == _selectedAsset) { row.Cells[1].Value = _selectedAsset.Title; break; }
            }
            UpdateAssetState(_selectedAsset);
        }

        // Adds the asset to LocalData list if it isn't there already (promotes from Synced to Modified)
        private void EnsureAssetIsLocal()
        {
            if (_selectedAsset == null || _projectRepo.LocalData == null) return;
            if (_selectedAsset.State == AssetState.Local || _selectedAsset.State == AssetState.Modified) return;

            if (_selectedAsset is RaAchievement ra)
            {
                if (!_projectRepo.LocalData.Achievements.Any(a => a.ID == ra.ID))
                    _projectRepo.LocalData.Achievements.Add(ra);
            }
            else if (_selectedAsset is RaLeaderboard rl)
            {
                if (!_projectRepo.LocalData.Leaderboards.Any(l => l.ID == rl.ID))
                    _projectRepo.LocalData.Leaderboards.Add(rl);
            }
        }

        // Resets a Modified asset back to Synced state (removes from LocalData)
        private void TriggerEditor_AssetResetRequested(object? sender, EventArgs e)
        {
            if (_selectedAsset == null || _projectRepo.LocalData == null) return;
            PushMainUndo();

            if (_selectedAsset.IsLeaderboard)
            {
                var toRemove = _projectRepo.LocalData.Leaderboards.FirstOrDefault(l => l.ID == _selectedAsset.ID);
                if (toRemove != null) _projectRepo.LocalData.Leaderboards.Remove(toRemove);
            }
            else
            {
                var toRemove = _projectRepo.LocalData.Achievements.FirstOrDefault(a => a.ID == _selectedAsset.ID);
                if (toRemove != null) _projectRepo.LocalData.Achievements.Remove(toRemove);
            }

            ReloadDataView();
            LoadSelectedAsset();
        }

        // Action: Create New Asset
        private void NewAsset_Click(object? sender, EventArgs e)
        {
            _projectRepo.EnsureLocalDataExists();
            PushMainUndo();

            // Generate ID starting at 111000001
            int newId = 111000001;
            var allIds = _currentDisplayList.Select(x => x.ID).ToList();
            if (allIds.Any())
            {
                int max = allIds.Max();
                newId = (max < 111000000) ? 111000001 : max + 1;
            }

            bool leaderboard = cmbAssetType.SelectedIndex == 1;
            if (leaderboard)
            {
                var lb = new RaLeaderboard { ID = newId, Title = "New Leaderboard", State = AssetState.Local };
                _projectRepo.LocalData!.Leaderboards.Add(lb);
            }
            else
            {
                var ach = new RaAchievement { ID = newId, Title = "New Achievement", State = AssetState.Local };
                _projectRepo.LocalData!.Achievements.Add(ach);
            }

            // Automatically switch to "New" view if not already there
            if (cmbSource.SelectedItem?.ToString() != "New")
                cmbSource.SelectedItem = "New";
            else
                ReloadDataView();

            // Select new item
            foreach (DataGridViewRow row in achievementGrid.Rows)
            {
                if ((int)row.Cells[0].Value == newId)
                {
                    row.Selected = true;
                    LoadSelectedAsset();
                    break;
                }
            }
        }

        // Action: Duplicate selected assets
        private void DuplicateAsset_Click(object? sender, EventArgs e)
        {
            if (achievementGrid.SelectedRows.Count == 0) return;
            _projectRepo.EnsureLocalDataExists();
            PushMainUndo();

            var selectedRows = achievementGrid.SelectedRows.Cast<DataGridViewRow>().Where(r => r.Tag is IAssetItem).OrderBy(r => r.Index).ToList();
            int nextId = 111000001;
            var allIds = _currentDisplayList.Select(x => x.ID).ToList();
            if (allIds.Any()) { int max = allIds.Max(); nextId = (max >= 111000000) ? max + 1 : 111000001; }

            List<int> newIds = new List<int>();
            foreach (var row in selectedRows)
            {
                if (row.Tag is IAssetItem source)
                {
                    if (source is RaAchievement ra)
                    {
                        var c = ra.Clone(); c.Title += " (Copy)"; c.ID = nextId++; c.State = AssetState.Local;
                        _projectRepo.LocalData!.Achievements.Add(c); newIds.Add(c.ID);
                    }
                    else if (source is RaLeaderboard rl)
                    {
                        var c = rl.Clone(); c.Title += " (Copy)"; c.ID = nextId++; c.State = AssetState.Local;
                        _projectRepo.LocalData!.Leaderboards.Add(c); newIds.Add(c.ID);
                    }
                }
            }

            // Switch to "New" view so we can see the duplicated items (Green), as JSON view hides them
            if (cmbSource.SelectedItem?.ToString() == "JSON")
            {
                cmbSource.SelectedItem = "New";
            }
            else
            {
                ReloadDataView();
            }

            // --- Restore Selection & Focus Logic ---
            // 1. Clear Selection
            achievementGrid.ClearSelection();

            // 2. Identify indices of new items
            List<int> indicesToSelect = new List<int>();
            foreach (DataGridViewRow row in achievementGrid.Rows)
            {
                if (row.Tag is IAssetItem item && newIds.Contains(item.ID))
                {
                    indicesToSelect.Add(row.Index);
                }
            }

            if (indicesToSelect.Count > 0)
            {
                int firstIdx = indicesToSelect.Min();
                // 3. Set CurrentCell to force grid focus and scroll
                achievementGrid.CurrentCell = achievementGrid.Rows[firstIdx].Cells[0];

                // 4. Select all duplicated rows
                foreach (int idx in indicesToSelect)
                {
                    achievementGrid.Rows[idx].Selected = true;
                }
            }
        }

        // Action: Delete local assets
        private void DeleteAsset_Click(object? sender, EventArgs e)
        {
            if (achievementGrid.SelectedRows.Count == 0 || _projectRepo.LocalData == null) return;
            PushMainUndo();
            foreach (DataGridViewRow row in achievementGrid.SelectedRows)
            {
                if (row.Tag is IAssetItem item && item.State == AssetState.Local)
                {
                    if (item is RaAchievement ra) _projectRepo.LocalData.Achievements.Remove(ra);
                    else if (item is RaLeaderboard rl) _projectRepo.LocalData.Leaderboards.Remove(rl);
                }
            }
            ReloadDataView();
        }

        // Action: Reset modified assets
        private void ResetAsset_Click(object? sender, EventArgs e)
        {
            if (achievementGrid.SelectedRows.Count == 0 || _projectRepo.LocalData == null) return;
            PushMainUndo();

            foreach (DataGridViewRow row in achievementGrid.SelectedRows)
            {
                if (row.Tag is IAssetItem item && item.State == AssetState.Modified)
                {
                    if (item is RaAchievement ra)
                    {
                        var loc = _projectRepo.LocalData.Achievements.FirstOrDefault(x => x.ID == ra.ID);
                        if (loc != null) _projectRepo.LocalData.Achievements.Remove(loc);
                    }
                    else if (item is RaLeaderboard rl)
                    {
                        var loc = _projectRepo.LocalData.Leaderboards.FirstOrDefault(x => x.ID == rl.ID);
                        if (loc != null) _projectRepo.LocalData.Leaderboards.Remove(loc);
                    }
                }
            }
            ReloadDataView();
            LoadSelectedAsset();
        }

        private void MoveAssetUp_Click(object? sender, EventArgs e) => MoveAsset(-1);
        private void MoveAssetDown_Click(object? sender, EventArgs e) => MoveAsset(1);

        // Action: Reorder local assets
        private void MoveAsset(int direction)
        {
            if (achievementGrid.SelectedRows.Count == 0 || _projectRepo.LocalData == null) return;

            var selectedRows = achievementGrid.SelectedRows.Cast<DataGridViewRow>().OrderBy(r => r.Index).ToList();
            if (direction > 0) selectedRows.Reverse();

            if (direction < 0 && selectedRows.Min(r => r.Index) == 0) return;
            if (direction > 0 && selectedRows.Max(r => r.Index) >= achievementGrid.Rows.Count - 1) return;

            PushMainUndo();
            bool changed = false;
            List<int> movedIds = new(); // Track IDs to restore selection

            foreach (var row in selectedRows)
            {
                if (row.Tag is IAssetItem item && item.State == AssetState.Local)
                {
                    int currentIdx = row.Index;
                    int targetIdx = currentIdx + direction;

                    if (targetIdx < 0 || targetIdx >= achievementGrid.Rows.Count) continue;

                    var targetRow = achievementGrid.Rows[targetIdx];
                    if (targetRow.Tag is IAssetItem targetItem && targetItem.State == AssetState.Local)
                    {
                        // Swap position in data list
                        if (item.IsLeaderboard) SwapInList(_projectRepo.LocalData.Leaderboards, item.ID, targetItem.ID);
                        else SwapInList(_projectRepo.LocalData.Achievements, item.ID, targetItem.ID);

                        // Swap IDs to maintain sequence
                        int tempId = item.ID; item.ID = targetItem.ID; targetItem.ID = tempId;
                        changed = true;
                        movedIds.Add(item.ID);
                    }
                }
            }

            if (changed)
            {
                int savedScroll = achievementGrid.FirstDisplayedScrollingRowIndex;
                ReloadDataView();

                // --- Restore Selection Logic ---
                achievementGrid.ClearSelection();
                List<int> indicesToSelect = new List<int>();

                foreach (DataGridViewRow row in achievementGrid.Rows)
                {
                    if (row.Tag is IAssetItem item && movedIds.Contains(item.ID))
                    {
                        indicesToSelect.Add(row.Index);
                    }
                }

                if (indicesToSelect.Count > 0)
                {
                    int firstIdx = indicesToSelect.Min();
                    achievementGrid.CurrentCell = achievementGrid.Rows[firstIdx].Cells[0];
                    foreach (int idx in indicesToSelect) achievementGrid.Rows[idx].Selected = true;
                }

                if (savedScroll >= 0 && savedScroll < achievementGrid.Rows.Count)
                    try { achievementGrid.FirstDisplayedScrollingRowIndex = savedScroll; } catch { }
            }
        }

        private void SwapInList<T>(List<T> list, int idA, int idB) where T : IAssetItem
        {
            var indexA = list.FindIndex(x => x.ID == idA);
            var indexB = list.FindIndex(x => x.ID == idB);
            if (indexA != -1 && indexB != -1)
            {
                var temp = list[indexA];
                list[indexA] = list[indexB];
                list[indexB] = temp;
            }
        }

        private void achievementGrid_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                LoadSelectedAsset();
                e.Handled = true;
            }
            else if (e.Alt && e.KeyCode == Keys.Up)
            {
                MoveAsset(-1);
                e.Handled = true;
            }
            else if (e.Alt && e.KeyCode == Keys.Down)
            {
                MoveAsset(1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                DeleteAsset_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.D)
            {
                DuplicateAsset_Click(sender, e);
                e.Handled = true;
            }
        }

        private void AchievementContextMenu_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            UpdateSelectionUI();
            bool hasSelection = achievementGrid.SelectedRows.Count > 0;
            bool allLocal = true;
            bool anyModified = false;

            foreach (DataGridViewRow row in achievementGrid.SelectedRows)
            {
                if (row.Tag is IAssetItem asset)
                {
                    if (asset.State != AssetState.Local) allLocal = false;
                    if (asset.State == AssetState.Modified) anyModified = true;
                }
            }

            cloneAssetToolStripMenuItem.Enabled = hasSelection;
            deleteAssetToolStripMenuItem.Enabled = hasSelection && allLocal;
            resetAssetToolStripMenuItem.Enabled = hasSelection && anyModified;
            moveUpAssetToolStripMenuItem.Enabled = hasSelection && allLocal;
            moveDownAssetToolStripMenuItem.Enabled = hasSelection && allLocal;
        }

        private void UpdateSelectionUI()
        {
            bool hasSelection = achievementGrid.SelectedRows.Count > 0;
            bool allLocal = true;
            foreach (DataGridViewRow row in achievementGrid.SelectedRows)
            {
                if (row.Tag is IAssetItem asset && asset.State != AssetState.Local) allLocal = false;
            }
            btnDuplicateAsset.Enabled = hasSelection;
            btnDeleteAsset.Enabled = hasSelection && allLocal;
        }

        private void UpdateRecentMenu()
        {
            recentToolStripMenuItem.DropDownItems.Clear();
            foreach (var file in _recentFiles.Files)
            {
                var item = new ToolStripMenuItem(file);
                item.Click += (s, args) => LoadFiles(file);
                recentToolStripMenuItem.DropDownItems.Add(item);
            }
        }
    }
}