using RALogicEditor.Controls;
using RALogicEditor.Definitions;
using RALogicEditor.Models;
using RALogicEditor.Utilities;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace RALogicEditor.Helpers
{
    // Responsibility: Manages visual state of the Grid (Colors, Rows, ReadOnly flags)
    // Delegates string formatting to ConditionFormatter
    public static class ConditionGridRenderer
    {
        public static void PopulateGridRows(
            DoubleBufferedDataGridView grid,
            AchievementConditionGroup group,
            bool readOnlyMode,
            bool showDecimal,
            bool showAliases,
            Dictionary<string, List<NoteNode>>? notesLookup,
            List<(long Start, long End, NoteNode Node)>? rangeCache,
            List<ValidationIssue>? issues = null,
            bool collapseChains = false) // New Parameter
        {
            grid.Rows.Clear();
            if (group == null || group.Conditions == null) return;

            grid.ReadOnly = readOnlyMode;
            grid.DefaultCellStyle.BackColor = readOnlyMode ? SystemColors.Control : SystemColors.Window;
            grid.DefaultCellStyle.ForeColor = readOnlyMode ? SystemColors.GrayText : SystemColors.ControlText;

            for (int i = 0; i < group.Conditions.Count; i++)
            {
                var cond = group.Conditions[i];

                // Filter Logic for Collapsed Chains
                if (collapseChains && cond.Flag == "Add Address")
                {
                    continue;
                }

                // Always update ID to match visual order in editor, or maintain model index?
                // The existing code did "cond.ID = i + 1", which modifies the model.
                // If we hide rows, "i" is still the model index. 
                // It's better to keep the ID as the Model Index so users know where they are in the real logic.
                cond.ID = i + 1;

                var row = new DataGridViewRow();
                row.CreateCells(grid);
                row.Tag = cond; // Critical: Link row to model object

                ValidationIssue? targetIssue = null;
                if (issues != null)
                {
                    targetIssue = issues.Where(iss => iss.Target == cond)
                                        .OrderByDescending(iss => iss.Severity)
                                        .FirstOrDefault();
                }

                SetRowValues(row, cond, readOnlyMode, showDecimal, showAliases, notesLookup, rangeCache, group, i, targetIssue);
                grid.Rows.Add(row);
            }
        }

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
            ValidationIssue? issue = null)
        {
            row.Cells[0].Value = cond.ID;
            row.Cells[1].Value = cond.Flag;
            row.Cells[2].Value = cond.LeftOperand.Type;
            row.Cells[3].Value = cond.LeftOperand.Size;

            // Use Formatter
            row.Cells[4].Value = ConditionFormatter.FormatOperandDisplay(
                cond.LeftOperand, showDecimal, showAliases, notesLookup, rangeCache, group, rowIndex);

            row.Cells[5].Value = cond.Operator;

            row.Cells[6].Value = cond.RightOperand.Type;
            row.Cells[7].Value = cond.RightOperand.Size;

            // Use Formatter with specific size ref
            string rightSizeRef = LogicFormatter.GetRightOperandSizeReference(cond);
            row.Cells[8].Value = ConditionFormatter.FormatOperandDisplay(
                cond.RightOperand, showDecimal, showAliases, notesLookup, rangeCache, group, rowIndex, rightSizeRef);

            // Style application
            ApplyCellFormatting(row, cond, readOnlyMode, showAliases, issue);
        }

        private static void ApplyCellFormatting(DataGridViewRow row, AchievementCondition cond, bool readOnlyMode, bool showAliases, ValidationIssue? issue)
        {
            Color editableColor = readOnlyMode ? SystemColors.Control : SystemColors.Window;
            Color staticColor = SystemColors.ControlLight;

            // Ensure strict ReadOnly state for non-text-input columns
            row.Cells[0].ReadOnly = true; // ID
            row.Cells[1].ReadOnly = true; // Flag
            row.Cells[2].ReadOnly = true; // LType
            row.Cells[3].ReadOnly = true; // LSize
            row.Cells[5].ReadOnly = true; // Cmp
            row.Cells[6].ReadOnly = true; // RType
            row.Cells[7].ReadOnly = true; // RSize

            // Issue Highlighting
            if (issue != null)
            {
                if (issue.Severity == FeedbackSeverity.Error)
                    row.Cells[0].Style.BackColor = Color.FromArgb(255, 220, 220);
                else if (issue.Severity == FeedbackSeverity.Warning)
                    row.Cells[0].Style.BackColor = Color.FromArgb(255, 245, 200);
                else
                    row.Cells[0].Style.BackColor = staticColor;
            }
            else
            {
                row.Cells[0].Style.BackColor = staticColor;
            }

            // Left Operand Styling
            var lSizeCell = row.Cells[3];
            var lValueCell = row.Cells[4];
            bool leftIsRecall = cond.LeftOperand.Type == "Recall";

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
                // Implicit size warning
                if (string.IsNullOrEmpty(cond.LeftOperand.Size))
                {
                    lSizeCell.Style.BackColor = readOnlyMode ? Color.FromArgb(240, 240, 220) : Color.LemonChiffon;
                    lSizeCell.ToolTipText = "Implicit 32-bit";
                }
                else
                {
                    lSizeCell.Style.BackColor = editableColor;
                    lSizeCell.ToolTipText = "";
                }

                // Alias Lock
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

            // Right Operand Styling
            var rTypeCell = row.Cells[6];
            var rSizeCell = row.Cells[7];
            var rValueCell = row.Cells[8];
            bool rightIsRecall = cond.RightOperand.Type == "Recall";

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

            // Hits Styling
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