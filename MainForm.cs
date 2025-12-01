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
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace RALogicEditor
{
    public partial class MainForm : Form
    {
        // -- Core Services --
        private readonly ProjectRepository _projectRepo = new();
        private readonly HistoryManager<UserTextParser.UserData> _mainHistory = new();
        private readonly RecentFilesManager _recentFiles = new();
        private readonly ILogicValidator _validator = new LogicValidator();
        private readonly RichPresenceService _rpService = new();
        private readonly RichPresenceMetadataService _rpMetadata = new();

        // -- Refactored Services --
        private readonly UserFileSerializer _fileSerializer = new();
        private readonly MultiRegionBatchProcessor _batchProcessor;

        // -- State Tracking --
        private IAssetItem? _selectedAsset;
        private bool _isLoading;
        private string _currentFilePath = "";
        private string _currentUserFilePath = "";
        private List<IAssetItem> _currentDisplayList = new();
        private List<ValidationIssue> _globalIssues = new();

        public MainForm()
        {
            InitializeComponent();

            _batchProcessor = new MultiRegionBatchProcessor(_projectRepo.Metadata);

            AppSettings.Load();
            ApplySettings();

            // Event Wiring
            triggerEditor.TriggerTextChanged += TriggerEditor_TriggerTextChanged;
            triggerEditor.AchievementDataChanged += TriggerEditor_AchievementDataChanged;
            triggerEditor.AssetResetRequested += TriggerEditor_AssetResetRequested;
            triggerEditor.AddFromCodeNoteRequested += TriggerEditor_AddFromCodeNoteRequested;
            triggerEditor.ValidationCompleted += TriggerEditor_ValidationCompleted;

            // This event comes from the Editor Control, so we handle it here in the "Controller"
            triggerEditor.MultiRegionLogicRequested += TriggerEditor_MultiRegionLogicRequested;

            // This event comes from the Editor Tool Menu, but logic is in Menu partial
            triggerEditor.LogicOptimizationRequested += LogicOptimizerToolStripMenuItem_Click;

            codeReviewControl.IssueSelected += CodeReviewControl_IssueSelected;

            triggerEditor.AddressSelected += (s, addr) => codeNoteEditor.HighlightAddress(addr);
            triggerEditor.PointerChainSelected += (s, e) =>
            {
                codeNoteEditor.HighlightAddress(e.BaseAddress);
                if (e.Offsets.Count > 0) codeNoteEditor.HighlightVariable(e.Offsets);
            };

            // Bridge Code Note Editor to Main Menu actions
            codeNoteEditor.ReloadRequested += Reload_Click;
            codeNoteEditor.SaveRequested += saveToolStripMenuItem_Click;

            achievementGrid.CellDoubleClick += achievementGrid_CellDoubleClick;
            achievementGrid.SelectionChanged += achievementGrid_SelectionChanged;
            achievementGrid.KeyDown += achievementGrid_KeyDown;
            achievementGrid.ContextMenuStrip.Opening += AchievementContextMenu_Opening;

            // Filters
            cmbSource.Items.Clear();
            cmbSource.Items.AddRange(new object[] { "JSON", "Local", "New" });
            cmbCategory.Items.Clear();
            cmbCategory.Items.AddRange(new object[] { "Core", "Unofficial" });

            if (AppSettings.Current.LastAssetTypeIndex >= 0 && AppSettings.Current.LastAssetTypeIndex < cmbAssetType.Items.Count)
                cmbAssetType.SelectedIndex = AppSettings.Current.LastAssetTypeIndex;
            else cmbAssetType.SelectedIndex = 0;

            if (AppSettings.Current.LastCategoryIndex >= 0 && AppSettings.Current.LastCategoryIndex < cmbCategory.Items.Count)
                cmbCategory.SelectedIndex = AppSettings.Current.LastCategoryIndex;
            else cmbCategory.SelectedIndex = 0;

            if (AppSettings.Current.LastSourceIndex >= 0 && AppSettings.Current.LastSourceIndex < cmbSource.Items.Count)
                cmbSource.SelectedIndex = AppSettings.Current.LastSourceIndex;
            else cmbSource.SelectedIndex = 0;

            cmbAssetType.SelectedIndexChanged += (s, e) => { ReloadDataView(); RunGlobalValidation(); };
            cmbSource.SelectedIndexChanged += (s, e) => ReloadDataView();
            cmbCategory.SelectedIndexChanged += (s, e) => ReloadDataView();
            txtAssetSearch.TextChanged += (s, e) => ReloadDataView();

            // Menu Wiring
            newAssetToolStripMenuItem.Click += NewAsset_Click;
            cloneAssetToolStripMenuItem.Click += DuplicateAsset_Click;
            deleteAssetToolStripMenuItem.Click += DeleteAsset_Click;
            resetAssetToolStripMenuItem.Click += ResetAsset_Click;
            moveUpAssetToolStripMenuItem.Click += MoveAssetUp_Click;
            moveDownAssetToolStripMenuItem.Click += MoveAssetDown_Click;
            renumberLocalIDsToolStripMenuItem.Click += RenumberLocalIDs_Click;
            renumberContextToolStripMenuItem.Click += RenumberLocalIDs_Click;
            clearDataResetToolStripMenuItem.Click += ClearDataResetToolStripMenuItem_Click;
            viewReviewerToolStripMenuItem.Click += (s, e) => ToggleReviewer();

            // Dynamic Menu Items
            var viewRpDiffItem = new ToolStripMenuItem("View RP Diff");
            viewRpDiffItem.Click += ViewRpDiff_Click;
            viewToolStripMenuItem.DropDownItems.Add(viewRpDiffItem);

            var revertRpItem = new ToolStripMenuItem("Revert Rich Presence");
            revertRpItem.Click += RevertRichPresence_Click;
            editToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
            editToolStripMenuItem.DropDownItems.Add(revertRpItem);

            var revertItem = new ToolStripMenuItem("Revert Multi-Region...");
            revertItem.Click += RevertRegion_Click;
            assetContextMenu.Items.Add(new ToolStripSeparator());
            assetContextMenu.Items.Add(revertItem);

            btnAddAsset.Click += NewAsset_Click;
            btnDuplicateAsset.Click += DuplicateAsset_Click;
            btnDeleteAsset.Click += DeleteAsset_Click;

            this.FormClosing += MainForm_FormClosing;

            _recentFiles.Load();
            UpdateRecentMenu();
            ToggleReviewer(AppSettings.Current.ShowLogicReviewer);

            _mainHistory.HistoryChanged += (s, e) => UpdateUndoRedoState();
            triggerEditor.HistoryChanged += (s, e) => UpdateUndoRedoState();
            achievementGrid.Enter += (s, e) => UpdateUndoRedoState();
            triggerEditor.Enter += (s, e) => UpdateUndoRedoState();
            UpdateUndoRedoState();
        }

        public void LoadFiles(string primaryPath)
        {
            ImageCache.Clear();
            _recentFiles.Add(primaryPath);
            UpdateRecentMenu();

            _currentFilePath = primaryPath;
            _selectedAsset = null;
            achievementGrid.Rows.Clear();
            _mainHistory.Clear();
            triggerEditor.Clear();

            _projectRepo.LoadFiles(primaryPath, out _currentUserFilePath);
            _rpMetadata.Load(_projectRepo.CurrentGameId);

            if (_projectRepo.ServerData != null)
                this.Text = $"RA Logic Editor - {_projectRepo.CurrentTitle}";

            if (cmbSource.SelectedItem?.ToString() != "JSON")
                cmbSource.SelectedItem = "JSON";

            ReloadDataView();

            var notes = _projectRepo.LocalData?.Notes ?? new List<CodeNote>();
            codeNoteEditor.SetNotes(notes);
            triggerEditor.SetNotes(notes);

            RunGlobalValidation();
            UpdateUndoRedoState();
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (!ConfirmDiscardChanges())
            {
                e.Cancel = true;
                return;
            }
            SaveSettings();
        }

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

        private void ApplySettings()
        {
            if (AppSettings.Current.WindowWidth > 0 && AppSettings.Current.WindowHeight > 0)
                this.Size = new Size(AppSettings.Current.WindowWidth, AppSettings.Current.WindowHeight);

            if (AppSettings.Current.WindowX != -1 && AppSettings.Current.WindowY != -1)
            {
                this.StartPosition = FormStartPosition.Manual;
                this.Location = new Point(AppSettings.Current.WindowX, AppSettings.Current.WindowY);
            }
            if (AppSettings.Current.IsMaximized) this.WindowState = FormWindowState.Maximized;

            if (AppSettings.Current.MainSplitterDistance > 0) splitMain.SplitterDistance = AppSettings.Current.MainSplitterDistance;
            if (AppSettings.Current.EditorSplitterDistance > 0) splitEditor.SplitterDistance = AppSettings.Current.EditorSplitterDistance;
            if (AppSettings.Current.VerticalSplitterDistance > 0) splitMiddle.SplitterDistance = AppSettings.Current.VerticalSplitterDistance;

            triggerEditor.ShowDecimal = AppSettings.Current.Trigger_ShowDecimal;
            triggerEditor.ShowAliases = AppSettings.Current.Trigger_ShowAliases;
            triggerEditor.CollapseChains = AppSettings.Current.Trigger_CollapseChains;
            codeNoteEditor.ShowLeafsOnly = AppSettings.Current.Notes_ShowLeafsOnly;
            codeNoteEditor.IndentValues = AppSettings.Current.Notes_IndentValues;
        }

        private void SaveSettings()
        {
            if (this.WindowState == FormWindowState.Normal)
            {
                AppSettings.Current.WindowX = this.Location.X;
                AppSettings.Current.WindowY = this.Location.Y;
                AppSettings.Current.WindowWidth = this.Size.Width;
                AppSettings.Current.WindowHeight = this.Size.Height;
                AppSettings.Current.IsMaximized = false;
            }
            else
            {
                AppSettings.Current.IsMaximized = (this.WindowState == FormWindowState.Maximized);
                if (this.RestoreBounds.Width > 0)
                {
                    AppSettings.Current.WindowWidth = this.RestoreBounds.Width;
                    AppSettings.Current.WindowHeight = this.RestoreBounds.Height;
                    AppSettings.Current.WindowX = this.RestoreBounds.X;
                    AppSettings.Current.WindowY = this.RestoreBounds.Y;
                }
            }

            AppSettings.Current.MainSplitterDistance = splitMain.SplitterDistance;
            AppSettings.Current.EditorSplitterDistance = splitEditor.SplitterDistance;
            AppSettings.Current.VerticalSplitterDistance = splitMiddle.SplitterDistance;

            AppSettings.Current.LastAssetTypeIndex = cmbAssetType.SelectedIndex;
            AppSettings.Current.LastCategoryIndex = cmbCategory.SelectedIndex;
            AppSettings.Current.LastSourceIndex = cmbSource.SelectedIndex;

            AppSettings.Current.Trigger_ShowDecimal = triggerEditor.ShowDecimal;
            AppSettings.Current.Trigger_ShowAliases = triggerEditor.ShowAliases;
            AppSettings.Current.Trigger_CollapseChains = triggerEditor.CollapseChains;

            AppSettings.Current.Notes_ShowLeafsOnly = codeNoteEditor.ShowLeafsOnly;
            AppSettings.Current.Notes_IndentValues = codeNoteEditor.IndentValues;
            AppSettings.Current.ShowLogicReviewer = viewReviewerToolStripMenuItem.Checked;

            AppSettings.Save();
        }

        private void ToggleReviewer(bool? forceState = null)
        {
            bool newState = forceState ?? !viewReviewerToolStripMenuItem.Checked;
            viewReviewerToolStripMenuItem.Checked = newState;
            splitMiddle.Panel2Collapsed = !newState;
        }

        private void UpdateUndoRedoState()
        {
            bool isTriggerContext = triggerEditor.ContainsFocus;
            undoToolStripMenuItem.Enabled = isTriggerContext ? triggerEditor.CanUndo : _mainHistory.CanUndo;
            redoToolStripMenuItem.Enabled = isTriggerContext ? triggerEditor.CanRedo : _mainHistory.CanRedo;
        }

        private void ClearDataResetToolStripMenuItem_Click(object? sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "This will delete all application configuration and cached data. The application will restart.\n\nAre you sure?",
                "Clear Data / Reset", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                try
                {
                    string appPath = Application.StartupPath;
                    if (File.Exists(Path.Combine(appPath, "settings.json"))) File.Delete(Path.Combine(appPath, "settings.json"));
                    if (File.Exists(Path.Combine(appPath, "recent.json"))) File.Delete(Path.Combine(appPath, "recent.json"));
                    if (File.Exists(Path.Combine(appPath, "snippets.json"))) File.Delete(Path.Combine(appPath, "snippets.json"));
                    string cacheDir = Path.Combine(appPath, "Cache");
                    if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true);
                    string metaDir = Path.Combine(appPath, "Metadata");
                    if (Directory.Exists(metaDir)) Directory.Delete(metaDir, true);

                    Application.Restart();
                }
                catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
            }
        }

        // Shared Logic Helpers
        private void EnsureAssetIsLocal(IAssetItem asset)
        {
            if (asset == null || _projectRepo.LocalData == null) return;
            if (asset.State == AssetState.Local || asset.State == AssetState.Modified) return;

            if (asset is RaAchievement ra)
            {
                if (!_projectRepo.LocalData.Achievements.Any(a => a.ID == ra.ID))
                {
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

        private void EnsureAssetIsLocal() => EnsureAssetIsLocal(_selectedAsset!);

        private void TriggerEditor_MultiRegionLogicRequested(object? sender, MultiRegionForm form)
        {
            var configs = form.GetConfiguredRegions();
            if (configs.Count == 0) return;

            var baseConfig = configs.FirstOrDefault(c => c.IsBase);
            var targetConfigs = configs.Where(c => !c.IsBase).ToList();

            _projectRepo.EnsureLocalDataExists();
            PushMainUndo();

            void InvertLockLogic(List<AchievementCondition> conditions)
            {
                foreach (var c in conditions)
                {
                    c.Flag = "Pause If";
                    c.Operator = InvertOperator(c.Operator);
                }
            }

            foreach (var cfg in configs) InvertLockLogic(cfg.LockConditions);

            List<IAssetItem> assetsToProcess;
            if (achievementGrid.SelectedRows.Count > 0)
            {
                assetsToProcess = achievementGrid.SelectedRows.Cast<DataGridViewRow>()
                    .Select(r => r.Tag as IAssetItem).Where(a => a != null).Cast<IAssetItem>().ToList();
            }
            else
            {
                assetsToProcess = new List<IAssetItem>(_currentDisplayList);
            }

            if (assetsToProcess.Count == 0) return;

            int count = 0;
            foreach (var item in assetsToProcess)
            {
                EnsureAssetIsLocal(item);
                IAssetItem? localItem = null;
                if (item.IsLeaderboard) localItem = _projectRepo.LocalData!.Leaderboards.FirstOrDefault(x => x.ID == item.ID);
                else localItem = _projectRepo.LocalData!.Achievements.FirstOrDefault(x => x.ID == item.ID);

                if (localItem == null) continue;

                _batchProcessor.ApplyMultiRegionToAsset(localItem, baseConfig, targetConfigs);
                count++;
            }

            if (form.ShouldUpdateRichPresence && _projectRepo.RichPresence != null)
            {
                _rpMetadata.SaveBackup(_projectRepo.RichPresence.ToString());
                var newRp = _rpService.GenerateMultiRegion(_projectRepo.RichPresence, configs);
                _projectRepo.RichPresence = newRp;
                MessageBox.Show("Rich Presence script updated in memory.", "RP Updated");
            }

            MessageBox.Show($"Multi-Region logic applied to {count} assets.", "Batch Complete");
            ReloadDataView();
            if (_selectedAsset != null) SelectAssetInGrid(_selectedAsset);
        }

        private string InvertOperator(string op)
        {
            return op switch { "=" => "!=", "!=" => "=", "<" => ">=", "<=" => ">", ">" => "<=", ">=" => "<", _ => op };
        }

        private void TriggerEditor_TriggerTextChanged(object? sender, EventArgs e)
        {
            if (_isLoading || _selectedAsset == null) return;
            _projectRepo.EnsureLocalDataExists();
            EnsureAssetIsLocal();
            UpdateAssetState(_selectedAsset);
        }

        private void TriggerEditor_AchievementDataChanged(object? sender, EventArgs e)
        {
            if (_isLoading || _selectedAsset == null) return;
            _projectRepo.EnsureLocalDataExists();

            if (achievementGrid.SelectedRows.Count > 1) ApplyBulkEdit(_selectedAsset);
            else
            {
                EnsureAssetIsLocal();
                UpdateAssetState(_selectedAsset);
            }

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
            if (sourceTemplate is not RaAchievement srcAch) return;
            foreach (DataGridViewRow row in achievementGrid.SelectedRows)
            {
                if (row.Tag is RaAchievement target && target != sourceTemplate)
                {
                    EnsureAssetIsLocal(target);
                    target.Points = srcAch.Points;
                    target.Type = srcAch.Type;
                    UpdateAssetState(target);
                }
            }
        }

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

        private void TriggerEditor_ValidationCompleted(object? sender, List<ValidationIssue> issues)
        {
            if (_selectedAsset == null) return;
            _globalIssues.RemoveAll(i => i.Asset != null && i.Asset.ID == _selectedAsset.ID);
            _globalIssues.AddRange(issues);
            if (!_isLoading) codeReviewControl.SetIssues(_globalIssues);
        }

        private void CodeReviewControl_IssueSelected(object? sender, ValidationIssue issue)
        {
            if (issue.Asset == null) return;
            if (_selectedAsset == null || _selectedAsset.ID != issue.Asset.ID) SelectAssetInGrid(issue.Asset);
            triggerEditor.SelectCondition(issue.Target);
        }

        private void RunGlobalValidation()
        {
            _globalIssues.Clear();
            bool validateLeaderboards = cmbAssetType.SelectedIndex == 1;
            var allAssets = new List<IAssetItem>();

            if (validateLeaderboards) allAssets.AddRange(_projectRepo.GetMergedList<RaLeaderboard>(true));
            else allAssets.AddRange(_projectRepo.GetMergedList<RaAchievement>(false));

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
                    if (nodes.Count > 0 && nodes[0].Size > 1)
                    {
                        if (long.TryParse(key.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long startAddr))
                            rangeCache.Add((startAddr, startAddr + nodes[0].Size, nodes[0]));
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

        private void TriggerEditor_AddFromCodeNoteRequested(object? sender, EventArgs e)
        {
            var data = codeNoteEditor.GetSelectedLogic();
            if (!string.IsNullOrEmpty(data.Logic)) triggerEditor.AddConditionFromNote(data.Logic, data.Type, data.Size);
            else MessageBox.Show("No valid address selected.", "Add Note");
        }

        // Global Undo (Helper methods used by Menu.cs)
        private void PushMainUndo()
        {
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
    }
}