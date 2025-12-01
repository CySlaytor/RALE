#nullable enable
using RALogicEditor.Forms;
using RALogicEditor.Models;
using RALogicEditor.Parsers;
using RALogicEditor.Services;
using RALogicEditor.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace RALogicEditor
{
    public partial class MainForm
    {
        // -- File Menu --

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!ConfirmDiscardChanges()) return;
            using var ofd = new OpenFileDialog { Filter = "JSON Files (*.json)|*.json|Text Files (*.txt)|*.txt|All Files (*.*)|*.*" };
            if (ofd.ShowDialog() == DialogResult.OK) LoadFiles(ofd.FileName);
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_projectRepo.LocalData == null && _projectRepo.ServerData == null) return;
            codeNoteEditor.CommitChanges();

            string savePath = _currentUserFilePath;
            if (string.IsNullOrEmpty(savePath) && !string.IsNullOrEmpty(_currentFilePath))
            {
                string dir = Path.GetDirectoryName(_currentFilePath) ?? "";
                string fileName = Path.GetFileNameWithoutExtension(_currentFilePath);
                if (fileName.EndsWith("-Notes")) fileName = fileName.Substring(0, fileName.Length - 6);
                savePath = Path.Combine(dir, $"{fileName}-User.txt");
            }

            if (string.IsNullOrEmpty(savePath))
            {
                using var sfd = new SaveFileDialog { Filter = "Text Files (*.txt)|*.txt" };
                if (sfd.ShowDialog() != DialogResult.OK) return;
                savePath = sfd.FileName;
            }

            // Backup
            try
            {
                if (File.Exists(savePath))
                {
                    string fileName = Path.GetFileName(savePath);
                    string gameIdStr = new string(fileName.TakeWhile(char.IsDigit).ToArray());
                    string backupRoot = Path.Combine(Application.StartupPath, "Backups");
                    string gameBackupDir = string.IsNullOrEmpty(gameIdStr) ? backupRoot : Path.Combine(backupRoot, gameIdStr);
                    if (!Directory.Exists(gameBackupDir)) Directory.CreateDirectory(gameBackupDir);
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    File.Copy(savePath, Path.Combine(gameBackupDir, $"{fileName}.{timestamp}.bak"), true);
                }
            }
            catch { }

            // Save
            try
            {
                _projectRepo.EnsureLocalDataExists();
                string fileContent = _fileSerializer.Serialize(
                    _projectRepo.ServerData?.Title ?? "Game Title",
                    _projectRepo.LocalData,
                    _projectRepo.GetMergedList<RaAchievement>(false),
                    _projectRepo.GetMergedList<RaLeaderboard>(true),
                    _projectRepo
                );

                File.WriteAllText(savePath, fileContent);
                _currentUserFilePath = savePath;
                _projectRepo.SaveRichPresence();
                _projectRepo.Metadata.Save();
                MessageBox.Show("Saved to " + savePath);
            }
            catch (Exception ex) { MessageBox.Show($"Error saving: {ex.Message}"); }
        }

        private void UpdateRecentMenu()
        {
            recentToolStripMenuItem.DropDownItems.Clear();
            foreach (var entry in _recentFiles.Files)
            {
                string text = string.IsNullOrEmpty(entry.GameTitle) ? entry.Path : $"{entry.GameTitle} | {entry.Path}";
                var item = new ToolStripMenuItem(text);
                string path = entry.Path;
                item.Click += (s, args) => { if (ConfirmDiscardChanges()) LoadFiles(path); };
                recentToolStripMenuItem.DropDownItems.Add(item);
            }
        }

        // -- Asset Actions --

        private void NewAsset_Click(object? sender, EventArgs e)
        {
            _projectRepo.EnsureLocalDataExists();
            PushMainUndo();

            int newId = 111000001;
            var allIds = _currentDisplayList.Select(x => x.ID).ToList();
            if (allIds.Any()) { int max = allIds.Max(); newId = (max < 111000000) ? 111000001 : max + 1; }

            bool leaderboard = cmbAssetType.SelectedIndex == 1;
            if (leaderboard)
            {
                _projectRepo.LocalData!.Leaderboards.Add(new RaLeaderboard { ID = newId, Title = "New Leaderboard", State = AssetState.Local, Category = "Local" });
            }
            else
            {
                _projectRepo.LocalData!.Achievements.Add(new RaAchievement { ID = newId, Title = "New Achievement", State = AssetState.Local, Category = "Local" });
            }

            if (cmbSource.SelectedItem?.ToString() != "New") cmbSource.SelectedItem = "New";
            ReloadDataView();

            foreach (DataGridViewRow row in achievementGrid.Rows)
            {
                if (row.Tag is IAssetItem item && item.ID == newId)
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
                        var c = ra.Clone(); c.Title += " (Copy)"; c.ID = nextId++; c.State = AssetState.Local; c.Category = "Local";
                        _projectRepo.LocalData!.Achievements.Add(c); newIds.Add(c.ID);
                    }
                    else if (source is RaLeaderboard rl)
                    {
                        var c = rl.Clone(); c.Title += " (Copy)"; c.ID = nextId++; c.State = AssetState.Local; c.Category = "Local";
                        _projectRepo.LocalData!.Leaderboards.Add(c); newIds.Add(c.ID);
                    }
                }
            }

            cmbSource.SelectedItem = "New";
            ReloadDataView();
            achievementGrid.ClearSelection();
            List<int> indicesToSelect = new List<int>();
            foreach (DataGridViewRow row in achievementGrid.Rows)
            {
                if (row.Tag is IAssetItem item && newIds.Contains(item.ID)) indicesToSelect.Add(row.Index);
            }
            if (indicesToSelect.Count > 0)
            {
                achievementGrid.CurrentCell = achievementGrid.Rows[indicesToSelect.Min()].Cells[0];
                foreach (int idx in indicesToSelect) achievementGrid.Rows[idx].Selected = true;
            }
        }

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

        private void MoveAsset(int direction)
        {
            if (achievementGrid.SelectedRows.Count == 0 || _projectRepo.LocalData == null) return;

            // Get all visible rows as items
            var allRows = achievementGrid.Rows.Cast<DataGridViewRow>().OrderBy(r => r.Index).ToList();
            if (allRows.Count == 0) return;

            // Identify indices to move
            var selectedIndices = achievementGrid.SelectedRows.Cast<DataGridViewRow>()
                                                 .Select(r => r.Index)
                                                 .OrderBy(i => i)
                                                 .ToList();

            // Bounds check
            if (direction < 0 && selectedIndices.Min() == 0) return;
            if (direction > 0 && selectedIndices.Max() >= allRows.Count - 1) return;

            // Check if all selected items are Local (Can't move Synced)
            foreach (int idx in selectedIndices)
            {
                if (allRows[idx].Tag is IAssetItem item && item.State != AssetState.Local) return;
            }

            PushMainUndo();

            // Extract the backing list of assets from the grid (preserving grid order)
            // This assumes the Grid is the source of truth for the current visual order
            var workingList = allRows.Select(r => r.Tag as IAssetItem).ToList();

            // Perform the move logic on the list
            if (direction < 0) // Up
            {
                foreach (int idx in selectedIndices)
                {
                    var item = workingList[idx];
                    workingList.RemoveAt(idx);
                    workingList.Insert(idx - 1, item);
                }
            }
            else // Down
            {
                // Iterate reverse for down move
                for (int i = selectedIndices.Count - 1; i >= 0; i--)
                {
                    int idx = selectedIndices[i];
                    var item = workingList[idx];
                    workingList.RemoveAt(idx);
                    workingList.Insert(idx + 1, item);
                }
            }

            // Re-assign IDs sequentially to ONLY the local items, based on their new visual order
            // We preserve Synced items' IDs but we assume they aren't mixed in `workingList` if we are in "Local" view.
            // If we are in "All" view, moving local items around synced items implies renumbering locals only?
            // Usually moving is done in "Local" view.
            // Safe approach: Reconstruct the LocalData lists from the workingList order.

            var newAchievements = new List<RaAchievement>();
            var newLeaderboards = new List<RaLeaderboard>();
            int currentId = 111000001;

            // First, preserve any local items NOT in the view (shouldn't happen if view filters correctly, but safety)
            // Actually, simpler: Just clear LocalData and rebuild from workingList IF workingList contains all locals.
            // If the grid is filtered, this is dangerous.

            // Safer logic: Just swap IDs of the moved items? No, that causes the previous bug.
            // Logic: Filter out all Local items from the working list, renumber them, and push back to Repo.

            var localItemsInOrder = workingList.Where(x => x != null && x.State == AssetState.Local).ToList();

            // Clear repo local lists
            _projectRepo.LocalData.Achievements.Clear();
            _projectRepo.LocalData.Leaderboards.Clear();

            // Re-add in new order with new IDs
            foreach (var item in localItemsInOrder)
            {
                if (item == null) continue;
                item.ID = currentId++;

                if (item.IsLeaderboard && item is RaLeaderboard lb)
                    _projectRepo.LocalData.Leaderboards.Add(lb);
                else if (item is RaAchievement ach)
                    _projectRepo.LocalData.Achievements.Add(ach);
            }

            ReloadDataView();

            // Restore Selection (Tracking by object reference, since IDs changed)
            achievementGrid.ClearSelection();

            // We need to find where our moved items ended up
            var selectedObjects = new HashSet<IAssetItem>();
            foreach (int idx in selectedIndices)
            {
                // Determine 'original' object reference that was at this index before reload?
                // No, workingList has the new order.
                // We need to select the items that were moved.
                // We can't rely on indices because reload might shift things (though shouldn't if filtered).
            }

            // Re-find rows by Object Reference (Tag)
            // Identify objects that were selected
            var objectsToSelect = new HashSet<IAssetItem>();
            foreach (int idx in selectedIndices)
            {
                if (allRows[idx].Tag is IAssetItem it) objectsToSelect.Add(it);
            }

            List<int> newIndices = new List<int>();
            foreach (DataGridViewRow row in achievementGrid.Rows)
            {
                if (row.Tag is IAssetItem item && objectsToSelect.Contains(item))
                {
                    newIndices.Add(row.Index);
                }
            }

            if (newIndices.Count > 0)
            {
                achievementGrid.CurrentCell = achievementGrid.Rows[newIndices.Min()].Cells[0];
                foreach (int idx in newIndices) achievementGrid.Rows[idx].Selected = true;
            }
        }

        private void RenumberLocalIDs_Click(object? sender, EventArgs e)
        {
            if (_projectRepo.LocalData == null) return;
            bool hasLocal = _projectRepo.LocalData.Achievements.Any() || _projectRepo.LocalData.Leaderboards.Any();
            if (!hasLocal) return;

            PushMainUndo();
            int currentId = 111000001;
            foreach (var ach in _projectRepo.LocalData.Achievements) ach.ID = currentId++;
            foreach (var lb in _projectRepo.LocalData.Leaderboards) lb.ID = currentId++;
            ReloadDataView();
        }

        private void viewDiffToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (achievementGrid.SelectedRows.Count != 1) return;
            var row = achievementGrid.SelectedRows[0];
            if (row.Tag is IAssetItem localItem && localItem.State == AssetState.Modified)
            {
                if (_projectRepo.ServerData == null) return;
                IAssetItem? serverItem = null;
                foreach (var s in _projectRepo.ServerData.Sets)
                {
                    if (localItem.IsLeaderboard) serverItem = s.Leaderboards.FirstOrDefault(x => x.ID == localItem.ID);
                    else serverItem = s.Achievements.FirstOrDefault(x => x.ID == localItem.ID);
                    if (serverItem != null) break;
                }
                if (serverItem != null)
                {
                    using (var diff = new AssetDiffForm(serverItem, localItem)) diff.ShowDialog();
                }
            }
        }

        private void RevertRegion_Click(object? sender, EventArgs e)
        {
            var regions = new HashSet<string>();
            var selectedAssets = achievementGrid.SelectedRows.Cast<DataGridViewRow>()
                .Select(r => r.Tag as IAssetItem).Where(a => a != null).ToList();

            if (selectedAssets.Count == 0) return;

            foreach (var asset in selectedAssets)
            {
                IAssetStrategy strategy = asset!.IsLeaderboard ? new LeaderboardStrategy() : new AchievementStrategy();
                var segments = strategy.ParseLogic(asset.Logic);
                foreach (var groupList in segments.Values)
                {
                    _projectRepo.Metadata.ApplyNamesToAsset(asset.ID, groupList);
                    foreach (var g in groupList)
                    {
                        var match = Regex.Match(g.GroupName, @"\((.*?)\)");
                        if (match.Success) regions.Add(match.Groups[1].Value);
                    }
                }
            }

            if (regions.Count == 0) { MessageBox.Show("No region-specific groups found."); return; }

            using (var form = new RevertRegionForm(regions.ToList()))
            {
                if (form.ShowDialog() != DialogResult.OK) return;
                string targetRegion = form.SelectedRegion;
                PushMainUndo();
                int count = 0;

                foreach (var item in selectedAssets)
                {
                    EnsureAssetIsLocal(item!);
                    IAssetItem? localItem = item!.IsLeaderboard
                        ? _projectRepo.LocalData!.Leaderboards.FirstOrDefault(x => x.ID == item.ID)
                        : _projectRepo.LocalData!.Achievements.FirstOrDefault(x => x.ID == item.ID);

                    if (localItem == null) continue;

                    IAssetStrategy strategy = localItem.IsLeaderboard ? new LeaderboardStrategy() : new AchievementStrategy();
                    var segments = strategy.ParseLogic(localItem.Logic);
                    bool modified = false;

                    foreach (var segKey in segments.Keys.ToList())
                    {
                        var groups = segments[segKey];
                        _projectRepo.Metadata.ApplyNamesToAsset(localItem.ID, groups);
                        var targetGroup = groups.FirstOrDefault(g => g.GroupName.Contains($"({targetRegion})"));

                        if (targetGroup != null)
                        {
                            while (targetGroup.Conditions.Count > 0 && targetGroup.Conditions[0].Flag == "Pause If")
                                targetGroup.Conditions.RemoveAt(0);

                            var newCore = new AchievementConditionGroup { GroupName = "Core", Conditions = targetGroup.Conditions };
                            segments[segKey] = new List<AchievementConditionGroup> { newCore };
                            modified = true;
                            _projectRepo.Metadata.UpdateAssetGroups(localItem.ID, segments[segKey]);
                        }
                    }

                    if (modified)
                    {
                        localItem.Logic = strategy.BuildLogic(segments);
                        count++;
                    }
                }
                ReloadDataView();
                if (_selectedAsset != null) LoadSelectedAsset();
                MessageBox.Show($"Reverted {count} assets to {targetRegion}.");
            }
        }

        private void RevertRichPresence_Click(object? sender, EventArgs e)
        {
            if (_rpMetadata.CurrentBackup == null) { MessageBox.Show("No backup found."); return; }
            if (MessageBox.Show($"Revert to state from {_rpMetadata.CurrentBackup.Timestamp}?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                try
                {
                    _projectRepo.RichPresence = RichPresenceParser.Parse(_rpMetadata.CurrentBackup.OriginalScript);
                    MessageBox.Show("Reverted.");
                }
                catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
            }
        }

        private void ViewRpDiff_Click(object? sender, EventArgs e)
        {
            if (_projectRepo.RichPresence == null) return;
            string ExtractDisplaySection(string rpScript)
            {
                if (string.IsNullOrEmpty(rpScript)) return "";
                var lines = rpScript.Replace("\r", "").Split('\n');
                var sb = new StringBuilder();
                bool inDisplay = false;
                foreach (var line in lines)
                {
                    if (line.Trim().StartsWith("Display:")) { inDisplay = true; continue; }
                    if (inDisplay) { if (line.Trim().StartsWith("Lookup:") || line.Trim().StartsWith("Format:")) break; sb.AppendLine(line); }
                }
                return sb.ToString().Trim();
            }

            string current = ExtractDisplaySection(_projectRepo.RichPresence.ToString());
            string path = _projectRepo.RichPresenceFilePath;
            string original = File.Exists(path) ? ExtractDisplaySection(File.ReadAllText(path)) : "";
            using (var form = new TextDiffForm("RP Diff", original, current)) form.ShowDialog();
        }

        private void LogicOptimizerToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            if (_currentDisplayList.Count == 0) return;
            var assetsToOptimize = new List<IAssetItem>();
            foreach (var item in _currentDisplayList)
            {
                if (item.State == AssetState.Synced)
                {
                    IAssetItem clone = item.IsLeaderboard ? ((RaLeaderboard)item).Clone(false) : ((RaAchievement)item).Clone(false);
                    clone.State = AssetState.Synced;
                    assetsToOptimize.Add(clone);
                }
                else assetsToOptimize.Add(item);
            }

            using (var form = new LogicOptimizerForm(assetsToOptimize)) form.ShowDialog();

            _projectRepo.EnsureLocalDataExists();
            bool anyChanges = false;
            foreach (var item in assetsToOptimize)
            {
                if (item.State == AssetState.Modified)
                {
                    if (!IsAssetInLocalStore(item))
                    {
                        if (item.IsLeaderboard) _projectRepo.LocalData!.Leaderboards.Add((RaLeaderboard)item);
                        else _projectRepo.LocalData!.Achievements.Add((RaAchievement)item);
                        anyChanges = true;
                    }
                    else anyChanges = true;
                }
            }
            if (anyChanges)
            {
                ReloadDataView();
                if (_selectedAsset != null) SelectAssetInGrid(_selectedAsset);
            }
        }

        private bool IsAssetInLocalStore(IAssetItem item)
        {
            if (_projectRepo.LocalData == null) return false;
            if (item.IsLeaderboard) return _projectRepo.LocalData.Leaderboards.Any(l => l.ID == item.ID);
            return _projectRepo.LocalData.Achievements.Any(a => a.ID == item.ID);
        }

        // -- Global Actions --

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

        private void Reload_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFilePath)) return;
            if (!ConfirmDiscardChanges()) return;

            int selectedId = -1;
            if (achievementGrid.SelectedRows.Count > 0 && achievementGrid.SelectedRows[0].Tag is IAssetItem item)
                selectedId = item.ID;

            LoadFiles(_currentFilePath);

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
    }
}