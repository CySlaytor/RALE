#nullable enable
using RALogicEditor.Definitions;
using RALogicEditor.Helpers;
using RALogicEditor.Logic;
using RALogicEditor.Models;
using RALogicEditor.Parsers;
using RALogicEditor.Utilities;
using RALogicEditor.Forms;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace RALogicEditor.Controls
{
    public partial class CodeNoteEditorControl : UserControl
    {
        public event EventHandler? ReloadRequested;
        public event EventHandler? SaveRequested;
        public event EventHandler? NoteUpdated;

        // Fired when the user clicks Reset to delete local override
        public event EventHandler<string>? NoteResetRequested;

        // The ViewModel handles the heavy logic (Moved out of View)
        private readonly CodeNoteViewModel _viewModel;

        // UI State for filtering
        private List<CodeNote> _filteredNotes = new();
        private bool _isInternalUpdate = false;
        private bool _hasUnsavedChanges = false; // Tracks manual modification state for status strip

        // -- Public Properties for Settings Persistence --
        public bool ShowLeafsOnly
        {
            get => chkShowLeafsOnly.Checked;
            set => chkShowLeafsOnly.Checked = value;
        }

        public bool IndentValues
        {
            get => chkIndentValues.Checked;
            set => chkIndentValues.Checked = value;
        }

        public CodeNoteEditorControl()
        {
            InitializeComponent();

            // Dependency Injection (Manual for now)
            IPointerTreeParser parser = new PointerTreeParser();
            _viewModel = new CodeNoteViewModel(parser);

            // Wire up ViewModel Events
            _viewModel.NotesLoaded += (s, e) => ApplyFilter(false);
            _viewModel.NoteContentChanged += ViewModel_NoteContentChanged;
            _viewModel.EditStateChanged += ViewModel_EditStateChanged;
            _viewModel.SelectionRequested += ViewModel_SelectionRequested;

            // Bridge ViewModel modification to real-time event
            _viewModel.NoteContentChanged += (s, e) => NoteUpdated?.Invoke(this, EventArgs.Empty);

            // Re-parse view when filter toggles
            chkShowLeafsOnly.CheckedChanged += (s, e) => RefreshVariableTreeDisplay();

            // Wire up Undo intercept
            txtNoteBody.KeyDown += txtNoteBody_KeyDown;
            txtNoteBody.Enter += txtNoteBody_Enter;

            // Handle Context Menu State Logic
            cmsNoteContext.Opening += (s, e) =>
            {
                deleteNodeToolStripMenuItem.Enabled = !_viewModel.EditState.IsVirtual;
            };

            // Setup Indent Style Dropdown
            cmbIndentStyle.Items.AddRange(new object[] {
                "Unicode Tree (├─)",
                "Auto Detect (. or +)",
                "Dots (.)",
                "Plusses (+)"
            });

            // Set Dropdown Value from Settings safely
            string savedStyle = AppSettings.Current.NoteIndentStyle;
            if (cmbIndentStyle.Items.Contains(savedStyle))
            {
                cmbIndentStyle.SelectedItem = savedStyle;
            }
            else
            {
                cmbIndentStyle.SelectedIndex = 0; // Default to Unicode
            }

            cmbIndentStyle.SelectedIndexChanged += (s, e) =>
            {
                AppSettings.Current.NoteIndentStyle = cmbIndentStyle.SelectedItem?.ToString() ?? "Unicode Tree (├─)";
                AppSettings.Save();
            };
        }

        private void txtNoteBody_Enter(object? sender, EventArgs e)
        {
            // Push undo state right before user starts manually typing
            if (!txtNoteBody.ReadOnly)
            {
                _viewModel.PushUndoState();
            }
        }

        private void txtNoteBody_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Z)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                if (_viewModel.CanUndo)
                {
                    _viewModel.Undo();
                    _hasUnsavedChanges = true;
                    UpdateStatusDisplay("Undone.");
                }
            }
            else if (e.Control && e.KeyCode == Keys.Y)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                if (_viewModel.CanRedo)
                {
                    _viewModel.Redo();
                    _hasUnsavedChanges = true;
                    UpdateStatusDisplay("Redone.");
                }
            }
        }

        private void btnReload_Click(object sender, EventArgs e)
        {
            ReloadRequested?.Invoke(this, EventArgs.Empty);
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            // Trigger save action in Main Form
            SaveRequested?.Invoke(this, EventArgs.Empty);

            // Mark as saved
            _hasUnsavedChanges = false;
            UpdateStatusDisplay("Notes saved.");
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            if (cmbNotes.SelectedItem is CodeNote note)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to reset the note for {note.Address} to its original state?\n\nThis will delete your local modifications and reload the file.",
                    "Reset Note",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    NoteResetRequested?.Invoke(this, note.Address);
                }
            }
        }

        private void btnFixHierarchy_Click(object sender, EventArgs e)
        {
            if (txtNoteBody.ReadOnly) return;

            string currentText = txtNoteBody.Text;
            if (string.IsNullOrWhiteSpace(currentText)) return;

            _viewModel.PushUndoState();

            // Use the ported Logic helper to fix indentation, sort, and merge
            // Pass the indentation prefix from the new dropdown and the indentation preference
            string fixedText = CodeNoteLogic.FixHierarchy(
                currentText,
                cmbIndentStyle.SelectedItem?.ToString() ?? "Unicode Tree (├─)",
                chkIndentValues.Checked
            );

            // Update text box (will trigger TextChanged -> ViewModel buffering)
            if (txtNoteBody.Text != fixedText)
            {
                txtNoteBody.Text = fixedText;
                _viewModel.UpdateNoteContent(fixedText);
                _hasUnsavedChanges = true;
                UpdateStatusDisplay("Hierarchy fixed.");
            }
        }

        // Exposed method for MainForm to call on Save
        public void CommitChanges()
        {
            _viewModel.CommitEditState();
        }

        // Called by Main Form
        public void SetNotes(List<CodeNote> notes)
        {
            _isInternalUpdate = true;
            try
            {
                string? previousAddress = null;
                if (cmbNotes.SelectedItem is CodeNote current)
                    previousAddress = current.Address;

                _viewModel.LoadNotes(notes); // VM processes range cache etc

                // UI Refresh
                cmbNotes.DataSource = null;
                cmbNotes.Items.Clear();
                txtNoteBody.Text = "";
                _viewModel.ClearEditState();

                ApplyFilter(false);

                // Restore selection
                bool selectionRestored = false;
                if (previousAddress != null)
                {
                    var match = _filteredNotes.FirstOrDefault(n => n.Address == previousAddress);
                    if (match != null)
                    {
                        cmbNotes.SelectedItem = match;
                        _viewModel.SelectBaseNote(match);
                        selectionRestored = true;
                    }
                }

                if (!selectionRestored && _filteredNotes.Count > 0)
                {
                    cmbNotes.SelectedIndex = 0;
                    _viewModel.SelectBaseNote(_filteredNotes[0]);
                }
            }
            finally
            {
                _isInternalUpdate = false;
            }
        }

        private void ApplyFilter(bool autoSelectFirst = true)
        {
            string filter = txtSearch.Text.Trim();
            if (string.IsNullOrWhiteSpace(filter))
            {
                _filteredNotes = new List<CodeNote>(_viewModel.AllNotes);
            }
            else
            {
                _filteredNotes = _viewModel.AllNotes
                    .Where(n => n.Address.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                n.Note.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            // FIX: Don't set DataSource to null if it's already empty to prevent infinite traversal failure loops
            // Using a fresh list prevents the control from getting stuck in a bound-error state
            cmbNotes.DataSource = _filteredNotes.Count > 0 ? _filteredNotes : new List<CodeNote>();

            if (autoSelectFirst && _filteredNotes.Count > 0)
            {
                cmbNotes.SelectedIndex = 0;
                _viewModel.SelectBaseNote(_filteredNotes[0]);
            }
            else if (_filteredNotes.Count == 0)
            {
                // Note: We don't clear txtNoteBody here so the user doesn't lose sight of what they were editing
                cmbVariables.DataSource = new List<NoteNode>();
                UpdateStatusDisplay("No matches found.");
            }
        }

        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            if (!_isInternalUpdate) ApplyFilter(true);
        }

        // Event from VM: Base Note changed or Content re-parsed
        private void ViewModel_NoteContentChanged(object? sender, EventArgs e)
        {
            if (_viewModel.CurrentBaseNote == null) return;

            bool wasUpdating = _isInternalUpdate;
            _isInternalUpdate = true;
            try
            {
                // 1. Update Text Box (if not in special edit mode)
                // Although RequestNodeSelection is usually preferred, this fallback handles direct root updates
                if (!_viewModel.EditState.IsVirtual && _viewModel.EditState.TargetNode == null)
                {
                    // If redirected, the VM provides the Linked Content, otherwise we use the Note's content
                    string raw = _viewModel.IsRedirected ? _viewModel.RedirectedContent : _viewModel.CurrentBaseNote.Note;
                    string normalized = raw.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");

                    txtNoteBody.Text = normalized;

                    // Lock if redirected
                    txtNoteBody.ReadOnly = _viewModel.IsRedirected;
                }

                // 2. Refresh Tree Dropdown
                RefreshVariableTreeDisplay();

                // 3. Select Root (If nothing else was selected during a tree refresh)
                if (cmbVariables.Items.Count > 0 && cmbVariables.SelectedIndex == -1)
                    cmbVariables.SelectedIndex = 0;

                UpdateStatusDisplay(null);
            }
            finally
            {
                _isInternalUpdate = wasUpdating;
            }
        }

        private void RefreshVariableTreeDisplay()
        {
            // Filter the *already parsed* nodes from the VM for display
            IEnumerable<NoteNode> query = _viewModel.CurrentParsedNodes;

            string filter = txtSearch.Text.Trim();
            if (!string.IsNullOrEmpty(filter))
            {
                query = query.Where(n =>
                    n.Description.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    n.Offset.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    (n.IndentLevel == -2 && n.Content.Contains(filter, StringComparison.OrdinalIgnoreCase))
                );
            }

            if (chkShowLeafsOnly.Checked)
            {
                query = query.Where(n => n.IndentLevel == -2 || (n.Children.Count == 0 && n.IndentLevel != -1));
            }

            var finalList = query.ToList();

            // Preserve selection
            NoteNode? previousSelection = cmbVariables.SelectedItem as NoteNode;

            cmbVariables.DataSource = finalList;

            if (previousSelection != null && finalList.Contains(previousSelection))
                cmbVariables.SelectedItem = previousSelection;

            // Disable dropdown if redirected to avoid confusing "edit via tree" actions on read-only content
            // Though reading is fine, editing is not. Let's keep it enabled for navigation but block write.
            cmbVariables.Enabled = finalList.Count > 1 && !_viewModel.EditState.IsVirtual;
            cmbVariables.BackColor = _viewModel.EditState.IsVirtual ? SystemColors.Control : SystemColors.Window;
        }

        private void ViewModel_EditStateChanged(object? sender, EventArgs e)
        {
            var state = _viewModel.EditState;

            // Prevent the TextChanged event from firing and marking the state dirty immediately
            bool wasUpdating = _isInternalUpdate;
            _isInternalUpdate = true;

            try
            {
                if (state.IsVirtual)
                {
                    // Virtual Mode (Creating new node)
                    // Implicitly dirty/unsaved if creating a new note
                    _hasUnsavedChanges = true;

                    cmbVariables.Enabled = false;
                    cmbVariables.BackColor = SystemColors.Control;

                    // Allow edit of virtual note to support injection
                    txtNoteBody.Text = state.DisplayText;
                    txtNoteBody.ReadOnly = false;
                }
                else if (state.TargetNode != null)
                {
                    // Real Node Editing Mode
                    cmbVariables.Enabled = true;
                    cmbVariables.BackColor = SystemColors.Window;

                    txtNoteBody.Text = state.DisplayText;

                    // Respect redirection lock
                    txtNoteBody.ReadOnly = _viewModel.IsRedirected;
                }
                else
                {
                    // Root / Default
                    cmbVariables.Enabled = true;
                    cmbVariables.BackColor = SystemColors.Window;
                    txtNoteBody.ReadOnly = _viewModel.IsRedirected;
                }

                // Disable fix hierarchy if readonly
                btnFixHierarchy.Enabled = !txtNoteBody.ReadOnly;

                UpdateStatusDisplay(null);
            }
            finally
            {
                _isInternalUpdate = wasUpdating;
            }
        }

        private void ViewModel_SelectionRequested(object? sender, NoteNode node)
        {
            // VM asks us to select a node in the combobox. We try to find it in the visible list.
            bool wasUpdating = _isInternalUpdate;
            _isInternalUpdate = true;
            try
            {
                if (cmbVariables.Items.Contains(node))
                {
                    cmbVariables.SelectedItem = node;
                }
                else
                {
                    // Node is filtered out of view, just clear selection
                    cmbVariables.SelectedIndex = -1;
                }
                // Note: Text Body update is handled by EditStateChanged
            }
            finally
            {
                _isInternalUpdate = wasUpdating;
            }
        }

        private void cmbNotes_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isInternalUpdate) return;
            if (cmbNotes.SelectedItem is CodeNote note)
            {
                _viewModel.SelectBaseNote(note);
                _hasUnsavedChanges = false; // Reset unsaved status on deliberate note switch
            }
        }

        private void cmbVariables_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isInternalUpdate) return;

            if (cmbVariables.SelectedItem is NoteNode node)
            {
                // UI Update Logic:
                // Requesting selection via ViewModel ensures EditState is set up correctly for buffering
                if (node.IndentLevel == -2)
                {
                    // Root node
                    _viewModel.ClearEditState();
                    // Manually allow editing the root note content via buffer too
                    _viewModel.RequestNodeSelection(node);
                }
                else
                {
                    _viewModel.RequestNodeSelection(node);
                }
            }
        }

        private void txtNoteBody_TextChanged(object sender, EventArgs e)
        {
            if (_isInternalUpdate) return;

            // All editing is now buffered via the ViewModel.
            // This prevents live overwrites of the file and allows cancelling/undo if needed (future feature).
            _viewModel.UpdateBufferedText(txtNoteBody.Text);

            // Mark as having unsaved changes whenever text is manually edited
            _hasUnsavedChanges = true;
            UpdateStatusDisplay(null);
        }

        // Centralized Status Update Logic
        private void UpdateStatusDisplay(string? overrideMsg)
        {
            if (overrideMsg != null)
            {
                lblOffsetChain.Text = overrideMsg;
                return;
            }

            var state = _viewModel.EditState;
            string status = "";

            if (state.IsVirtual)
            {
                status = $"{state.Description} (New Note)";
            }
            else if (state.TargetNode != null)
            {
                status = state.Description;
            }
            else
            {
                // Fallback / Root
                if (_viewModel.IsRedirected)
                    status = $"[Virtual: Linked to {_viewModel.RedirectTargetAddress}]";
                else
                    status = "Ready";
            }

            // Append persistent modified state
            // Logic: IsDirty is ephemeral (cleared on commit), _hasUnsavedChanges is persistent (cleared on Save)
            // We want to show "Modified" if either the current buffer is dirty OR we have unsaved commits.
            if (_hasUnsavedChanges || state.IsDirty)
            {
                if (!status.Contains("(Modified"))
                    status += " (Modified)";
            }

            lblOffsetChain.Text = status;
        }

        // Public Bridge Methods calling VM
        public void HighlightAddress(string address)
        {
            // Try to find note in VM
            _viewModel.HighlightAddress(address);

            // If HighlightAddress found an exact note, it called SelectBaseNote -> NoteContentChanged.
            // We just need to ensure the Note ComboBox reflects the VM state.
            if (_viewModel.CurrentBaseNote != null)
            {
                if (cmbNotes.SelectedItem != _viewModel.CurrentBaseNote)
                {
                    bool wasUpdating = _isInternalUpdate;
                    _isInternalUpdate = true;

                    // If not in filtered list, clear filter
                    if (!_filteredNotes.Contains(_viewModel.CurrentBaseNote))
                    {
                        txtSearch.Text = ""; // This triggers ApplyFilter -> sets _filteredNotes -> sets DataSource
                    }

                    cmbNotes.SelectedItem = _viewModel.CurrentBaseNote;
                    _isInternalUpdate = wasUpdating;
                }
            }
        }

        public void HighlightVariable(List<long> offsets)
        {
            _viewModel.HighlightVariable(offsets);
            // Ensure UI combo matches ViewModel selection
            if (_viewModel.CurrentBaseNote != null && cmbNotes.SelectedItem != _viewModel.CurrentBaseNote)
            {
                // Sync logic similar to HighlightAddress
                bool wasUpdating = _isInternalUpdate;
                _isInternalUpdate = true;
                if (!_filteredNotes.Contains(_viewModel.CurrentBaseNote)) txtSearch.Text = "";
                cmbNotes.SelectedItem = _viewModel.CurrentBaseNote;
                _isInternalUpdate = wasUpdating;
            }
        }

        public (string Logic, string Type, string Size) GetSelectedLogic()
        {
            var selectedNode = cmbVariables.SelectedItem as NoteNode;
            return _viewModel.GetSelectedLogic(selectedNode);
        }

        private void cmbNotes_Format(object sender, ListControlConvertEventArgs e)
        {
            if (e.ListItem is CodeNote note)
            {
                string firstLine = note.Note.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                if (firstLine.Length > 50) firstLine = firstLine.Substring(0, 47) + "...";
                e.Value = $"{note.Address} | {firstLine}";
            }
        }

        private void cmbVariables_Format(object sender, ListControlConvertEventArgs e)
        {
            if (e.ListItem is NoteNode node)
            {
                string text = node.ToString();
                if (!chkShowLeafsOnly.Checked && node.IndentLevel > 0)
                {
                    string pad = new string(' ', node.IndentLevel * 2);
                    text = pad + text;
                }
                e.Value = text;
            }
        }

        // Context Menu Handlers
        private void cutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!txtNoteBody.ReadOnly)
            {
                _viewModel.PushUndoState();
                txtNoteBody.Cut();
            }
        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!txtNoteBody.ReadOnly)
            {
                _viewModel.PushUndoState();
                txtNoteBody.Paste();
            }
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(txtNoteBody.SelectedText))
            {
                string raw = txtNoteBody.SelectedText;
                string normalized = raw.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
                Clipboard.SetText(normalized);
                lblOffsetChain.Text = "Copied selection to clipboard.";
            }
        }

        // --- Multiplier Features ---
        private void multiplySelectedTextToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (txtNoteBody.ReadOnly) return;

            string targetText = txtNoteBody.SelectedText;

            if (string.IsNullOrEmpty(targetText))
            {
                MessageBox.Show("Please highlight the block of text you want to duplicate before clicking this option.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var form = new NoteMultiplierForm())
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    _viewModel.PushUndoState(); // Store undo state before applying the multiplier

                    string generated = CodeNoteMultiplier.GenerateText(
                        targetText,
                        form.Copies,
                        form.Stride,
                        form.Naming,
                        form.SearchText,
                        form.ReplaceText,
                        form.StartNumber,
                        form.ApplyToChildren);

                    // We apply via the ViewModel to ensure it tracks history properly
                    int insertPos = txtNoteBody.SelectionStart + txtNoteBody.SelectionLength;
                    string newFullText = txtNoteBody.Text.Insert(insertPos, "\r\n" + generated);

                    _viewModel.UpdateNoteContent(newFullText);

                    txtNoteBody.SelectionStart = insertPos + 2;
                    txtNoteBody.SelectionLength = generated.Length;

                    _hasUnsavedChanges = true;
                    UpdateStatusDisplay("Note multiplied.");
                }
            }
        }

        private void batchMultiplyNotesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_viewModel.AllNotes.Count == 0) return;

            using (var form = new BatchNoteMultiplierForm(_viewModel.AllNotes))
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    _viewModel.BatchMultiplyNotes(
                        form.SelectedNotes,
                        form.SearchKeyword,
                        form.ReplacePattern,
                        form.Copies,
                        form.Stride,
                        form.StartNumber);

                    _hasUnsavedChanges = true;
                    UpdateStatusDisplay($"Batch duplicated {form.SelectedNotes.Count} note(s).");
                }
            }
        }

        private void deleteNodeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (txtNoteBody.ReadOnly) return;

            if (_viewModel.EditState.IsVirtual)
            {
                MessageBox.Show("Cannot use Intelligent Delete in Virtual Mode. Please select a real node.", "Invalid Target", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (cmbVariables.SelectedItem is NoteNode node)
            {
                if (node.IndentLevel == -2)
                {
                    MessageBox.Show("Cannot intelligently delete the Root note. Use manual text deletion if you want to clear it.", "Invalid Target", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                _viewModel.IntelligentDelete(node);
                _hasUnsavedChanges = true;
                UpdateStatusDisplay("Node and orphaned parents deleted.");
            }
            else
            {
                MessageBox.Show("Please select a specific node in the dropdown tree to use Intelligent Delete.", "No Node Selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}