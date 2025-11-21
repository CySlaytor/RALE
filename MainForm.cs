#nullable enable
using RALogicEditor.Controls;
using RALogicEditor.Models;
using RALogicEditor.Parsers;
using RARPEditor.Helpers;
using RARPEditor.Models;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace RALogicEditor
{
    public partial class MainForm : Form
    {
        private RaRoot? _serverData;
        private UserTextParser.UserData? _localData;

        private IAssetItem? _selectedAsset;
        private bool _isLoading;
        private string _currentFilePath = "";
        private string _currentUserFilePath = "";
        private string _lastLoadedPath = "";

        private RecentFilesManager _recentFiles = new RecentFilesManager();
        private List<IAssetItem> _currentDisplayList = new();

        // Main Undo/Redo stacks for asset management
        private Stack<UserTextParser.UserData> _mainUndoStack = new();
        private Stack<UserTextParser.UserData> _mainRedoStack = new();

        public MainForm()
        {
            InitializeComponent();
            triggerEditor.TriggerTextChanged += TriggerEditor_TriggerTextChanged;
            triggerEditor.AchievementDataChanged += TriggerEditor_AchievementDataChanged;
            triggerEditor.AssetResetRequested += TriggerEditor_AssetResetRequested;
            triggerEditor.AddFromCodeNoteRequested += TriggerEditor_AddFromCodeNoteRequested;

            codeNoteEditor.ReloadRequested += Reload_Click;

            achievementGrid.CellDoubleClick += achievementGrid_CellDoubleClick;
            achievementGrid.KeyDown += achievementGrid_KeyDown;

            cmbAssetType.SelectedIndex = 0;
            cmbSource.SelectedIndex = 0;
            cmbAssetType.SelectedIndexChanged += (s, e) => PopulateList();
            cmbSource.SelectedIndexChanged += (s, e) => { ReloadDataView(); };

            newAssetToolStripMenuItem.Click += NewAsset_Click;
            cloneAssetToolStripMenuItem.Click += DuplicateAsset_Click;
            deleteAssetToolStripMenuItem.Click += DeleteAsset_Click;
            resetAssetToolStripMenuItem.Click += ResetAsset_Click;
            moveUpAssetToolStripMenuItem.Click += MoveAssetUp_Click;
            moveDownAssetToolStripMenuItem.Click += MoveAssetDown_Click;

            btnAddAsset.Click += NewAsset_Click;
            btnDuplicateAsset.Click += DuplicateAsset_Click;
            btnDeleteAsset.Click += DeleteAsset_Click;

            achievementGrid.ContextMenuStrip.Opening += AchievementContextMenu_Opening;

            // Hook closing event for unsaved check
            this.FormClosing += MainForm_FormClosing;

            _recentFiles.Load();
            UpdateRecentMenu();
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (!ConfirmDiscardChanges())
            {
                e.Cancel = true;
            }
        }

        private bool ConfirmDiscardChanges()
        {
            if (_localData == null) return true;

            // Check for any modified or local items
            bool hasChanges = _localData.Achievements.Any(a => a.State != AssetState.Synced) ||
                              _localData.Leaderboards.Any(l => l.State != AssetState.Synced);

            if (hasChanges)
            {
                var result = MessageBox.Show("You have unsaved changes. Any unsaved local changes will be lost.\n\nContinue?",
                    "Unsaved Changes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                return result == DialogResult.Yes;
            }
            return true;
        }

        private void Reload_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_lastLoadedPath)) return;
            if (!ConfirmDiscardChanges()) return;

            int selectedId = -1;
            if (achievementGrid.SelectedRows.Count > 0 && achievementGrid.SelectedRows[0].Tag is IAssetItem item)
            {
                selectedId = item.ID;
            }

            LoadFiles(_lastLoadedPath);

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

        #region Main Undo/Redo Logic
        private UserTextParser.UserData CreateSnapshot()
        {
            var snapshot = new UserTextParser.UserData();
            if (_localData != null)
            {
                foreach (var ach in _localData.Achievements) snapshot.Achievements.Add(ach.Clone(false));
                foreach (var lb in _localData.Leaderboards) snapshot.Leaderboards.Add(lb.Clone(false));
                foreach (var n in _localData.Notes) snapshot.Notes.Add(new CodeNote { Address = n.Address, Note = n.Note, User = n.User });
            }
            return snapshot;
        }

        private void PushMainUndo()
        {
            _mainUndoStack.Push(CreateSnapshot());
            _mainRedoStack.Clear();
        }

        private void UndoMain()
        {
            if (_mainUndoStack.Count == 0) return;
            _mainRedoStack.Push(CreateSnapshot());
            _localData = _mainUndoStack.Pop();
            ReloadDataView();
            LoadSelectedAsset();
        }

        private void RedoMain()
        {
            if (_mainRedoStack.Count == 0) return;
            _mainUndoStack.Push(CreateSnapshot());
            _localData = _mainRedoStack.Pop();
            ReloadDataView();
            LoadSelectedAsset();
        }
        #endregion

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
            // Clear image cache to prevent memory leaks
            ImageCache.Clear();

            _recentFiles.Add(primaryPath);
            UpdateRecentMenu();

            _lastLoadedPath = primaryPath;

            _serverData = null;
            _localData = null;
            _selectedAsset = null;
            _currentFilePath = "";
            _currentUserFilePath = "";
            achievementGrid.Rows.Clear();
            _mainUndoStack.Clear();
            _mainRedoStack.Clear();

            triggerEditor.Clear();

            string dir = Path.GetDirectoryName(primaryPath) ?? "";
            string fileName = Path.GetFileNameWithoutExtension(primaryPath);

            string gameIdStr = new string(fileName.TakeWhile(char.IsDigit).ToArray());
            if (string.IsNullOrEmpty(gameIdStr))
            {
                var digits = fileName.Where(char.IsDigit).ToArray();
                if (digits.Length > 0) gameIdStr = new string(digits);
                else gameIdStr = fileName;
            }

            string jsonPath = Path.Combine(dir, $"{gameIdStr}.json");
            string userPath = Path.Combine(dir, $"{gameIdStr}-User.txt");
            string notesPath = Path.Combine(dir, $"{gameIdStr}-Notes.json");

            string selectedExt = Path.GetExtension(primaryPath).ToLower();

            if (selectedExt == ".json")
            {
                if (primaryPath.EndsWith("-Notes.json", StringComparison.OrdinalIgnoreCase))
                    notesPath = primaryPath;
                else
                    jsonPath = primaryPath;
            }
            else if (selectedExt == ".txt")
            {
                userPath = primaryPath;
            }

            if (File.Exists(jsonPath)) LoadLogic(jsonPath);
            if (File.Exists(userPath)) LoadUserFile(userPath);

            List<CodeNote> jsonNotes = new();
            if (File.Exists(notesPath))
            {
                try
                {
                    string jsonContent = File.ReadAllText(notesPath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    };
                    jsonNotes = JsonSerializer.Deserialize<List<CodeNote>>(jsonContent, options) ?? new();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error loading notes: " + ex.Message);
                }
            }

            var mergedNotesMap = new Dictionary<string, CodeNote>();

            foreach (var n in jsonNotes)
            {
                if (!mergedNotesMap.ContainsKey(n.Address))
                    mergedNotesMap[n.Address] = n;
            }

            if (_localData != null && _localData.Notes != null)
            {
                foreach (var n in _localData.Notes)
                {
                    mergedNotesMap[n.Address] = n;
                }
            }

            var finalNotesList = mergedNotesMap.Values.OrderBy(n => n.Address).ToList();
            codeNoteEditor.SetNotes(finalNotesList);

            if (cmbSource.SelectedItem?.ToString() != "JSON")
                cmbSource.SelectedItem = "JSON";

            ReloadDataView();
        }

        private void LoadLogic(string filepath)
        {
            try
            {
                string jsonContent = File.ReadAllText(filepath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };
                _serverData = JsonSerializer.Deserialize<RaRoot>(jsonContent, options);

                if (_serverData == null) return;
                if (_serverData.Sets == null) _serverData.Sets = new List<RaSet>();

                _currentFilePath = filepath;
                this.Text = $"RA Logic Editor - {_serverData.Title} ({_serverData.GameId})";
            }
            catch (Exception ex) { MessageBox.Show($"Error loading logic: {ex.Message}"); }
        }

        private void LoadUserFile(string filepath)
        {
            try
            {
                _localData = UserTextParser.ParseUserFile(filepath);
                _currentUserFilePath = filepath;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading user file: {ex.Message}");
            }
        }

        private void ReloadDataView()
        {
            int savedScroll = achievementGrid.FirstDisplayedScrollingRowIndex;
            int savedId = -1;
            if (achievementGrid.SelectedRows.Count > 0 && achievementGrid.SelectedRows[0].Tag is IAssetItem item)
                savedId = item.ID;

            PopulateList();

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
            {
                try { achievementGrid.FirstDisplayedScrollingRowIndex = savedScroll; } catch { }
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_localData == null && _serverData == null) return;

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
                sb.AppendLine(_serverData?.Title ?? "Game Title");

                if (_localData == null) _localData = new UserTextParser.UserData();

                var mergedAchievements = GetMergedList<RaAchievement>(false);
                var mergedLeaderboards = GetMergedList<RaLeaderboard>(true);

                foreach (var a in mergedAchievements)
                {
                    if (a.State == AssetState.Synced) continue;
                    sb.AppendLine($"{a.ID}:\"{a.Logic}\":{a.Title}:{a.Description}:::{a.Type}:{a.Author}:{a.Points}:::::{a.BadgeName}");
                }

                foreach (var l in mergedLeaderboards)
                {
                    if (l.State == AssetState.Synced) continue;
                    var parts = l.Logic.Split(new[] { "::" }, StringSplitOptions.None);
                    string pSta = parts.Length > 0 ? parts[0].Replace("STA:", "") : "";
                    string pCan = parts.Length > 1 ? parts[1].Replace("CAN:", "") : "";
                    string pSub = parts.Length > 2 ? parts[2].Replace("SUB:", "") : "";
                    string pVal = parts.Length > 3 ? parts[3].Replace("VAL:", "") : "";
                    sb.AppendLine($"L{l.ID}:\"{pSta}\":\"{pCan}\":\"{pSub}\":\"{pVal}\":{l.Format}:{l.Title}:{l.Description}:{l.LowerIsBetter}");
                }

                var notesToSave = _localData?.Notes ?? new List<CodeNote>();
                foreach (var n in notesToSave)
                {
                    sb.AppendLine($"N0:{n.Address}:\"{n.Note.Replace("\r\n", "\\r\\n")}\"");
                }

                File.WriteAllText(savePath, sb.ToString());
                MessageBox.Show("Saved to " + savePath);
            }
            catch (Exception ex) { MessageBox.Show($"Error saving: {ex.Message}"); }
        }

        private void PopulateList()
        {
            achievementGrid.Rows.Clear();
            _currentDisplayList.Clear();

            bool showLeaderboards = cmbAssetType.SelectedIndex == 1;
            string sourceMode = cmbSource.SelectedItem?.ToString() ?? "Local";

            if (sourceMode == "New")
            {
                foreach (DataGridViewColumn col in achievementGrid.Columns) col.SortMode = DataGridViewColumnSortMode.NotSortable;
            }
            else
            {
                foreach (DataGridViewColumn col in achievementGrid.Columns) col.SortMode = DataGridViewColumnSortMode.Automatic;
            }

            if (sourceMode == "New")
            {
                if (_localData != null)
                {
                    if (showLeaderboards)
                    {
                        var list = _localData.Leaderboards.Cast<IAssetItem>().ToList();
                        foreach (var item in list)
                        {
                            UpdateAssetState(item);
                            if (item.State == AssetState.Local) AddGridRow(item);
                        }
                    }
                    else
                    {
                        var list = _localData.Achievements.Cast<IAssetItem>().ToList();
                        foreach (var item in list)
                        {
                            UpdateAssetState(item);
                            if (item.State == AssetState.Local) AddGridRow(item);
                        }
                    }
                }
            }
            else if (sourceMode == "Local")
            {
                if (showLeaderboards)
                {
                    var merged = GetMergedList<RaLeaderboard>(true);
                    foreach (var item in merged)
                    {
                        if (item.State == AssetState.Synced) continue;
                        AddGridRow(item);
                    }
                }
                else
                {
                    var merged = GetMergedList<RaAchievement>(false);
                    foreach (var item in merged)
                    {
                        if (item.State == AssetState.Synced) continue;
                        AddGridRow(item);
                    }
                }
            }
            else
            {
                if (showLeaderboards)
                {
                    var merged = GetMergedList<RaLeaderboard>(true);
                    foreach (var item in merged)
                    {
                        AddGridRow(item);
                    }
                }
                else
                {
                    var merged = GetMergedList<RaAchievement>(false);
                    foreach (var item in merged)
                    {
                        AddGridRow(item);
                    }
                }
            }
        }

        private List<T> GetMergedList<T>(bool leaderboard) where T : IAssetItem
        {
            var list = new List<T>();
            if (_serverData == null && _localData == null) return list;

            var serverItems = _serverData?.Sets.SelectMany(s => leaderboard ? s.Leaderboards.Cast<T>() : s.Achievements.Cast<T>()).ToDictionary(i => i.ID) ?? new Dictionary<int, T>();
            var localItems = (leaderboard ? _localData?.Leaderboards.Cast<T>() : _localData?.Achievements.Cast<T>()) ?? Enumerable.Empty<T>();

            var processedIds = new HashSet<int>();
            foreach (var loc in localItems)
            {
                if (serverItems.ContainsKey(loc.ID))
                {
                    var srv = serverItems[loc.ID];
                    if (AreAssetsEqual(loc, srv))
                        loc.State = AssetState.Synced;
                    else
                        loc.State = AssetState.Modified;
                }
                else
                {
                    loc.State = AssetState.Local;
                }
                list.Add(loc);
                processedIds.Add(loc.ID);
            }

            foreach (var srv in serverItems.Values)
            {
                if (!processedIds.Contains(srv.ID))
                {
                    srv.State = AssetState.Synced;
                    list.Add(srv);
                }
            }

            return list.OrderBy(x => x.ID).ToList();
        }

        private bool AreAssetsEqual(IAssetItem a, IAssetItem b)
        {
            if (a.Title != b.Title) return false;
            if (a.Description != b.Description) return false;
            if (a.Logic != b.Logic) return false;

            if (a is RaAchievement aa && b is RaAchievement bb)
            {
                if (aa.Points != bb.Points) return false;
                if (aa.Type != bb.Type) return false;
            }
            return true;
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

        private void UpdateAssetState(IAssetItem item)
        {
            if (_serverData == null)
            {
                item.State = AssetState.Local;
                return;
            }

            var allServerItems = _serverData.Sets.SelectMany(s => item.IsLeaderboard ? (IEnumerable<IAssetItem>)s.Leaderboards : s.Achievements);
            var serverItem = allServerItems.FirstOrDefault(x => x.ID == item.ID);

            if (serverItem == null)
            {
                item.State = AssetState.Local;
            }
            else
            {
                item.State = AreAssetsEqual(item, serverItem) ? AssetState.Synced : AssetState.Modified;
            }

            foreach (DataGridViewRow row in achievementGrid.Rows)
            {
                if (row.Tag == item)
                {
                    UpdateRowColor(row, item);
                    break;
                }
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

        private void MoveAsset(int direction)
        {
            if (achievementGrid.SelectedRows.Count == 0) return;
            if (_localData == null) return;

            var selectedRows = achievementGrid.SelectedRows.Cast<DataGridViewRow>().ToList();
            if (selectedRows.Any(r => (r.Tag as IAssetItem)?.State != AssetState.Local)) return;

            selectedRows.Sort((a, b) => a.Index.CompareTo(b.Index));

            bool canMove = true;
            if (direction < 0)
            {
                if (selectedRows.First().Index == 0) canMove = false;
            }
            else
            {
                if (selectedRows.Last().Index >= achievementGrid.Rows.Count - 1) canMove = false;
                selectedRows.Reverse();
            }

            if (!canMove) return;

            PushMainUndo();

            List<int> newSelectedIndices = new List<int>();

            foreach (var row in selectedRows)
            {
                var item = row.Tag as IAssetItem;
                int currentIdx = row.Index;
                int targetIdx = currentIdx + direction;

                var targetRow = achievementGrid.Rows[targetIdx];
                var targetItem = targetRow.Tag as IAssetItem;

                if (targetItem?.State == AssetState.Local && item != null)
                {
                    if (item.IsLeaderboard) SwapInList(_localData.Leaderboards, item.ID, targetItem.ID);
                    else SwapInList(_localData.Achievements, item.ID, targetItem.ID);

                    int tempId = item.ID;
                    item.ID = targetItem.ID;
                    targetItem.ID = tempId;

                    newSelectedIndices.Add(targetIdx);
                }
                else
                {
                    newSelectedIndices.Add(currentIdx);
                }
            }

            ReloadDataView();

            achievementGrid.ClearSelection();
            foreach (int idx in newSelectedIndices)
            {
                if (idx >= 0 && idx < achievementGrid.Rows.Count)
                    achievementGrid.Rows[idx].Selected = true;
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

        private void LoadSelectedAsset()
        {
            if (achievementGrid.SelectedRows.Count > 0)
            {
                var row = achievementGrid.SelectedRows[0];
                if (row.Tag is IAssetItem asset)
                {
                    _isLoading = true;
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
                    string setType = "Core";
                    if (_selectedAsset is RaAchievement ra2) setType = ra2.Type;
                    triggerEditor.BindAchievement(_selectedAsset, setType);
                    _isLoading = false;
                }
            }
            UpdateSelectionUI();
        }

        private void EnsureAssetIsLocal()
        {
            if (_selectedAsset == null || _localData == null) return;
            if (_selectedAsset.State == AssetState.Local || _selectedAsset.State == AssetState.Modified) return;

            if (_selectedAsset is RaAchievement ra)
            {
                if (!_localData.Achievements.Any(a => a.ID == ra.ID))
                    _localData.Achievements.Add(ra);
            }
            else if (_selectedAsset is RaLeaderboard rl)
            {
                if (!_localData.Leaderboards.Any(l => l.ID == rl.ID))
                    _localData.Leaderboards.Add(rl);
            }
            UpdateAssetState(_selectedAsset);
        }

        private void TriggerEditor_TriggerTextChanged(object? sender, EventArgs e)
        {
            if (_isLoading || _selectedAsset == null) return;
            if (_localData == null) _localData = new UserTextParser.UserData();
            EnsureAssetIsLocal();
            _selectedAsset.Logic = triggerEditor.TriggerText;
            UpdateAssetState(_selectedAsset);
        }

        private void TriggerEditor_AchievementDataChanged(object? sender, EventArgs e)
        {
            if (_isLoading || _selectedAsset == null) return;
            if (_localData == null) _localData = new UserTextParser.UserData();
            EnsureAssetIsLocal();
            foreach (DataGridViewRow row in achievementGrid.Rows)
            {
                if (row.Tag == _selectedAsset)
                {
                    row.Cells[1].Value = _selectedAsset.Title;
                    break;
                }
            }
            UpdateAssetState(_selectedAsset);
        }

        private void TriggerEditor_AssetResetRequested(object? sender, EventArgs e)
        {
            if (_selectedAsset == null || _serverData == null) return;
            var allServerItems = _serverData.Sets.SelectMany(s => _selectedAsset.IsLeaderboard ? (IEnumerable<IAssetItem>)s.Leaderboards : s.Achievements);
            var serverItem = allServerItems.FirstOrDefault(x => x.ID == _selectedAsset.ID);

            if (serverItem != null)
            {
                if (_localData != null)
                {
                    PushMainUndo();

                    if (_selectedAsset.IsLeaderboard)
                    {
                        var toRemove = _localData.Leaderboards.FirstOrDefault(l => l.ID == _selectedAsset.ID);
                        if (toRemove != null) _localData.Leaderboards.Remove(toRemove);
                    }
                    else
                    {
                        var toRemove = _localData.Achievements.FirstOrDefault(a => a.ID == _selectedAsset.ID);
                        if (toRemove != null) _localData.Achievements.Remove(toRemove);
                    }
                }
                int currentId = _selectedAsset.ID;

                ReloadDataView();

                if (achievementGrid.SelectedRows.Count == 0 || (achievementGrid.SelectedRows[0].Tag as IAssetItem)?.ID != currentId)
                {
                    _selectedAsset = null;
                }
                else
                {
                    LoadSelectedAsset();
                }
            }
        }

        private void NewAsset_Click(object? sender, EventArgs e)
        {
            if (_localData == null) _localData = new UserTextParser.UserData();

            PushMainUndo();

            int newId = 111000001;
            var allIds = _currentDisplayList.Select(x => x.ID).ToList();
            if (allIds.Any())
            {
                int max = allIds.Max();
                if (max < 111000000) max = 111000000;
                newId = max + 1;
            }
            bool leaderboard = cmbAssetType.SelectedIndex == 1;
            IAssetItem newItem;
            if (leaderboard) { var lb = new RaLeaderboard { ID = newId, Title = "New Leaderboard", State = AssetState.Local }; _localData.Leaderboards.Add(lb); newItem = lb; }
            else { var ach = new RaAchievement { ID = newId, Title = "New Achievement", State = AssetState.Local }; _localData.Achievements.Add(ach); newItem = ach; }

            if (cmbSource.SelectedItem?.ToString() != "New")
            {
                cmbSource.SelectedItem = "New";
            }
            else
            {
                ReloadDataView();
            }

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

        private void DuplicateAsset_Click(object? sender, EventArgs e)
        {
            if (achievementGrid.SelectedRows.Count == 0) return;
            if (_localData == null) _localData = new UserTextParser.UserData();

            PushMainUndo();

            var selectedRows = achievementGrid.SelectedRows.Cast<DataGridViewRow>()
                .Where(r => r.Tag is IAssetItem)
                .OrderBy(r => r.Index)
                .ToList();

            if (selectedRows.Count == 0) return;

            int nextId = 111000001;
            var allIds = _currentDisplayList.Select(x => x.ID).ToList();
            if (allIds.Any())
            {
                int max = allIds.Max();
                if (max >= 111000000) nextId = max + 1;
                else nextId = 111000001;
            }

            List<int> newIds = new List<int>();

            foreach (var row in selectedRows)
            {
                if (row.Tag is IAssetItem source)
                {
                    IAssetItem? clone = null;
                    if (source is RaAchievement ra)
                    {
                        var c = ra.Clone();
                        c.Title += " (Copy)";
                        _localData.Achievements.Add(c);
                        clone = c;
                    }
                    else if (source is RaLeaderboard rl)
                    {
                        var c = rl.Clone();
                        c.Title += " (Copy)";
                        _localData.Leaderboards.Add(c);
                        clone = c;
                    }

                    if (clone != null)
                    {
                        clone.ID = nextId++;
                        clone.State = AssetState.Local;
                        newIds.Add(clone.ID);
                    }
                }
            }

            ReloadDataView();

            achievementGrid.ClearSelection();
            int firstNewIndex = -1;
            foreach (DataGridViewRow row in achievementGrid.Rows)
            {
                if (row.Tag is IAssetItem item && newIds.Contains(item.ID))
                {
                    row.Selected = true;
                    if (firstNewIndex == -1) firstNewIndex = row.Index;
                }
            }

            if (firstNewIndex != -1)
            {
                try { achievementGrid.FirstDisplayedScrollingRowIndex = firstNewIndex; } catch { }
            }
        }

        private void ResetAsset_Click(object? sender, EventArgs e)
        {
            if (achievementGrid.SelectedRows.Count == 0 || _localData == null) return;

            var modifiedRows = achievementGrid.SelectedRows.Cast<DataGridViewRow>()
                .Where(r => (r.Tag as IAssetItem)?.State == AssetState.Modified)
                .ToList();

            if (modifiedRows.Count == 0) return;

            PushMainUndo();

            foreach (var row in modifiedRows)
            {
                if (row.Tag is IAssetItem item)
                {
                    if (item.IsLeaderboard)
                    {
                        var toRemove = _localData.Leaderboards.FirstOrDefault(l => l.ID == item.ID);
                        if (toRemove != null) _localData.Leaderboards.Remove(toRemove);
                    }
                    else
                    {
                        var toRemove = _localData.Achievements.FirstOrDefault(a => a.ID == item.ID);
                        if (toRemove != null) _localData.Achievements.Remove(toRemove);
                    }
                }
            }

            ReloadDataView();
            LoadSelectedAsset();
        }

        private void DeleteAsset_Click(object? sender, EventArgs e)
        {
            if (achievementGrid.SelectedRows.Count == 0) return;

            PushMainUndo();

            var rowsToDelete = achievementGrid.SelectedRows.Cast<DataGridViewRow>().ToList();
            foreach (var row in rowsToDelete)
            {
                if (row.Tag is IAssetItem item && item.State == AssetState.Local)
                {
                    if (item is RaAchievement ra) _localData?.Achievements.Remove(ra);
                    if (item is RaLeaderboard rl) _localData?.Leaderboards.Remove(rl);
                }
            }
            ReloadDataView();
        }

        private void MoveAssetUp_Click(object? sender, EventArgs e) => MoveAsset(-1);
        private void MoveAssetDown_Click(object? sender, EventArgs e) => MoveAsset(1);

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

        private void UpdateRecentMenu() { recentToolStripMenuItem.DropDownItems.Clear(); foreach (var file in _recentFiles.Files) { var item = new ToolStripMenuItem(file); item.Click += (s, args) => LoadFiles(file); recentToolStripMenuItem.DropDownItems.Add(item); } }

        private void achievementGrid_SelectionChanged(object? sender, EventArgs e) { UpdateSelectionUI(); }
        private void achievementGrid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) LoadSelectedAsset(); }
    }

    public class RecentFilesManager
    {
        private const int MaxFiles = 5;
        private string FilePath = Path.Combine(Application.StartupPath, "recent.txt");
        public List<string> Files { get; private set; } = new();
        public void Load() { if (File.Exists(FilePath)) Files = File.ReadAllLines(FilePath).ToList(); }
        public void Add(string path) { Files.RemoveAll(f => f.Equals(path, StringComparison.OrdinalIgnoreCase)); Files.Insert(0, path); if (Files.Count > MaxFiles) Files.RemoveAt(Files.Count - 1); File.WriteAllLines(FilePath, Files); }
    }
}