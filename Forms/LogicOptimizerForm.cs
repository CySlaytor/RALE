#nullable enable
using RALogicEditor.Models;
using RALogicEditor.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace RALogicEditor.Forms
{
    public partial class LogicOptimizerForm : Form
    {
        private List<IAssetItem> _assets;
        private LogicOptimizer _optimizer = new LogicOptimizer();

        public LogicOptimizerForm(List<IAssetItem> assets)
        {
            InitializeComponent();
            _assets = assets;
            ApplyTheme();
            PopulateGrid();

            // Initial button state check
            UpdateSelectAllButtonState();
        }

        private void ApplyTheme()
        {
            // Basic dark mode detection for grid style
            if (SystemColors.Window.R < 128)
            {
                this.BackColor = Color.FromArgb(45, 45, 48);
                this.ForeColor = Color.White;
                dgvAssets.BackgroundColor = Color.FromArgb(30, 30, 30);
                dgvAssets.DefaultCellStyle.BackColor = Color.FromArgb(30, 30, 30);
                dgvAssets.DefaultCellStyle.ForeColor = Color.White;
                dgvAssets.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 45, 48);
                dgvAssets.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
                dgvAssets.GridColor = Color.FromArgb(60, 60, 60);
                dgvAssets.EnableHeadersVisualStyles = false;
            }
        }

        private void PopulateGrid()
        {
            dgvAssets.Rows.Clear();
            foreach (var asset in _assets)
            {
                UpdateRowForAsset(asset);
            }
        }

        private void UpdateRowForAsset(IAssetItem asset)
        {
            int len = asset.Logic.Length;
            string status = "OK";
            Color statusColor = Color.Green;

            if (len > 65535)
            {
                status = "EXCEEDED";
                statusColor = Color.Red;
            }
            else if (len > 60000)
            {
                status = "Warning";
                statusColor = Color.Orange;
            }

            bool isStripped = _optimizer.IsFullyOptimized(asset);
            string strippedStatus = isStripped ? "Yes" : "No";
            Color strippedColor = isStripped ? Color.Green : Color.LightGray;

            // Check if row exists to update, else add
            DataGridViewRow? row = null;
            foreach (DataGridViewRow r in dgvAssets.Rows)
            {
                if (r.Tag == asset) { row = r; break; }
            }

            if (row == null)
            {
                int idx = dgvAssets.Rows.Add(false, asset.ID, asset.Title, len, status, strippedStatus);
                row = dgvAssets.Rows[idx];
                row.Tag = asset;
            }
            else
            {
                row.Cells[3].Value = len;
                row.Cells[4].Value = status;
                row.Cells[5].Value = strippedStatus;
            }

            row.Cells[4].Style.ForeColor = statusColor;
            row.Cells[5].Style.ForeColor = strippedColor;
        }

        private void btnSelectAll_Click(object sender, EventArgs e)
        {
            bool allSelected = AreAllRowsSelected();
            bool newState = !allSelected; // Toggle

            foreach (DataGridViewRow row in dgvAssets.Rows)
            {
                row.Cells[0].Value = newState;
            }
            UpdateSelectAllButtonState();
        }

        private void UpdateSelectAllButtonState()
        {
            if (AreAllRowsSelected()) btnSelectAll.Text = "Deselect All";
            else btnSelectAll.Text = "Select All";
        }

        private bool AreAllRowsSelected()
        {
            if (dgvAssets.Rows.Count == 0) return false;
            foreach (DataGridViewRow row in dgvAssets.Rows)
            {
                if (Convert.ToBoolean(row.Cells[0].Value) == false) return false;
            }
            return true;
        }

        // Grid click to update button state
        private void dgvAssets_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == 0)
            {
                // Must commit edit to see value change immediately
                dgvAssets.CommitEdit(DataGridViewDataErrorContexts.Commit);
                UpdateSelectAllButtonState();
            }
        }

        private void btnSelectWarning_Click(object sender, EventArgs e)
        {
            // First deselect everything
            foreach (DataGridViewRow row in dgvAssets.Rows)
                row.Cells[0].Value = false;

            // Then select only warnings
            int count = 0;
            foreach (DataGridViewRow row in dgvAssets.Rows)
            {
                string s = row.Cells[4].Value?.ToString() ?? "";
                if (s == "EXCEEDED" || s == "Warning")
                {
                    row.Cells[0].Value = true;
                    count++;
                }
            }
            UpdateSelectAllButtonState();
            MessageBox.Show($"Selected {count} items with warnings.", "Select Warnings", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnOptimize_Click(object sender, EventArgs e)
        {
            ProcessSelectedAssets((asset) => _optimizer.OptimizeAsset(asset), "Optimization");
        }

        private void btnPad_Click(object sender, EventArgs e)
        {
            ProcessSelectedAssets((asset) => _optimizer.PadAsset(asset), "Padding");
        }

        private void ProcessSelectedAssets(Func<IAssetItem, int> action, string actionName)
        {
            int totalChanges = 0;
            int assetsChanged = 0;

            foreach (DataGridViewRow row in dgvAssets.Rows)
            {
                if (Convert.ToBoolean(row.Cells[0].Value) == true && row.Tag is IAssetItem asset)
                {
                    int changes = action(asset);
                    if (changes > 0)
                    {
                        totalChanges += changes;
                        assetsChanged++;
                        UpdateRowForAsset(asset); // Refresh view
                    }
                }
            }

            MessageBox.Show($"{actionName} applied to {assetsChanged} assets ({totalChanges} modifications).", "Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnHelp_Click(object sender, EventArgs e)
        {
            string msg =
                "LOGIC OPTIMIZER INSTRUCTIONS:\n\n" +
                "1. LENGTH REDUCTION:\n" +
                "   The database has a limit of 65,535 characters. Large scripts may fail to upload.\n" +
                "   Click 'Optimize Selected' to remove leading zeros from addresses and values.\n" +
                "   Example: 0x00001234 -> 0x1234 (Saves 4 chars).\n\n" +
                "2. RESET / PAD:\n" +
                "   If you want to standardize format or fix readability, click 'Reset / Pad'.\n" +
                "   This pads all hex values to 8 characters (lowercase).\n" +
                "   Example: 0x1234 -> 0x00001234.\n\n" +
                "3. STATUS COLUMNS:\n" +
                "   - Length: Shows current character count. Red means over limit.\n" +
                "   - Stripped?: 'Yes' means all values are minimal. 'No' means leading zeros exist.";

            MessageBox.Show(msg, "Logic Optimizer Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}