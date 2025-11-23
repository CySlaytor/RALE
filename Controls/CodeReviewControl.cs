#nullable enable
using RALogicEditor.Definitions;
using RALogicEditor.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace RALogicEditor.Controls
{
    public partial class CodeReviewControl : UserControl
    {
        public event EventHandler<ValidationIssue>? IssueSelected;

        public CodeReviewControl()
        {
            InitializeComponent();
            InitializeListView();

            // Ensure columns resize when the control size changes
            this.Resize += (s, e) => AdjustColumnWidths();
        }

        private void InitializeListView()
        {
            listIssues.View = View.Details;
            listIssues.FullRowSelect = true;
            listIssues.GridLines = true;
            listIssues.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            listIssues.MultiSelect = false;

            // Initial widths
            listIssues.Columns.Add("Sev", 30);
            listIssues.Columns.Add("Asset", 150); // New Column
            listIssues.Columns.Add("Title", 150);
            listIssues.Columns.Add("Description", 200); // Will autosize
            listIssues.Columns.Add("Ref", 40);

            listIssues.DoubleClick += ListIssues_DoubleClick;
            listIssues.SelectedIndexChanged += ListIssues_SelectedIndexChanged;
        }

        private void AdjustColumnWidths()
        {
            if (listIssues.Columns.Count < 5) return;

            // Calculate available width
            int totalWidth = listIssues.ClientSize.Width;

            // Fixed columns
            int colSev = listIssues.Columns[0].Width;
            int colAsset = listIssues.Columns[1].Width;
            int colTitle = listIssues.Columns[2].Width;
            int colRef = listIssues.Columns[4].Width;

            // Description takes remaining space
            int descWidth = totalWidth - (colSev + colAsset + colTitle + colRef);

            // Ensure minimum readable width
            if (descWidth < 100) descWidth = 100;

            listIssues.Columns[3].Width = descWidth;
        }

        public void SetIssues(List<ValidationIssue> issues)
        {
            listIssues.BeginUpdate();
            listIssues.Items.Clear();

            foreach (var issue in issues)
            {
                var item = new ListViewItem(GetSeverityIcon(issue.Severity));

                // Asset Column: ID - Title (or Global if null)
                string assetStr = issue.Asset != null ? $"{issue.Asset.ID} - {issue.Asset.Title}" : "Global";
                item.SubItems.Add(assetStr);

                item.SubItems.Add(issue.Title);
                item.SubItems.Add(issue.Description);

                if (!string.IsNullOrEmpty(issue.WikiUrl))
                {
                    item.SubItems.Add("Link");
                    item.SubItems[4].ForeColor = Color.Blue;
                    item.SubItems[4].Font = new Font(listIssues.Font, FontStyle.Underline);
                }
                else
                {
                    item.SubItems.Add("");
                }

                item.Tag = issue;

                // Color coding
                if (issue.Severity == FeedbackSeverity.Error) item.ForeColor = Color.Red;
                else if (issue.Severity == FeedbackSeverity.Warning) item.ForeColor = Color.DarkOrange;

                listIssues.Items.Add(item);
            }

            listIssues.EndUpdate();
            UpdateStatus(issues.Count);

            // Force resize after populating
            AdjustColumnWidths();
        }

        private void UpdateStatus(int count)
        {
            lblStatus.Text = count == 0 ? "No issues found." : $"{count} issue(s) found.";
            lblStatus.ForeColor = count == 0 ? Color.Green : Color.Black;
        }

        private string GetSeverityIcon(FeedbackSeverity severity)
        {
            return severity switch
            {
                FeedbackSeverity.Error => "❌",
                FeedbackSeverity.Warning => "⚠️",
                FeedbackSeverity.Info => "ℹ️",
                _ => ""
            };
        }

        private void ListIssues_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (listIssues.SelectedItems.Count > 0)
            {
                var issue = listIssues.SelectedItems[0].Tag as ValidationIssue;
                if (issue != null)
                {
                    IssueSelected?.Invoke(this, issue);
                }
            }
        }

        private void ListIssues_DoubleClick(object? sender, EventArgs e)
        {
            if (listIssues.SelectedItems.Count > 0)
            {
                var issue = listIssues.SelectedItems[0].Tag as ValidationIssue;
                if (issue != null && !string.IsNullOrEmpty(issue.WikiUrl))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(issue.WikiUrl) { UseShellExecute = true });
                    }
                    catch { /* Ignore browser errors */ }
                }
            }
        }
    }
}