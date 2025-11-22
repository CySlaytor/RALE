using RALogicEditor.Controls;
using RALogicEditor.Definitions;
using RALogicEditor.Utilities;
using RALogicEditor.Models;
using System.Drawing;
using System.Windows.Forms;

namespace RALogicEditor.Helpers
{
    public static class ConditionGridHelper
    {
        public static void PopulateGridRows(DoubleBufferedDataGridView grid, AchievementConditionGroup group, bool readOnlyMode)
        {
            grid.Rows.Clear();
            if (group == null || group.Conditions == null) return;

            grid.ReadOnly = readOnlyMode;
            grid.DefaultCellStyle.BackColor = readOnlyMode ? SystemColors.Control : SystemColors.Window;
            grid.DefaultCellStyle.ForeColor = readOnlyMode ? SystemColors.GrayText : SystemColors.ControlText;

            for (int i = 0; i < group.Conditions.Count; i++)
            {
                var cond = group.Conditions[i];
                cond.ID = i + 1;
                var row = new DataGridViewRow();
                row.CreateCells(grid);
                row.Tag = cond;
                SetRowValues(row, cond, readOnlyMode);
                grid.Rows.Add(row);
            }
        }

        public static void SetRowValues(DataGridViewRow row, AchievementCondition cond, bool readOnlyMode, bool showDecimal = false)
        {
            row.Cells[0].Value = cond.ID;
            row.Cells[1].Value = cond.Flag;
            row.Cells[2].Value = cond.LeftOperand.Type;
            row.Cells[3].Value = cond.LeftOperand.Size;
            row.Cells[4].Value = LogicFormatter.FormatDisplayValue(cond.LeftOperand, showDecimal);
            row.Cells[5].Value = cond.Operator;
            row.Cells[6].Value = cond.RightOperand.Type;
            row.Cells[7].Value = cond.RightOperand.Size;
            string rightSizeRef = LogicFormatter.GetRightOperandSizeReference(cond);
            row.Cells[8].Value = LogicFormatter.FormatDisplayValue(cond.RightOperand, showDecimal, rightSizeRef);
            ApplyCellFormatting(row, cond, readOnlyMode);
        }

        private static void ApplyCellFormatting(DataGridViewRow row, AchievementCondition cond, bool readOnlyMode)
        {
            var lSizeCell = row.Cells[3]; var lValueCell = row.Cells[4]; bool leftIsRecall = cond.LeftOperand.Type == "Recall";
            Color editableColor = readOnlyMode ? SystemColors.Control : SystemColors.Window; Color staticColor = SystemColors.ControlLight;

            if (cond.LeftOperand.Type is "Float" or "Value" or "Recall") { lSizeCell.Style.BackColor = staticColor; lSizeCell.ToolTipText = ""; if (leftIsRecall) { lValueCell.ReadOnly = true; lValueCell.Style.BackColor = staticColor; row.Cells[4].Value = ""; } else { lValueCell.ReadOnly = readOnlyMode; lValueCell.Style.BackColor = editableColor; } }
            else { if (string.IsNullOrEmpty(cond.LeftOperand.Size)) { lSizeCell.Style.BackColor = readOnlyMode ? Color.FromArgb(240, 240, 220) : Color.LemonChiffon; lSizeCell.ToolTipText = "Implicit 32-bit"; } else { lSizeCell.Style.BackColor = editableColor; lSizeCell.ToolTipText = ""; } lValueCell.ReadOnly = readOnlyMode; lValueCell.Style.BackColor = editableColor; }

            var rTypeCell = row.Cells[6]; var rSizeCell = row.Cells[7]; var rValueCell = row.Cells[8]; bool rightIsRecall = cond.RightOperand.Type == "Recall";
            if (string.IsNullOrEmpty(cond.Operator)) { rTypeCell.Style.BackColor = staticColor; rSizeCell.Style.BackColor = staticColor; rValueCell.ReadOnly = true; rValueCell.Style.BackColor = staticColor; row.Cells[6].Value = ""; row.Cells[7].Value = ""; row.Cells[8].Value = ""; }
            else { rTypeCell.Style.BackColor = editableColor; if (rightIsRecall) { rSizeCell.Style.BackColor = staticColor; rSizeCell.ToolTipText = ""; rValueCell.ReadOnly = true; rValueCell.Style.BackColor = staticColor; row.Cells[8].Value = ""; } else if (cond.RightOperand.Type is "Float" or "Value") { rValueCell.ReadOnly = readOnlyMode; rValueCell.Style.BackColor = editableColor; rSizeCell.Style.BackColor = staticColor; rSizeCell.ToolTipText = ""; } else { if (string.IsNullOrEmpty(cond.RightOperand.Size)) { if (RaLogicSyntax.MemoryTypes.Contains(cond.RightOperand.Type)) { rSizeCell.Style.BackColor = readOnlyMode ? Color.FromArgb(240, 240, 220) : Color.LemonChiffon; rSizeCell.ToolTipText = "Implicit 32-bit"; } else { rSizeCell.Style.BackColor = editableColor; rSizeCell.ToolTipText = ""; } } else { rSizeCell.Style.BackColor = editableColor; rSizeCell.ToolTipText = ""; } rValueCell.ReadOnly = readOnlyMode; rValueCell.Style.BackColor = editableColor; } }

            if (RaLogicSyntax.FlagsWithoutHits.Contains(cond.Flag)) { cond.RequiredHits = 0; row.Cells[9].Value = ""; row.Cells[9].ReadOnly = true; row.Cells[9].Style.BackColor = staticColor; }
            else { row.Cells[9].Value = cond.RequiredHits > 0 ? cond.RequiredHits.ToString() : ""; row.Cells[9].ReadOnly = readOnlyMode; row.Cells[9].Style.BackColor = editableColor; }
        }
    }
}