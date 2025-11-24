using RALogicEditor.Controls;
using RALogicEditor.Definitions;
using RALogicEditor.Utilities;
using RALogicEditor.Models;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Linq;
using System;

namespace RALogicEditor.Helpers
{
    // Static helper class to handle the visual population and formatting of the DataGridView
    // based on the underlying AchievementCondition model.
    public static class ConditionGridHelper
    {
        // Clears the grid and repopulates it with the conditions from the provided group.
        // Handles read-only state, decimal display, and now Alias Mode with Pointer Context.
        public static void PopulateGridRows(
            DoubleBufferedDataGridView grid,
            AchievementConditionGroup group,
            bool readOnlyMode,
            bool showDecimal,
            bool showAliases,
            Dictionary<string, List<NoteNode>>? notesLookup,
            List<(long Start, long End, NoteNode Node)>? rangeCache,
            List<ValidationIssue>? issues = null) // Added Issues parameter
        {
            grid.Rows.Clear();
            if (group == null || group.Conditions == null) return;

            // Set global grid read-only state and base colors
            grid.ReadOnly = readOnlyMode;
            grid.DefaultCellStyle.BackColor = readOnlyMode ? SystemColors.Control : SystemColors.Window;
            grid.DefaultCellStyle.ForeColor = readOnlyMode ? SystemColors.GrayText : SystemColors.ControlText;

            // Iterate through conditions and create rows
            for (int i = 0; i < group.Conditions.Count; i++)
            {
                var cond = group.Conditions[i];
                cond.ID = i + 1; // Renumber IDs sequentially based on visual order
                var row = new DataGridViewRow();
                row.CreateCells(grid);
                row.Tag = cond; // Link the row to the model object

                // Check if this condition has issues
                ValidationIssue? targetIssue = null;
                if (issues != null)
                {
                    // Find highest severity issue targeting this condition
                    targetIssue = issues.Where(iss => iss.Target == cond)
                                        .OrderByDescending(iss => iss.Severity)
                                        .FirstOrDefault();
                }

                // Pass 'i' and 'group' for pointer chain context scanning
                SetRowValues(row, cond, readOnlyMode, showDecimal, showAliases, notesLookup, rangeCache, group, i, targetIssue);
                grid.Rows.Add(row);
            }
        }

        // Maps the properties of an AchievementCondition to the specific cells in a DataGridViewRow.
        public static void SetRowValues(
            DataGridViewRow row,
            AchievementCondition cond,
            bool readOnlyMode,
            bool showDecimal,
            bool showAliases,
            Dictionary<string, List<NoteNode>>? notesLookup,
            List<(long Start, long End, NoteNode Node)>? rangeCache,
            AchievementConditionGroup? group = null,
            int rowIndex = -1,
            ValidationIssue? issue = null) // Added Issue parameter
        {
            row.Cells[0].Value = cond.ID;
            row.Cells[1].Value = cond.Flag;
            row.Cells[2].Value = cond.LeftOperand.Type;
            row.Cells[3].Value = cond.LeftOperand.Size;

            // Format Left Value
            row.Cells[4].Value = FormatOperandDisplay(cond.LeftOperand, showDecimal, showAliases, notesLookup, rangeCache, group, rowIndex, true);

            row.Cells[5].Value = cond.Operator;

            row.Cells[6].Value = cond.RightOperand.Type;
            row.Cells[7].Value = cond.RightOperand.Size;

            // Format Right Value
            string rightSizeRef = LogicFormatter.GetRightOperandSizeReference(cond);
            row.Cells[8].Value = FormatOperandDisplay(cond.RightOperand, showDecimal, showAliases, notesLookup, rangeCache, group, rowIndex, false, rightSizeRef);

            // Apply visual styling (graying out invalid cells and applying issue highlights)
            ApplyCellFormatting(row, cond, readOnlyMode, showAliases, issue);
        }

        private static string FormatOperandDisplay(Operand op, bool showDecimal, bool showAliases, Dictionary<string, List<NoteNode>>? notesLookup, List<(long Start, long End, NoteNode Node)>? rangeCache, AchievementConditionGroup? group, int rowIndex, bool isLeft, string sizeRef = "")
        {
            // If Alias Mode is ON and this is a Memory type
            if (showAliases && RaLogicSyntax.MemoryTypes.Contains(op.Type) && notesLookup != null)
            {
                // Normalize the address using the same logic as the dictionary keys
                string addr = LogicFormatter.NormalizeAddress(op.Value);

                string alias = ResolveAlias(addr, group, rowIndex, isLeft, notesLookup, rangeCache);
                if (!string.IsNullOrEmpty(alias))
                {
                    // We strip tags inside ResolveAlias now for cleaner duplicate checking, 
                    // but we do a final trim here just in case.
                    return alias.Trim();
                }
            }

            // Fallback to standard formatting (Hex/Decimal/Float)
            return LogicFormatter.FormatDisplayValue(op, showDecimal, sizeRef);
        }

        private static string ResolveAlias(
            string address,
            AchievementConditionGroup? group,
            int rowIndex,
            bool isLeft,
            Dictionary<string, List<NoteNode>> notesLookup,
            List<(long Start, long End, NoteNode Node)>? rangeCache)
        {
            // Helper to clean hex strings "0x123" -> long
            long ParseHex(string s) =>
                long.TryParse(s.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long v) ? v : -1;

            long targetVal = ParseHex(address);

            // 1. Strict Pointer Chain Logic (Offsets)
            // We verify that the sequence of 'Add Address' offsets in the Grid matches the hierarchy in the Notes.
            if (group != null && rowIndex > 0)
            {
                // Collect the chain of offsets from the grid (scanning backwards)
                var chainStack = new Stack<string>();
                int scan = rowIndex - 1;
                while (scan >= 0)
                {
                    var prevCond = group.Conditions[scan];
                    // RA logic: A contiguous block of "Add Address" defines the pointer chain.
                    // The first "Add Address" in the block is the Base.
                    if (prevCond.Flag == "Add Address")
                    {
                        chainStack.Push(prevCond.LeftOperand.Value);
                        scan--;
                    }
                    else
                    {
                        break;
                    }
                }

                if (chainStack.Count > 0)
                {
                    // The bottom of the stack is the Base Address
                    string baseAddrStr = chainStack.Pop();
                    string normalizedBase = LogicFormatter.NormalizeAddress(baseAddrStr);

                    if (notesLookup.TryGetValue(normalizedBase, out var nodes))
                    {
                        // Find valid top-level nodes (children of logical root)
                        var currentLevelNodes = nodes.Where(n => n.IndentLevel != -2 && (n.Parent == null || n.Parent.IndentLevel == -1)).ToList();
                        bool chainValid = true;

                        // Traverse the intermediate offsets (if any)
                        while (chainStack.Count > 0)
                        {
                            string offsetStr = chainStack.Pop();
                            long offset = ParseHex(offsetStr);
                            if (offset == -1) { chainValid = false; break; }

                            // Find a node in currentLevelNodes that matches this offset
                            NoteNode? match = null;
                            foreach (var node in currentLevelNodes)
                            {
                                long nodeOff = ParseHex(node.Offset.Replace("+", "").Replace("-", "").Trim());
                                if (node.Offset.Contains("-")) nodeOff = -nodeOff;

                                if (offset >= nodeOff && offset < nodeOff + node.Size)
                                {
                                    match = node;
                                    break;
                                }
                            }

                            if (match != null)
                            {
                                currentLevelNodes = match.Children; // Go deeper
                            }
                            else
                            {
                                chainValid = false;
                                break;
                            }
                        }

                        // If the chain matched so far, look for the Target (current row address) in the final context
                        if (chainValid)
                        {
                            if (targetVal != -1)
                            {
                                foreach (var node in currentLevelNodes)
                                {
                                    long nodeOff = ParseHex(node.Offset.Replace("+", "").Replace("-", "").Trim());
                                    if (node.Offset.Contains("-")) nodeOff = -nodeOff;

                                    if (targetVal >= nodeOff && targetVal < nodeOff + node.Size)
                                    {
                                        string desc = node.Description;
                                        // For offsets (children in a chain), we always strip ALL tags for cleaner look
                                        desc = Regex.Replace(desc, @"\[.*?\]", "").Trim();

                                        long delta = targetVal - nodeOff;
                                        if (delta > 0)
                                        {
                                            string suffix = $" +0x{delta:X}";
                                            if (!desc.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                                                desc += suffix;
                                        }
                                        return desc;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // 2. Direct Lookup (Base Address or simple memory)
            if (notesLookup.TryGetValue(address, out var directNodes))
            {
                var root = directNodes.FirstOrDefault();
                if (root != null)
                {
                    var main = directNodes.FirstOrDefault(n => n.IndentLevel == -2) ?? directNodes.First();
                    string desc = main.Description;

                    if (string.IsNullOrEmpty(desc) || desc.Equals("Full Note", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(main.Content))
                        {
                            desc = main.Content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                        }
                    }

                    // Base Addresses: Simplify pointer tags to [Pointer]
                    desc = Regex.Replace(desc, @"\[.*?\]", m =>
                    {
                        if (m.Value.IndexOf("Pointer", StringComparison.OrdinalIgnoreCase) >= 0) return "[Pointer]";
                        return ""; // Strip other tags like [8-bit]
                    }).Trim();

                    // Clean up potential double spaces from removals
                    desc = Regex.Replace(desc, @"\s{2,}", " ");
                    return desc;
                }
            }

            // 3. Ranged Direct Lookup (e.g. Entity Slot [176 bytes])
            if (rangeCache != null && targetVal != -1)
            {
                var match = rangeCache.FirstOrDefault(r => targetVal >= r.Start && targetVal < r.End);
                if (match.Node != null)
                {
                    string desc = match.Node.Description;
                    if (string.IsNullOrEmpty(desc) || desc.Equals("Full Note", StringComparison.OrdinalIgnoreCase))
                    {
                        string content = match.Node.Content;
                        desc = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                    }

                    // Ranged Base Addresses: Simplify pointer tags to [Pointer]
                    desc = Regex.Replace(desc, @"\[.*?\]", m =>
                    {
                        if (m.Value.IndexOf("Pointer", StringComparison.OrdinalIgnoreCase) >= 0) return "[Pointer]";
                        return "";
                    }).Trim();
                    desc = Regex.Replace(desc, @"\s{2,}", " ");

                    long delta = targetVal - match.Start;
                    if (delta > 0)
                    {
                        string suffix = $" +0x{delta:X}";
                        if (!desc.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                        {
                            desc += suffix;
                        }
                    }
                    return desc;
                }
            }

            return "";
        }

        private static void ApplyCellFormatting(DataGridViewRow row, AchievementCondition cond, bool readOnlyMode, bool showAliases, ValidationIssue? issue)
        {
            // Define colors
            Color editableColor = readOnlyMode ? SystemColors.Control : SystemColors.Window;
            Color staticColor = SystemColors.ControlLight;

            // -- Issue Highlighting (ID Column ONLY) --
            if (issue != null)
            {
                if (issue.Severity == FeedbackSeverity.Error)
                    row.Cells[0].Style.BackColor = Color.FromArgb(255, 220, 220); // Light Red
                else if (issue.Severity == FeedbackSeverity.Warning)
                    row.Cells[0].Style.BackColor = Color.FromArgb(255, 245, 200); // Light Orange
                else
                    row.Cells[0].Style.BackColor = staticColor; // Default ID Color if info/none
            }
            else
            {
                // Reset to default static color
                row.Cells[0].Style.BackColor = staticColor;
            }

            // -- Left Operand Formatting --
            var lSizeCell = row.Cells[3];
            var lValueCell = row.Cells[4];
            bool leftIsRecall = cond.LeftOperand.Type == "Recall";

            // If Left Type is Float, Value, or Recall, size is irrelevant
            if (cond.LeftOperand.Type is "Float" or "Value" or "Recall")
            {
                lSizeCell.Style.BackColor = staticColor;
                lSizeCell.ToolTipText = "";

                if (leftIsRecall)
                {
                    lValueCell.ReadOnly = true;
                    lValueCell.Style.BackColor = staticColor;
                    row.Cells[4].Value = "";
                }
                else
                {
                    lValueCell.ReadOnly = readOnlyMode;
                    lValueCell.Style.BackColor = editableColor;
                }
            }
            else
            {
                // Memory types: specific coloring for implicit vs explicit sizes
                if (string.IsNullOrEmpty(cond.LeftOperand.Size))
                {
                    // Highlight implicit 32-bit to warn user
                    lSizeCell.Style.BackColor = readOnlyMode ? Color.FromArgb(240, 240, 220) : Color.LemonChiffon;
                    lSizeCell.ToolTipText = "Implicit 32-bit";
                }
                else
                {
                    lSizeCell.Style.BackColor = editableColor;
                    lSizeCell.ToolTipText = "";
                }

                // If Alias mode is active and this is a memory type, disable editing to prevent string corruption
                if (showAliases && RaLogicSyntax.MemoryTypes.Contains(cond.LeftOperand.Type))
                {
                    lValueCell.ReadOnly = true;
                    lValueCell.Style.BackColor = staticColor;
                }
                else
                {
                    lValueCell.ReadOnly = readOnlyMode;
                    lValueCell.Style.BackColor = editableColor;
                }
            }

            // -- Right Operand Formatting --
            var rTypeCell = row.Cells[6];
            var rSizeCell = row.Cells[7];
            var rValueCell = row.Cells[8];
            bool rightIsRecall = cond.RightOperand.Type == "Recall";

            // If no operator exists (e.g. PauseIf), disable the entire right side
            if (string.IsNullOrEmpty(cond.Operator))
            {
                rTypeCell.Style.BackColor = staticColor;
                rSizeCell.Style.BackColor = staticColor;
                rValueCell.ReadOnly = true;
                rValueCell.Style.BackColor = staticColor;
                row.Cells[6].Value = "";
                row.Cells[7].Value = "";
                row.Cells[8].Value = "";
            }
            else
            {
                rTypeCell.Style.BackColor = editableColor;

                if (rightIsRecall)
                {
                    rSizeCell.Style.BackColor = staticColor;
                    rSizeCell.ToolTipText = "";
                    rValueCell.ReadOnly = true;
                    rValueCell.Style.BackColor = staticColor;
                    row.Cells[8].Value = "";
                }
                else if (cond.RightOperand.Type is "Float" or "Value")
                {
                    rValueCell.ReadOnly = readOnlyMode;
                    rValueCell.Style.BackColor = editableColor;
                    rSizeCell.Style.BackColor = staticColor;
                    rSizeCell.ToolTipText = "";
                }
                else
                {
                    // Memory on right side
                    if (string.IsNullOrEmpty(cond.RightOperand.Size))
                    {
                        if (RaLogicSyntax.MemoryTypes.Contains(cond.RightOperand.Type))
                        {
                            rSizeCell.Style.BackColor = readOnlyMode ? Color.FromArgb(240, 240, 220) : Color.LemonChiffon;
                            rSizeCell.ToolTipText = "Implicit 32-bit";
                        }
                        else
                        {
                            rSizeCell.Style.BackColor = editableColor;
                            rSizeCell.ToolTipText = "";
                        }
                    }
                    else
                    {
                        rSizeCell.Style.BackColor = editableColor;
                        rSizeCell.ToolTipText = "";
                    }

                    // Alias Lock for Right Operand
                    if (showAliases && RaLogicSyntax.MemoryTypes.Contains(cond.RightOperand.Type))
                    {
                        rValueCell.ReadOnly = true;
                        rValueCell.Style.BackColor = staticColor;
                    }
                    else
                    {
                        rValueCell.ReadOnly = readOnlyMode;
                        rValueCell.Style.BackColor = editableColor;
                    }
                }
            }

            // -- Hits Formatting --
            // Some flags (AddSource, SubSource) do not support Hit counts
            if (RaLogicSyntax.FlagsWithoutHits.Contains(cond.Flag))
            {
                cond.RequiredHits = 0;
                row.Cells[9].Value = "";
                row.Cells[9].ReadOnly = true;
                row.Cells[9].Style.BackColor = staticColor;
            }
            else
            {
                row.Cells[9].Value = cond.RequiredHits > 0 ? cond.RequiredHits.ToString() : "";
                row.Cells[9].ReadOnly = readOnlyMode;
                row.Cells[9].Style.BackColor = editableColor;
            }
        }
    }
}