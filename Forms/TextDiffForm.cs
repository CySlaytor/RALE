#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace RALogicEditor.Forms
{
    public partial class TextDiffForm : Form
    {
        public TextDiffForm(string title, string leftText, string rightText)
        {
            InitializeComponent();
            this.Text = title;
            ApplyTheme();

            // Normalize split to ensure clean lists
            var leftLines = new List<string>(leftText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None));
            var rightLines = new List<string>(rightText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None));

            AlignDiffLists(ref leftLines, ref rightLines);
            HighlightDifferences(leftLines, rightLines);
            UpdateDiffStats(leftLines, rightLines);
        }

        private void ApplyTheme()
        {
            if (SystemColors.Window.R < 128)
            {
                this.BackColor = Color.FromArgb(45, 45, 48);
                this.ForeColor = Color.WhiteSmoke;

                rtbLeft.BackColor = Color.FromArgb(30, 30, 30);
                rtbLeft.ForeColor = Color.WhiteSmoke;
                rtbRight.BackColor = Color.FromArgb(30, 30, 30);
                rtbRight.ForeColor = Color.WhiteSmoke;

                lblStats.ForeColor = Color.Gray;
                lblLeft.ForeColor = Color.WhiteSmoke;
                lblRight.ForeColor = Color.WhiteSmoke;
            }
        }

        private void AlignDiffLists(ref List<string> left, ref List<string> right)
        {
            // Simple greedy alignment algorithm
            // If line A != line B, check if matching line appears later.
            // Insert blank lines to align matching blocks.

            int i = 0;
            int j = 0;
            var newLeft = new List<string>();
            var newRight = new List<string>();

            while (i < left.Count || j < right.Count)
            {
                string? lLine = (i < left.Count) ? left[i] : null;
                string? rLine = (j < right.Count) ? right[j] : null;

                if (lLine == rLine)
                {
                    if (lLine != null)
                    {
                        newLeft.Add(lLine);
                        newRight.Add(rLine!);
                        i++; j++;
                    }
                    else break;
                }
                else
                {
                    // IMPROVEMENT: Check for Similarity before looking ahead.
                    // If lines are "Similar" (e.g. same display text but different logic), align them immediately.
                    // This forces the "Word Diff" logic to run on them, highlighting specific address changes
                    // instead of showing a full red line vs full green line.
                    if (IsSimilar(lLine, rLine))
                    {
                        newLeft.Add(lLine ?? "");
                        newRight.Add(rLine ?? "");
                        i++; j++;
                        continue;
                    }

                    // Standard Diff Lookahead
                    // Ensure start index is within bounds before calling IndexOf
                    int idxInRight = (lLine != null && j < right.Count) ? right.IndexOf(lLine, j) : -1;
                    int idxInLeft = (rLine != null && i < left.Count) ? left.IndexOf(rLine!, i) : -1;

                    if (idxInRight != -1 && (idxInLeft == -1 || idxInRight - j < idxInLeft - i))
                    {
                        // Right inserted lines
                        while (j < idxInRight)
                        {
                            newLeft.Add("");
                            newRight.Add(right[j]);
                            j++;
                        }
                    }
                    else if (idxInLeft != -1)
                    {
                        // Left had extra lines
                        while (i < idxInLeft)
                        {
                            newLeft.Add(left[i]);
                            newRight.Add("");
                            i++;
                        }
                    }
                    else
                    {
                        // Both different and not similar - Treat as Block Replace
                        newLeft.Add(lLine ?? "");
                        newRight.Add(rLine ?? "");
                        i++; j++;
                    }
                }
            }
            left = newLeft;
            right = newRight;
        }

        // Heuristic to detect if two lines are "versions of the same line"
        private bool IsSimilar(string? left, string? right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right)) return false;

            // Strategy for Rich Presence: Match the Display Text (after the last '?')
            string GetDisplay(string s)
            {
                int idx = s.LastIndexOf('?');
                if (idx >= 0 && idx < s.Length - 1) return s.Substring(idx + 1);
                return s;
            }

            string txtL = GetDisplay(left);
            string txtR = GetDisplay(right);

            // If the display text matches and is substantial (>2 chars), treat as aligned
            if (txtL.Length > 2 && txtL == txtR) return true;

            return false;
        }

        private void HighlightDifferences(List<string> leftLines, List<string> rightLines)
        {
            Color diffBack = Color.FromArgb(50, 0, 0); // Dark Red background for changed line
            Color addedBack = Color.FromArgb(0, 50, 0); // Dark Green background

            // Text Highlight Colors (Brighter foregrounds for specific changes)
            Color textRemove = Color.FromArgb(255, 100, 100);
            Color textAdd = Color.FromArgb(100, 255, 100);

            if (this.BackColor.R > 200)
            {
                diffBack = Color.LightPink;
                addedBack = Color.LightGreen;
                textRemove = Color.Red;
                textAdd = Color.Green;
            }

            RenderBoxWithDiff(rtbLeft, rtbRight, leftLines, rightLines, diffBack, addedBack, textRemove, textAdd);
        }

        private void RenderBoxWithDiff(
            RichTextBox boxL, RichTextBox boxR,
            List<string> leftLines, List<string> rightLines,
            Color colRemove, Color colAdd,
            Color txtRemove, Color txtAdd)
        {
            boxL.Clear();
            boxR.Clear();

            for (int i = 0; i < leftLines.Count; i++)
            {
                string left = leftLines[i];
                string right = rightLines[i];

                if (left == right)
                {
                    AppendText(boxL, left + "\n", boxL.ForeColor, boxL.BackColor);
                    AppendText(boxR, right + "\n", boxR.ForeColor, boxR.BackColor);
                }
                else
                {
                    // Empty lines means strict addition/deletion of the whole line
                    if (string.IsNullOrEmpty(left))
                    {
                        AppendText(boxL, "\n", boxL.ForeColor, boxL.BackColor); // Spacer
                        AppendText(boxR, right + "\n", boxR.ForeColor, colAdd);
                    }
                    else if (string.IsNullOrEmpty(right))
                    {
                        AppendText(boxL, left + "\n", boxL.ForeColor, colRemove);
                        AppendText(boxR, "\n", boxR.ForeColor, boxR.BackColor); // Spacer
                    }
                    else
                    {
                        // Content Diff (Intra-line)
                        var diffs = ComputeWordDiff(left, right);

                        // Render Left
                        boxL.SelectionStart = boxL.TextLength;
                        boxL.SelectionLength = 0;
                        boxL.SelectionBackColor = colRemove;
                        foreach (var d in diffs.LeftTokens)
                        {
                            boxL.SelectionColor = d.IsMatch ? boxL.ForeColor : txtRemove;
                            boxL.SelectionFont = new Font(boxL.Font, d.IsMatch ? FontStyle.Regular : FontStyle.Bold);
                            boxL.AppendText(d.Text);
                        }
                        boxL.AppendText("\n");

                        // Render Right
                        boxR.SelectionStart = boxR.TextLength;
                        boxR.SelectionLength = 0;
                        boxR.SelectionBackColor = colAdd;
                        foreach (var d in diffs.RightTokens)
                        {
                            boxR.SelectionColor = d.IsMatch ? boxR.ForeColor : txtAdd;
                            boxR.SelectionFont = new Font(boxR.Font, d.IsMatch ? FontStyle.Regular : FontStyle.Bold);
                            boxR.AppendText(d.Text);
                        }
                        boxR.AppendText("\n");
                    }
                }
            }
        }

        private void AppendText(RichTextBox box, string text, Color fore, Color back)
        {
            box.SelectionStart = box.TextLength;
            box.SelectionLength = 0;
            box.SelectionColor = fore;
            box.SelectionBackColor = back;
            box.AppendText(text);
        }

        private void UpdateDiffStats(List<string> leftLines, List<string> rightLines)
        {
            int added = 0;
            int removed = 0;
            for (int i = 0; i < leftLines.Count; i++)
            {
                if (leftLines[i] != rightLines[i])
                {
                    if (string.IsNullOrEmpty(leftLines[i])) added++;
                    else if (string.IsNullOrEmpty(rightLines[i])) removed++;
                    else { added++; removed++; }
                }
            }
            lblStats.Text = $"Changes: +{added} / -{removed} lines";
        }

        // --- Basic Word Diff Algorithm ---
        private class DiffResult { public List<DiffToken> LeftTokens = new(); public List<DiffToken> RightTokens = new(); }

        // Added default empty string initialization to avoid CS8618 warning
        private class DiffToken { public string Text = ""; public bool IsMatch; }

        private DiffResult ComputeWordDiff(string left, string right)
        {
            // Tokenize by splitting but keeping delimiters to reconstruct accurately.
            // \w includes alphanumeric. 0x1234 is a word.
            string pattern = @"([^\w]+)";
            var lTokens = Regex.Split(left, pattern).Where(s => !string.IsNullOrEmpty(s)).ToList();
            var rTokens = Regex.Split(right, pattern).Where(s => !string.IsNullOrEmpty(s)).ToList();

            // LCS implementation
            int[,] matrix = new int[lTokens.Count + 1, rTokens.Count + 1];

            for (int i = 1; i <= lTokens.Count; i++)
                for (int j = 1; j <= rTokens.Count; j++)
                    if (lTokens[i - 1] == rTokens[j - 1])
                        matrix[i, j] = matrix[i - 1, j - 1] + 1;
                    else
                        matrix[i, j] = Math.Max(matrix[i - 1, j], matrix[i, j - 1]);

            var res = new DiffResult();
            var matchedIndicesL = new HashSet<int>();
            var matchedIndicesR = new HashSet<int>();

            int x = lTokens.Count, y = rTokens.Count;
            while (x > 0 && y > 0)
            {
                if (lTokens[x - 1] == rTokens[y - 1])
                {
                    matchedIndicesL.Add(x - 1);
                    matchedIndicesR.Add(y - 1);
                    x--; y--;
                }
                else if (matrix[x - 1, y] > matrix[x, y - 1]) x--;
                else y--;
            }

            for (int i = 0; i < lTokens.Count; i++)
                res.LeftTokens.Add(new DiffToken { Text = lTokens[i], IsMatch = matchedIndicesL.Contains(i) });

            for (int i = 0; i < rTokens.Count; i++)
                res.RightTokens.Add(new DiffToken { Text = rTokens[i], IsMatch = matchedIndicesR.Contains(i) });

            return res;
        }
    }
}