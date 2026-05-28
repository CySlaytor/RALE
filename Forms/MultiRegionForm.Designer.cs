namespace RALogicEditor.Forms
{
    partial class MultiRegionForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MultiRegionForm));
            dgvMapping = new DataGridView();
            colBaseAddr = new DataGridViewTextBoxColumn();
            colMappedAddr = new DataGridViewTextBoxColumn();
            colMappedRegion = new DataGridViewTextBoxColumn();
            colDesc = new DataGridViewTextBoxColumn();
            lblMapping = new Label();
            btnOK = new Button();
            btnCancel = new Button();
            lblInstruction = new Label();
            tabControlRegions = new TabControl();
            ctxGridActions = new ContextMenuStrip(components);
            pasteLogicToolStripMenuItem = new ToolStripMenuItem();
            copyLogicToolStripMenuItem = new ToolStripMenuItem();
            deleteRowToolStripMenuItem = new ToolStripMenuItem();
            btnHelp = new Button();
            chkUpdateRP = new CheckBox();
            chkUsePauseLocks = new CheckBox();
            ((System.ComponentModel.ISupportInitialize)dgvMapping).BeginInit();
            ctxGridActions.SuspendLayout();
            SuspendLayout();
            // 
            // dgvMapping
            // 
            dgvMapping.AllowUserToAddRows = false;
            dgvMapping.AllowUserToDeleteRows = false;
            dgvMapping.AllowUserToResizeRows = false;
            dgvMapping.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvMapping.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvMapping.Columns.AddRange(new DataGridViewColumn[] { colBaseAddr, colMappedAddr, colMappedRegion, colDesc });
            dgvMapping.Location = new Point(12, 45);
            dgvMapping.Name = "dgvMapping";
            dgvMapping.ReadOnly = true;
            dgvMapping.RowHeadersVisible = false;
            dgvMapping.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvMapping.Size = new Size(760, 200);
            dgvMapping.TabIndex = 0;
            // 
            // colBaseAddr
            // 
            colBaseAddr.HeaderText = "Base Addr (Active)";
            colBaseAddr.Name = "colBaseAddr";
            colBaseAddr.ReadOnly = true;
            colBaseAddr.Width = 120;
            // 
            // colMappedAddr
            // 
            colMappedAddr.HeaderText = "Mapped Addr";
            colMappedAddr.Name = "colMappedAddr";
            colMappedAddr.ReadOnly = true;
            colMappedAddr.Width = 120;
            // 
            // colMappedRegion
            // 
            colMappedRegion.HeaderText = "Region";
            colMappedRegion.Name = "colMappedRegion";
            colMappedRegion.ReadOnly = true;
            colMappedRegion.Width = 80;
            // 
            // colDesc
            // 
            colDesc.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colDesc.HeaderText = "Note Description";
            colDesc.Name = "colDesc";
            colDesc.ReadOnly = true;
            // 
            // lblMapping
            // 
            lblMapping.AutoSize = true;
            lblMapping.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblMapping.Location = new Point(12, 27);
            lblMapping.Name = "lblMapping";
            lblMapping.Size = new Size(269, 15);
            lblMapping.TabIndex = 1;
            lblMapping.Text = "Detected Address Mappings (from Code Notes)";
            // 
            // btnOK
            // 
            btnOK.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnOK.Location = new Point(616, 520);
            btnOK.Name = "btnOK";
            btnOK.Size = new Size(75, 23);
            btnOK.TabIndex = 4;
            btnOK.Text = "Apply";
            btnOK.UseVisualStyleBackColor = true;
            btnOK.Click += btnOK_Click;
            // 
            // btnCancel
            // 
            btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCancel.Location = new Point(697, 520);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(75, 23);
            btnCancel.TabIndex = 5;
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += btnCancel_Click;
            // 
            // lblInstruction
            // 
            lblInstruction.AutoSize = true;
            lblInstruction.ForeColor = Color.DimGray;
            lblInstruction.Location = new Point(12, 9);
            lblInstruction.Name = "lblInstruction";
            lblInstruction.Size = new Size(441, 15);
            lblInstruction.TabIndex = 6;
            lblInstruction.Text = "Detects regions from notes. Define specific locks for each region in the tabs below.";
            // 
            // tabControlRegions
            // 
            tabControlRegions.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tabControlRegions.Location = new Point(12, 260);
            tabControlRegions.Name = "tabControlRegions";
            tabControlRegions.SelectedIndex = 0;
            tabControlRegions.Size = new Size(760, 250);
            tabControlRegions.TabIndex = 7;
            // 
            // ctxGridActions
            // 
            ctxGridActions.Items.AddRange(new ToolStripItem[] { pasteLogicToolStripMenuItem, copyLogicToolStripMenuItem, deleteRowToolStripMenuItem });
            ctxGridActions.Name = "ctxGridActions";
            ctxGridActions.Size = new Size(176, 70);
            // 
            // pasteLogicToolStripMenuItem
            // 
            pasteLogicToolStripMenuItem.Name = "pasteLogicToolStripMenuItem";
            pasteLogicToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.V;
            pasteLogicToolStripMenuItem.Size = new Size(175, 22);
            pasteLogicToolStripMenuItem.Text = "Paste Logic";
            // 
            // copyLogicToolStripMenuItem
            // 
            copyLogicToolStripMenuItem.Name = "copyLogicToolStripMenuItem";
            copyLogicToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.C;
            copyLogicToolStripMenuItem.Size = new Size(175, 22);
            copyLogicToolStripMenuItem.Text = "Copy";
            // 
            // deleteRowToolStripMenuItem
            // 
            deleteRowToolStripMenuItem.Name = "deleteRowToolStripMenuItem";
            deleteRowToolStripMenuItem.ShortcutKeys = Keys.Delete;
            deleteRowToolStripMenuItem.Size = new Size(175, 22);
            deleteRowToolStripMenuItem.Text = "Delete Row";
            // 
            // btnHelp
            // 
            btnHelp.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            btnHelp.Location = new Point(12, 520);
            btnHelp.Name = "btnHelp";
            btnHelp.Size = new Size(75, 23);
            btnHelp.TabIndex = 8;
            btnHelp.Text = "Help";
            btnHelp.UseVisualStyleBackColor = true;
            btnHelp.Click += btnHelp_Click;
            // 
            // chkUpdateRP
            // 
            chkUpdateRP.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            chkUpdateRP.AutoSize = true;
            chkUpdateRP.Checked = true;
            chkUpdateRP.CheckState = CheckState.Checked;
            chkUpdateRP.Location = new Point(463, 523);
            chkUpdateRP.Name = "chkUpdateRP";
            chkUpdateRP.Size = new Size(140, 19);
            chkUpdateRP.TabIndex = 9;
            chkUpdateRP.Text = "Update Rich Presence";
            chkUpdateRP.UseVisualStyleBackColor = true;
            // 
            // chkUsePauseLocks
            // 
            chkUsePauseLocks.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            chkUsePauseLocks.AutoSize = true;
            chkUsePauseLocks.Checked = true;
            chkUsePauseLocks.CheckState = CheckState.Checked;
            chkUsePauseLocks.Location = new Point(100, 523);
            chkUsePauseLocks.Name = "chkUsePauseLocks";
            chkUsePauseLocks.Size = new Size(151, 19);
            chkUsePauseLocks.TabIndex = 10;
            chkUsePauseLocks.Text = "Generate as PauseLocks";
            chkUsePauseLocks.UseVisualStyleBackColor = true;
            // 
            // MultiRegionForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(784, 561);
            Controls.Add(chkUsePauseLocks);
            Controls.Add(chkUpdateRP);
            Controls.Add(btnHelp);
            Controls.Add(tabControlRegions);
            Controls.Add(lblInstruction);
            Controls.Add(btnCancel);
            Controls.Add(btnOK);
            Controls.Add(lblMapping);
            Controls.Add(dgvMapping);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MinimumSize = new Size(800, 600);
            Name = "MultiRegionForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Multi-Region Logic Generator";
            ((System.ComponentModel.ISupportInitialize)dgvMapping).EndInit();
            ctxGridActions.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();

        }

        private System.Windows.Forms.DataGridView dgvMapping;
        private System.Windows.Forms.Label lblMapping;
        private System.Windows.Forms.DataGridViewTextBoxColumn colBaseAddr;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMappedAddr;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMappedRegion;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDesc;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label lblInstruction;
        private System.Windows.Forms.TabControl tabControlRegions;
        private System.Windows.Forms.ContextMenuStrip ctxGridActions;
        private System.Windows.Forms.ToolStripMenuItem pasteLogicToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem deleteRowToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyLogicToolStripMenuItem;
        private System.Windows.Forms.Button btnHelp;
        private System.Windows.Forms.CheckBox chkUpdateRP;
        private System.Windows.Forms.CheckBox chkUsePauseLocks;
    }
}