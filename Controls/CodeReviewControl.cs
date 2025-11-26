#nullable enable
using RALogicEditor.Definitions;
using RALogicEditor.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace RALogicEditor.Controls
{
    public partial class CodeReviewControl : UserControl
    {
        public event EventHandler<ValidationIssue>? IssueSelected;

        // Store full list to support filtering
        private List<ValidationIssue> _allIssues = new();

        public CodeReviewControl()
        {
            InitializeComponent();
            InitializeListView();
            EnableDoubleBuffering(listIssues);

            // Ensure columns resize when the control size changes
            this.Resize += (s, e) => AdjustColumnWidths();

            // Filter Setup
            cmbFilter.Items.Clear();
            cmbFilter.Items.Add("Show All Issues");
            cmbFilter.Items.Add("Logic Warnings & Errors");
            cmbFilter.Items.Add("Missing Code Notes");
            cmbFilter.SelectedIndex = 0;
            cmbFilter.SelectedIndexChanged += (s, e) => ApplyFilter();
        }

        private void EnableDoubleBuffering(Control control)
        {
            // Reduces flickering during list updates
            typeof(Control).InvokeMember("DoubleBuffered",
                BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                null, control, new object[] { true });
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
            listIssues.Columns.Add("Asset", 150);
            listIssues.Columns.Add("Title", 150);
            listIssues.Columns.Add("Description", 200); // Will autosize
            listIssues.Columns.Add("Ref", 40);

            // Updated: Only DoubleClick triggers navigation now
            listIssues.DoubleClick += ListIssues_DoubleClick;

            // Handle Enter key for keyboard navigation
            listIssues.KeyDown += ListIssues_KeyDown;
        }

        private void AdjustColumnWidths()
        {
            if (listIssues.Columns.Count < 5) return;

            int totalWidth = listIssues.ClientSize.Width;
            int colSev = listIssues.Columns[0].Width;
            int colAsset = listIssues.Columns[1].Width;
            int colTitle = listIssues.Columns[2].Width;
            int colRef = listIssues.Columns[4].Width;

            int descWidth = totalWidth - (colSev + colAsset + colTitle + colRef);
            if (descWidth < 100) descWidth = 100;

            listIssues.Columns[3].Width = descWidth;
        }

        public void SetIssues(List<ValidationIssue> issues)
        {
            // Store source data
            _allIssues = issues;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            // Preserve selection if possible to prevent scroll jumping
            ValidationIssue? selectedIssue = null;
            if (listIssues.SelectedItems.Count > 0)
                selectedIssue = listIssues.SelectedItems[0].Tag as ValidationIssue;

            listIssues.BeginUpdate();
            listIssues.Items.Clear();

            // Filter Logic
            int filterMode = cmbFilter.SelectedIndex; // 0=All, 1=Logic, 2=Missing

            // Deduplication Set for Missing Notes
            var seenMissingAddresses = new HashSet<string>();

            // Sort Issues Stably: Severity -> Asset ID -> Title
            var sortedIssues = _allIssues.OrderByDescending(i => i.Severity)
                                         .ThenBy(i => i.Asset?.ID ?? 0)
                                         .ThenBy(i => i.Title)
                                         .ToList();

            foreach (var issue in sortedIssues)
            {
                bool isMissingNote = issue.Title == "Undocumented Memory";

                // Filter Mode Check
                if (filterMode == 1 && isMissingNote) continue; // Hide Missing Notes
                if (filterMode == 2 && !isMissingNote) continue; // Hide Logic Issues

                // Deduplication Logic for Missing Notes
                if (isMissingNote)
                {
                    if (seenMissingAddresses.Contains(issue.Description)) continue;
                    seenMissingAddresses.Add(issue.Description);
                }

                var item = new ListViewItem(GetSeverityIcon(issue.Severity));

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

                if (issue.Severity == FeedbackSeverity.Error) item.ForeColor = Color.Red;
                else if (issue.Severity == FeedbackSeverity.Warning) item.ForeColor = Color.DarkOrange;

                listIssues.Items.Add(item);
            }

            // Restore selection if valid
            if (selectedIssue != null)
            {
                foreach (ListViewItem item in listIssues.Items)
                {
                    if (item.Tag == selectedIssue)
                    {
                        item.Selected = true;
                        item.EnsureVisible();
                        break;
                    }
                }
            }

            listIssues.EndUpdate();
            UpdateStatus(listIssues.Items.Count);
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

        private void TriggerIssueNavigation()
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
            // Check if clicking a link column first
            Point mousePos = listIssues.PointToClient(Control.MousePosition);
            ListViewHitTestInfo hit = listIssues.HitTest(mousePos);

            if (hit.Item != null)
            {
                var issue = hit.Item.Tag as ValidationIssue;
                // If clicking the Link column (Index 4), open URL
                // Note: SubItem hit testing varies, but double click usually means "Go To".
                // If specific column check is needed, we can add it, but usually Double Click on row = Go To Asset.
                // If the user wants to open the Wiki, they usually expect a single click on a link or double click row.
                // Let's prioritizing Navigation on Double Click.

                // If there is a URL and we are specifically over the link column (not easy to detect perfectly in standard ListView without OwnerDraw),
                // we just trigger navigation. The user can copy/paste link if needed or we assume navigation is primary action.

                // Actually, checking for URL opening on DoubleClick might conflict.
                // Let's stick to: Double Click = Navigate to Asset in Editor.

                if (issue != null && !string.IsNullOrEmpty(issue.WikiUrl))
                {
                    // Optional: If we want to support Wiki links still?
                    // Maybe Middle Click or Context Menu? 
                    // For now, Double Click triggers the EDITOR navigation as requested.
                }

                TriggerIssueNavigation();
            }
        }

        private void ListIssues_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                TriggerIssueNavigation();
                e.Handled = true;
            }
        }
    }
}