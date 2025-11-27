#nullable enable
using RALogicEditor.Controls;
using RALogicEditor.Definitions;
using RALogicEditor.Forms;
using RALogicEditor.Helpers;
using RALogicEditor.Models;
using RALogicEditor.Parsers;
using RALogicEditor.Services;
using RALogicEditor.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
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

        // Logic Validator Service (Instantiated locally for now, could be injected)
        private readonly ILogicValidator _validator = new LogicValidator();

        // -- State Tracking --
        private IAssetItem? _selectedAsset; // The achievement/leaderboard currently being edited
        private bool _isLoading;            // Flag to suppress events during bulk updates
        private string _currentFilePath = ""; // Path to the main loaded file (JSON)
        private string _currentUserFilePath = ""; // Path to the companion User.txt
        private List<IAssetItem> _currentDisplayList = new(); // The filtered list currently shown in the grid

        // Store global issues for the Code Reviewer
        private List<ValidationIssue> _globalIssues = new();

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

            // -- New: Reviewer Events --
            // Trigger Editor notifies when validation runs on the CURRENT asset. 
            // We merge this result into the global list.
            triggerEditor.ValidationCompleted += TriggerEditor_ValidationCompleted;

            // -- NEW: Multi-Region Batch Processing Request --
            // Handles the bulk update logic from the context menu
            triggerEditor.MultiRegionLogicRequested += TriggerEditor_MultiRegionLogicRequested;

            // Code Reviewer clicks: select the asset and the specific condition
            codeReviewControl.IssueSelected += CodeReviewControl_IssueSelected;

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
            cmbSource.Items.Clear();
            cmbSource.Items.AddRange(new object[] { "JSON", "Local", "New" });

            // Initialize Category Filter (Core/Unofficial/Local)
            cmbCategory.Items.Clear();
            cmbCategory.Items.AddRange(new object[] { "Core", "Unofficial", "Local" });
            cmbCategory.SelectedIndex = 0; // Default to Core

            cmbAssetType.SelectedIndex = 0; // Default to Achievements
            cmbSource.SelectedIndex = 0;    // Default to JSON view

            // Events for filtering
            cmbAssetType.SelectedIndexChanged += (s, e) =>
            {
                ReloadDataView();
                // Re-run validation when asset type changes to filter the review list
                RunGlobalValidation();
            };
            cmbSource.SelectedIndexChanged += (s, e) => ReloadDataView();
            cmbCategory.SelectedIndexChanged += (s, e) => ReloadDataView();

            // Search Box Wiring
            txtAssetSearch.TextChanged += (s, e) => ReloadDataView();

            // -- Menu Item Wiring --
            newAssetToolStripMenuItem.Click += NewAsset_Click;
            cloneAssetToolStripMenuItem.Click += DuplicateAsset_Click;
            deleteAssetToolStripMenuItem.Click += DeleteAsset_Click;
            resetAssetToolStripMenuItem.Click += ResetAsset_Click;
            moveUpAssetToolStripMenuItem.Click += MoveAssetUp_Click;
            moveDownAssetToolStripMenuItem.Click += MoveAssetDown_Click;
            renumberLocalIDsToolStripMenuItem.Click += RenumberLocalIDs_Click;
            renumberContextToolStripMenuItem.Click += RenumberLocalIDs_Click;

            // View Menu
            viewReviewerToolStripMenuItem.Click += (s, e) => ToggleReviewer();

            // -- Button Wiring --
            btnAddAsset.Click += NewAsset_Click;
            btnDuplicateAsset.Click += DuplicateAsset_Click;
            btnDeleteAsset.Click += DeleteAsset_Click;

            this.FormClosing += MainForm_FormClosing;

            // Load recent files list on startup
            _recentFiles.Load();
            UpdateRecentMenu();

            // Load and Apply Settings
            AppSettings.Load();
            ToggleReviewer(AppSettings.Current.ShowLogicReviewer);
        }

        private void ToggleReviewer(bool? forceState = null)
        {
            bool newState = forceState ?? !viewReviewerToolStripMenuItem.Checked;
            viewReviewerToolStripMenuItem.Checked = newState;
            splitMiddle.Panel2Collapsed = !newState;

            // Save setting immediately
            AppSettings.Current.ShowLogicReviewer = newState;
            AppSettings.Save();
        }

        // NEW: Multi-Region Batch Logic Implementation
        private void TriggerEditor_MultiRegionLogicRequested(object? sender, MultiRegionForm form)
        {
            // 1. Validate State
            if (_currentDisplayList.Count == 0) return;

            // Ensure local repo exists
            _projectRepo.EnsureLocalDataExists();
            PushMainUndo(); // Global Undo State

            // 2. Prepare Lock Logic (Invert for Pause If)
            // Helper to invert lock logic: User provides check "Is Region A". We want "Pause If NOT Region A".
            void InvertLockLogic(List<AchievementCondition> conditions)
            {
                foreach (var c in conditions)
                {
                    c.Flag = "Pause If";
                    // Invert operator
                    c.Operator = c.Operator switch
                    {
                        "=" => "!=",
                        "!=" => "=",
                        "<" => ">=",
                        "<=" => ">",
                        ">" => "<=",
                        ">=" => "<",
                        _ => c.Operator
                    };
                }
            }

            // Clone to ensure we don't modify the original lists if reused
            var regionACondsBase = form.RegionAConditions;
            var regionBCondsBase = form.RegionBConditions;
            InvertLockLogic(regionACondsBase);
            InvertLockLogic(regionBCondsBase);

            // 3. Iterate currently filtered list
            int count = 0;

            // Need to iterate a copy or by index because we might modify the underlying data structures structure or view
            var assetsToProcess = new List<IAssetItem>(_currentDisplayList);

            foreach (var item in assetsToProcess)
            {
                // Ensure item is local so we can modify it.
                // This creates a CLONE in LocalData if it was previously Synced.
                EnsureAssetIsLocal(item);

                // Get the Local version (The Clone)
                IAssetItem? localItem = null;
                if (item.IsLeaderboard) localItem = _projectRepo.LocalData!.Leaderboards.FirstOrDefault(x => x.ID == item.ID);
                else localItem = _projectRepo.LocalData!.Achievements.FirstOrDefault(x => x.ID == item.ID);

                if (localItem == null) continue;

                // Parse Existing Logic
                IAssetStrategy strategy = localItem.IsLeaderboard ? new LeaderboardStrategy() : new AchievementStrategy();
                var segments = strategy.ParseLogic(localItem.Logic);

                var keys = new List<string>(segments.Keys);

                foreach (var segmentKey in keys)
                {
                    var groups = segments[segmentKey];
                    if (groups.Count == 0) continue;

                    // Identify existing groups (Region A)
                    var existingGroups = new List<AchievementConditionGroup>();
                    // Deep copy existing groups to avoid reference issues
                    foreach (var g in groups)
                    {
                        var newG = new AchievementConditionGroup { GroupName = g.GroupName };
                        foreach (var c in g.Conditions) newG.Conditions.Add(c.Clone());
                        existingGroups.Add(newG);
                    }

                    // Clone for Region B
                    var newRegionBGroups = new List<AchievementConditionGroup>();
                    foreach (var grp in existingGroups)
                    {
                        var clone = new AchievementConditionGroup { GroupName = grp.GroupName + " (Region B)" };
                        foreach (var c in grp.Conditions)
                        {
                            clone.Conditions.Add(c.Clone());
                        }
                        newRegionBGroups.Add(clone);
                    }

                    // Transform Region B Addresses
                    foreach (var grp in newRegionBGroups)
                    {
                        foreach (var c in grp.Conditions)
                        {
                            ApplyAddressSwap(c.LeftOperand, form.AddressMap);
                            ApplyAddressSwap(c.RightOperand, form.AddressMap);
                        }
                    }

                    // Inject Locks
                    // Insert Region A Lock to Existing Groups
                    foreach (var grp in existingGroups)
                    {
                        grp.Conditions.InsertRange(0, regionACondsBase.Select(c => c.Clone()));
                    }
                    // Insert Region B Lock to New Groups
                    foreach (var grp in newRegionBGroups)
                    {
                        grp.Conditions.InsertRange(0, regionBCondsBase.Select(c => c.Clone()));
                    }

                    // Reassemble
                    var finalGroups = new List<AchievementConditionGroup>();

                    // Standard Achievement Pattern: Empty Core + Alts
                    if (!localItem.IsLeaderboard && existingGroups.Count > 0 && existingGroups[0].GroupName == "Core" && existingGroups[0].Conditions.Count > 0)
                    {
                        existingGroups[0].GroupName = "Alt 1 (Region A)";
                        for (int i = 1; i < existingGroups.Count; i++) existingGroups[i].GroupName = $"Alt {i + 1} (Region A)";
                        finalGroups.Add(new AchievementConditionGroup { GroupName = "Core" }); // Empty Core
                    }
                    else if (!localItem.IsLeaderboard && existingGroups.Count > 0 && existingGroups[0].GroupName == "Core")
                    {
                        finalGroups.Add(existingGroups[0]);
                        existingGroups.RemoveAt(0);
                    }

                    finalGroups.AddRange(existingGroups);

                    // Add Region B groups
                    int startIdx = finalGroups.Count;
                    for (int i = 0; i < newRegionBGroups.Count; i++)
                    {
                        if (!localItem.IsLeaderboard) newRegionBGroups[i].GroupName = $"Alt {startIdx + i} (Region B)";
                    }
                    finalGroups.AddRange(newRegionBGroups);

                    segments[segmentKey] = finalGroups;
                }

                // Save Back
                localItem.Logic = strategy.BuildLogic(segments);
                // State will be updated to Modified when ReloadDataView calls UpdateAssetState
                count++;
            }

            MessageBox.Show($"Multi-Region logic applied to {count} assets.", "Batch Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // Refresh UI
            ReloadDataView();

            // If an asset is currently selected, reload it to show changes
            if (_selectedAsset != null)
            {
                // Re-select finding the new Local object
                SelectAssetInGrid(_selectedAsset);
            }
        }

        private void ApplyAddressSwap(Operand op, Dictionary<string, string> map)
        {
            if (!RaLogicSyntax.MemoryTypes.Contains(op.Type)) return;
            string addr = op.Value.Trim();
            if (string.IsNullOrEmpty(addr)) return;

            // Normalize input address for lookup (remove 0x, trim zeros)
            string clean = addr.Replace("0x", "").Trim();
            if (long.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long val))
            {
                string normalizedKey = val.ToString("x"); // Lowercase, no leading zeros

                // Check direct match
                if (map.ContainsKey(normalizedKey))
                {
                    op.Value = "0x" + map[normalizedKey];
                }
            }
        }

        // Triggered when user clicks a row in the Code Review Control
        private void CodeReviewControl_IssueSelected(object? sender, ValidationIssue issue)
        {
            if (issue.Asset == null) return;

            // 1. Ensure the correct asset is loaded
            if (_selectedAsset == null || _selectedAsset.ID != issue.Asset.ID)
            {
                SelectAssetInGrid(issue.Asset);
            }

            // 2. Highlight the specific condition in the editor
            triggerEditor.SelectCondition(issue.Target);

            // 3. Show Debug Dialog if available (DISABLED Internally)
            /*
            if (!string.IsNullOrEmpty(issue.DebugLog))
            {
                using (var dbg = new DebugChainForm(issue.DebugLog))
                {
                    dbg.ShowDialog();
                }
            }
            */
        }

        private void SelectAssetInGrid(IAssetItem asset)
        {
            // 1. Ensure correct Asset Type (Achievement vs Leaderboard)
            bool isLeaderboard = asset.IsLeaderboard;
            int typeIndex = isLeaderboard ? 1 : 0; // 0=Ach, 1=LB
            if (cmbAssetType.SelectedIndex != typeIndex)
            {
                cmbAssetType.SelectedIndex = typeIndex;
                Application.DoEvents(); // Allow UI to process the change
            }

            // 2. Ensure correct Category (Core vs Unofficial)
            // asset.Category is usually "core", "unofficial", "Local" (lowercase from JSON or titlecase)
            // We need to match with ComboBox items "Core", "Unofficial", "Local"
            string targetCat = string.IsNullOrEmpty(asset.Category) ? "Local" : asset.Category;
            foreach (var item in cmbCategory.Items)
            {
                if (string.Equals(item.ToString(), targetCat, StringComparison.OrdinalIgnoreCase))
                {
                    cmbCategory.SelectedItem = item;
                    Application.DoEvents();
                    break;
                }
            }

            // 3. Ensure correct Source View (JSON vs Local vs New) based on Asset State
            // Items: "JSON", "Local", "New" -> Indices: 0, 1, 2
            int sourceIndex = cmbSource.SelectedIndex;
            switch (asset.State)
            {
                case AssetState.Local:
                    sourceIndex = 2; // New
                    break;
                case AssetState.Modified:
                    sourceIndex = 1; // Local
                    break;
                case AssetState.Synced:
                    sourceIndex = 0; // JSON
                    break;
            }

            // Force switch to "JSON" view if we are looking for a synced asset, even if current view is Local/New
            // Actually, if we are in "New" view, we can't see Synced assets.
            // If we are in "Local" view, we can't see Synced assets.
            // So we MUST switch to "JSON" if the asset is Synced.
            if (cmbSource.SelectedIndex != sourceIndex)
            {
                cmbSource.SelectedIndex = sourceIndex;
                Application.DoEvents();
            }

            // 4. Find row in grid
            foreach (DataGridViewRow row in achievementGrid.Rows)
            {
                if (row.Tag is IAssetItem item && item.ID == asset.ID)
                {
                    achievementGrid.ClearSelection();
                    row.Selected = true;
                    achievementGrid.CurrentCell = row.Cells[0];
                    // LoadSelectedAsset is handled by SelectionChanged event usually, 
                    // but explicitly calling it ensures TriggerEditor updates immediately
                    LoadSelectedAsset();
                    break;
                }
            }
        }

        // Runs validation on assets in the project to populate the review list
        private void RunGlobalValidation()
        {
            _globalIssues.Clear();

            // Only validating the currently active asset type to reduce noise and issues
            bool validateLeaderboards = cmbAssetType.SelectedIndex == 1;

            var allAssets = new List<IAssetItem>();

            if (validateLeaderboards)
            {
                var lbs = _projectRepo.GetMergedList<RaLeaderboard>(true);
                allAssets.AddRange(lbs);
            }
            else
            {
                var achs = _projectRepo.GetMergedList<RaAchievement>(false);
                allAssets.AddRange(achs);
            }

            var notesLookup = new Dictionary<string, List<NoteNode>>();
            var rangeCache = new List<(long Start, long End, NoteNode Node)>();
            var parser = new PointerTreeParser();

            if (_projectRepo.LocalData != null && _projectRepo.LocalData.Notes != null)
            {
                foreach (var n in _projectRepo.LocalData.Notes)
                {
                    string key = LogicFormatter.NormalizeAddress(n.Address);
                    var nodes = parser.ParseNoteText(n.Note);
                    if (!notesLookup.ContainsKey(key)) notesLookup[key] = nodes;

                    if (nodes.Count > 0)
                    {
                        var root = nodes[0];
                        if (root.Size > 1)
                        {
                            if (long.TryParse(key.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out long startAddr))
                            {
                                rangeCache.Add((startAddr, startAddr + root.Size, root));
                            }
                        }
                    }
                }
            }

            foreach (var asset in allAssets)
            {
                IAssetStrategy strategy = asset.IsLeaderboard ? new LeaderboardStrategy() : new AchievementStrategy();
                var segments = strategy.ParseLogic(asset.Logic);
                var allGroups = segments.Values.SelectMany(g => g).ToList();
                var issues = _validator.ValidateAsset(allGroups, asset, notesLookup, rangeCache);
                _globalIssues.AddRange(issues);
            }

            codeReviewControl.SetIssues(_globalIssues);
        }

        // Called when the Trigger Editor updates (user edits logic) -> Update global list for THAT asset
        private void TriggerEditor_ValidationCompleted(object? sender, List<ValidationIssue> issues)
        {
            if (_selectedAsset == null) return;

            // Remove existing issues for this specific asset ID from the backing list
            _globalIssues.RemoveAll(i => i.Asset != null && i.Asset.ID == _selectedAsset.ID);

            // Add updated issues
            _globalIssues.AddRange(issues);

            // Only update UI if not in loading state (prevents flicker/reset on navigation)
            // The user will see the issues when they stop navigating or edit the logic.
            if (!_isLoading)
            {
                codeReviewControl.SetIssues(_globalIssues);
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

            // Ensure notes are set or cleared
            var notes = _projectRepo.LocalData?.Notes ?? new List<CodeNote>();
            codeNoteEditor.SetNotes(notes);
            // Pass notes to Trigger Editor for Tooltips and Alias Mode
            triggerEditor.SetNotes(notes);

            // Run global validation scan - This will now respect the active asset type
            RunGlobalValidation();
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_projectRepo.LocalData == null && _projectRepo.ServerData == null) return;

            // NEW: Ensure any pending virtual note edits are committed before saving
            codeNoteEditor.CommitChanges();

            string savePath = _currentUserFilePath;

            // If path is empty, try to auto-determine based on JSON path
            if (string.IsNullOrEmpty(savePath) && !string.IsNullOrEmpty(_currentFilePath))
            {
                string dir = Path.GetDirectoryName(_currentFilePath) ?? "";
                string fileName = Path.GetFileNameWithoutExtension(_currentFilePath);

                // If the loaded file was a notes file (-Notes.json), strip the suffix to find ID
                if (fileName.EndsWith("-Notes"))
                    fileName = fileName.Substring(0, fileName.Length - 6);

                // If loading 1234.json or 1234-Notes.json, auto-save to 1234-User.txt
                savePath = Path.Combine(dir, $"{fileName}-User.txt");
            }

            if (string.IsNullOrEmpty(savePath))
            {
                using var sfd = new SaveFileDialog { Filter = "Text Files (*.txt)|*.txt" };
                if (sfd.ShowDialog() != DialogResult.OK) return;
                savePath = sfd.FileName;
            }

            // -- Auto-Backup Strategy --
            try
            {
                if (File.Exists(savePath))
                {
                    string backupDir = Path.Combine(Path.GetDirectoryName(savePath) ?? "", "Backups");
                    if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);

                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string fileName = Path.GetFileName(savePath);
                    string backupPath = Path.Combine(backupDir, $"{fileName}.{timestamp}.bak");

                    File.Copy(savePath, backupPath, true);
                }
            }
            catch { /* Ignore backup failures, proceed with save */ }

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("1.0");
                sb.AppendLine(_projectRepo.ServerData?.Title ?? "Game Title");

                _projectRepo.EnsureLocalDataExists();

                // Get merged lists excluding fully synced items
                var mergedAchievements = _projectRepo.GetMergedList<RaAchievement>(false);
                var mergedLeaderboards = _projectRepo.GetMergedList<RaLeaderboard>(true);

                var lbStrategy = new LeaderboardStrategy();

                foreach (var a in mergedAchievements)
                {
                    if (a.State == AssetState.Synced) continue; // Don't save synced items to User.txt
                    sb.AppendLine($"{a.ID}:\"{a.Logic}\":{a.Title}:{a.Description}:::{a.Type}:{a.Author}:{a.Points}:::::{a.BadgeName}");
                }

                foreach (var l in mergedLeaderboards)
                {
                    if (l.State == AssetState.Synced) continue;

                    // Use strategy to ensure we get clean segments for the file format
                    var segments = lbStrategy.ParseLogic(l.Logic);

                    // Helper to build a specific segment string for the file line (e.g. "STA:...")
                    // File format expects: L{ID}:"START_LOGIC":"CANCEL_LOGIC":"SUBMIT_LOGIC":"VALUE_LOGIC":...
                    // We construct each part manually to ensure separators match file spec (LogicFormatter)

                    string GetSeg(string key)
                    {
                        if (!segments.ContainsKey(key)) return "";
                        var groups = segments[key];
                        StringBuilder segSb = new StringBuilder();
                        for (int i = 0; i < groups.Count; i++)
                        {
                            if (i > 0) segSb.Append('S');
                            for (int c = 0; c < groups[i].Conditions.Count; c++)
                            {
                                if (c > 0) segSb.Append('_');
                                segSb.Append(LogicFormatter.ConditionToString(groups[i].Conditions[c]));
                            }
                        }
                        return segSb.ToString();
                    }

                    sb.AppendLine($"L{l.ID}:\"{GetSeg("Start")}\":\"{GetSeg("Cancel")}\":\"{GetSeg("Submit")}\":\"{GetSeg("Value")}\":{l.Format}:{l.Title}:{l.Description}:{l.LowerIsBetter}");
                }

                // Save notes - Filter out notes that match the server data
                if (_projectRepo.LocalData?.Notes != null)
                {
                    foreach (var n in _projectRepo.LocalData.Notes)
                    {
                        // Only save notes that are modified or new compared to the server JSON
                        if (_projectRepo.IsNoteSynced(n)) continue;

                        sb.AppendLine($"N0:{n.Address}:\"{n.Note.Replace("\r\n", "\\r\\n")}\"");
                    }
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
            string categoryFilter = cmbCategory.SelectedItem?.ToString() ?? "Core";
            string filterText = txtAssetSearch.Text.Trim();

            // Fetch merged list (contains everything)
            var mergedList = showLeaderboards
                ? _projectRepo.GetMergedList<RaLeaderboard>(true).Cast<IAssetItem>()
                : _projectRepo.GetMergedList<RaAchievement>(false).Cast<IAssetItem>();

            foreach (var item in mergedList)
            {
                UpdateAssetState(item); // Ensure State is current (Modified/Synced/Local)

                // 1. Filter by Source Mode (JSON vs Local vs New)
                if (sourceMode == "New")
                {
                    if (item.State != AssetState.Local) continue;
                }
                else if (sourceMode == "JSON")
                {
                    if (item.State == AssetState.Local) continue;
                }
                else // "Local" view
                {
                    if (item.State == AssetState.Synced) continue;
                }

                // 2. Filter by Category (Core vs Unofficial vs Local)
                // "Local" items might have empty or "Local" category string
                string itemCat = string.IsNullOrEmpty(item.Category) ? "Local" : item.Category;

                if (!itemCat.Equals(categoryFilter, StringComparison.OrdinalIgnoreCase))
                {
                    // Special case: If user selects "Local" category, we also include items with null/empty/Local category
                    // which we handled above by defaulting itemCat to "Local".
                    continue;
                }

                // 3. Filter by Search Text
                AddFilteredGridRow(item, filterText);
            }
        }

        private void AddFilteredGridRow(IAssetItem item, string filter)
        {
            // Apply text filter
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
            // Only prefix 'L' for Leaderboards that are effectively "Local" (High ID range)
            // Standard server IDs (e.g. 42948) should appear as raw numbers.
            string displayId = item.ID.ToString();
            if (item.IsLeaderboard && item.ID >= 111000001)
            {
                displayId = $"L{item.ID}";
            }

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

                    // Display category nicely
                    string displayCat = string.IsNullOrEmpty(_selectedAsset.Category) ? "Core" :
                        char.ToUpper(_selectedAsset.Category[0]) + _selectedAsset.Category.Substring(1);

                    triggerEditor.BindAchievement(_selectedAsset, displayCat);
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

            // The BoundAsset Logic property is updated by the ViewModel inside TriggerEditorControl
            // We just need to refresh the state tracking
            UpdateAssetState(_selectedAsset);
        }

        // Callback from Trigger Editor: Metadata (Title, Desc) changed
        private void TriggerEditor_AchievementDataChanged(object? sender, EventArgs e)
        {
            if (_isLoading || _selectedAsset == null) return;
            _projectRepo.EnsureLocalDataExists();

            // Bulk Edit Logic
            if (achievementGrid.SelectedRows.Count > 1)
            {
                ApplyBulkEdit(_selectedAsset);
            }
            else
            {
                EnsureAssetIsLocal();
                UpdateAssetState(_selectedAsset);
            }

            // Visual update
            foreach (DataGridViewRow row in achievementGrid.Rows)
            {
                if (row.Tag is IAssetItem asset && asset.ID == _selectedAsset.ID)
                {
                    row.Cells[1].Value = _selectedAsset.Title;
                    UpdateRowColor(row, asset);
                    break;
                }
            }
        }

        private void ApplyBulkEdit(IAssetItem sourceTemplate)
        {
            // Propagate metadata changes to all selected rows
            // We do NOT propagate Logic or Description usually, just Type/Author/Points as those are common batch ops
            // But since this event fires for ANY change in TriggerEditor including Title/Desc, we need to be careful.

            // For now, let's strictly limit bulk edit to Points, Author, and Type.
            // Detecting exactly which field changed is hard without more complex event args.
            // As a safe approach, we apply Points/Type/Author from the source.

            if (sourceTemplate is not RaAchievement srcAch) return;

            foreach (DataGridViewRow row in achievementGrid.SelectedRows)
            {
                if (row.Tag is RaAchievement target && target != sourceTemplate)
                {
                    EnsureAssetIsLocal(target);
                    target.Points = srcAch.Points;
                    target.Type = srcAch.Type;
                    // target.Author = srcAch.Author; // Author isn't editable in UI currently for existing assets easily, but if added later:

                    UpdateAssetState(target);
                }
            }
        }

        // Overload to force a specific asset to local
        private void EnsureAssetIsLocal(IAssetItem asset)
        {
            if (asset == null || _projectRepo.LocalData == null) return;
            if (asset.State == AssetState.Local || asset.State == AssetState.Modified) return;

            if (asset is RaAchievement ra)
            {
                if (!_projectRepo.LocalData.Achievements.Any(a => a.ID == ra.ID))
                {
                    // Clone server asset before adding to local to prevent reference issues
                    var clone = ra.Clone(false);
                    _projectRepo.LocalData.Achievements.Add(clone);
                }
            }
            else if (asset is RaLeaderboard rl)
            {
                if (!_projectRepo.LocalData.Leaderboards.Any(l => l.ID == rl.ID))
                {
                    var clone = rl.Clone(false);
                    _projectRepo.LocalData.Leaderboards.Add(clone);
                }
            }
        }

        // Adds the asset to LocalData list if it isn't there already (promotes from Synced to Modified)
        private void EnsureAssetIsLocal()
        {
            EnsureAssetIsLocal(_selectedAsset!);
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
                var lb = new RaLeaderboard { ID = newId, Title = "New Leaderboard", State = AssetState.Local, Category = "Local" };
                _projectRepo.LocalData!.Leaderboards.Add(lb);
            }
            else
            {
                var ach = new RaAchievement { ID = newId, Title = "New Achievement", State = AssetState.Local, Category = "Local" };
                _projectRepo.LocalData!.Achievements.Add(ach);
            }

            // Automatically switch to "New" view and "Local" Category
            if (cmbSource.SelectedItem?.ToString() != "New")
                cmbSource.SelectedItem = "New";

            if (cmbCategory.SelectedItem?.ToString() != "Local")
                cmbCategory.SelectedItem = "Local";

            ReloadDataView();

            // Select new item
            foreach (DataGridViewRow row in achievementGrid.Rows)
            {
                // Use Tag to verify ID safely, as display value might be a string (e.g. "L111...")
                if (row.Tag is IAssetItem item && item.ID == newId)
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

            // Switch to "New" view / "Local" category so we can see the duplicated items
            cmbSource.SelectedItem = "New";
            cmbCategory.SelectedItem = "Local";
            ReloadDataView();

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

        // Action: Renumber all local assets sequentially
        private void RenumberLocalIDs_Click(object? sender, EventArgs e)
        {
            if (_projectRepo.LocalData == null) return;

            bool hasLocal = _projectRepo.LocalData.Achievements.Any() || _projectRepo.LocalData.Leaderboards.Any();
            if (!hasLocal) return;

            PushMainUndo();

            int currentId = 111000001;

            // Achievements first
            foreach (var ach in _projectRepo.LocalData.Achievements)
            {
                ach.ID = currentId++;
            }

            // Leaderboards follow
            foreach (var lb in _projectRepo.LocalData.Leaderboards)
            {
                lb.ID = currentId++;
            }

            ReloadDataView();
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

            // Diff is only valid for Modified assets (1 item selected)
            viewDiffToolStripMenuItem.Enabled = achievementGrid.SelectedRows.Count == 1 && anyModified;

            bool hasAnyLocal = _projectRepo.LocalData != null &&
                              (_projectRepo.LocalData.Achievements.Count > 0 || _projectRepo.LocalData.Leaderboards.Count > 0);
            renumberContextToolStripMenuItem.Enabled = hasAnyLocal;
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

        private void viewDiffToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (achievementGrid.SelectedRows.Count != 1) return;
            var row = achievementGrid.SelectedRows[0];
            if (row.Tag is IAssetItem localItem && localItem.State == AssetState.Modified)
            {
                // Find server equivalent
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
                    using (var diff = new AssetDiffForm(serverItem, localItem))
                    {
                        diff.ShowDialog();
                    }
                }
            }
        }
    }
}