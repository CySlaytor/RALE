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
            this.components = new System.ComponentModel.Container();
            this.dgvMapping = new System.Windows.Forms.DataGridView();
            this.colBaseAddr = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colMappedAddr = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colMappedRegion = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colDesc = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.lblMapping = new System.Windows.Forms.Label();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.lblInstruction = new System.Windows.Forms.Label();
            this.tabControlRegions = new System.Windows.Forms.TabControl();
            this.ctxGridActions = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.pasteLogicToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copyLogicToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.deleteRowToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)(this.dgvMapping)).BeginInit();
            this.ctxGridActions.SuspendLayout();
            this.SuspendLayout();
            // 
            // dgvMapping
            // 
            this.dgvMapping.AllowUserToAddRows = false;
            this.dgvMapping.AllowUserToDeleteRows = false;
            this.dgvMapping.AllowUserToResizeRows = false;
            this.dgvMapping.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvMapping.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvMapping.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colBaseAddr,
            this.colMappedAddr,
            this.colMappedRegion,
            this.colDesc});
            this.dgvMapping.Location = new System.Drawing.Point(12, 45);
            this.dgvMapping.Name = "dgvMapping";
            this.dgvMapping.ReadOnly = true;
            this.dgvMapping.RowHeadersVisible = false;
            this.dgvMapping.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvMapping.Size = new System.Drawing.Size(760, 200);
            this.dgvMapping.TabIndex = 0;
            // 
            // colBaseAddr
            // 
            this.colBaseAddr.HeaderText = "Base Addr (Active)";
            this.colBaseAddr.Name = "colBaseAddr";
            this.colBaseAddr.ReadOnly = true;
            this.colBaseAddr.Width = 120;
            // 
            // colMappedAddr
            // 
            this.colMappedAddr.HeaderText = "Mapped Addr";
            this.colMappedAddr.Name = "colMappedAddr";
            this.colMappedAddr.ReadOnly = true;
            this.colMappedAddr.Width = 120;
            // 
            // colMappedRegion
            // 
            this.colMappedRegion.HeaderText = "Region";
            this.colMappedRegion.Name = "colMappedRegion";
            this.colMappedRegion.ReadOnly = true;
            this.colMappedRegion.Width = 80;
            // 
            // colDesc
            // 
            this.colDesc.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.colDesc.HeaderText = "Note Description";
            this.colDesc.Name = "colDesc";
            this.colDesc.ReadOnly = true;
            // 
            // lblMapping
            // 
            this.lblMapping.AutoSize = true;
            this.lblMapping.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold);
            this.lblMapping.Location = new System.Drawing.Point(12, 27);
            this.lblMapping.Name = "lblMapping";
            this.lblMapping.Size = new System.Drawing.Size(263, 15);
            this.lblMapping.TabIndex = 1;
            this.lblMapping.Text = "Detected Address Mappings (from Code Notes)";
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(616, 520);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 4;
            this.btnOK.Text = "Apply";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.Location = new System.Drawing.Point(697, 520);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 5;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // lblInstruction
            // 
            this.lblInstruction.AutoSize = true;
            this.lblInstruction.ForeColor = System.Drawing.Color.DimGray;
            this.lblInstruction.Location = new System.Drawing.Point(12, 9);
            this.lblInstruction.Name = "lblInstruction";
            this.lblInstruction.Size = new System.Drawing.Size(437, 15);
            this.lblInstruction.TabIndex = 6;
            this.lblInstruction.Text = "Detects regions from notes. Define specific locks for each region in the tabs below.";
            // 
            // tabControlRegions
            // 
            this.tabControlRegions.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControlRegions.Location = new System.Drawing.Point(12, 260);
            this.tabControlRegions.Name = "tabControlRegions";
            this.tabControlRegions.SelectedIndex = 0;
            this.tabControlRegions.Size = new System.Drawing.Size(760, 250);
            this.tabControlRegions.TabIndex = 7;
            // 
            // ctxGridActions
            // 
            this.ctxGridActions.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.pasteLogicToolStripMenuItem,
            this.copyLogicToolStripMenuItem,
            this.deleteRowToolStripMenuItem});
            this.ctxGridActions.Name = "ctxGridActions";
            this.ctxGridActions.Size = new System.Drawing.Size(181, 92);
            // 
            // pasteLogicToolStripMenuItem
            // 
            this.pasteLogicToolStripMenuItem.Name = "pasteLogicToolStripMenuItem";
            this.pasteLogicToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.V)));
            this.pasteLogicToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.pasteLogicToolStripMenuItem.Text = "Paste Logic";
            // 
            // copyLogicToolStripMenuItem
            // 
            this.copyLogicToolStripMenuItem.Name = "copyLogicToolStripMenuItem";
            this.copyLogicToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.C)));
            this.copyLogicToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.copyLogicToolStripMenuItem.Text = "Copy";
            // 
            // deleteRowToolStripMenuItem
            // 
            this.deleteRowToolStripMenuItem.Name = "deleteRowToolStripMenuItem";
            this.deleteRowToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.Delete;
            this.deleteRowToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.deleteRowToolStripMenuItem.Text = "Delete Row";
            // 
            // MultiRegionForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 555);
            this.Controls.Add(this.tabControlRegions);
            this.Controls.Add(this.lblInstruction);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.lblMapping);
            this.Controls.Add(this.dgvMapping);
            this.MinimumSize = new System.Drawing.Size(800, 600);
            this.Name = "MultiRegionForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Multi-Region Logic Generator";
            ((System.ComponentModel.ISupportInitialize)(this.dgvMapping)).EndInit();
            this.ctxGridActions.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

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
    }
}