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
        #region Services & Dependencies
        // Handles File I/O and merging Server vs Local data
        private readonly ProjectRepository _projectRepo = new();
        // Manages global Undo/Redo for the asset list
        private readonly HistoryManager<UserTextParser.UserData> _mainHistory = new();
        // Tracks recently opened files
        private readonly RecentFilesManager _recentFiles = new();
        // Validates logic rules (PauseLocks, Stale Pointers, etc.)
        private readonly ILogicValidator _validator = new LogicValidator();
        // Handles Rich Presence script generation
        private readonly RichPresenceService _rpService = new();
        // Manages RP backups for reversion
        private readonly RichPresenceMetadataService _rpMetadata = new();

        // Extracted Logic Services
        private readonly UserFileSerializer _fileSerializer = new();
        private readonly MultiRegionBatchProcessor _batchProcessor;
        #endregion

        #region State
        private IAssetItem? _selectedAsset;   // Currently edited item
        private bool _isLoading;              // Suppress events during batch updates
        private bool _isDirty = false;        // Tracks if changes were made in current session
        private string _currentFilePath = ""; // Path to source JSON
        private string _currentUserFilePath = ""; // Path to local User.txt
        private List<IAssetItem> _currentDisplayList = new(); // Items currently visible in grid
        private List<ValidationIssue> _globalIssues = new(); // Code Review violations
        #endregion

        public MainForm()
        {
            InitializeComponent();

            // Initialize services that require dependencies
            _batchProcessor = new MultiRegionBatchProcessor(_projectRepo.Metadata);

            // Load UI settings (Window size, splitters)
            AppSettings.Load();
            ApplySettings();

            // --- Event Wiring ---

            // Trigger Editor (Logic)
            triggerEditor.TriggerTextChanged += TriggerEditor_TriggerTextChanged;
            triggerEditor.AchievementDataChanged += TriggerEditor_AchievementDataChanged;
            triggerEditor.AssetResetRequested += TriggerEditor_AssetResetRequested;
            triggerEditor.AddFromCodeNoteRequested += TriggerEditor_AddFromCodeNoteRequested;
            triggerEditor.ValidationCompleted += TriggerEditor_ValidationCompleted;
            triggerEditor.MultiRegionLogicRequested += TriggerEditor_MultiRegionLogicRequested;
            triggerEditor.LogicOptimizationRequested += LogicOptimizerToolStripMenuItem_Click;

            // Bridge Trigger Editor -> Code Note Editor (Highlighting)
            triggerEditor.AddressSelected += (s, addr) => codeNoteEditor.HighlightAddress(addr);
            triggerEditor.PointerChainSelected += (s, e) =>
            {
                codeNoteEditor.HighlightAddress(e.BaseAddress);
                if (e.Offsets.Count > 0) codeNoteEditor.HighlightVariable(e.Offsets);
            };

            // Trigger Editor -> MainForm Bridge for Multiplier
            triggerEditor.BatchMultiplyNotesRequested += TriggerEditor_BatchMultiplyNotesRequested;

            // Code Reviewer
            codeReviewControl.IssueSelected += CodeReviewControl_IssueSelected;

            // Code Note Editor actions
            codeNoteEditor.ReloadRequested += Reload_Click;
            codeNoteEditor.SaveRequested += saveToolStripMenuItem_Click;
            codeNoteEditor.NoteUpdated += CodeNoteEditor_NoteUpdated; // Real-time grid updates
            codeNoteEditor.NoteResetRequested += CodeNoteEditor_NoteResetRequested; // Resets local note override

            // Main Grid
            achievementGrid.CellDoubleClick += achievementGrid_CellDoubleClick;
            achievementGrid.SelectionChanged += achievementGrid_SelectionChanged;
            achievementGrid.KeyDown += achievementGrid_KeyDown;
            achievementGrid.ContextMenuStrip.Opening += AchievementContextMenu_Opening;

            // Filters & Toolbar
            InitializeFilters();

            // Main Menu
            InitializeMainMenu();

            // Global Events
            this.FormClosing += MainForm_FormClosing;
            this.Load += MainForm_Load; // Apply splitters post-layout

            // Undo/Redo State Listeners
            _mainHistory.HistoryChanged += (s, e) => UpdateUndoRedoState();
            triggerEditor.HistoryChanged += (s, e) => UpdateUndoRedoState();
            achievementGrid.Enter += (s, e) => UpdateUndoRedoState();
            triggerEditor.Enter += (s, e) => UpdateUndoRedoState();

            // Initial Load
            _recentFiles.Load();
            UpdateRecentMenu();
            ToggleReviewer(AppSettings.Current.ShowLogicReviewer);
            UpdateUndoRedoState();
        }

        private void MainForm_Load(object? sender, EventArgs e)
        {
            // Apply splitter distances after the form has fully scaled and laid out
            // This prevents the code reviewer pane and editor from squashing over time
            try
            {
                if (AppSettings.Current.MainSplitterDistance > 0) splitMain.SplitterDistance = AppSettings.Current.MainSplitterDistance;
                if (AppSettings.Current.EditorSplitterDistance > 0) splitEditor.SplitterDistance = AppSettings.Current.EditorSplitterDistance;
                if (AppSettings.Current.VerticalSplitterDistance > 0) splitMiddle.SplitterDistance = AppSettings.Current.VerticalSplitterDistance;
            }
            catch { /* Ignore boundary exceptions if the window starts minimized or too small */ }
        }

        private void CodeNoteEditor_NoteUpdated(object? sender, EventArgs e)
        {
            // Sync changes instantly to the Grid Editor when notes are edited/fixed
            if (!_isLoading && _projectRepo.LocalData != null)
            {
                triggerEditor.SetNotes(_projectRepo.LocalData.Notes);
            }
        }

        private void CodeNoteEditor_NoteResetRequested(object? sender, string address)
        {
            // Removes a local note modification and restores the original server note logic instantly
            if (_projectRepo.LocalData == null) return;

            PushMainUndo();
            _projectRepo.RevertNote(address);
            _isDirty = true;

            // Instantly refresh editors with the restored notes list
            var notes = _projectRepo.LocalData.Notes;
            codeNoteEditor.SetNotes(notes);
            triggerEditor.SetNotes(notes);
            RunGlobalValidation();
        }

        private void TriggerEditor_BatchMultiplyNotesRequested(object? sender, Forms.BatchNoteMultiplierForm form)
        {
            _projectRepo.EnsureLocalDataExists();
            PushMainUndo();

            // Pass execution to code note editor which handles its internal UI formatting
            codeNoteEditor.ExecuteBatchMultiply(
                form.SelectedNotes,
                form.SearchKeyword,
                form.ReplacePattern,
                form.Copies,
                form.Stride,
                form.StartNumber);

            _isDirty = true;

            // Instantly refresh logic editors with the restored/modified notes list
            var notes = _projectRepo.LocalData!.Notes;
            triggerEditor.SetNotes(notes);
            RunGlobalValidation();
        }

        private void InitializeFilters()
        {
            cmbSource.Items.Clear();
            cmbSource.Items.AddRange(new object[] { "JSON", "Local", "New" });
            cmbCategory.Items.Clear();
            cmbCategory.Items.AddRange(new object[] { "Core", "Unofficial" });

            // Restore previous filter state
            cmbAssetType.SelectedIndex = (AppSettings.Current.LastAssetTypeIndex >= 0 && AppSettings.Current.LastAssetTypeIndex < cmbAssetType.Items.Count)
                ? AppSettings.Current.LastAssetTypeIndex : 0;

            cmbCategory.SelectedIndex = (AppSettings.Current.LastCategoryIndex >= 0 && AppSettings.Current.LastCategoryIndex < cmbCategory.Items.Count)
                ? AppSettings.Current.LastCategoryIndex : 0;

            cmbSource.SelectedIndex = (AppSettings.Current.LastSourceIndex >= 0 && AppSettings.Current.LastSourceIndex < cmbSource.Items.Count)
                ? AppSettings.Current.LastSourceIndex : 0;

            // Wiring
            cmbAssetType.SelectedIndexChanged += (s, e) => { ReloadDataView(); RunGlobalValidation(); };
            cmbSource.SelectedIndexChanged += (s, e) => ReloadDataView();
            cmbCategory.SelectedIndexChanged += (s, e) => ReloadDataView();
            txtAssetSearch.TextChanged += (s, e) => ReloadDataView();
        }

        private void InitializeMainMenu()
        {
            newAssetToolStripMenuItem.Click += NewAsset_Click;
            cloneAssetToolStripMenuItem.Click += DuplicateAsset_Click;
            deleteAssetToolStripMenuItem.Click += DeleteAsset_Click;
            resetAssetToolStripMenuItem.Click += ResetAsset_Click;
            moveUpAssetToolStripMenuItem.Click += MoveAssetUp_Click;
            moveDownAssetToolStripMenuItem.Click += MoveAssetDown_Click;
            restoreOrderToolStripMenuItem.Click += RestoreOrder_Click; // Added Natural Order Revert
            renumberLocalIDsToolStripMenuItem.Click += RenumberLocalIDs_Click;
            renumberContextToolStripMenuItem.Click += RenumberLocalIDs_Click;
            clearDataResetToolStripMenuItem.Click += ClearDataResetToolStripMenuItem_Click;
            viewReviewerToolStripMenuItem.Click += (s, e) => ToggleReviewer();

            // Dynamic Menu Construction
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
        }

        // Full Project Load Logic
        public void LoadFiles(string primaryPath)
        {
            // 1. Lock UI to prevent flickering and event storms
            _isLoading = true;
            achievementGrid.SuspendLayout();

            try
            {
                ImageCache.Clear();

                // Configure local paths for badge resolution
                // This looks for the ../Badge folder relative to the loaded file
                ImageCache.ConfigureLocalPaths(primaryPath);

                _recentFiles.Add(primaryPath);
                UpdateRecentMenu();

                _currentFilePath = primaryPath;
                _selectedAsset = null;
                achievementGrid.Rows.Clear();
                _mainHistory.Clear();
                triggerEditor.Clear();

                // 2. Load Repository
                try
                {
                    _projectRepo.LoadFiles(primaryPath, out _currentUserFilePath);
                    _rpMetadata.Load(_projectRepo.CurrentGameId);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading files: {ex.Message}", "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (_projectRepo.ServerData != null)
                    this.Text = $"RA Logic Editor - {_projectRepo.CurrentTitle}";
                else
                    this.Text = $"RA Logic Editor - {Path.GetFileName(primaryPath)}";

                // 3. INTELLIGENT SOURCE SELECTION
                // Check if Server Data actually contains assets.
                // Just checking _projectRepo.ServerData != null is insufficient because empty JSONs exist.
                bool hasServerAssets = _projectRepo.ServerData != null &&
                                       _projectRepo.ServerData.Sets != null &&
                                       _projectRepo.ServerData.Sets.Any(s =>
                                           (s.Achievements != null && s.Achievements.Count > 0) ||
                                           (s.Leaderboards != null && s.Leaderboards.Count > 0));

                // If Server Data has assets, default to "JSON".
                // If it is empty or missing, default to "New" so the user can see their local work immediately.
                string targetSource = hasServerAssets ? "JSON" : "New";

                // Only update if different to avoid triggering unnecessary events, 
                // though _isLoading protects us now anyway.
                if (cmbSource.SelectedItem?.ToString() != targetSource)
                {
                    cmbSource.SelectedItem = targetSource;
                }

                // 4. Force one clean refresh
                ReloadDataView();

                // Push notes to sub-editors
                var notes = _projectRepo.LocalData?.Notes ?? new List<CodeNote>();
                codeNoteEditor.SetNotes(notes);
                triggerEditor.SetNotes(notes);

                RunGlobalValidation();
                UpdateUndoRedoState();

                // Reset dirty flag after a fresh load
                _isDirty = false;
            }
            finally
            {
                // 5. Unlock UI
                achievementGrid.ResumeLayout();
                _isLoading = false;
            }
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

        // Prompts user if there are unsaved local changes
        private bool ConfirmDiscardChanges()
        {
            if (!_isDirty) return true;

            var result = MessageBox.Show("You have unsaved changes. Any unsaved local changes will be lost.\n\nContinue?",
                "Unsaved Changes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            return result == DialogResult.Yes;
        }

        #region State & Settings Management

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
                // Don't overwrite coords if minimized
                if (this.RestoreBounds.Width > 0)
                {
                    AppSettings.Current.WindowWidth = this.RestoreBounds.Width;
                    AppSettings.Current.WindowHeight = this.RestoreBounds.Height;
                    AppSettings.Current.WindowX = this.RestoreBounds.X;
                    AppSettings.Current.WindowY = this.RestoreBounds.Y;
                }
            }

            // Ensure we don't save collapsed splitter distances if the window is minimized or hidden
            if (this.WindowState != FormWindowState.Minimized)
            {
                AppSettings.Current.MainSplitterDistance = splitMain.SplitterDistance;
                AppSettings.Current.EditorSplitterDistance = splitEditor.SplitterDistance;
                AppSettings.Current.VerticalSplitterDistance = splitMiddle.SplitterDistance;
            }

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
                    // Delete settings files
                    if (File.Exists(Path.Combine(appPath, "settings.json"))) File.Delete(Path.Combine(appPath, "settings.json"));
                    if (File.Exists(Path.Combine(appPath, "recent.json"))) File.Delete(Path.Combine(appPath, "recent.json"));
                    if (File.Exists(Path.Combine(appPath, "snippets.json"))) File.Delete(Path.Combine(appPath, "snippets.json"));

                    // Delete cache directories
                    string cacheDir = Path.Combine(appPath, "Cache");
                    if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true);
                    string metaDir = Path.Combine(appPath, "Metadata");
                    if (Directory.Exists(metaDir)) Directory.Delete(metaDir, true);

                    Application.Restart();
                }
                catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
            }
        }
        #endregion

        #region Shared Logic Helpers (Used by Partials)

        // Ensures an asset exists in the Local Data list (Clone if Synced)
        private void EnsureAssetIsLocal(IAssetItem asset)
        {
            if (asset == null || _projectRepo.LocalData == null) return;
            if (asset.State == AssetState.Local || asset.State == AssetState.Modified) return;

            if (asset is RaAchievement ra)
            {
                if (!_projectRepo.LocalData.Achievements.Any(a => a.ID == ra.ID))
                {
                    // FIX: If the asset passed in IS the currently selected asset (which is already a working clone),
                    // we must add IT directly to LocalData, not create a new clone.
                    // This ensures the object bound to the UI is the one being saved.
                    if (object.ReferenceEquals(asset, _selectedAsset))
                    {
                        _projectRepo.LocalData.Achievements.Add(ra);
                    }
                    else
                    {
                        var clone = ra.Clone(false);
                        _projectRepo.LocalData.Achievements.Add(clone);
                    }
                }
            }
            else if (asset is RaLeaderboard rl)
            {
                if (!_projectRepo.LocalData.Leaderboards.Any(l => l.ID == rl.ID))
                {
                    if (object.ReferenceEquals(asset, _selectedAsset))
                    {
                        _projectRepo.LocalData.Leaderboards.Add(rl);
                    }
                    else
                    {
                        var clone = rl.Clone(false);
                        _projectRepo.LocalData.Leaderboards.Add(clone);
                    }
                }
            }
        }

        private void EnsureAssetIsLocal() => EnsureAssetIsLocal(_selectedAsset!);

        private string InvertOperator(string op)
        {
            return op switch { "=" => "!=", "!=" => "=", "<" => ">=", "<=" => ">", ">" => "<=", ">=" => "<", _ => op };
        }

        #endregion

        #region Logic & Trigger Editor Events

        private void TriggerEditor_MultiRegionLogicRequested(object? sender, MultiRegionForm form)
        {
            var configs = form.GetConfiguredRegions();
            if (configs.Count == 0) return;

            var baseConfig = configs.FirstOrDefault(c => c.IsBase);
            var targetConfigs = configs.Where(c => !c.IsBase).ToList();

            _projectRepo.EnsureLocalDataExists();
            PushMainUndo();

            // 1. GENERATE RICH PRESENCE FIRST (Using original, un-inverted positive conditions)
            if (form.ShouldUpdateRichPresence && _projectRepo.RichPresence != null)
            {
                _rpMetadata.SaveBackup(_projectRepo.RichPresence.ToString());
                var newRp = _rpService.GenerateMultiRegion(_projectRepo.RichPresence, configs);
                _projectRepo.RichPresence = newRp;
                MessageBox.Show("Rich Presence script updated in memory.", "RP Updated");
            }

            // 2. INVERT LOCK LOGIC FOR ACHIEVEMENTS (If PauseLocks are enabled)
            if (form.UsePauseLocks)
            {
                void InvertLockLogic(List<AchievementCondition> conditions)
                {
                    foreach (var c in conditions)
                    {
                        c.Flag = "Pause If";
                        c.Operator = InvertOperator(c.Operator);
                    }
                }

                foreach (var cfg in configs) InvertLockLogic(cfg.LockConditions);
            }

            // Determine targets: Selected or All Filtered
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

                // DELEGATE TO BATCH PROCESSOR
                _batchProcessor.ApplyMultiRegionToAsset(localItem, baseConfig, targetConfigs);
                count++;
            }

            _isDirty = true;
            MessageBox.Show($"Multi-Region logic applied to {count} assets.", "Batch Complete");
            ReloadDataView();
            if (_selectedAsset != null) SelectAssetInGrid(_selectedAsset);
        }

        private void TriggerEditor_TriggerTextChanged(object? sender, EventArgs e)
        {
            if (_isLoading || _selectedAsset == null) return;
            _projectRepo.EnsureLocalDataExists();
            EnsureAssetIsLocal();
            UpdateAssetState(_selectedAsset);
            _isDirty = true;
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

            _isDirty = true;

            // Update visible grid row
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

            _isDirty = true;
            ReloadDataView();
            LoadSelectedAsset();
        }

        private void TriggerEditor_AddFromCodeNoteRequested(object? sender, EventArgs e)
        {
            var data = codeNoteEditor.GetSelectedLogic();
            if (!string.IsNullOrEmpty(data.Logic)) triggerEditor.AddConditionFromNote(data.Logic, data.Type, data.Size);
            else MessageBox.Show("No valid address selected.", "Add Note");
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
        #endregion

        #region Undo/Redo System
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
            _isDirty = true;
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
            _isDirty = true;
            ReloadDataView();
            LoadSelectedAsset();
        }
        #endregion
    }
}