#nullable enable
using RALogicEditor.Models;
using RALogicEditor.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace RALogicEditor
{
    public partial class MainForm
    {
        // Refreshes the grid view while preserving scroll/selection state
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
                try { achievementGrid.FirstDisplayedScrollingRowIndex = savedScroll; } catch { }
        }

        // Fills the grid based on current filters (Source, Category, Search)
        private void PopulateList()
        {
            achievementGrid.Rows.Clear();
            _currentDisplayList.Clear();

            bool showLeaderboards = cmbAssetType.SelectedIndex == 1;
            string sourceMode = cmbSource.SelectedItem?.ToString() ?? "Local";
            string categoryFilter = cmbCategory.SelectedItem?.ToString() ?? "Core";
            string filterText = txtAssetSearch.Text.Trim();

            var mergedList = showLeaderboards
                ? _projectRepo.GetMergedList<RaLeaderboard>(true).Cast<IAssetItem>()
                : _projectRepo.GetMergedList<RaAchievement>(false).Cast<IAssetItem>();

            foreach (var item in mergedList)
            {
                UpdateAssetState(item);

                // Filter by Source (JSON vs Local)
                if (sourceMode == "New") { if (item.State != AssetState.Local) continue; }
                else if (sourceMode == "JSON") { if (item.State == AssetState.Local) continue; }
                else { if (item.State == AssetState.Synced) continue; }

                // Filter by Category (Core vs Unofficial) - ignored for "New" mode
                if (sourceMode != "New")
                {
                    string itemCat = string.IsNullOrEmpty(item.Category) ? "Local" : item.Category;
                    if (!itemCat.Equals(categoryFilter, StringComparison.OrdinalIgnoreCase)) continue;
                }

                AddFilteredGridRow(item, filterText);
            }
        }

        private void AddFilteredGridRow(IAssetItem item, string filter)
        {
            if (!string.IsNullOrEmpty(filter))
            {
                bool matchesId = item.ID.ToString().Contains(filter);
                bool matchesTitle = item.Title.Contains(filter, StringComparison.OrdinalIgnoreCase);
                if (!matchesId && !matchesTitle) return;
            }
            AddGridRow(item);
        }

        private void AddGridRow(IAssetItem item)
        {
            string displayId = item.ID.ToString();
            // Prefix large IDs with L for Leaderboards if they are local logic
            if (item.IsLeaderboard && item.ID >= 111000001) displayId = $"L{item.ID}";

            int idx = achievementGrid.Rows.Add(displayId, item.Title);
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

        // Loads the Asset into the Trigger Editor Logic
        private void LoadSelectedAsset()
        {
            if (achievementGrid.SelectedRows.Count > 0)
            {
                var row = achievementGrid.SelectedRows[0];
                if (row.Tag is IAssetItem asset)
                {
                    _isLoading = true;
                    // If Synced, clone it before editing so we don't dirty the server data object directly
                    if (asset.State == AssetState.Synced)
                    {
                        IAssetItem clone = asset.IsLeaderboard ? ((RaLeaderboard)asset).Clone(false) : ((RaAchievement)asset).Clone(false);
                        _selectedAsset = clone;
                        row.Tag = clone;
                    }
                    else
                    {
                        _selectedAsset = asset;
                    }

                    // Parse logic and apply metadata
                    IAssetStrategy strategy = _selectedAsset.IsLeaderboard ? new LeaderboardStrategy() : new AchievementStrategy();
                    var segments = strategy.ParseLogic(_selectedAsset.Logic);
                    foreach (var groupList in segments.Values) _projectRepo.Metadata.ApplyNamesToAsset(_selectedAsset.ID, groupList);

                    string displayCat = string.IsNullOrEmpty(_selectedAsset.Category) ? "Core" : char.ToUpper(_selectedAsset.Category[0]) + _selectedAsset.Category.Substring(1);
                    triggerEditor.BindAchievement(_selectedAsset, displayCat);
                    _isLoading = false;
                }
            }
            UpdateSelectionUI();
        }

        // Checks asset against Server Data to set Synced/Modified state
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
                else if (item is RaLeaderboard l && serverItem is RaLeaderboard sl)
                {
                    if (l.Format != sl.Format || l.LowerIsBetter != sl.LowerIsBetter) equal = false;
                }
                item.State = equal ? AssetState.Synced : AssetState.Modified;
            }

            foreach (DataGridViewRow row in achievementGrid.Rows)
            {
                if (row.Tag == item) { UpdateRowColor(row, item); break; }
            }
        }

        private void SelectAssetInGrid(IAssetItem asset)
        {
            bool isLeaderboard = asset.IsLeaderboard;
            int typeIndex = isLeaderboard ? 1 : 0;
            if (cmbAssetType.SelectedIndex != typeIndex) { cmbAssetType.SelectedIndex = typeIndex; Application.DoEvents(); }

            string targetCat = string.IsNullOrEmpty(asset.Category) ? "Local" : asset.Category;
            foreach (var item in cmbCategory.Items)
            {
                if (string.Equals(item.ToString(), targetCat, StringComparison.OrdinalIgnoreCase))
                {
                    cmbCategory.SelectedItem = item; Application.DoEvents(); break;
                }
            }

            int sourceIndex = cmbSource.SelectedIndex;
            switch (asset.State)
            {
                case AssetState.Local: sourceIndex = 2; break;
                case AssetState.Modified: sourceIndex = 1; break;
                case AssetState.Synced: sourceIndex = 0; break;
            }
            if (cmbSource.SelectedIndex != sourceIndex) { cmbSource.SelectedIndex = sourceIndex; Application.DoEvents(); }

            foreach (DataGridViewRow row in achievementGrid.Rows)
            {
                if (row.Tag is IAssetItem item && item.ID == asset.ID)
                {
                    achievementGrid.ClearSelection();
                    row.Selected = true;
                    achievementGrid.CurrentCell = row.Cells[0];
                    LoadSelectedAsset();
                    break;
                }
            }
        }

        private void achievementGrid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0) LoadSelectedAsset();
        }

        private void achievementGrid_SelectionChanged(object? sender, EventArgs e)
        {
            UpdateSelectionUI();
        }

        private void achievementGrid_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { LoadSelectedAsset(); e.Handled = true; }
            else if (e.Alt && e.KeyCode == Keys.Up) { MoveAsset(-1); e.Handled = true; }
            else if (e.Alt && e.KeyCode == Keys.Down) { MoveAsset(1); e.Handled = true; }
            else if (e.KeyCode == Keys.Delete) { DeleteAsset_Click(sender, e); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.D) { DuplicateAsset_Click(sender, e); e.Handled = true; }
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
            viewDiffToolStripMenuItem.Enabled = achievementGrid.SelectedRows.Count == 1 && anyModified;
            renumberContextToolStripMenuItem.Enabled = _projectRepo.LocalData != null && (_projectRepo.LocalData.Achievements.Count > 0 || _projectRepo.LocalData.Leaderboards.Count > 0);
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
    }
}