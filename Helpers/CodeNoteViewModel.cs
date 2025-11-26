#nullable enable
using RALogicEditor.Definitions;
using RALogicEditor.Models;
using RALogicEditor.Parsers;
using RALogicEditor.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RALogicEditor.Helpers
{
    // ViewModel to decouple complex pointer logic from the WinForms UI.
    // Maintains the state of notes, pointer trees, and virtual selections.
    public class CodeNoteViewModel
    {
        // Injected Parser Dependency
        private readonly IPointerTreeParser _parser;

        // -- State --
        public List<CodeNote> AllNotes { get; private set; } = new();

        // Cache for range-based notes (Start, End, Note, Node)
        private List<(long Start, long End, CodeNote Note, NoteNode Node)> _rangeCache = new();

        // The currently selected base note (parent of the pointer tree)
        public CodeNote? CurrentBaseNote { get; private set; }

        // The full parsed tree of the current note
        public List<NoteNode> CurrentParsedNodes { get; private set; } = new();

        // -- Virtual/Edit State --
        // Unified State for both Virtual (Ghost) and Real (Existing) edits.
        // This acts as a buffer: changing DisplayText does not immediately affect the CodeNote.
        public class EditorState
        {
            public bool IsVirtual; // True if creating new node, False if editing existing node
            public bool IsDirty;   // Tracks if the user has modified the text
            public string Description = "";
            public string LogicCode = "";
            public string LogicType = "";
            public string LogicSize = "";
            public string DisplayText = ""; // The text to show in the body box

            // Store the offsets required to recreate this virtual path
            public List<long> ChainOffsets = new();

            // Store the target node if editing a real existing node
            public NoteNode? TargetNode;
        }
        public EditorState EditState { get; private set; } = new();

        // -- Events --
        public event EventHandler? NotesLoaded;
        public event EventHandler? NoteContentChanged;
        public event EventHandler? EditStateChanged; // Changed from VirtualStateChanged to reflect unified editing
        public event EventHandler<NoteNode>? SelectionRequested;

        // Constructor Injection
        public CodeNoteViewModel(IPointerTreeParser parser)
        {
            _parser = parser;
        }

        // -- Logic --

        public void LoadNotes(List<CodeNote> notes)
        {
            AllNotes = notes ?? new List<CodeNote>();
            _rangeCache.Clear();

            // Rebuild Range Cache
            foreach (var note in AllNotes)
            {
                string cleanAddr = note.Address.Replace("0x", "").Trim();
                if (long.TryParse(cleanAddr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long startAddr))
                {
                    var rootNode = _parser.ParseNoteText(note.Note).FirstOrDefault();
                    if (rootNode != null && rootNode.Size > 1)
                    {
                        long endAddr = startAddr + rootNode.Size;
                        _rangeCache.Add((startAddr, endAddr, note, rootNode));
                    }
                }
            }
            NotesLoaded?.Invoke(this, EventArgs.Empty);
        }

        public void SelectBaseNote(CodeNote note)
        {
            // Auto-commit any pending edits before switching context
            if (EditState.IsDirty) CommitEditState();

            CurrentBaseNote = note;
            ClearEditState();

            string normalized = note.Note.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
            CurrentParsedNodes = _parser.ParseNoteText(normalized);

            NoteContentChanged?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateNoteContent(string newText)
        {
            if (CurrentBaseNote == null || EditState.IsVirtual) return;

            CurrentBaseNote.Note = newText;
            CurrentParsedNodes = _parser.ParseNoteText(newText);
        }

        // Called by text box on every keystroke
        // This updates the buffer but NOT the underlying CodeNote.
        public void UpdateBufferedText(string newText)
        {
            if (CurrentBaseNote == null) return;

            // If we weren't in a specific edit state, we are now editing the Root note (default fallback)
            if (EditState.TargetNode == null && !EditState.IsVirtual)
            {
                // Root editing context is usually handled by SelectBaseNote, 
                // but if user just starts typing, we assume root.
            }

            EditState.DisplayText = newText;
            EditState.IsDirty = true;
        }

        // Commits the buffered text into the actual Code Note structure.
        // Called by Save (Ctrl+S) or Navigation.
        public void CommitEditState()
        {
            if (!EditState.IsDirty || CurrentBaseNote == null) return;

            string updatedBaseNote = "";

            // UPDATED: Use DetectStyle with Current Note context first, fallback to AllNotes
            var style = CodeNoteModifier.DetectStyle(CurrentBaseNote.Note, AllNotes);

            if (EditState.IsVirtual)
            {
                // Inject new pointer chain
                if (EditState.ChainOffsets.Count == 0) return;

                updatedBaseNote = CodeNoteModifier.InjectNoteContent(
                    CurrentBaseNote.Note,
                    EditState.ChainOffsets,
                    EditState.DisplayText,
                    style
                );
            }
            else
            {
                // Update existing node
                if (EditState.TargetNode != null)
                {
                    // If editing Root, simple replace
                    if (EditState.TargetNode.IndentLevel == -2)
                    {
                        updatedBaseNote = EditState.DisplayText;
                    }
                    else
                    {
                        // Splicing for child node
                        updatedBaseNote = CodeNoteModifier.UpdateRealNode(
                            CurrentBaseNote.Note,
                            EditState.TargetNode,
                            EditState.DisplayText,
                            style
                        );
                    }
                }
                else
                {
                    // Fallback -> editing root
                    updatedBaseNote = EditState.DisplayText;
                }
            }

            // Apply to Model
            CurrentBaseNote.Note = updatedBaseNote;

            // Re-parse
            CurrentParsedNodes = _parser.ParseNoteText(updatedBaseNote);

            // Save context to restore selection after clear
            var savedOffsets = EditState.IsVirtual ? new List<long>(EditState.ChainOffsets) : null;
            var savedNodeOffset = (!EditState.IsVirtual && EditState.TargetNode != null) ? EditState.TargetNode.Offset : null;

            ClearEditState();

            // IMPORTANT: Notify UI that content changed so it refreshes the node list (CurrentParsedNodes).
            NoteContentChanged?.Invoke(this, EventArgs.Empty);

            // Restore Selection
            if (savedOffsets != null)
            {
                HighlightVariable(savedOffsets);
            }
            else if (savedNodeOffset != null)
            {
                // Try to find the node we just edited by offset string match
                var node = FindNodeByOffsetString(savedNodeOffset);
                if (node != null) RequestNodeSelection(node);
            }
        }

        private NoteNode? FindNodeByOffsetString(string offset)
        {
            Stack<NoteNode> stack = new Stack<NoteNode>(CurrentParsedNodes);
            while (stack.Count > 0)
            {
                var n = stack.Pop();
                if (n.Offset == offset) return n;
                foreach (var c in n.Children) stack.Push(c);
            }
            return null;
        }

        // Set up state for editing a Real Node
        public void RequestNodeSelection(NoteNode node)
        {
            // If we were editing something else and it's dirty, commit it first
            if (EditState.IsDirty) CommitEditState();

            string content = "";
            // Extract content logic
            if (node.IndentLevel == -2) content = CurrentBaseNote?.Note ?? "";
            else if (!string.IsNullOrWhiteSpace(node.Content)) content = node.Content;
            else if (!string.IsNullOrWhiteSpace(node.Description)) content = node.Description;
            else content = node.ToString();

            string desc = node.Description;
            if (string.IsNullOrEmpty(desc)) desc = node.Offset;

            // Set TargetNode BEFORE setting edit state to avoid race conditions in UI update
            EditState.TargetNode = node;

            SetEditState(
                false, // IsVirtual
                desc,
                content, // Initial Text
                "", "", "", // Logic strings not needed for real editing display
                null
            );

            SelectionRequested?.Invoke(this, node);
        }

        public (string Logic, string Type, string Size) GetSelectedLogic(NoteNode? uiSelectedNode)
        {
            if (EditState.IsVirtual)
            {
                return (EditState.LogicCode, EditState.LogicType, EditState.LogicSize);
            }

            if (CurrentBaseNote == null) return ("", "", "");
            if (uiSelectedNode == null) return ("", "", "");

            return _parser.GeneratePointerLogic(CurrentBaseNote, uiSelectedNode);
        }

        public void ClearEditState()
        {
            EditState = new EditorState();
            EditStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void HighlightAddress(string address)
        {
            if (EditState.IsDirty) CommitEditState();

            if (string.IsNullOrEmpty(address)) return;
            string hex = address.Trim();
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) hex = hex.Substring(2);
            if (!long.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long targetVal)) return;

            var exactNote = AllNotes.FirstOrDefault(n => ParseAddress(n.Address) == targetVal);
            if (exactNote != null)
            {
                SelectBaseNote(exactNote);
                return;
            }

            var rangeMatch = _rangeCache.FirstOrDefault(r => targetVal >= r.Start && targetVal < r.End);
            if (rangeMatch.Note != null)
            {
                SelectBaseNote(rangeMatch.Note);

                long delta = targetVal - rangeMatch.Start;
                string desc = rangeMatch.Node.Description;
                if (string.IsNullOrEmpty(desc) || desc == "Full Note")
                    desc = rangeMatch.Node.Content.Split('\n').FirstOrDefault()?.Trim() ?? "";

                string virtualDesc = $"{desc} +0x{delta:X}";

                // Setup Virtual Mode
                SetEditState(
                    true,
                    $"[Indirect from {rangeMatch.Note.Address}] {virtualDesc}",
                    virtualDesc, // Default Text
                    $"0x{targetVal:x}",
                    "Mem",
                    "8-bit",
                    new List<long> { delta } // Treat as offset from base
                );
            }
        }

        public void HighlightVariable(List<long> offsets)
        {
            if (EditState.IsDirty) CommitEditState();

            if (offsets == null || offsets.Count == 0) return;
            if (CurrentBaseNote == null) return;
            if (CurrentParsedNodes.Count == 0) return;

            var nodes = CurrentParsedNodes;
            NoteNode? bestMatchNode = null;
            int matchedDepth = 0;
            long finalRemainder = 0;

            long ParseOffsetString(string offsetStr)
            {
                string clean = offsetStr.Trim().Replace("+", "").Replace("-", "").Replace("0x", "");
                if (long.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long val))
                {
                    if (offsetStr.Contains("-")) return -val;
                    return val;
                }
                return long.MinValue;
            }

            var rootNodes = nodes.Where(n => n.IndentLevel == 0 || (n.Parent != null && n.Parent.IndentLevel == -1) || (n.Parent == null && n.IndentLevel != -2)).ToList();
            var current = rootNodes.FirstOrDefault(n => ParseOffsetString(n.Offset) == offsets[0]);

            if (current == null)
            {
                foreach (var n in rootNodes)
                {
                    long start = ParseOffsetString(n.Offset);
                    if (offsets[0] >= start && offsets[0] < start + n.Size)
                    {
                        current = n;
                        finalRemainder = offsets[0] - start;
                        break;
                    }
                }
            }

            if (current != null)
            {
                bestMatchNode = current;
                matchedDepth = 1;

                while (matchedDepth < offsets.Count)
                {
                    if (finalRemainder != 0) break;

                    long nextOffset = offsets[matchedDepth];
                    var nextNode = current.Children.FirstOrDefault(n => ParseOffsetString(n.Offset) == nextOffset);

                    if (nextNode == null)
                    {
                        foreach (var n in current.Children)
                        {
                            long start = ParseOffsetString(n.Offset);
                            if (nextOffset >= start && nextOffset < start + n.Size)
                            {
                                nextNode = n;
                                finalRemainder = nextOffset - start;
                                break;
                            }
                        }
                    }

                    if (nextNode != null)
                    {
                        current = nextNode;
                        bestMatchNode = current;
                        matchedDepth++;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (bestMatchNode != null && matchedDepth == offsets.Count && finalRemainder == 0)
            {
                // Exact match found -> Real Node Editing Mode
                RequestNodeSelection(bestMatchNode);
                return;
            }

            // Partial Match or No Match -> Virtual Mode
            if (bestMatchNode != null)
            {
                var (baseLogic, type, size) = _parser.GeneratePointerLogic(CurrentBaseNote, bestMatchNode);

                string baseAddrStr = CurrentBaseNote.Address.StartsWith("0x") ? CurrentBaseNote.Address.Substring(2) : CurrentBaseNote.Address;
                StringBuilder sbLogic = new StringBuilder();
                sbLogic.Append($"I:0x{baseAddrStr}");
                for (int i = 0; i < offsets.Count; i++)
                {
                    sbLogic.Append("_");
                    if (i == offsets.Count - 1) sbLogic.Append($"0x{offsets[i]:x}");
                    else sbLogic.Append($"I:0x{offsets[i]:x}");
                }

                string desc = bestMatchNode.Description.Trim();
                for (int i = matchedDepth; i < offsets.Count; i++)
                {
                    desc += $" +0x{offsets[i]:X}";
                }
                if (finalRemainder != 0)
                {
                    desc += $" +0x{finalRemainder:X}";
                }

                // Pass the full chain of offsets to allow recreation
                SetEditState(true, desc, desc, sbLogic.ToString(), type, size, offsets);
                return;
            }

            if (bestMatchNode == null)
            {
                string baseAddrStr = CurrentBaseNote.Address.StartsWith("0x") ? CurrentBaseNote.Address.Substring(2) : CurrentBaseNote.Address;
                StringBuilder sb = new StringBuilder();
                sb.Append($"I:0x{baseAddrStr}");

                for (int i = 0; i < offsets.Count; i++)
                {
                    sb.Append("_");
                    if (i == offsets.Count - 1) sb.Append($"0x{offsets[i]:x}");
                    else sb.Append($"I:0x{offsets[i]:x}");
                }

                string desc = $"{CurrentBaseNote.Address} Chain";
                foreach (var o in offsets) desc += $" +0x{o:X}";

                SetEditState(true, desc, desc, sb.ToString(), "Mem", "32-bit", offsets);
                return;
            }
        }

        private void SetEditState(bool isVirtual, string description, string displayText, string logicCode, string type, string size, List<long>? offsets)
        {
            EditState.IsVirtual = isVirtual;
            EditState.Description = description;
            EditState.DisplayText = displayText;
            EditState.LogicCode = logicCode;
            EditState.LogicType = type;
            EditState.LogicSize = size;
            EditState.ChainOffsets = offsets ?? new List<long>();
            EditState.IsDirty = false; // Reset dirty flag when creating new virtual state

            EditStateChanged?.Invoke(this, EventArgs.Empty);
        }

        private long ParseAddress(string addr)
        {
            if (string.IsNullOrEmpty(addr)) return -1;
            string hex = addr.Trim();
            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) hex = hex.Substring(2);
            if (long.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long result)) return result;
            return -1;
        }
    }
}