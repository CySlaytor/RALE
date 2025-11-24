using RALogicEditor.Models;
using RALogicEditor.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace RALogicEditor.Forms
{
    public partial class LogicExplainerForm : Form
    {
        public LogicExplainerForm()
        {
            InitializeComponent();
        }

        public void DisplayLogic(
            IAssetItem asset,
            List<AchievementConditionGroup> groups,
            Dictionary<string, List<NoteNode>> notesLookup,
            List<(long Start, long End, NoteNode Node)> rangeCache)
        {
            this.Text = $"Logic Explainer - {asset.Title}";
            this.BackColor = Color.FromArgb(30, 30, 30); // Dark form background

            // Ensure RichTextBox matches dark theme
            rtbOutput.BackColor = Color.FromArgb(30, 30, 30);
            rtbOutput.ForeColor = Color.Gainsboro;

            // Generate the explanation text using the service
            string explanation = LogicExplainer.ExplainAsset(asset, groups, notesLookup, rangeCache);

            // Render with formatting
            rtbOutput.Clear();

            // Parse the custom markdown-like syntax from the service
            foreach (var line in explanation.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                if (line.StartsWith("# "))
                {
                    // Main Header (Asset Title) - Light Blue
                    AppendText(line.Substring(2) + "\n", new Font(this.Font.FontFamily, 14, FontStyle.Bold), Color.LightSkyBlue);
                }
                else if (line.StartsWith("## "))
                {
                    // Sub Header (Core/Alt Requirements) - White
                    AppendText(line.Substring(3) + "\n", new Font(this.Font.FontFamily, 11, FontStyle.Bold), Color.White);
                }
                else if (line.StartsWith("### "))
                {
                    // Group Header (Alt 1, Alt 2...) - Light Gray
                    AppendText(line.Substring(4) + "\n", new Font(this.Font.FontFamily, 10, FontStyle.Bold | FontStyle.Underline), Color.LightGray);
                }
                else if (line.StartsWith("- [RESET]"))
                {
                    // Reset - Salmon/Light Red
                    AppendText(line + "\n", this.Font, Color.Salmon);
                }
                else if (line.StartsWith("- [PAUSE]"))
                {
                    // Pause - Cyan
                    AppendText(line + "\n", this.Font, Color.Cyan);
                }
                else if (line.StartsWith("- [MEASURE]"))
                {
                    // Measure - Violet
                    AppendText(line + "\n", this.Font, Color.Violet);
                }
                else if (line.StartsWith("- [TRIGGER]"))
                {
                    // Trigger - Lime Green
                    AppendText(line + "\n", this.Font, Color.LimeGreen);
                }
                else
                {
                    // Standard Logic Line - Off-White (Gainsboro)
                    AppendText(line + "\n", this.Font, Color.Gainsboro);
                }
            }

            // Scroll to top
            rtbOutput.SelectionStart = 0;
            rtbOutput.ScrollToCaret();
        }

        private void AppendText(string text, Font font, Color color)
        {
            rtbOutput.SelectionStart = rtbOutput.TextLength;
            rtbOutput.SelectionLength = 0;
            rtbOutput.SelectionFont = font;
            rtbOutput.SelectionColor = color;
            rtbOutput.AppendText(text);
        }
    }
}