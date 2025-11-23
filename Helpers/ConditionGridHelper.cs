using RALogicEditor.Controls;
using RALogicEditor.Definitions;
using RALogicEditor.Utilities;
using RALogicEditor.Models;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Globalization;

namespace RALogicEditor.Helpers
{
    // Static helper class to handle the visual population and formatting of the DataGridView
    // based on the underlying AchievementCondition model.
    public static class ConditionGridHelper
    {
        // Clears the grid and repopulates it with the conditions from the provided group.
        // Handles read-only state, decimal display, and now Alias Mode.
        public static void PopulateGridRows(DoubleBufferedDataGridView grid, AchievementConditionGroup group, bool readOnlyMode, bool showDecimal, bool showAliases, Dictionary<string, string>? notesLookup)
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

                SetRowValues(row, cond, readOnlyMode, showDecimal, showAliases, notesLookup);
                grid.Rows.Add(row);
            }
        }

        // Maps the properties of an AchievementCondition to the specific cells in a DataGridViewRow.
        public static void SetRowValues(DataGridViewRow row, AchievementCondition cond, bool readOnlyMode, bool showDecimal, bool showAliases, Dictionary<string, string>? notesLookup)
        {
            row.Cells[0].Value = cond.ID;
            row.Cells[1].Value = cond.Flag;
            row.Cells[2].Value = cond.LeftOperand.Type;
            row.Cells[3].Value = cond.LeftOperand.Size;

            // Format Left Value (Alias check for Memory, Decimal check for Values)
            row.Cells[4].Value = FormatOperandDisplay(cond.LeftOperand, showDecimal, showAliases, notesLookup);

            row.Cells[5].Value = cond.Operator;

            row.Cells[6].Value = cond.RightOperand.Type;
            row.Cells[7].Value = cond.RightOperand.Size;

            // Format Right Value (Alias check for Memory, Decimal check for Values)
            // Needs reference size from Left operand for accurate masking if it is a Value
            string rightSizeRef = LogicFormatter.GetRightOperandSizeReference(cond);
            // We pass the operand itself, but we handle the formatting logic manually if needed or delegate
            row.Cells[8].Value = FormatOperandDisplay(cond.RightOperand, showDecimal, showAliases, notesLookup, rightSizeRef);

            // Apply visual styling (graying out invalid cells)
            ApplyCellFormatting(row, cond, readOnlyMode);
        }

        private static string FormatOperandDisplay(Operand op, bool showDecimal, bool showAliases, Dictionary<string, string>? notesLookup, string sizeRef = "")
        {
            // If Alias Mode is ON and this is a Memory type, try to find the note
            if (showAliases && RaLogicSyntax.MemoryTypes.Contains(op.Type) && notesLookup != null)
            {
                // Normalize address (ensure 0x prefix)
                string addr = op.Value.Trim();
                if (!addr.StartsWith("0x") && long.TryParse(addr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
                {
                    addr = "0x" + addr;
                }

                if (notesLookup.ContainsKey(addr))
                {
                    return $"[{notesLookup[addr]}]";
                }
            }

            // Fallback to standard formatting (Hex/Decimal/Float)
            return LogicFormatter.FormatDisplayValue(op, showDecimal, sizeRef);
        }

        // Applies specific background colors and read-only states to cells based on logic rules.
        // For example: If Type is "Value", the "Size" column should be disabled/grayed out.
        private static void ApplyCellFormatting(DataGridViewRow row, AchievementCondition cond, bool readOnlyMode)
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
                    row.Cells[4].Value = ""; // Recalls don't have a value
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
                lValueCell.ReadOnly = readOnlyMode;
                lValueCell.Style.BackColor = editableColor;
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
                    rSizeCell.Style.BackColor = staticColor; // Values don't have size on right side
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
                    rValueCell.ReadOnly = readOnlyMode;
                    rValueCell.Style.BackColor = editableColor;
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