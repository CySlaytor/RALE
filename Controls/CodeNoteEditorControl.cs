#nullable enable
using RALogicEditor.Definitions;
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

        private List<CodeNote> _allNotes = new();
        private List<CodeNote> _filteredNotes = new();
        private bool _isInternalUpdate = false;
        private string _fullNoteCache = "";

        // Cache for range-based notes (e.g. Structs covering [176 bytes])
        // Tuple: Start Address, End Address, Note Object
        private List<(long Start, long End, CodeNote Note, NoteNode Node)> _rangeCache = new();

        // Virtual State for handling Indirect/Ranged selections that don't strictly exist in the dropdowns
        private class VirtualNoteState
        {
            public bool IsActive;
            public string Description = "";
            public string LogicCode = "";
            public string LogicType = "";
            public string LogicSize = "";
        }
        private VirtualNoteState _virtualState = new();

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
            try
            {
                // 1. Capture current selection to restore it after reload
                string? previousAddress = null;
                if (cmbNotes.SelectedItem is CodeNote current)
                {
                    previousAddress = current.Address;
                }

                _allNotes = notes ?? new List<CodeNote>();
                _rangeCache.Clear();

                // 2. Build Range Cache
                foreach (var note in _allNotes)
                {
                    // Clean address
                    string cleanAddr = note.Address.Replace("0x", "").Trim();
                    if (long.TryParse(cleanAddr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long startAddr))
                    {
                        var rootNode = PointerTreeParser.ParseNoteText(note.Note).FirstOrDefault();
                        if (rootNode != null && rootNode.Size > 1)
                        {
                            long endAddr = startAddr + rootNode.Size;
                            _rangeCache.Add((startAddr, endAddr, note, rootNode));
                        }
                    }
                }

                // 3. Reset UI
                cmbNotes.DataSource = null;
                cmbNotes.Items.Clear();
                txtNoteBody.Text = "";
                ClearVirtualState();

                // 4. Apply Filter (forces a rebuild of the display list from the new _allNotes)
                ApplyFilter(false);

                // 5. Restore selection
                bool selectionRestored = false;
                if (previousAddress != null)
                {
                    var match = _filteredNotes.FirstOrDefault(n => n.Address == previousAddress);
                    if (match != null)
                    {
                        cmbNotes.SelectedItem = match;
                        LoadNoteToTextBox(match);
                        selectionRestored = true;
                    }
                }

                // 6. Fallback if nothing restored or selected
                if (!selectionRestored && _filteredNotes.Count > 0)
                {
                    cmbNotes.SelectedIndex = 0;
                    LoadNoteToTextBox(_filteredNotes[0]);
                }
            }
            finally
            {
                _isInternalUpdate = false;
            }
        }

        // Filters note list based on search text
        private void ApplyFilter(bool autoSelectFirst = true)
        {
            string filter = txtSearch.Text.Trim();
            if (string.IsNullOrWhiteSpace(filter))
            {
                // Always create a new list from _allNotes.
                _filteredNotes = new List<CodeNote>(_allNotes);
            }
            else
            {
                _filteredNotes = _allNotes
                    .Where(n => n.Address.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                n.Note.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            // Reset DataSource to force the ComboBox to repaint with new data
            cmbNotes.DataSource = null;
            cmbNotes.DataSource = _filteredNotes;

            if (autoSelectFirst && _filteredNotes.Count > 0)
            {
                cmbNotes.SelectedIndex = 0;
                LoadNoteToTextBox(_filteredNotes[0]);
            }
            else if (_filteredNotes.Count == 0)
            {
                txtNoteBody.Text = "";
                _fullNoteCache = "";
                cmbVariables.DataSource = null;
                UpdateStatusStrip(null);
            }
        }

        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            if (!_isInternalUpdate) ApplyFilter(true);
        }

        // Parses the selected note text to find pointer offsets
        private void LoadNoteToTextBox(CodeNote note)
        {
            // Preserve existing state to prevent prematurely enabling events
            bool wasUpdating = _isInternalUpdate;
            _isInternalUpdate = true;
            try
            {
                // Fix: Robust normalization to prevent \r\r\n double spacing issues
                _fullNoteCache = note.Note.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");

                // Ensure text box is editable when loading a standard note
                txtNoteBody.ReadOnly = false;
                txtNoteBody.Text = _fullNoteCache;

                // Clear virtual state when explicitly loading a note via UI
                ClearVirtualState();

                RefreshVariableTree();
                if (cmbVariables.Items.Count > 0) cmbVariables.SelectedIndex = 0; else cmbVariables.SelectedIndex = -1;
                UpdateStatusStrip(null);
            }
            finally
            {
                _isInternalUpdate = wasUpdating;
            }
        }

        private void RefreshVariableTree()
        {
            // Delegate heavy parsing to PointerTreeParser
            var flatList = PointerTreeParser.ParseNoteText(_fullNoteCache);

            IEnumerable<NoteNode> query = flatList;

            // Apply Search Filter to Variable Tree as well
            string filter = txtSearch.Text.Trim();
            if (!string.IsNullOrEmpty(filter))
            {
                query = query.Where(n =>
                    n.Description.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    n.Offset.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    (n.IndentLevel == -2 && n.Content.Contains(filter, StringComparison.OrdinalIgnoreCase)) // Keep root if content matches
                );
            }

            // Filter view if "Show Leafs Only" is checked
            if (chkShowLeafsOnly.Checked)
            {
                // Leafs = Root (-2) OR Nodes with no children
                query = query.Where(n => n.IndentLevel == -2 || (n.Children.Count == 0 && n.IndentLevel != -1));
            }

            var finalList = query.ToList();
            cmbVariables.DataSource = finalList;

            cmbVariables.Enabled = finalList.Count > 1;
            // Ensure background is reset if it was grayed out by virtual mode
            cmbVariables.BackColor = SystemColors.Window;
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
            // Priority 1: Virtual State (Indirect/Range selection)
            if (_virtualState.IsActive)
            {
                return (_virtualState.LogicCode, _virtualState.LogicType, _virtualState.LogicSize);
            }

            // Priority 2: Selected Variable in Dropdown
            if (cmbNotes.SelectedItem is not CodeNote baseNote) return ("", "", "");
            var selectedNode = cmbVariables.SelectedItem as NoteNode;
            if (selectedNode == null) return ("", "", "");
            return PointerTreeParser.GeneratePointerLogic(baseNote, selectedNode);
        }

        private void cmbVariables_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbVariables.SelectedItem is NoteNode node)
            {
                bool wasUpdating = _isInternalUpdate;
                _isInternalUpdate = true; // Lock while updating text box

                try
                {
                    if (node.IndentLevel == -2)
                    {
                        txtNoteBody.Text = _fullNoteCache;
                        UpdateStatusStrip(null);
                    }
                    else
                    {
                        // Display Logic:
                        // 1. If Node has CONTENT (extra lines below), show it.
                        // 2. Else if Node has DESCRIPTION (the line itself), show it.
                        // 3. Else show Full Note (fallback).

                        if (!string.IsNullOrWhiteSpace(node.Content))
                        {
                            txtNoteBody.Text = node.Content;
                        }
                        else if (!string.IsNullOrWhiteSpace(node.Description))
                        {
                            txtNoteBody.Text = node.Description;
                        }
                        else
                        {
                            // Fallback to Base Note
                            if (!string.IsNullOrWhiteSpace(_fullNoteCache))
                            {
                                txtNoteBody.Text = _fullNoteCache;
                            }
                            else
                            {
                                txtNoteBody.Text = "No Code Note found for this address.";
                            }
                        }
                        UpdateStatusStrip(node);
                    }
                }
                finally
                {
                    _isInternalUpdate = wasUpdating; // Restore previous state
                }
            }
        }

        private void UpdateStatusStrip(NoteNode? node)
        {
            if (_virtualState.IsActive)
            {
                lblOffsetChain.Text = _virtualState.Description;
                return;
            }

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
            // Prevent editing sub-nodes directly or virtual nodes
            if ((cmbVariables.SelectedIndex != -1 && cmbVariables.SelectedIndex != 0) || _virtualState.IsActive) return;

            if (cmbNotes.SelectedItem is CodeNote note)
            {
                note.Note = txtNoteBody.Text;
                _fullNoteCache = txtNoteBody.Text;
                RefreshVariableTree();
                cmbNotes.Invalidate();
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

            // 1. Try Exact Match
            var note = _filteredNotes.FirstOrDefault(n => ParseAddress(n.Address) == targetVal);
            if (note == null)
            {
                // Try searching in all notes
                note = _allNotes.FirstOrDefault(n => ParseAddress(n.Address) == targetVal);
                if (note != null) txtSearch.Text = ""; // Clear filter to show it
            }

            if (note != null)
            {
                // Found exact note
                bool wasSame = cmbNotes.SelectedItem == note;
                cmbNotes.SelectedItem = note;
                if (wasSame) LoadNoteToTextBox(note); // Force refresh
                ClearVirtualState(); // Ensure we aren't in virtual mode
            }
            else
            {
                // 2. Try Range Match (Virtual Note)
                var rangeMatch = _rangeCache.FirstOrDefault(r => targetVal >= r.Start && targetVal < r.End);
                if (rangeMatch.Note != null)
                {
                    // Select base note
                    cmbNotes.SelectedItem = rangeMatch.Note;
                    LoadNoteToTextBox(rangeMatch.Note); // Load base note text

                    // Enter Virtual Mode
                    long delta = targetVal - rangeMatch.Start;
                    string desc = rangeMatch.Node.Description;
                    if (string.IsNullOrEmpty(desc) || desc == "Full Note")
                        desc = rangeMatch.Node.Content.Split('\n').FirstOrDefault()?.Trim() ?? "";

                    string virtualDesc = $"{desc} +0x{delta:X}";

                    // Update Text Box
                    txtNoteBody.Text = virtualDesc;
                    txtNoteBody.ReadOnly = true;

                    SetVirtualState(
                        true,
                        $"[Indirect from {rangeMatch.Note.Address}] {virtualDesc}",
                        $"0x{targetVal:x}", // Logic code is the calculated address
                        "Mem", // Default type
                        "8-bit" // Default size
                    );
                }
            }
        }

        // Bridge: Traverses the tree to find specific pointer offsets
        public void HighlightVariable(List<long> offsets)
        {
            if (cmbVariables.Items.Count <= 1) return;
            if (offsets == null || offsets.Count == 0) return;
            if (cmbNotes.SelectedItem is not CodeNote baseNote) return;

            var nodes = cmbVariables.DataSource as List<NoteNode>;
            if (nodes == null) return;

            // 1. Try Exact Match
            foreach (var node in nodes)
            {
                if (node.IndentLevel == -2) continue;
                var nodeChain = PointerTreeParser.GetOffsetsFromNode(node);
                if (nodeChain.Count == offsets.Count)
                {
                    bool match = true;
                    for (int i = 0; i < nodeChain.Count; i++) { if (nodeChain[i] != offsets[i]) { match = false; break; } }
                    if (match)
                    {
                        ClearVirtualState();
                        cmbVariables.SelectedItem = node;
                        return;
                    }
                }
            }

            // 2. Try Range Match (Pointer Chain Range)
            foreach (var node in nodes)
            {
                if (node.IndentLevel == -2) continue;

                var nodeChain = PointerTreeParser.GetOffsetsFromNode(node);

                if (nodeChain.Count == offsets.Count)
                {
                    bool pathMatch = true;
                    for (int i = 0; i < offsets.Count - 1; i++)
                    {
                        if (nodeChain[i] != offsets[i]) { pathMatch = false; break; }
                    }

                    if (pathMatch)
                    {
                        long nodeStart = nodeChain.Last();
                        long target = offsets.Last();

                        if (target >= nodeStart && target < nodeStart + node.Size)
                        {
                            long delta = target - nodeStart;

                            var (baseLogic, type, size) = PointerTreeParser.GeneratePointerLogic(baseNote, node);

                            string finalLogic = baseLogic;
                            int lastUnderscore = baseLogic.LastIndexOf('_');
                            if (lastUnderscore != -1)
                            {
                                finalLogic = baseLogic.Substring(0, lastUnderscore + 1) + $"0x{target:x}";
                            }
                            else
                            {
                                int firstI = baseLogic.IndexOf("I:");
                                if (firstI != -1)
                                {
                                    finalLogic = baseLogic.Substring(0, baseLogic.LastIndexOf('_') + 1) + $"0x{target:x}";
                                }
                            }

                            string desc = node.Description;
                            if (delta > 0) desc += $" +0x{delta:X}";

                            txtNoteBody.Text = desc;
                            txtNoteBody.ReadOnly = true;

                            SetVirtualState(true,
                                $"[Indirect from {node.Parent?.Offset ?? "Base"}] {desc}",
                                finalLogic,
                                type,
                                size
                            );

                            cmbVariables.SelectedItem = node;
                            cmbVariables.Enabled = false;
                            cmbVariables.BackColor = SystemColors.Control;
                            return;
                        }
                    }
                }
            }
        }

        private void SetVirtualState(bool active, string description, string logicCode, string type, string size)
        {
            _virtualState.IsActive = active;
            _virtualState.Description = description;
            _virtualState.LogicCode = logicCode;
            _virtualState.LogicType = type;
            _virtualState.LogicSize = size;

            if (active)
            {
                UpdateStatusStrip(null);
                cmbVariables.Enabled = false;
                cmbVariables.BackColor = SystemColors.Control;
            }
            else
            {
                cmbVariables.Enabled = true;
                cmbVariables.BackColor = SystemColors.Window;
            }
        }

        private void ClearVirtualState()
        {
            SetVirtualState(false, "", "", "", "");
            txtNoteBody.ReadOnly = false;
        }

        public string GetNoteText(string address)
        {
            long targetVal = ParseAddress(address);
            if (targetVal == -1) return "";
            var note = _allNotes.FirstOrDefault(n => ParseAddress(n.Address) == targetVal);
            if (note != null) return note.Note.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            return "";
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

        // Check for Code Note Size Implementation
        private void checkSizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var lines = txtNoteBody.Lines;
            var errors = new List<string>();

            // Regular expression to parse lines:
            // Group 1: Indentation/Prefix (e.g., "+", ".+", "..+")
            // Group 2: Sign (+ or -)
            // Group 3: Hex (0x...)
            // Group 4: Remainder (Description)
            var lineRegex = new Regex(@"^([.\s]*)([-+])?(0x[0-9A-Fa-f]+)(.*)$", RegexOptions.IgnoreCase);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var match = lineRegex.Match(line);
                if (!match.Success) continue; // Not a hex definition

                string indent = match.Groups[1].Value;
                string sign = match.Groups[2].Value;
                string rawDesc = match.Groups[4].Value.Trim();

                // RULE: If it's indented (has dots) but has NO sign (+/-), it's likely a value definition (e.g. ....0x7F = Inf). Skip it.
                if (!string.IsNullOrEmpty(indent) && indent.Contains(".") && string.IsNullOrEmpty(sign)) continue;

                // Clean leading separators commonly used in RA notes
                while (rawDesc.Length > 0 && (rawDesc[0] == '|' || rawDesc[0] == ':' || rawDesc[0] == '=' || rawDesc[0] == '-'))
                {
                    rawDesc = rawDesc.Substring(1).Trim();
                }

                // SKIP Empty descriptions (just structural offsets)
                if (string.IsNullOrWhiteSpace(rawDesc)) continue;

                // SKIP "Pointer" descriptions (they define structure, not leaf data)
                if (rawDesc.Contains("Pointer", StringComparison.OrdinalIgnoreCase)) continue;

                // Check for Size Tag [x]
                var tagMatch = Regex.Match(rawDesc, @"\[(.*?)\]");

                if (!tagMatch.Success)
                {
                    errors.Add($"Line {i + 1}: Missing size tag. Content: '{rawDesc}'");
                }
                else
                {
                    // Typo check
                    string tag = tagMatch.Groups[1].Value;
                    bool isValid = RaLogicSyntax.MemorySizes.Contains(tag) ||
                                   tag.Equals("Float", StringComparison.OrdinalIgnoreCase) ||
                                   tag.EndsWith(" bytes");

                    if (!isValid)
                    {
                        errors.Add($"Line {i + 1}: Typo detected in tag '[{tag}]'.");
                    }
                }
            }

            if (errors.Count > 0)
            {
                MessageBox.Show(string.Join("\n", errors), "Code Note Issues", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                MessageBox.Show("No size syntax errors found!", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}