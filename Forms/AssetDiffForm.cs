using RALogicEditor.Definitions;
using RALogicEditor.Models;
using RALogicEditor.Parsers;
using RALogicEditor.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace RALogicEditor.Forms
{
    public partial class AssetDiffForm : Form
    {
        public AssetDiffForm(IAssetItem serverItem, IAssetItem localItem)
        {
            InitializeComponent();
            ApplyTheme();

            this.Text = $"Diff Viewer - {localItem.Title} (ID: {localItem.ID})";

            // 1. Convert raw logic strings into human-readable lists of lines
            var serverLines = GetReadableLines(serverItem.Logic, serverItem.IsLeaderboard);
            var localLines = GetReadableLines(localItem.Logic, localItem.IsLeaderboard);

            // 2. Align lists (insert blanks to keep them side-by-side)
            AlignDiffLists(ref serverLines, ref localLines);

            // 3. Perform Diff and Render
            HighlightDifferences(serverLines, localLines);

            UpdateDiffStats(serverLines, localLines);
        }

        private void ApplyTheme()
        {
            // Auto-detect dark mode from system color or assume standard dark gray
            Color backColor = SystemColors.Control;
            Color foreColor = SystemColors.ControlText;

            // If window color is dark, assume dark theme
            if (SystemColors.Window.R < 128)
            {
                backColor = Color.FromArgb(30, 30, 30);
                foreColor = Color.WhiteSmoke;
            }

            this.BackColor = backColor;
            this.ForeColor = foreColor;

            rtbServer.BackColor = backColor;
            rtbServer.ForeColor = foreColor;
            rtbLocal.BackColor = backColor;
            rtbLocal.ForeColor = foreColor;

            lblServer.ForeColor = foreColor;
            lblLocal.ForeColor = foreColor;
            lblStats.ForeColor = Color.Gray;
        }

        private List<string> GetReadableLines(string rawLogic, bool isLeaderboard)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(rawLogic))
            {
                lines.Add("(Empty)");
                return lines;
            }

            // Use the Parser to get the object model
            var groups = AchievementParser.ParseAchievementTrigger(rawLogic, 'S', isLeaderboard);

            foreach (var group in groups)
            {
                // Add Group Header
                lines.Add($"[{group.GroupName}]");

                foreach (var cond in group.Conditions)
                {
                    lines.Add(ConditionToAlignedString(cond));
                }

                // visual spacer between groups
                lines.Add("");
            }

            return lines;
        }

        // Format with fixed widths: Flag(14) Type(6) Size(9) Value(12) Op(3) Type(6) Size(9) Value(12) Hits
        private string ConditionToAlignedString(AchievementCondition cond)
        {
            StringBuilder sb = new StringBuilder();

            // 1. Flag
            string flag = string.IsNullOrEmpty(cond.Flag) ? "" : cond.Flag + ":";
            sb.Append(flag.PadRight(14));

            // 2. Left Operand
            sb.Append(OperandToAligned(cond.LeftOperand));

            // 3. Operator
            string op = string.IsNullOrEmpty(cond.Operator) ? "" : cond.Operator;
            sb.Append("  ").Append(op.PadRight(3));

            // 4. Right Operand
            if (!string.IsNullOrEmpty(cond.Operator))
            {
                sb.Append(" ").Append(OperandToAligned(cond.RightOperand));
            }
            else
            {
                // Pad empty right side to maintain alignment for Hits
                sb.Append(" ".PadRight(28)); // 6+1+9+1+11 approx
            }

            // 5. Hits
            if (cond.RequiredHits > 0)
            {
                sb.Append($" ({cond.RequiredHits})");
            }

            return sb.ToString();
        }

        private string OperandToAligned(Operand op)
        {
            if (op.Type == "Recall") return "{Result}".PadRight(27);

            // Type
            string type = op.Type.PadRight(6);

            // Size
            string size = string.IsNullOrEmpty(op.Size) ? "" : op.Size;
            size = size.PadRight(9);

            // Value
            string val = op.Value;
            if (val.Length > 12) val = val.Substring(0, 10) + ".."; // Truncate huge floats
            val = val.PadRight(12);

            return $"{type} {size} {val}";
        }

        private void AlignDiffLists(ref List<string> server, ref List<string> local)
        {
            // Simple greedy alignment algorithm
            // If line A != line B, check if matching line appears later.
            // Insert blank lines to align matching blocks.

            int i = 0;
            int j = 0;
            var newServer = new List<string>();
            var newLocal = new List<string>();

            while (i < server.Count || j < local.Count)
            {
                // Fix: Changed type to string? to allow null assignment without warning
                string? sLine = (i < server.Count) ? server[i] : null;
                string? lLine = (j < local.Count) ? local[j] : null;

                if (sLine == lLine)
                {
                    if (sLine != null)
                    {
                        newServer.Add(sLine);
                        // Fix: Added '!' because we know lLine is not null if sLine is not null and they are equal
                        newLocal.Add(lLine!);
                        i++; j++;
                    }
                    else break;
                }
                else
                {
                    // Mismatch. Look ahead.
                    // Does sLine appear in local later?
                    int idxInLocal = (sLine != null) ? local.IndexOf(sLine, j) : -1;
                    // Does lLine appear in server later?
                    int idxInServer = (lLine != null) ? server.IndexOf(lLine!, i) : -1;

                    if (idxInLocal != -1 && (idxInServer == -1 || idxInLocal - j < idxInServer - i))
                    {
                        // Local inserted lines before the match
                        while (j < idxInLocal)
                        {
                            newServer.Add(""); // Spacer
                            newLocal.Add(local[j]);
                            j++;
                        }
                    }
                    else if (idxInServer != -1)
                    {
                        // Server had extra lines here (deleted in local)
                        while (i < idxInServer)
                        {
                            newServer.Add(server[i]);
                            newLocal.Add(""); // Spacer
                            i++;
                        }
                    }
                    else
                    {
                        // Both different, just add matching row
                        newServer.Add(sLine ?? "");
                        newLocal.Add(lLine ?? "");
                        i++; j++;
                    }
                }
            }
            server = newServer;
            local = newLocal;
        }

        private void HighlightDifferences(List<string> serverLines, List<string> localLines)
        {
            // Dark Theme colors
            Color addedBack = Color.FromArgb(0, 50, 0); // Dark Green
            Color removedBack = Color.FromArgb(50, 0, 0); // Dark Red
            Color metaColor = Color.Gray;

            // If light theme active (by check), swap colors
            if (this.BackColor.R > 200)
            {
                addedBack = Color.LightGreen;
                removedBack = Color.LightPink;
            }

            RenderBox(rtbServer, serverLines, localLines, removedBack, metaColor, true);
            RenderBox(rtbLocal, localLines, serverLines, addedBack, metaColor, false);
        }

        private void RenderBox(RichTextBox box, List<string> sourceLines, List<string> compareLines, Color diffBack, Color metaColor, bool isServer)
        {
            box.Text = "";
            for (int i = 0; i < sourceLines.Count; i++)
            {
                string line = sourceLines[i];
                string compareLine = (i < compareLines.Count) ? compareLines[i] : "";

                box.SelectionStart = box.TextLength;
                box.SelectionLength = 0;

                bool isEmpty = string.IsNullOrEmpty(line);
                bool isMeta = line.StartsWith("[");
                bool isSame = line == compareLine;

                // Coloring Logic
                if (isMeta)
                {
                    box.SelectionColor = metaColor;
                }
                else if (isEmpty)
                {
                    // Just a spacer line
                }
                else if (!isSame)
                {
                    box.SelectionBackColor = diffBack;
                    // Server side shows red text for removed lines? No, standard white on red back is fine.
                }

                box.AppendText(line + "\n");

                // Reset styles
                box.SelectionBackColor = box.BackColor;
                box.SelectionColor = box.ForeColor;
            }
        }

        private void UpdateDiffStats(List<string> serverLines, List<string> localLines)
        {
            int added = 0;
            int removed = 0;

            for (int i = 0; i < serverLines.Count; i++)
            {
                string s = serverLines[i];
                string l = localLines[i];

                if (s != l)
                {
                    if (!string.IsNullOrEmpty(l) && string.IsNullOrEmpty(s)) added++; // Spacer in server -> Added in local
                    if (!string.IsNullOrEmpty(s) && string.IsNullOrEmpty(l)) removed++; // Spacer in local -> Removed from server
                    if (!string.IsNullOrEmpty(s) && !string.IsNullOrEmpty(l)) { added++; removed++; } // Modification counts as both
                }
            }

            lblStats.Text = $"Changes: +{added} / -{removed} lines";
        }
    }
}