#nullable enable
using RALogicEditor.Models;
using RALogicEditor.Parsers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace RALogicEditor.Controls
{
    public partial class CodeNoteEditorControl : UserControl
    {
        public event EventHandler? ReloadRequested;

        private List<CodeNote> _allNotes = new();
        private List<CodeNote> _filteredNotes = new();
        private bool _isInternalUpdate = false;
        private string _fullNoteCache = "";

        public CodeNoteEditorControl()
        {
            InitializeComponent();
            // Re-parse note when "Show Leafs" toggles to update the dropdown structure
            chkShowLeafsOnly.CheckedChanged += (s, e) =>
            {
                if (cmbNotes.SelectedItem is CodeNote note) LoadNoteToTextBox(note);
            };
        }

        private void btnReload_Click(object sender, EventArgs e)
        {
            ReloadRequested?.Invoke(this, EventArgs.Empty);
        }

        // Called by Main Form to load the note list
        public void SetNotes(List<CodeNote> notes)
        {
            _isInternalUpdate = true;
            _allNotes = notes ?? new List<CodeNote>();
            ApplyFilter();
            _isInternalUpdate = false;
        }

        // Filters note list based on search text
        private void ApplyFilter()
        {
            string filter = txtSearch.Text.Trim();
            if (string.IsNullOrWhiteSpace(filter))
            {
                if (_filteredNotes.Count != _allNotes.Count) _filteredNotes = new List<CodeNote>(_allNotes);
            }
            else
            {
                _filteredNotes = _allNotes
                    .Where(n => n.Address.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                n.Note.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            cmbNotes.DataSource = null;
            cmbNotes.DataSource = _filteredNotes;

            if (_filteredNotes.Count > 0) { cmbNotes.SelectedIndex = 0; LoadNoteToTextBox(_filteredNotes[0]); }
            else { txtNoteBody.Text = ""; _fullNoteCache = ""; cmbVariables.DataSource = null; UpdateStatusStrip(null); }
        }

        private void txtSearch_TextChanged(object sender, EventArgs e) { if (!_isInternalUpdate) ApplyFilter(); }

        // Parses the selected note text to find pointer offsets
        private void LoadNoteToTextBox(CodeNote note)
        {
            _isInternalUpdate = true;
            _fullNoteCache = note.Note.Replace("\n", "\r\n");
            txtNoteBody.Text = _fullNoteCache;
            RefreshVariableTree();
            if (cmbVariables.Items.Count > 0) cmbVariables.SelectedIndex = 0; else cmbVariables.SelectedIndex = -1;
            UpdateStatusStrip(null);
            _isInternalUpdate = false;
        }

        private void RefreshVariableTree()
        {
            // Delegate heavy parsing to PointerTreeParser
            var flatList = PointerTreeParser.ParseNoteText(_fullNoteCache);

            // Filter view if "Show Leafs Only" is checked
            if (chkShowLeafsOnly.Checked)
            {
                var leafList = flatList.Where(n => n.IndentLevel == -2 || (n.Children.Count == 0 && n.IndentLevel != -1)).ToList();
                cmbVariables.DataSource = leafList;
            }
            else cmbVariables.DataSource = flatList;

            cmbVariables.Enabled = flatList.Count > 1;
        }

        // Renders indentation in the dropdown list
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

        // Generates RA Logic string for the selected variable (pointer chain)
        public (string Logic, string Type, string Size) GetSelectedLogic()
        {
            if (cmbNotes.SelectedItem is not CodeNote baseNote) return ("", "", "");
            var selectedNode = cmbVariables.SelectedItem as NoteNode;
            if (selectedNode == null) return ("", "", "");
            return PointerTreeParser.GeneratePointerLogic(baseNote, selectedNode);
        }

        private void cmbVariables_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isInternalUpdate) return;
            if (cmbVariables.SelectedItem is NoteNode node)
            {
                _isInternalUpdate = true;
                if (node.IndentLevel == -2) { txtNoteBody.Text = _fullNoteCache; UpdateStatusStrip(null); }
                else
                {
                    // Show specific content for that node or a fallback message
                    if (!string.IsNullOrWhiteSpace(node.Content)) txtNoteBody.Text = node.Content;
                    else txtNoteBody.Text = $"{node.Description}\r\n(No specific details nested under this pointer)";
                    UpdateStatusStrip(node);
                }
                _isInternalUpdate = false;
            }
        }

        private void UpdateStatusStrip(NoteNode? node)
        {
            if (node == null) { lblOffsetChain.Text = "Ready"; return; }
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

        private void cmbNotes_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isInternalUpdate) return;
            if (cmbNotes.SelectedItem is CodeNote note) LoadNoteToTextBox(note);
        }

        private void txtNoteBody_TextChanged(object sender, EventArgs e)
        {
            if (_isInternalUpdate) return;
            // Prevent editing sub-nodes directly (complex re-parsing needed)
            if (cmbVariables.SelectedIndex != -1 && cmbVariables.SelectedIndex != 0) return;

            if (cmbNotes.SelectedItem is CodeNote note)
            {
                note.Note = txtNoteBody.Text; _fullNoteCache = txtNoteBody.Text; RefreshVariableTree(); cmbNotes.Invalidate();
            }
        }

        private long ParseAddress(string addr)
        {
            if (string.IsNullOrEmpty(addr)) return -1;
            string hex = addr.Trim();
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) hex = hex.Substring(2);
            if (long.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long result)) return result;
            return -1;
        }

        // Bridge: Highlights an address requested by Trigger Editor
        public void HighlightAddress(string address)
        {
            if (string.IsNullOrEmpty(address)) return;
            string hex = address.Trim();
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) hex = hex.Substring(2);
            if (!long.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long targetVal)) return;

            var note = _filteredNotes.FirstOrDefault(n => ParseAddress(n.Address) == targetVal);
            if (note == null) { note = _allNotes.FirstOrDefault(n => ParseAddress(n.Address) == targetVal); if (note != null) txtSearch.Text = ""; }
            if (note != null) { bool wasSame = cmbNotes.SelectedItem == note; cmbNotes.SelectedItem = note; if (wasSame) LoadNoteToTextBox(note); }
        }

        // Bridge: Traverses the tree to find specific pointer offsets
        public void HighlightVariable(List<long> offsets)
        {
            if (cmbVariables.Items.Count <= 1) return;
            if (offsets == null || offsets.Count == 0) return;
            var nodes = cmbVariables.DataSource as List<NoteNode>;
            if (nodes == null) return;

            foreach (var node in nodes)
            {
                if (node.IndentLevel == -2) continue;
                var nodeChain = PointerTreeParser.GetOffsetsFromNode(node);
                if (nodeChain.Count == offsets.Count)
                {
                    bool match = true;
                    for (int i = 0; i < nodeChain.Count; i++) { if (nodeChain[i] != offsets[i]) { match = false; break; } }
                    if (match) { cmbVariables.SelectedItem = node; return; }
                }
            }
        }

        public string GetNoteText(string address)
        {
            long targetVal = ParseAddress(address);
            if (targetVal == -1) return "";
            var note = _allNotes.FirstOrDefault(n => ParseAddress(n.Address) == targetVal);
            if (note != null) return note.Note.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            return "";
        }
    }
}