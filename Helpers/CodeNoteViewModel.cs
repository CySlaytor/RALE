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

        // -- Virtual State --
        // Handles "Ghost" notes that don't exist in the file but are calculated (e.g. +0x10 off a struct)
        public class VirtualStateData
        {
            public bool IsActive;
            public string Description = "";
            public string LogicCode = "";
            public string LogicType = "";
            public string LogicSize = "";
            public string DisplayText = ""; // The text to show in the body box
        }
        public VirtualStateData VirtualState { get; private set; } = new();

        // -- Events --
        public event EventHandler? NotesLoaded;
        public event EventHandler? NoteContentChanged;
        public event EventHandler? VirtualStateChanged;
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
            CurrentBaseNote = note;
            ClearVirtualState();

            string normalized = note.Note.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
            CurrentParsedNodes = _parser.ParseNoteText(normalized);

            NoteContentChanged?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateNoteContent(string newText)
        {
            if (CurrentBaseNote == null || VirtualState.IsActive) return;

            CurrentBaseNote.Note = newText;
            CurrentParsedNodes = _parser.ParseNoteText(newText);
        }

        public (string Logic, string Type, string Size) GetSelectedLogic(NoteNode? uiSelectedNode)
        {
            if (VirtualState.IsActive)
            {
                return (VirtualState.LogicCode, VirtualState.LogicType, VirtualState.LogicSize);
            }

            if (CurrentBaseNote == null) return ("", "", "");
            if (uiSelectedNode == null) return ("", "", "");

            return _parser.GeneratePointerLogic(CurrentBaseNote, uiSelectedNode);
        }

        public void ClearVirtualState()
        {
            VirtualState = new VirtualStateData();
            VirtualStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void HighlightAddress(string address)
        {
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

                SetVirtualState(
                    true,
                    $"[Indirect from {rangeMatch.Note.Address}] {virtualDesc}",
                    virtualDesc,
                    $"0x{targetVal:x}",
                    "Mem",
                    "8-bit"
                );
            }
        }

        public void HighlightVariable(List<long> offsets)
        {
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
                ClearVirtualState();
                SelectionRequested?.Invoke(this, bestMatchNode);
                return;
            }

            if (bestMatchNode != null)
            {
                SelectionRequested?.Invoke(this, bestMatchNode);

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

                SetVirtualState(true, desc, desc, sbLogic.ToString(), type, size);
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

                SetVirtualState(true, desc, desc, sb.ToString(), "Mem", "32-bit");
                return;
            }
        }

        private void SetVirtualState(bool active, string description, string displayText, string logicCode, string type, string size)
        {
            VirtualState.IsActive = active;
            VirtualState.Description = description;
            VirtualState.DisplayText = displayText;
            VirtualState.LogicCode = logicCode;
            VirtualState.LogicType = type;
            VirtualState.LogicSize = size;

            VirtualStateChanged?.Invoke(this, EventArgs.Empty);
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