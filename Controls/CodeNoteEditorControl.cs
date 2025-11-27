#nullable enable
using RALogicEditor.Definitions;
using RALogicEditor.Helpers;
using RALogicEditor.Models;
using RALogicEditor.Parsers;
using RALogicEditor.Utilities;
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

        // The ViewModel handles the heavy logic (Moved out of View)
        private readonly CodeNoteViewModel _viewModel;

        // UI State for filtering
        private List<CodeNote> _filteredNotes = new();
        private bool _isInternalUpdate = false;

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

            // Re-parse view when filter toggles
            chkShowLeafsOnly.CheckedChanged += (s, e) => RefreshVariableTreeDisplay();
        }

        private void btnReload_Click(object sender, EventArgs e)
        {
            ReloadRequested?.Invoke(this, EventArgs.Empty);
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

            cmbNotes.DataSource = null;
            cmbNotes.DataSource = _filteredNotes;

            if (autoSelectFirst && _filteredNotes.Count > 0)
            {
                cmbNotes.SelectedIndex = 0;
                _viewModel.SelectBaseNote(_filteredNotes[0]);
            }
            else if (_filteredNotes.Count == 0)
            {
                txtNoteBody.Text = "";
                cmbVariables.DataSource = null;
                UpdateStatusStrip(null);
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
                    // New: If redirected, the VM provides the Linked Content, otherwise we use the Note's content
                    string raw = _viewModel.IsRedirected ? _viewModel.RedirectedContent : _viewModel.CurrentBaseNote.Note;
                    string normalized = raw.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");

                    txtNoteBody.Text = normalized;

                    // Lock if redirected
                    txtNoteBody.ReadOnly = _viewModel.IsRedirected;
                    if (_viewModel.IsRedirected)
                        lblOffsetChain.Text = $"[Virtual: Linked to {_viewModel.RedirectTargetAddress}]";
                }

                // 2. Refresh Tree Dropdown
                RefreshVariableTreeDisplay();

                // 3. Select Root
                if (cmbVariables.Items.Count > 0) cmbVariables.SelectedIndex = 0;
                else cmbVariables.SelectedIndex = -1;

                UpdateStatusStrip(null);
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
            cmbVariables.DataSource = finalList;

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
                    string status = state.IsDirty ? " (Modified - Save to Commit)" : "";
                    lblOffsetChain.Text = state.Description + " (Unsaved Virtual Note)" + status;

                    cmbVariables.Enabled = false;
                    cmbVariables.BackColor = SystemColors.Control;

                    // Allow edit of virtual note to support injection
                    txtNoteBody.Text = state.DisplayText;
                    txtNoteBody.ReadOnly = false;
                }
                else if (state.TargetNode != null)
                {
                    // Real Node Editing Mode
                    string status = state.IsDirty ? " (Modified)" : "";
                    lblOffsetChain.Text = state.Description + status;

                    cmbVariables.Enabled = true;
                    cmbVariables.BackColor = SystemColors.Window;

                    txtNoteBody.Text = state.DisplayText;

                    // Respect redirection lock
                    txtNoteBody.ReadOnly = _viewModel.IsRedirected;
                }
                else
                {
                    // Root / Default
                    if (!_viewModel.IsRedirected) lblOffsetChain.Text = "Ready";
                    cmbVariables.Enabled = true;
                    cmbVariables.BackColor = SystemColors.Window;
                    txtNoteBody.ReadOnly = _viewModel.IsRedirected;
                }
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

            // Update status label to show dirty state without full refresh
            if (_viewModel.EditState.IsDirty)
            {
                if (!lblOffsetChain.Text.Contains("(Modified"))
                    lblOffsetChain.Text += " (Modified)";
            }
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

        private void UpdateStatusStrip(NoteNode? node)
        {
            // Fallback status update, mostly handled by EditStateChanged now
            if (_viewModel.EditState.IsVirtual) return;

            if (node == null)
            {
                if (_viewModel.IsRedirected) lblOffsetChain.Text = $"[Virtual: Linked to {_viewModel.RedirectTargetAddress}]";
                else lblOffsetChain.Text = "Ready";
                return;
            }
            var chain = new List<string>();
            var current = node;
            while (current != null && current.IndentLevel != -1) { chain.Insert(0, current.Offset); current = current.Parent; }
            if (chain.Count > 0) lblOffsetChain.Text = $"[Indirect Note {string.Join(" ", chain)}]"; else lblOffsetChain.Text = "Direct Variable";
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
        private void cutToolStripMenuItem_Click(object sender, EventArgs e) { if (!txtNoteBody.ReadOnly) txtNoteBody.Cut(); }
        private void pasteToolStripMenuItem_Click(object sender, EventArgs e) { if (!txtNoteBody.ReadOnly) txtNoteBody.Paste(); }

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

        private void btnCopyAll_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(txtNoteBody.Text))
            {
                string raw = txtNoteBody.Text;
                string normalized = raw.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
                Clipboard.SetText(normalized);
                lblOffsetChain.Text = "Copied full note to clipboard.";
            }
        }

        private void checkSizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var lines = txtNoteBody.Lines;
            var errors = new List<string>();
            var lineRegex = new Regex(@"^([.\s]*)([-+])?(0x[0-9A-Fa-f]+)(.*)$", RegexOptions.IgnoreCase);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var match = lineRegex.Match(line);
                if (!match.Success) continue;

                string indent = match.Groups[1].Value;
                string sign = match.Groups[2].Value;
                string rawDesc = match.Groups[4].Value.Trim();

                if (!string.IsNullOrEmpty(indent) && indent.Contains(".") && string.IsNullOrEmpty(sign)) continue;

                while (rawDesc.Length > 0 && (rawDesc[0] == '|' || rawDesc[0] == ':' || rawDesc[0] == '=' || rawDesc[0] == '-'))
                    rawDesc = rawDesc.Substring(1).Trim();

                if (string.IsNullOrWhiteSpace(rawDesc)) continue;
                if (rawDesc.Contains("Pointer", StringComparison.OrdinalIgnoreCase)) continue;

                var tagMatch = Regex.Match(rawDesc, @"\[(.*?)\]");
                if (!tagMatch.Success)
                {
                    errors.Add($"Line {i + 1}: Missing size tag. Content: '{rawDesc}'");
                }
                else
                {
                    string tag = tagMatch.Groups[1].Value;
                    bool isValid = RaLogicSyntax.MemorySizes.Contains(tag) ||
                                   tag.Equals("Float", StringComparison.OrdinalIgnoreCase) ||
                                   tag.EndsWith(" bytes");
                    if (!isValid) errors.Add($"Line {i + 1}: Typo detected in tag '[{tag}]'.");
                }
            }

            if (errors.Count > 0) MessageBox.Show(string.Join("\n", errors), "Code Note Issues", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else MessageBox.Show("No size syntax errors found!", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}