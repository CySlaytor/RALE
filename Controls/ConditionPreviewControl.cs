using RALogicEditor.Definitions;
using RALogicEditor.Helpers;
using RALogicEditor.Models;
using RALogicEditor.Utilities;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace RALogicEditor.Controls
{
    public partial class ConditionPreviewControl : UserControl
    {
        public ConditionPreviewControl()
        {
            InitializeComponent();
        }

        public void UpdatePreview(
            AchievementCondition cond,
            AchievementConditionGroup group,
            int rowIndex,
            Dictionary<string, List<NoteNode>> notesLookup,
            List<(long Start, long End, NoteNode Node)> rangeCache)
        {
            rtbPreview.Clear();

            if (cond == null)
            {
                AppendText("Select a condition to preview.", Color.Gray);
                return;
            }

            // -- Render Flag --
            RenderFlag(cond.Flag);

            // -- Render Left Operand --
            RenderOperand(cond.LeftOperand, group, rowIndex, notesLookup, rangeCache);

            // -- Render Operator --
            if (!string.IsNullOrEmpty(cond.Operator))
            {
                AppendText($" {GetOperatorText(cond.Operator)} ", Color.Black);

                // -- Render Right Operand --
                RenderOperand(cond.RightOperand, group, rowIndex, notesLookup, rangeCache);
            }

            // -- Render Hits --
            if (cond.RequiredHits > 0)
            {
                AppendText($" (occurs {cond.RequiredHits} times)", Color.DarkSlateBlue, true);
            }
        }

        private void RenderFlag(string flag)
        {
            if (string.IsNullOrEmpty(flag) || flag == RaLogicSyntax.NO_FLAG_TEXT) return;

            Color color = Color.Black;
            string text = flag;

            if (flag == "Reset If") { color = Color.Red; text = "Reset Trigger If"; }
            else if (flag == "Pause If") { color = Color.Blue; text = "Pause Logic If"; }
            else if (flag == "Add Source") { color = Color.Green; text = "Add Source +"; }
            else if (flag == "Sub Source") { color = Color.Green; text = "Subtract Source -"; }
            else if (flag == "Add Hits") { color = Color.Purple; text = "Add Hits From"; }
            else if (flag == "Add Address") { color = Color.DarkOrange; text = "Pointer Offset"; }
            else if (flag == "Measured") { color = Color.DarkMagenta; text = "Measure"; }
            else if (flag == "MeasuredIf") { color = Color.DarkMagenta; text = "Only Measure When"; }

            AppendText(text + " ", color, true);
        }

        private void RenderOperand(
            Operand op,
            AchievementConditionGroup group,
            int rowIndex,
            Dictionary<string, List<NoteNode>> notesLookup,
            List<(long Start, long End, NoteNode Node)> rangeCache)
        {
            if (op.Type == "Value")
            {
                AppendText(op.Value, Color.DarkBlue, true);
            }
            else if (op.Type == "Float")
            {
                AppendText(op.Value + "f", Color.DarkBlue, true);
            }
            else if (op.Type == "Recall")
            {
                AppendText("{Result}", Color.Teal, true);
            }
            else
            {
                // Memory Type
                string prefix = "";
                if (op.Type == "Delta") prefix = "(Previous Frame) ";
                else if (op.Type == "Prior") prefix = "(Prior Frame) ";
                else if (op.Type == "BCD") prefix = "(BCD Decoded) ";
                else if (op.Type == "Inverted") prefix = "(Inverted bits) ";

                if (prefix != "") AppendText(prefix, Color.DimGray);

                // Attempt to resolve Alias
                string addr = LogicFormatter.NormalizeAddress(op.Value);
                string alias = ConditionFormatter.ResolveAlias(addr, group, rowIndex, notesLookup, rangeCache);

                if (!string.IsNullOrEmpty(alias))
                {
                    AppendText($"[{alias}]", Color.Black, true);
                    AppendText($" ({op.Value})", Color.Gray, false); // Show hex in gray
                }
                else
                {
                    AppendText($"Memory {op.Value}", Color.Black, false);
                }

                if (!string.IsNullOrEmpty(op.Size))
                {
                    AppendText($" [{op.Size}]", Color.Gray, false);
                }
            }
        }

        private string GetOperatorText(string op)
        {
            return op switch
            {
                "=" => "is equal to",
                "!=" => "is not equal to",
                "<" => "is less than",
                "<=" => "is less than or equal to",
                ">" => "is greater than",
                ">=" => "is greater than or equal to",
                "*" => "multiplied by",
                "/" => "divided by",
                "+" => "plus",
                "-" => "minus",
                "&" => "bitwise AND",
                "^" => "bitwise XOR",
                _ => op
            };
        }

        private void AppendText(string text, Color color, bool bold = false)
        {
            rtbPreview.SelectionStart = rtbPreview.TextLength;
            rtbPreview.SelectionLength = 0;
            rtbPreview.SelectionColor = color;
            rtbPreview.SelectionFont = new Font(rtbPreview.Font, bold ? FontStyle.Bold : FontStyle.Regular);
            rtbPreview.AppendText(text);
            rtbPreview.SelectionColor = rtbPreview.ForeColor; // Reset
        }
    }
}