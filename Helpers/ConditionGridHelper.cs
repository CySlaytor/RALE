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
        public static void PopulateGridRows(DoubleBufferedDataGridView grid, AchievementConditionGroup group, bool readOnlyMode, bool showDecimal, bool showAliases, Dictionary<string, List<NoteNode>>? notesLookup)
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

                // Pass 'i' and 'group' for pointer chain context scanning
                SetRowValues(row, cond, readOnlyMode, showDecimal, showAliases, notesLookup, group, i);
                grid.Rows.Add(row);
            }
        }

        // Maps the properties of an AchievementCondition to the specific cells in a DataGridViewRow.
        public static void SetRowValues(DataGridViewRow row, AchievementCondition cond, bool readOnlyMode, bool showDecimal, bool showAliases, Dictionary<string, List<NoteNode>>? notesLookup, AchievementConditionGroup? group = null, int rowIndex = -1)
        {
            row.Cells[0].Value = cond.ID;
            row.Cells[1].Value = cond.Flag;
            row.Cells[2].Value = cond.LeftOperand.Type;
            row.Cells[3].Value = cond.LeftOperand.Size;

            // Format Left Value
            row.Cells[4].Value = FormatOperandDisplay(cond.LeftOperand, showDecimal, showAliases, notesLookup, group, rowIndex, true);

            row.Cells[5].Value = cond.Operator;

            row.Cells[6].Value = cond.RightOperand.Type;
            row.Cells[7].Value = cond.RightOperand.Size;

            // Format Right Value
            string rightSizeRef = LogicFormatter.GetRightOperandSizeReference(cond);
            row.Cells[8].Value = FormatOperandDisplay(cond.RightOperand, showDecimal, showAliases, notesLookup, group, rowIndex, false, rightSizeRef);

            // Apply visual styling (graying out invalid cells)
            ApplyCellFormatting(row, cond, readOnlyMode, showAliases);
        }

        private static string FormatOperandDisplay(Operand op, bool showDecimal, bool showAliases, Dictionary<string, List<NoteNode>>? notesLookup, AchievementConditionGroup? group, int rowIndex, bool isLeft, string sizeRef = "")
        {
            // If Alias Mode is ON and this is a Memory type
            if (showAliases && RaLogicSyntax.MemoryTypes.Contains(op.Type) && notesLookup != null)
            {
                // Normalize the address using the same logic as the dictionary keys
                string addr = LogicFormatter.NormalizeAddress(op.Value);

                string alias = ResolveAlias(addr, group, rowIndex, isLeft, notesLookup);
                if (!string.IsNullOrEmpty(alias))
                {
                    // Strip [Tags] like [32-bit Pointer] or [Void]
                    // Regex: Matches [anything] and removes it.
                    alias = Regex.Replace(alias, @"\[.*?\]", "").Trim();

                    // Use a shorter display format for Aliases to indicate it is not a raw value
                    return alias;
                }
            }

            // Fallback to standard formatting (Hex/Decimal/Float)
            return LogicFormatter.FormatDisplayValue(op, showDecimal, sizeRef);
        }

        private static string ResolveAlias(string address, AchievementConditionGroup? group, int rowIndex, bool isLeft, Dictionary<string, List<NoteNode>> notesLookup)
        {
            // 1. Check for Pointer Chain context (Backwards scan)
            // Only applicable if we have a group context and rowIndex > 0
            if (group != null && rowIndex > 0)
            {
                // Scan backwards to find the Base Address of the chain
                int scan = rowIndex - 1;
                string? baseAddress = null;

                // We only care about "Add Address" flags forming a chain immediately preceding this row
                while (scan >= 0)
                {
                    var prevCond = group.Conditions[scan];
                    if (prevCond.Flag == "Add Address")
                    {
                        baseAddress = prevCond.LeftOperand.Value;
                        scan--;
                    }
                    else
                    {
                        // Chain broken
                        break;
                    }
                }

                // If we found a base address
                if (!string.IsNullOrEmpty(baseAddress))
                {
                    string normalizedBase = LogicFormatter.NormalizeAddress(baseAddress);

                    // Look up the Base Note
                    if (notesLookup.TryGetValue(normalizedBase, out var nodes))
                    {
                        // Convert current address to a relative offset
                        // e.g. address is "0x73" (value in grid for offset)
                        // remove 0x, parse hex
                        string cleanAddr = address.Replace("0x", "");

                        if (long.TryParse(cleanAddr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long targetOffset))
                        {
                            // Try to find a node with this offset
                            var matchingNode = nodes.FirstOrDefault(n =>
                            {
                                // Strip + and 0x for comparison from node offset string like "+0x73"
                                string cleanNodeOff = n.Offset.Replace("+", "").Replace("0x", "").Trim();
                                if (long.TryParse(cleanNodeOff, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long nodeVal))
                                {
                                    return nodeVal == targetOffset;
                                }
                                return false;
                            });

                            if (matchingNode != null)
                            {
                                return matchingNode.Description;
                            }
                        }
                    }
                }
            }

            // 2. Direct Lookup (Base Address or simple memory)
            if (notesLookup.TryGetValue(address, out var directNodes))
            {
                // Return the description of the root node
                var root = directNodes.FirstOrDefault();
                if (root != null)
                {
                    var main = directNodes.FirstOrDefault(n => n.IndentLevel == -2) ?? directNodes.First();

                    string desc = main.Description;

                    // "Full Note" is a generic placeholder from the PointerTreeParser. 
                    // If we see it, we must extract the first line of the content instead.
                    if (string.IsNullOrEmpty(desc) || desc.Equals("Full Note", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(main.Content))
                        {
                            desc = main.Content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                        }
                    }
                    return desc;
                }
            }

            return "";
        }

        private static void ApplyCellFormatting(DataGridViewRow row, AchievementCondition cond, bool readOnlyMode, bool showAliases)
        {
            // Define colors
            Color editableColor = readOnlyMode ? SystemColors.Control : SystemColors.Window;
            Color staticColor = SystemColors.ControlLight;

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
                // LogicFormatter checks RaLogicSyntax.MemoryTypes for alias support, so we do too.
                if (showAliases && RaLogicSyntax.MemoryTypes.Contains(cond.LeftOperand.Type))
                {
                    lValueCell.ReadOnly = true;
                    // Use a slightly different color (staticColor) to indicate non-editable state visually
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