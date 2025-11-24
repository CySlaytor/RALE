namespace RALogicEditor.Controls
{
    partial class CodeNoteEditorControl
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.panelTop = new System.Windows.Forms.Panel();
            this.panelRowVariable = new System.Windows.Forms.Panel();
            this.chkShowLeafsOnly = new System.Windows.Forms.CheckBox();
            this.cmbVariables = new System.Windows.Forms.ComboBox();
            this.lblVariable = new System.Windows.Forms.Label();
            this.panelRowAddress = new System.Windows.Forms.Panel();
            this.cmbNotes = new System.Windows.Forms.ComboBox();
            this.lblSelect = new System.Windows.Forms.Label();
            this.panelRowSearch = new System.Windows.Forms.Panel();
            this.txtSearch = new System.Windows.Forms.TextBox();
            this.btnReload = new System.Windows.Forms.Button();
            this.lblSearch = new System.Windows.Forms.Label();
            this.txtNoteBody = new System.Windows.Forms.TextBox();
            this.cmsNoteContext = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.cutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pasteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.checkSizeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statusStripBottom = new System.Windows.Forms.StatusStrip();
            this.lblOffsetChain = new System.Windows.Forms.ToolStripStatusLabel();
            this.panelBottom = new System.Windows.Forms.Panel();
            this.btnCopyAll = new System.Windows.Forms.Button();
            this.panelTop.SuspendLayout();
            this.panelRowVariable.SuspendLayout();
            this.panelRowAddress.SuspendLayout();
            this.panelRowSearch.SuspendLayout();
            this.cmsNoteContext.SuspendLayout();
            this.statusStripBottom.SuspendLayout();
            this.panelBottom.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelTop
            // 
            this.panelTop.Controls.Add(this.panelRowVariable);
            this.panelTop.Controls.Add(this.panelRowAddress);
            this.panelTop.Controls.Add(this.panelRowSearch);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Location = new System.Drawing.Point(0, 0);
            this.panelTop.Name = "panelTop";
            this.panelTop.Size = new System.Drawing.Size(300, 96);
            this.panelTop.TabIndex = 0;
            // 
            // panelRowVariable
            // 
            this.panelRowVariable.Controls.Add(this.chkShowLeafsOnly);
            this.panelRowVariable.Controls.Add(this.cmbVariables);
            this.panelRowVariable.Controls.Add(this.lblVariable);
            this.panelRowVariable.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelRowVariable.Location = new System.Drawing.Point(0, 64);
            this.panelRowVariable.Name = "panelRowVariable";
            this.panelRowVariable.Padding = new System.Windows.Forms.Padding(5, 2, 5, 2);
            this.panelRowVariable.Size = new System.Drawing.Size(300, 30);
            this.panelRowVariable.TabIndex = 2;
            // 
            // chkShowLeafsOnly
            // 
            this.chkShowLeafsOnly.AutoSize = true;
            this.chkShowLeafsOnly.Dock = System.Windows.Forms.DockStyle.Right;
            this.chkShowLeafsOnly.Location = new System.Drawing.Point(200, 2);
            this.chkShowLeafsOnly.Name = "chkShowLeafsOnly";
            this.chkShowLeafsOnly.Padding = new System.Windows.Forms.Padding(5, 0, 0, 0);
            this.chkShowLeafsOnly.Size = new System.Drawing.Size(95, 26);
            this.chkShowLeafsOnly.TabIndex = 2;
            this.chkShowLeafsOnly.Text = "Show Leafs Only";
            this.chkShowLeafsOnly.UseVisualStyleBackColor = true;
            // 
            // cmbVariables
            // 
            this.cmbVariables.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cmbVariables.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbVariables.FormattingEnabled = true;
            this.cmbVariables.Location = new System.Drawing.Point(59, 2);
            this.cmbVariables.Name = "cmbVariables";
            this.cmbVariables.Size = new System.Drawing.Size(141, 23);
            this.cmbVariables.TabIndex = 1;
            this.cmbVariables.SelectedIndexChanged += new System.EventHandler(this.cmbVariables_SelectedIndexChanged);
            this.cmbVariables.Format += new System.Windows.Forms.ListControlConvertEventHandler(this.cmbVariables_Format);
            // 
            // lblVariable
            // 
            this.lblVariable.AutoSize = true;
            this.lblVariable.Dock = System.Windows.Forms.DockStyle.Left;
            this.lblVariable.Location = new System.Drawing.Point(5, 2);
            this.lblVariable.Name = "lblVariable";
            this.lblVariable.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.lblVariable.Size = new System.Drawing.Size(54, 19);
            this.lblVariable.TabIndex = 0;
            this.lblVariable.Text = "Variable: ";
            // 
            // panelRowAddress
            // 
            this.panelRowAddress.Controls.Add(this.cmbNotes);
            this.panelRowAddress.Controls.Add(this.lblSelect);
            this.panelRowAddress.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelRowAddress.Location = new System.Drawing.Point(0, 32);
            this.panelRowAddress.Name = "panelRowAddress";
            this.panelRowAddress.Padding = new System.Windows.Forms.Padding(5, 5, 5, 2);
            this.panelRowAddress.Size = new System.Drawing.Size(300, 32);
            this.panelRowAddress.TabIndex = 1;
            // 
            // cmbNotes
            // 
            this.cmbNotes.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.cmbNotes.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.cmbNotes.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cmbNotes.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbNotes.FormattingEnabled = true;
            this.cmbNotes.Location = new System.Drawing.Point(60, 5);
            this.cmbNotes.Name = "cmbNotes";
            this.cmbNotes.Size = new System.Drawing.Size(235, 23);
            this.cmbNotes.TabIndex = 1;
            this.cmbNotes.SelectedIndexChanged += new System.EventHandler(this.cmbNotes_SelectedIndexChanged);
            this.cmbNotes.Format += new System.Windows.Forms.ListControlConvertEventHandler(this.cmbNotes_Format);
            // 
            // lblSelect
            // 
            this.lblSelect.AutoSize = true;
            this.lblSelect.Dock = System.Windows.Forms.DockStyle.Left;
            this.lblSelect.Location = new System.Drawing.Point(5, 5);
            this.lblSelect.Name = "lblSelect";
            this.lblSelect.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.lblSelect.Size = new System.Drawing.Size(55, 19);
            this.lblSelect.TabIndex = 0;
            this.lblSelect.Text = "Address: ";
            // 
            // panelRowSearch
            // 
            this.panelRowSearch.Controls.Add(this.txtSearch);
            this.panelRowSearch.Controls.Add(this.btnReload);
            this.panelRowSearch.Controls.Add(this.lblSearch);
            this.panelRowSearch.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelRowSearch.Location = new System.Drawing.Point(0, 0);
            this.panelRowSearch.Name = "panelRowSearch";
            this.panelRowSearch.Padding = new System.Windows.Forms.Padding(5, 5, 5, 2);
            this.panelRowSearch.Size = new System.Drawing.Size(300, 32);
            this.panelRowSearch.TabIndex = 0;
            // 
            // txtSearch
            // 
            this.txtSearch.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtSearch.Location = new System.Drawing.Point(53, 5);
            this.txtSearch.Name = "txtSearch";
            this.txtSearch.Size = new System.Drawing.Size(182, 23);
            this.txtSearch.TabIndex = 1;
            this.txtSearch.TextChanged += new System.EventHandler(this.txtSearch_TextChanged);
            // 
            // btnReload
            // 
            this.btnReload.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnReload.Location = new System.Drawing.Point(235, 5);
            this.btnReload.Name = "btnReload";
            this.btnReload.Size = new System.Drawing.Size(60, 25);
            this.btnReload.TabIndex = 2;
            this.btnReload.Text = "Reload";
            this.btnReload.UseVisualStyleBackColor = true;
            this.btnReload.Click += new System.EventHandler(this.btnReload_Click);
            // 
            // lblSearch
            // 
            this.lblSearch.AutoSize = true;
            this.lblSearch.Dock = System.Windows.Forms.DockStyle.Left;
            this.lblSearch.Location = new System.Drawing.Point(5, 5);
            this.lblSearch.Name = "lblSearch";
            this.lblSearch.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.lblSearch.Size = new System.Drawing.Size(48, 19);
            this.lblSearch.TabIndex = 0;
            this.lblSearch.Text = "Search: ";
            // 
            // txtNoteBody
            // 
            this.txtNoteBody.AcceptsReturn = true;
            this.txtNoteBody.AcceptsTab = true;
            this.txtNoteBody.ContextMenuStrip = this.cmsNoteContext;
            this.txtNoteBody.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtNoteBody.Font = new System.Drawing.Font("Consolas", 9.75F);
            this.txtNoteBody.Location = new System.Drawing.Point(0, 96);
            this.txtNoteBody.Multiline = true;
            this.txtNoteBody.Name = "txtNoteBody";
            this.txtNoteBody.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtNoteBody.Size = new System.Drawing.Size(300, 253);
            this.txtNoteBody.TabIndex = 1;
            this.txtNoteBody.TextChanged += new System.EventHandler(this.txtNoteBody_TextChanged);
            // 
            // cmsNoteContext
            // 
            this.cmsNoteContext.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.cutToolStripMenuItem,
            this.copyToolStripMenuItem,
            this.pasteToolStripMenuItem,
            this.toolStripSeparator1,
            this.checkSizeToolStripMenuItem});
            this.cmsNoteContext.Name = "cmsNoteContext";
            this.cmsNoteContext.Size = new System.Drawing.Size(209, 98);
            // 
            // cutToolStripMenuItem
            // 
            this.cutToolStripMenuItem.Name = "cutToolStripMenuItem";
            this.cutToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.X)));
            this.cutToolStripMenuItem.Size = new System.Drawing.Size(208, 22);
            this.cutToolStripMenuItem.Text = "Cut";
            this.cutToolStripMenuItem.Click += new System.EventHandler(this.cutToolStripMenuItem_Click);
            // 
            // copyToolStripMenuItem
            // 
            this.copyToolStripMenuItem.Name = "copyToolStripMenuItem";
            this.copyToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.C)));
            this.copyToolStripMenuItem.Size = new System.Drawing.Size(208, 22);
            this.copyToolStripMenuItem.Text = "Copy";
            this.copyToolStripMenuItem.Click += new System.EventHandler(this.copyToolStripMenuItem_Click);
            // 
            // pasteToolStripMenuItem
            // 
            this.pasteToolStripMenuItem.Name = "pasteToolStripMenuItem";
            this.pasteToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.V)));
            this.pasteToolStripMenuItem.Size = new System.Drawing.Size(208, 22);
            this.pasteToolStripMenuItem.Text = "Paste";
            this.pasteToolStripMenuItem.Click += new System.EventHandler(this.pasteToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(205, 6);
            // 
            // checkSizeToolStripMenuItem
            // 
            this.checkSizeToolStripMenuItem.Name = "checkSizeToolStripMenuItem";
            this.checkSizeToolStripMenuItem.Size = new System.Drawing.Size(208, 22);
            this.checkSizeToolStripMenuItem.Text = "Check for Code Note Size";
            this.checkSizeToolStripMenuItem.Click += new System.EventHandler(this.checkSizeToolStripMenuItem_Click);
            // 
            // statusStripBottom
            // 
            this.statusStripBottom.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblOffsetChain});
            this.statusStripBottom.Location = new System.Drawing.Point(0, 378);
            this.statusStripBottom.Name = "statusStripBottom";
            this.statusStripBottom.Size = new System.Drawing.Size(300, 22);
            this.statusStripBottom.TabIndex = 2;
            this.statusStripBottom.Text = "statusStrip1";
            // 
            // lblOffsetChain
            // 
            this.lblOffsetChain.Name = "lblOffsetChain";
            this.lblOffsetChain.Size = new System.Drawing.Size(39, 17);
            this.lblOffsetChain.Text = "Ready";
            // 
            // panelBottom
            // 
            this.panelBottom.Controls.Add(this.btnCopyAll);
            this.panelBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelBottom.Location = new System.Drawing.Point(0, 349);
            this.panelBottom.Name = "panelBottom";
            this.panelBottom.Padding = new System.Windows.Forms.Padding(2);
            this.panelBottom.Size = new System.Drawing.Size(300, 29);
            this.panelBottom.TabIndex = 3;
            // 
            // btnCopyAll
            // 
            this.btnCopyAll.Dock = System.Windows.Forms.DockStyle.Right;
            this.btnCopyAll.Location = new System.Drawing.Point(223, 2);
            this.btnCopyAll.Name = "btnCopyAll";
            this.btnCopyAll.Size = new System.Drawing.Size(75, 25);
            this.btnCopyAll.TabIndex = 0;
            this.btnCopyAll.Text = "Copy All";
            this.btnCopyAll.UseVisualStyleBackColor = true;
            this.btnCopyAll.Click += new System.EventHandler(this.btnCopyAll_Click);
            // 
            // CodeNoteEditorControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.txtNoteBody);
            this.Controls.Add(this.panelBottom);
            this.Controls.Add(this.statusStripBottom);
            this.Controls.Add(this.panelTop);
            this.Name = "CodeNoteEditorControl";
            this.Size = new System.Drawing.Size(300, 400);
            this.panelTop.ResumeLayout(false);
            this.panelRowVariable.ResumeLayout(false);
            this.panelRowVariable.PerformLayout();
            this.panelRowAddress.ResumeLayout(false);
            this.panelRowAddress.PerformLayout();
            this.panelRowSearch.ResumeLayout(false);
            this.panelRowSearch.PerformLayout();
            this.cmsNoteContext.ResumeLayout(false);
            this.statusStripBottom.ResumeLayout(false);
            this.statusStripBottom.PerformLayout();
            this.panelBottom.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.Panel panelRowVariable;
        private System.Windows.Forms.ComboBox cmbVariables;
        private System.Windows.Forms.Label lblVariable;
        private System.Windows.Forms.Panel panelRowAddress;
        private System.Windows.Forms.ComboBox cmbNotes;
        private System.Windows.Forms.Label lblSelect;
        private System.Windows.Forms.TextBox txtNoteBody;
        private System.Windows.Forms.StatusStrip statusStripBottom;
        private System.Windows.Forms.ToolStripStatusLabel lblOffsetChain;
        private System.Windows.Forms.Panel panelRowSearch;
        private System.Windows.Forms.TextBox txtSearch;
        private System.Windows.Forms.Label lblSearch;
        private System.Windows.Forms.Button btnReload;
        private System.Windows.Forms.CheckBox chkShowLeafsOnly;
        private System.Windows.Forms.Panel panelBottom;
        private System.Windows.Forms.Button btnCopyAll;
        private System.Windows.Forms.ContextMenuStrip cmsNoteContext;
        private System.Windows.Forms.ToolStripMenuItem cutToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pasteToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem checkSizeToolStripMenuItem;
    }
}