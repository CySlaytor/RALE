#nullable enable
using RALogicEditor.Models;
using RARPEditor.Models;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RALogicEditor.Controls
{
    public partial class CodeNoteEditorControl : UserControl
    {
        public event EventHandler? ReloadRequested;

        private List<CodeNote> _allNotes = new();
        private List<CodeNote> _filteredNotes = new();
        private bool _isInternalUpdate = false;
        private string _fullNoteCache = "";

        // Internal class to represent the hierarchy of pointer offsets
        private class NoteNode
        {
            public string Offset { get; set; } = "";
            public string Description { get; set; } = "";
            public int RawLineIndex { get; set; }
            public int IndentLevel { get; set; }
            public string Content { get; set; } = "";
            public NoteNode? Parent { get; set; }
            public List<NoteNode> Children { get; set; } = new();

            public override string ToString()
            {
                // Formatting happens in the ListControl's Format event instead
                if (string.IsNullOrWhiteSpace(Description)) return Offset;
                return $"{Offset} | {Description}";
            }
        }

        private List<NoteNode> _currentVariableNodes = new();

        public CodeNoteEditorControl()
        {
            InitializeComponent();
            
            // Re-load the current note when checkbox changes to refresh tree view style
            chkShowLeafsOnly.CheckedChanged += (s, e) => 
            {
                if (cmbNotes.SelectedItem is CodeNote note)
                {
                    LoadNoteToTextBox(note);
                }
            };
        }

        private void btnReload_Click(object sender, EventArgs e)
        {
            ReloadRequested?.Invoke(this, EventArgs.Empty);
        }

        // Sets the raw list of notes loaded from file
        public void SetNotes(List<CodeNote> notes)
        {
            _isInternalUpdate = true;
            _allNotes = notes ?? new List<CodeNote>();

            ApplyFilter();

            _isInternalUpdate = false;
        }

        // Filters the dropdown list based on search text
        private void ApplyFilter()
        {
            string filter = txtSearch.Text.Trim();

            if (string.IsNullOrWhiteSpace(filter))
            {
                if (_filteredNotes.Count != _allNotes.Count)
                    _filteredNotes = new List<CodeNote>(_allNotes);
            }
            else
            {
                _filteredNotes = _allNotes
                    .Where(n => n.Address.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                n.Note.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            cmbNotes.DataSource = null;
            cmbNotes.DataSource = _filteredNotes;

            if (_filteredNotes.Count > 0)
            {
                cmbNotes.SelectedIndex = 0;
                LoadNoteToTextBox(_filteredNotes[0]);
            }
            else
            {
                txtNoteBody.Text = "";
                _fullNoteCache = "";
                cmbVariables.DataSource = null;
                UpdateStatusStrip(null);
            }
        }

        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            if (_isInternalUpdate) return;
            ApplyFilter();
        }

        // Parses the note text to find pointer structure
        private void LoadNoteToTextBox(CodeNote note)
        {
            _isInternalUpdate = true;
            _fullNoteCache = note.Note.Replace("\n", "\r\n");
            txtNoteBody.Text = _fullNoteCache;

            ParseNestedVariables(txtNoteBody.Lines);

            if (cmbVariables.Items.Count > 0) cmbVariables.SelectedIndex = 0;
            else cmbVariables.SelectedIndex = -1;

            UpdateStatusStrip(null);
            _isInternalUpdate = false;
        }

        // The core logic for parsing indented pointer offsets (e.g. +0x4, ++0x8)
        private void ParseNestedVariables(string[] lines)
        {
            _currentVariableNodes.Clear();

            bool isPointerNote = _fullNoteCache.Contains("Pointer", StringComparison.OrdinalIgnoreCase);

            var flatList = new List<NoteNode>();

            flatList.Add(new NoteNode
            {
                Offset = "(Default)",
                Description = "Full Note",
                IndentLevel = -2,
                Content = _fullNoteCache
            });

            if (isPointerNote)
            {
                var stack = new Stack<NoteNode>();
                var root = new NoteNode { IndentLevel = -1 };
                stack.Push(root);

                NoteNode? lastAddedNode = null;

                var offsetRegex = new Regex(@"^([.\+]*)\s*([-+]?0x[0-9A-Fa-f]+)\s*(?:[|=:]\s*)?(.*)$");
                var prefixStripRegex = new Regex(@"^([.\+]+)");

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Replace("\r", "").Replace("\n", "");
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var match = offsetRegex.Match(line.Trim());
                    if (match.Success)
                    {
                        int indent = match.Groups[1].Length;
                        string offset = match.Groups[2].Value;
                        string desc = match.Groups[3].Value;

                        if (!offset.StartsWith("+") && !offset.StartsWith("-"))
                        {
                            offset = "+" + offset;
                        }

                        var node = new NoteNode
                        {
                            Offset = offset,
                            Description = desc,
                            RawLineIndex = i,
                            IndentLevel = indent
                        };

                        // Pop stack until we find the correct parent level
                        while (stack.Peek().IndentLevel >= indent)
                        {
                            stack.Pop();
                        }

                        var parent = stack.Peek();
                        node.Parent = parent;
                        parent.Children.Add(node);
                        stack.Push(node);

                        lastAddedNode = node;
                    }
                    else
                    {
                        // Append description lines to the last node found
                        if (lastAddedNode != null)
                        {
                            if (lastAddedNode.Content.Length > 0) lastAddedNode.Content += "\r\n";
                            string cleanContentLine = prefixStripRegex.Replace(line.Trim(), "");
                            lastAddedNode.Content += cleanContentLine;
                        }
                    }
                }

                // Decide whether to flatten the tree or show hierarchy
                if (chkShowLeafsOnly.Checked)
                {
                    CollectLeaves(root, flatList);
                }
                else
                {
                    CollectAllNodes(root, flatList);
                }
            }

            cmbVariables.DataSource = null;
            cmbVariables.DataSource = flatList;
            cmbVariables.Enabled = flatList.Count > 1;
        }

        private void CollectLeaves(NoteNode node, List<NoteNode> leaves)
        {
            if (node.Children.Count == 0 && node.IndentLevel != -1)
            {
                leaves.Add(node);
            }
            else
            {
                foreach (var child in node.Children)
                {
                    CollectLeaves(child, leaves);
                }
            }
        }

        private void CollectAllNodes(NoteNode node, List<NoteNode> flatList)
        {
            if (node.IndentLevel != -1)
            {
                flatList.Add(node);
            }

            foreach (var child in node.Children)
            {
                CollectAllNodes(child, flatList);
            }
        }

        // Formats the dropdown to show visual indentation based on tree depth
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

        // Generates the logic string (e.g., I:0x1234_0x5678) for the selected variable
        public (string Logic, string Type, string Size) GetSelectedLogic()
        {
            if (cmbNotes.SelectedItem is not CodeNote baseNote) return ("", "", "");

            string baseAddrStr = baseNote.Address.StartsWith("0x") ? baseNote.Address.Substring(2) : baseNote.Address;
            string type = "Mem";
            string size = "32-bit";

            string noteHeader = baseNote.Note.Split('\n').FirstOrDefault() ?? "";
            char ptrPrefix = ' ';
            bool defaultMask = false;

            // Auto-detect pointer size/type from header
            if (noteHeader.Contains("32-bit BE Pointer", StringComparison.OrdinalIgnoreCase)) { ptrPrefix = 'G'; defaultMask = true; }
            else if (noteHeader.Contains("32-bit Pointer", StringComparison.OrdinalIgnoreCase)) ptrPrefix = 'X';
            else if (noteHeader.Contains("24-bit", StringComparison.OrdinalIgnoreCase)) ptrPrefix = 'W';
            else if (noteHeader.Contains("16-bit", StringComparison.OrdinalIgnoreCase)) ptrPrefix = ' ';

            long explicitMaskVal = -1;
            var maskMatch = Regex.Match(noteHeader, @"&\s*(?:0x)?([0-9A-Fa-f]+)");
            if (maskMatch.Success) long.TryParse(maskMatch.Groups[1].Value, NumberStyles.HexNumber, null, out explicitMaskVal);

            string prefixStr = ptrPrefix == ' ' ? "" : ptrPrefix.ToString();
            string maskStr = "";

            if (explicitMaskVal != -1) maskStr = "&" + explicitMaskVal.ToString();
            else if (defaultMask) maskStr = "&536870911";

            var sb = new StringBuilder();
            string descriptionForType = "";

            if (cmbVariables.Enabled && cmbVariables.SelectedItem is NoteNode selectedNode && selectedNode.IndentLevel != -2)
            {
                // Build pointer chain string from tree structure
                var chain = new List<string>();
                var current = selectedNode;
                descriptionForType = selectedNode.Description;

                while (current != null && current.IndentLevel != -1)
                {
                    string hex = current.Offset.TrimStart('+', '-').Replace("0x", "");
                    if (hex.Length < 8) hex = hex.PadLeft(8, '0');
                    chain.Insert(0, hex);
                    current = current.Parent;
                }

                sb.Append($"I:0x{prefixStr}{baseAddrStr}{maskStr}");
                for (int i = 0; i < chain.Count - 1; i++) sb.Append($"_I:0x{prefixStr}{chain[i]}{maskStr}");
                if (chain.Count > 0) sb.Append($"_0x{chain[chain.Count - 1]}");
            }
            else
            {
                sb.Append($"0x{baseAddrStr}");
                descriptionForType = noteHeader;
            }

            // Extract size from description (e.g. [8-bit])
            var typeMatch = Regex.Match(descriptionForType, @"\[(.*?)]");
            if (typeMatch.Success)
            {
                string tag = typeMatch.Groups[1].Value;
                if (tag.Equals("Float", StringComparison.OrdinalIgnoreCase) || tag.Equals("Flaot", StringComparison.OrdinalIgnoreCase)) { type = "Mem"; size = "Float"; }
                else { size = tag; type = "Mem"; }
            }
            else size = "8-bit";

            return (sb.ToString(), type, size);
        }

        private void cmbVariables_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isInternalUpdate) return;

            if (cmbVariables.SelectedItem is NoteNode node)
            {
                _isInternalUpdate = true;

                if (node.IndentLevel == -2)
                {
                    txtNoteBody.Text = _fullNoteCache;
                    UpdateStatusStrip(null);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(node.Content))
                        txtNoteBody.Text = node.Content;
                    else
                        txtNoteBody.Text = $"{node.Description}\r\n(No specific details nested under this pointer)";
                    
                    UpdateStatusStrip(node);
                }

                _isInternalUpdate = false;
            }
        }

        private void UpdateStatusStrip(NoteNode? node)
        {
            if (node == null)
            {
                lblOffsetChain.Text = "Ready";
                return;
            }

            var chain = new List<string>();
            var current = node;

            while (current != null && current.IndentLevel != -1)
            {
                chain.Insert(0, current.Offset);
                current = current.Parent;
            }

            if (chain.Count > 0) lblOffsetChain.Text = $"[Indirect Note {string.Join(" ", chain)}]";
            else lblOffsetChain.Text = "Direct Variable";
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
            if (cmbNotes.SelectedItem is CodeNote note)
            {
                LoadNoteToTextBox(note);
            }
        }

        private void txtNoteBody_TextChanged(object sender, EventArgs e)
        {
            if (_isInternalUpdate) return;
            // Prevent editing if a sub-variable is selected (too complex to handle back-parsing)
            if (cmbVariables.SelectedIndex != -1 && cmbVariables.SelectedIndex != 0) return;

            if (cmbNotes.SelectedItem is CodeNote note)
            {
                note.Note = txtNoteBody.Text;
                _fullNoteCache = txtNoteBody.Text;
                ParseNestedVariables(txtNoteBody.Lines);
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

        // Selects a specific code note by address (Used by TriggerEditor)
        public void HighlightAddress(string address)
        {
            if (string.IsNullOrEmpty(address)) return;
            
            string hex = address.Trim();
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) hex = hex.Substring(2);
            if (!long.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long targetVal)) return;

            var note = _filteredNotes.FirstOrDefault(n => ParseAddress(n.Address) == targetVal);
            if (note == null)
            {
                note = _allNotes.FirstOrDefault(n => ParseAddress(n.Address) == targetVal);
                if (note != null) txtSearch.Text = ""; // Clear search if hidden
            }

            if (note != null)
            {
                // Force reload to reset variable view
                bool wasSame = cmbNotes.SelectedItem == note;
                cmbNotes.SelectedItem = note;
                if (wasSame)
                {
                    LoadNoteToTextBox(note);
                }
            }
        }

        // Traverses the tree structure to select a specific pointer offset
        public void HighlightVariable(List<long> offsets)
        {
            if (cmbVariables.Items.Count <= 1) return;
            if (offsets == null || offsets.Count == 0) return;

            var nodes = cmbVariables.DataSource as List<NoteNode>;
            if (nodes == null) return;

            foreach (var node in nodes)
            {
                if (node.IndentLevel == -2) continue;

                var nodeChain = new List<long>();
                var current = node;
                bool parseError = false;

                // Build chain upwards from current node
                while (current != null && current.IndentLevel != -1)
                {
                    string raw = current.Offset.TrimStart('+');
                    if (long.TryParse(raw.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long val))
                    {
                        nodeChain.Insert(0, val);
                    }
                    else { parseError = true; break; }
                    current = current.Parent;
                }

                if (parseError) continue;

                // Check if chains match
                if (nodeChain.Count == offsets.Count)
                {
                    bool match = true;
                    for (int i = 0; i < nodeChain.Count; i++)
                    {
                        if (nodeChain[i] != offsets[i]) { match = false; break; }
                    }
                    if (match)
                    {
                        cmbVariables.SelectedItem = node;
                        return;
                    }
                }
            }
        }

        public string GetNoteText(string address)
        {
            long targetVal = ParseAddress(address);
            if (targetVal == -1) return "";
            var note = _allNotes.FirstOrDefault(n => ParseAddress(n.Address) == targetVal);
            if (note != null)
            {
                string firstLine = note.Note.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                return firstLine;
            }
            return "";
        }
    }
}