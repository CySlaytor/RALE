#nullable enable
using RALogicEditor.Models;
using RARPEditor.Models;
using System.Data;
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
                if (string.IsNullOrWhiteSpace(Description)) return Offset;
                return $"{Offset} | {Description}";
            }
        }

        private List<NoteNode> _currentVariableNodes = new();

        public CodeNoteEditorControl()
        {
            InitializeComponent();
        }

        private void btnReload_Click(object sender, EventArgs e)
        {
            ReloadRequested?.Invoke(this, EventArgs.Empty);
        }

        public void SetNotes(List<CodeNote> notes)
        {
            _isInternalUpdate = true;
            _allNotes = notes ?? new List<CodeNote>();

            ApplyFilter();

            _isInternalUpdate = false;
        }

        private void ApplyFilter()
        {
            string filter = txtSearch.Text.Trim();

            if (string.IsNullOrWhiteSpace(filter))
            {
                // Performance: Reuse list if searching is cleared
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
                        if (lastAddedNode != null)
                        {
                            if (lastAddedNode.Content.Length > 0) lastAddedNode.Content += "\r\n";
                            string cleanContentLine = prefixStripRegex.Replace(line.Trim(), "");
                            lastAddedNode.Content += cleanContentLine;
                        }
                    }
                }

                CollectLeaves(root, flatList);
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

        public (string Logic, string Type, string Size) GetSelectedLogic()
        {
            if (cmbNotes.SelectedItem is not CodeNote baseNote) return ("", "", "");

            string baseAddrStr = baseNote.Address.StartsWith("0x") ? baseNote.Address.Substring(2) : baseNote.Address;
            string type = "Mem";
            string size = "32-bit";

            string noteHeader = baseNote.Note.Split('\n').FirstOrDefault() ?? "";
            char ptrPrefix = ' ';
            bool defaultMask = false;

            if (noteHeader.Contains("32-bit BE Pointer", StringComparison.OrdinalIgnoreCase))
            {
                ptrPrefix = 'G';
                defaultMask = true;
            }
            else if (noteHeader.Contains("32-bit Pointer", StringComparison.OrdinalIgnoreCase))
            {
                ptrPrefix = 'X';
            }
            else if (noteHeader.Contains("24-bit", StringComparison.OrdinalIgnoreCase))
            {
                ptrPrefix = 'W';
            }
            else if (noteHeader.Contains("16-bit", StringComparison.OrdinalIgnoreCase))
            {
                ptrPrefix = ' ';
            }

            long explicitMaskVal = -1;
            var maskMatch = Regex.Match(noteHeader, @"&\s*(?:0x)?([0-9A-Fa-f]+)");
            if (maskMatch.Success)
            {
                long.TryParse(maskMatch.Groups[1].Value, System.Globalization.NumberStyles.HexNumber, null, out explicitMaskVal);
            }

            string prefixStr = ptrPrefix == ' ' ? "" : ptrPrefix.ToString();
            string maskStr = "";

            if (explicitMaskVal != -1)
            {
                maskStr = "&" + explicitMaskVal.ToString();
            }
            else if (defaultMask)
            {
                maskStr = "&536870911";
            }

            var sb = new StringBuilder();
            string descriptionForType = "";

            if (cmbVariables.Enabled && cmbVariables.SelectedItem is NoteNode selectedNode && selectedNode.IndentLevel != -2)
            {
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

                for (int i = 0; i < chain.Count - 1; i++)
                {
                    sb.Append($"_I:0x{prefixStr}{chain[i]}{maskStr}");
                }

                if (chain.Count > 0)
                {
                    sb.Append($"_0x{chain[chain.Count - 1]}");
                }
            }
            else
            {
                sb.Append($"0x{baseAddrStr}");
                descriptionForType = noteHeader;
            }

            var typeMatch = Regex.Match(descriptionForType, @"\[(.*?)]");
            if (typeMatch.Success)
            {
                string tag = typeMatch.Groups[1].Value;
                // Note: "Flaot" is a common typo in RA notes, keeping it intentionally
                if (tag.Equals("Float", StringComparison.OrdinalIgnoreCase) || tag.Equals("Flaot", StringComparison.OrdinalIgnoreCase))
                {
                    type = "Mem";
                    size = "Float";
                }
                else
                {
                    size = tag;
                    type = "Mem";
                }
            }
            else
            {
                size = "8-bit";
            }

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
                    {
                        txtNoteBody.Text = node.Content;
                    }
                    else
                    {
                        txtNoteBody.Text = $"{node.Description}\r\n(No specific value definitions found)";
                    }
                    UpdateStatusStrip(node);
                }

                _isInternalUpdate = false;
            }
            else
            {
                _isInternalUpdate = true;
                txtNoteBody.Text = _fullNoteCache;
                UpdateStatusStrip(null);
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

            if (chain.Count > 0)
            {
                lblOffsetChain.Text = $"[Indirect Note {string.Join(" ", chain)}]";
            }
            else
            {
                lblOffsetChain.Text = "Direct Variable";
            }
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
            if (cmbVariables.SelectedIndex != -1) return;

            if (cmbNotes.SelectedItem is CodeNote note)
            {
                note.Note = txtNoteBody.Text;
                _fullNoteCache = txtNoteBody.Text;
                ParseNestedVariables(txtNoteBody.Lines);
                cmbNotes.Invalidate();
            }
        }

        public void HighlightAddress(string address)
        {
            if (string.IsNullOrEmpty(address)) return;
            var note = _filteredNotes.FirstOrDefault(n => n.Address.Equals(address, StringComparison.OrdinalIgnoreCase));
            if (note != null) cmbNotes.SelectedItem = note;
        }
    }
}