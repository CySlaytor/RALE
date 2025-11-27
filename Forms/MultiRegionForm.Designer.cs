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
            this.colTarget = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSource = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colDesc = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.lblMapping = new System.Windows.Forms.Label();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.lblInstruction = new System.Windows.Forms.Label();
            this.splitRegionLocks = new System.Windows.Forms.SplitContainer();
            this.grpRegionA = new System.Windows.Forms.GroupBox();
            this.dgvRegionA = new System.Windows.Forms.DataGridView();
            this.colTypeA = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSizeA = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colMemA = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colOpA = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colType2A = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSize2A = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colMem2A = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ctxGridActions = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.pasteLogicToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copyLogicToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.deleteRowToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.grpRegionB = new System.Windows.Forms.GroupBox();
            this.dgvRegionB = new System.Windows.Forms.DataGridView();
            this.colTypeB = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSizeB = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colMemB = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colOpB = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colType2B = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colSize2B = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colMem2B = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.dgvMapping)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitRegionLocks)).BeginInit();
            this.splitRegionLocks.Panel1.SuspendLayout();
            this.splitRegionLocks.Panel2.SuspendLayout();
            this.splitRegionLocks.SuspendLayout();
            this.grpRegionA.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvRegionA)).BeginInit();
            this.ctxGridActions.SuspendLayout();
            this.grpRegionB.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvRegionB)).BeginInit();
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
            this.colTarget,
            this.colSource,
            this.colDesc});
            this.dgvMapping.Location = new System.Drawing.Point(12, 45);
            this.dgvMapping.Name = "dgvMapping";
            this.dgvMapping.ReadOnly = true;
            this.dgvMapping.RowHeadersVisible = false;
            this.dgvMapping.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvMapping.Size = new System.Drawing.Size(760, 250);
            this.dgvMapping.TabIndex = 0;
            // 
            // colTarget
            // 
            this.colTarget.HeaderText = "USA (Target)";
            this.colTarget.Name = "colTarget";
            this.colTarget.ReadOnly = true;
            this.colTarget.Width = 120;
            // 
            // colSource
            // 
            this.colSource.HeaderText = "PAL (Source)";
            this.colSource.Name = "colSource";
            this.colSource.ReadOnly = true;
            this.colSource.Width = 120;
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
            this.lblInstruction.Text = "This tool clones current logic into a new Region B set, swapping addresses found below.";
            // 
            // splitRegionLocks
            // 
            this.splitRegionLocks.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitRegionLocks.Location = new System.Drawing.Point(12, 310);
            this.splitRegionLocks.Name = "splitRegionLocks";
            // 
            // splitRegionLocks.Panel1
            // 
            this.splitRegionLocks.Panel1.Controls.Add(this.grpRegionA);
            // 
            // splitRegionLocks.Panel2
            // 
            this.splitRegionLocks.Panel2.Controls.Add(this.grpRegionB);
            this.splitRegionLocks.Size = new System.Drawing.Size(760, 200);
            this.splitRegionLocks.SplitterDistance = 375;
            this.splitRegionLocks.TabIndex = 7;
            // 
            // grpRegionA
            // 
            this.grpRegionA.Controls.Add(this.dgvRegionA);
            this.grpRegionA.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grpRegionA.Location = new System.Drawing.Point(0, 0);
            this.grpRegionA.Name = "grpRegionA";
            this.grpRegionA.Size = new System.Drawing.Size(375, 200);
            this.grpRegionA.TabIndex = 0;
            this.grpRegionA.TabStop = false;
            this.grpRegionA.Text = "Region A Check (Is this Region A?)";
            // 
            // dgvRegionA
            // 
            this.dgvRegionA.AllowUserToResizeColumns = false;
            this.dgvRegionA.AllowUserToResizeRows = false;
            this.dgvRegionA.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvRegionA.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colTypeA,
            this.colSizeA,
            this.colMemA,
            this.colOpA,
            this.colType2A,
            this.colSize2A,
            this.colMem2A});
            this.dgvRegionA.ContextMenuStrip = this.ctxGridActions;
            this.dgvRegionA.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvRegionA.Location = new System.Drawing.Point(3, 19);
            this.dgvRegionA.Name = "dgvRegionA";
            this.dgvRegionA.RowHeadersWidth = 25;
            this.dgvRegionA.RowHeadersWidthSizeMode = System.Windows.Forms.DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            this.dgvRegionA.Size = new System.Drawing.Size(369, 178);
            this.dgvRegionA.TabIndex = 0;
            // 
            // colTypeA
            // 
            this.colTypeA.HeaderText = "Type";
            this.colTypeA.Name = "colTypeA";
            this.colTypeA.Width = 50;
            // 
            // colSizeA
            // 
            this.colSizeA.HeaderText = "Size";
            this.colSizeA.Name = "colSizeA";
            this.colSizeA.Width = 60;
            // 
            // colMemA
            // 
            this.colMemA.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.colMemA.HeaderText = "Memory";
            this.colMemA.Name = "colMemA";
            // 
            // colOpA
            // 
            this.colOpA.HeaderText = "Cmp";
            this.colOpA.Name = "colOpA";
            this.colOpA.Width = 40;
            // 
            // colType2A
            // 
            this.colType2A.HeaderText = "Type";
            this.colType2A.Name = "colType2A";
            this.colType2A.Width = 50;
            // 
            // colSize2A
            // 
            this.colSize2A.HeaderText = "Size";
            this.colSize2A.Name = "colSize2A";
            this.colSize2A.Width = 60;
            // 
            // colMem2A
            // 
            this.colMem2A.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.colMem2A.HeaderText = "Val/Mem";
            this.colMem2A.Name = "colMem2A";
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
            // grpRegionB
            // 
            this.grpRegionB.Controls.Add(this.dgvRegionB);
            this.grpRegionB.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grpRegionB.Location = new System.Drawing.Point(0, 0);
            this.grpRegionB.Name = "grpRegionB";
            this.grpRegionB.Size = new System.Drawing.Size(381, 200);
            this.grpRegionB.TabIndex = 0;
            this.grpRegionB.TabStop = false;
            this.grpRegionB.Text = "Region B Check (Is this Region B?)";
            // 
            // dgvRegionB
            // 
            this.dgvRegionB.AllowUserToResizeColumns = false;
            this.dgvRegionB.AllowUserToResizeRows = false;
            this.dgvRegionB.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvRegionB.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colTypeB,
            this.colSizeB,
            this.colMemB,
            this.colOpB,
            this.colType2B,
            this.colSize2B,
            this.colMem2B});
            this.dgvRegionB.ContextMenuStrip = this.ctxGridActions;
            this.dgvRegionB.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvRegionB.Location = new System.Drawing.Point(3, 19);
            this.dgvRegionB.Name = "dgvRegionB";
            this.dgvRegionB.RowHeadersWidth = 25;
            this.dgvRegionB.RowHeadersWidthSizeMode = System.Windows.Forms.DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            this.dgvRegionB.Size = new System.Drawing.Size(375, 178);
            this.dgvRegionB.TabIndex = 0;
            // 
            // colTypeB
            // 
            this.colTypeB.HeaderText = "Type";
            this.colTypeB.Name = "colTypeB";
            this.colTypeB.Width = 50;
            // 
            // colSizeB
            // 
            this.colSizeB.HeaderText = "Size";
            this.colSizeB.Name = "colSizeB";
            this.colSizeB.Width = 60;
            // 
            // colMemB
            // 
            this.colMemB.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.colMemB.HeaderText = "Memory";
            this.colMemB.Name = "colMemB";
            // 
            // colOpB
            // 
            this.colOpB.HeaderText = "Cmp";
            this.colOpB.Name = "colOpB";
            this.colOpB.Width = 40;
            // 
            // colType2B
            // 
            this.colType2B.HeaderText = "Type";
            this.colType2B.Name = "colType2B";
            this.colType2B.Width = 50;
            // 
            // colSize2B
            // 
            this.colSize2B.HeaderText = "Size";
            this.colSize2B.Name = "colSize2B";
            this.colSize2B.Width = 60;
            // 
            // colMem2B
            // 
            this.colMem2B.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.colMem2B.HeaderText = "Val/Mem";
            this.colMem2B.Name = "colMem2B";
            // 
            // MultiRegionForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 555);
            this.Controls.Add(this.splitRegionLocks);
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
            this.splitRegionLocks.Panel1.ResumeLayout(false);
            this.splitRegionLocks.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitRegionLocks)).EndInit();
            this.splitRegionLocks.ResumeLayout(false);
            this.grpRegionA.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvRegionA)).EndInit();
            this.ctxGridActions.ResumeLayout(false);
            this.grpRegionB.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvRegionB)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private System.Windows.Forms.DataGridView dgvMapping;
        private System.Windows.Forms.Label lblMapping;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTarget;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSource;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDesc;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label lblInstruction;
        private System.Windows.Forms.SplitContainer splitRegionLocks;
        private System.Windows.Forms.GroupBox grpRegionA;
        private System.Windows.Forms.DataGridView dgvRegionA;
        private System.Windows.Forms.GroupBox grpRegionB;
        private System.Windows.Forms.DataGridView dgvRegionB;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTypeA;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSizeA;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMemA;
        private System.Windows.Forms.DataGridViewTextBoxColumn colOpA;
        private System.Windows.Forms.DataGridViewTextBoxColumn colType2A;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSize2A;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMem2A;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTypeB;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSizeB;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMemB;
        private System.Windows.Forms.DataGridViewTextBoxColumn colOpB;
        private System.Windows.Forms.DataGridViewTextBoxColumn colType2B;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSize2B;
        private System.Windows.Forms.DataGridViewTextBoxColumn colMem2B;
        private System.Windows.Forms.ContextMenuStrip ctxGridActions;
        private System.Windows.Forms.ToolStripMenuItem pasteLogicToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem deleteRowToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyLogicToolStripMenuItem;
    }
}