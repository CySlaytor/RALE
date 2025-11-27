namespace RALogicEditor.Forms
{
    partial class LogicOptimizerForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.dgvAssets = new System.Windows.Forms.DataGridView();
            this.panelBottom = new System.Windows.Forms.Panel();
            this.lblInfo = new System.Windows.Forms.Label();
            this.btnHelp = new System.Windows.Forms.Button();
            this.btnPad = new System.Windows.Forms.Button();
            this.btnSelectWarning = new System.Windows.Forms.Button();
            this.btnSelectAll = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.btnOptimize = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.colSelect = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.colID = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTitle = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colLength = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colStatus = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colOptimized = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.dgvAssets)).BeginInit();
            this.panelBottom.SuspendLayout();
            this.SuspendLayout();
            // 
            // dgvAssets
            // 
            this.dgvAssets.AllowUserToAddRows = false;
            this.dgvAssets.AllowUserToDeleteRows = false;
            this.dgvAssets.AllowUserToResizeColumns = false;
            this.dgvAssets.AllowUserToResizeRows = false;
            this.dgvAssets.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvAssets.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colSelect,
            this.colID,
            this.colTitle,
            this.colLength,
            this.colStatus,
            this.colOptimized});
            this.dgvAssets.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvAssets.Location = new System.Drawing.Point(0, 0);
            this.dgvAssets.Name = "dgvAssets";
            this.dgvAssets.RowHeadersVisible = false;
            this.dgvAssets.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvAssets.Size = new System.Drawing.Size(784, 411);
            this.dgvAssets.TabIndex = 0;
            this.dgvAssets.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvAssets_CellContentClick);
            // 
            // panelBottom
            // 
            this.panelBottom.Controls.Add(this.lblInfo);
            this.panelBottom.Controls.Add(this.btnHelp);
            this.panelBottom.Controls.Add(this.btnPad);
            this.panelBottom.Controls.Add(this.btnSelectWarning);
            this.panelBottom.Controls.Add(this.btnSelectAll);
            this.panelBottom.Controls.Add(this.btnClose);
            this.panelBottom.Controls.Add(this.btnOptimize);
            this.panelBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelBottom.Location = new System.Drawing.Point(0, 411);
            this.panelBottom.Name = "panelBottom";
            this.panelBottom.Size = new System.Drawing.Size(784, 50);
            this.panelBottom.TabIndex = 1;
            // 
            // lblInfo
            // 
            this.lblInfo.AutoSize = true;
            this.lblInfo.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblInfo.Location = new System.Drawing.Point(230, 18);
            this.lblInfo.Name = "lblInfo";
            this.lblInfo.Size = new System.Drawing.Size(125, 15);
            this.lblInfo.TabIndex = 6;
            this.lblInfo.Text = "Strip 0x00... to save chars";
            // 
            // btnHelp
            // 
            this.btnHelp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnHelp.Location = new System.Drawing.Point(395, 12);
            this.btnHelp.Name = "btnHelp";
            this.btnHelp.Size = new System.Drawing.Size(30, 26);
            this.btnHelp.TabIndex = 5;
            this.btnHelp.Text = "?";
            this.toolTip1.SetToolTip(this.btnHelp, "View Instructions");
            this.btnHelp.UseVisualStyleBackColor = true;
            this.btnHelp.Click += new System.EventHandler(this.btnHelp_Click);
            // 
            // btnPad
            // 
            this.btnPad.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnPad.Location = new System.Drawing.Point(431, 12);
            this.btnPad.Name = "btnPad";
            this.btnPad.Size = new System.Drawing.Size(95, 26);
            this.btnPad.TabIndex = 4;
            this.btnPad.Text = "Reset / Pad";
            this.toolTip1.SetToolTip(this.btnPad, "Pads hex values to 8 characters (0x00001234)");
            this.btnPad.UseVisualStyleBackColor = true;
            this.btnPad.Click += new System.EventHandler(this.btnPad_Click);
            // 
            // btnSelectWarning
            // 
            this.btnSelectWarning.Location = new System.Drawing.Point(93, 12);
            this.btnSelectWarning.Name = "btnSelectWarning";
            this.btnSelectWarning.Size = new System.Drawing.Size(120, 26);
            this.btnSelectWarning.TabIndex = 3;
            this.btnSelectWarning.Text = "Select Warnings";
            this.toolTip1.SetToolTip(this.btnSelectWarning, "Deselects all, then selects only items exceeding length limits");
            this.btnSelectWarning.UseVisualStyleBackColor = true;
            this.btnSelectWarning.Click += new System.EventHandler(this.btnSelectWarning_Click);
            // 
            // btnSelectAll
            // 
            this.btnSelectAll.Location = new System.Drawing.Point(12, 12);
            this.btnSelectAll.Name = "btnSelectAll";
            this.btnSelectAll.Size = new System.Drawing.Size(75, 26);
            this.btnSelectAll.TabIndex = 2;
            this.btnSelectAll.Text = "Select All";
            this.btnSelectAll.UseVisualStyleBackColor = true;
            this.btnSelectAll.Click += new System.EventHandler(this.btnSelectAll_Click);
            // 
            // btnClose
            // 
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.Location = new System.Drawing.Point(697, 12);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(75, 26);
            this.btnClose.TabIndex = 1;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // btnOptimize
            // 
            this.btnOptimize.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOptimize.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.btnOptimize.Location = new System.Drawing.Point(532, 12);
            this.btnOptimize.Name = "btnOptimize";
            this.btnOptimize.Size = new System.Drawing.Size(140, 26);
            this.btnOptimize.TabIndex = 0;
            this.btnOptimize.Text = "Optimize Selected";
            this.toolTip1.SetToolTip(this.btnOptimize, "Removes leading zeros from addresses and values to save space");
            this.btnOptimize.UseVisualStyleBackColor = true;
            this.btnOptimize.Click += new System.EventHandler(this.btnOptimize_Click);
            // 
            // colSelect
            // 
            this.colSelect.HeaderText = "";
            this.colSelect.Name = "colSelect";
            this.colSelect.Width = 30;
            // 
            // colID
            // 
            this.colID.HeaderText = "ID";
            this.colID.Name = "colID";
            this.colID.ReadOnly = true;
            this.colID.Width = 80;
            // 
            // colTitle
            // 
            this.colTitle.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.colTitle.HeaderText = "Title";
            this.colTitle.Name = "colTitle";
            this.colTitle.ReadOnly = true;
            // 
            // colLength
            // 
            this.colLength.HeaderText = "Length";
            this.colLength.Name = "colLength";
            this.colLength.ReadOnly = true;
            this.colLength.Width = 70;
            // 
            // colStatus
            // 
            this.colStatus.HeaderText = "Status";
            this.colStatus.Name = "colStatus";
            this.colStatus.ReadOnly = true;
            this.colStatus.Width = 80;
            // 
            // colOptimized
            // 
            this.colOptimized.HeaderText = "Stripped?";
            this.colOptimized.Name = "colOptimized";
            this.colOptimized.ReadOnly = true;
            this.colOptimized.Width = 70;
            // 
            // LogicOptimizerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 461);
            this.Controls.Add(this.dgvAssets);
            this.Controls.Add(this.panelBottom);
            this.MinimumSize = new System.Drawing.Size(700, 400);
            this.Name = "LogicOptimizerForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Logic Optimizer";
            ((System.ComponentModel.ISupportInitialize)(this.dgvAssets)).EndInit();
            this.panelBottom.ResumeLayout(false);
            this.panelBottom.PerformLayout();
            this.ResumeLayout(false);

        }

        private System.Windows.Forms.DataGridView dgvAssets;
        private System.Windows.Forms.Panel panelBottom;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Button btnOptimize;
        private System.Windows.Forms.Button btnSelectWarning;
        private System.Windows.Forms.Button btnSelectAll;
        private System.Windows.Forms.Label lblInfo;
        private System.Windows.Forms.Button btnPad;
        private System.Windows.Forms.Button btnHelp;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.DataGridViewCheckBoxColumn colSelect;
        private System.Windows.Forms.DataGridViewTextBoxColumn colID;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTitle;
        private System.Windows.Forms.DataGridViewTextBoxColumn colLength;
        private System.Windows.Forms.DataGridViewTextBoxColumn colStatus;
        private System.Windows.Forms.DataGridViewTextBoxColumn colOptimized;
    }
}