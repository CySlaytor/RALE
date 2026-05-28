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
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LogicOptimizerForm));
            dgvAssets = new DataGridView();
            colSelect = new DataGridViewCheckBoxColumn();
            colID = new DataGridViewTextBoxColumn();
            colTitle = new DataGridViewTextBoxColumn();
            colLength = new DataGridViewTextBoxColumn();
            colStatus = new DataGridViewTextBoxColumn();
            colOptimized = new DataGridViewTextBoxColumn();
            panelBottom = new Panel();
            lblInfo = new Label();
            btnHelp = new Button();
            btnPad = new Button();
            btnSelectWarning = new Button();
            btnSelectAll = new Button();
            btnClose = new Button();
            btnOptimize = new Button();
            toolTip1 = new ToolTip(components);
            ((System.ComponentModel.ISupportInitialize)dgvAssets).BeginInit();
            panelBottom.SuspendLayout();
            SuspendLayout();
            // 
            // dgvAssets
            // 
            dgvAssets.AllowUserToAddRows = false;
            dgvAssets.AllowUserToDeleteRows = false;
            dgvAssets.AllowUserToResizeColumns = false;
            dgvAssets.AllowUserToResizeRows = false;
            dgvAssets.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvAssets.Columns.AddRange(new DataGridViewColumn[] { colSelect, colID, colTitle, colLength, colStatus, colOptimized });
            dgvAssets.Dock = DockStyle.Fill;
            dgvAssets.Location = new Point(0, 0);
            dgvAssets.Name = "dgvAssets";
            dgvAssets.RowHeadersVisible = false;
            dgvAssets.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvAssets.Size = new Size(784, 411);
            dgvAssets.TabIndex = 0;
            dgvAssets.CellContentClick += dgvAssets_CellContentClick;
            // 
            // colSelect
            // 
            colSelect.HeaderText = "";
            colSelect.Name = "colSelect";
            colSelect.Width = 30;
            // 
            // colID
            // 
            colID.HeaderText = "ID";
            colID.Name = "colID";
            colID.ReadOnly = true;
            colID.Width = 80;
            // 
            // colTitle
            // 
            colTitle.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colTitle.HeaderText = "Title";
            colTitle.Name = "colTitle";
            colTitle.ReadOnly = true;
            // 
            // colLength
            // 
            colLength.HeaderText = "Length";
            colLength.Name = "colLength";
            colLength.ReadOnly = true;
            colLength.Width = 70;
            // 
            // colStatus
            // 
            colStatus.HeaderText = "Status";
            colStatus.Name = "colStatus";
            colStatus.ReadOnly = true;
            colStatus.Width = 80;
            // 
            // colOptimized
            // 
            colOptimized.HeaderText = "Stripped?";
            colOptimized.Name = "colOptimized";
            colOptimized.ReadOnly = true;
            colOptimized.Width = 70;
            // 
            // panelBottom
            // 
            panelBottom.Controls.Add(lblInfo);
            panelBottom.Controls.Add(btnHelp);
            panelBottom.Controls.Add(btnPad);
            panelBottom.Controls.Add(btnSelectWarning);
            panelBottom.Controls.Add(btnSelectAll);
            panelBottom.Controls.Add(btnClose);
            panelBottom.Controls.Add(btnOptimize);
            panelBottom.Dock = DockStyle.Bottom;
            panelBottom.Location = new Point(0, 411);
            panelBottom.Name = "panelBottom";
            panelBottom.Size = new Size(784, 50);
            panelBottom.TabIndex = 1;
            // 
            // lblInfo
            // 
            lblInfo.AutoSize = true;
            lblInfo.ForeColor = SystemColors.GrayText;
            lblInfo.Location = new Point(230, 18);
            lblInfo.Name = "lblInfo";
            lblInfo.Size = new Size(138, 15);
            lblInfo.TabIndex = 6;
            lblInfo.Text = "Strip 0x00... to save chars";
            // 
            // btnHelp
            // 
            btnHelp.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnHelp.Location = new Point(395, 12);
            btnHelp.Name = "btnHelp";
            btnHelp.Size = new Size(30, 26);
            btnHelp.TabIndex = 5;
            btnHelp.Text = "?";
            toolTip1.SetToolTip(btnHelp, "View Instructions");
            btnHelp.UseVisualStyleBackColor = true;
            btnHelp.Click += btnHelp_Click;
            // 
            // btnPad
            // 
            btnPad.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnPad.Location = new Point(431, 12);
            btnPad.Name = "btnPad";
            btnPad.Size = new Size(95, 26);
            btnPad.TabIndex = 4;
            btnPad.Text = "Reset / Pad";
            toolTip1.SetToolTip(btnPad, "Pads hex values to 8 characters (0x00001234)");
            btnPad.UseVisualStyleBackColor = true;
            btnPad.Click += btnPad_Click;
            // 
            // btnSelectWarning
            // 
            btnSelectWarning.Location = new Point(93, 12);
            btnSelectWarning.Name = "btnSelectWarning";
            btnSelectWarning.Size = new Size(120, 26);
            btnSelectWarning.TabIndex = 3;
            btnSelectWarning.Text = "Select Warnings";
            toolTip1.SetToolTip(btnSelectWarning, "Deselects all, then selects only items exceeding length limits");
            btnSelectWarning.UseVisualStyleBackColor = true;
            btnSelectWarning.Click += btnSelectWarning_Click;
            // 
            // btnSelectAll
            // 
            btnSelectAll.Location = new Point(12, 12);
            btnSelectAll.Name = "btnSelectAll";
            btnSelectAll.Size = new Size(75, 26);
            btnSelectAll.TabIndex = 2;
            btnSelectAll.Text = "Select All";
            btnSelectAll.UseVisualStyleBackColor = true;
            btnSelectAll.Click += btnSelectAll_Click;
            // 
            // btnClose
            // 
            btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.Location = new Point(697, 12);
            btnClose.Name = "btnClose";
            btnClose.Size = new Size(75, 26);
            btnClose.TabIndex = 1;
            btnClose.Text = "Close";
            btnClose.UseVisualStyleBackColor = true;
            btnClose.Click += btnClose_Click;
            // 
            // btnOptimize
            // 
            btnOptimize.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnOptimize.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnOptimize.Location = new Point(532, 12);
            btnOptimize.Name = "btnOptimize";
            btnOptimize.Size = new Size(140, 26);
            btnOptimize.TabIndex = 0;
            btnOptimize.Text = "Optimize Selected";
            toolTip1.SetToolTip(btnOptimize, "Removes leading zeros from addresses and values to save space");
            btnOptimize.UseVisualStyleBackColor = true;
            btnOptimize.Click += btnOptimize_Click;
            // 
            // LogicOptimizerForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(784, 461);
            Controls.Add(dgvAssets);
            Controls.Add(panelBottom);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MinimumSize = new Size(700, 400);
            Name = "LogicOptimizerForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Logic Optimizer";
            ((System.ComponentModel.ISupportInitialize)dgvAssets).EndInit();
            panelBottom.ResumeLayout(false);
            panelBottom.PerformLayout();
            ResumeLayout(false);

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