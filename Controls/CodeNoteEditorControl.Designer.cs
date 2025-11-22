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
            panelTop = new Panel();
            panelRowVariable = new Panel();
            cmbVariables = new ComboBox();
            lblVariable = new Label();
            chkShowLeafsOnly = new CheckBox();
            panelRowAddress = new Panel();
            cmbNotes = new ComboBox();
            lblSelect = new Label();
            panelRowSearch = new Panel();
            txtSearch = new TextBox();
            btnReload = new Button();
            lblSearch = new Label();
            txtNoteBody = new TextBox();
            statusStripBottom = new StatusStrip();
            lblOffsetChain = new ToolStripStatusLabel();
            panelTop.SuspendLayout();
            panelRowVariable.SuspendLayout();
            panelRowAddress.SuspendLayout();
            panelRowSearch.SuspendLayout();
            statusStripBottom.SuspendLayout();
            SuspendLayout();
            // 
            // panelTop
            // 
            panelTop.Controls.Add(panelRowVariable);
            panelTop.Controls.Add(panelRowAddress);
            panelTop.Controls.Add(panelRowSearch);
            panelTop.Dock = DockStyle.Top;
            panelTop.Location = new Point(0, 0);
            panelTop.Name = "panelTop";
            panelTop.Size = new Size(300, 96);
            panelTop.TabIndex = 0;
            // 
            // panelRowVariable
            // 
            panelRowVariable.Controls.Add(chkShowLeafsOnly);
            panelRowVariable.Controls.Add(cmbVariables);
            panelRowVariable.Controls.Add(lblVariable);
            panelRowVariable.Dock = DockStyle.Top;
            panelRowVariable.Location = new Point(0, 64);
            panelRowVariable.Name = "panelRowVariable";
            panelRowVariable.Padding = new Padding(5, 2, 5, 2);
            panelRowVariable.Size = new Size(300, 30);
            panelRowVariable.TabIndex = 2;
            // 
            // cmbVariables
            // 
            cmbVariables.Dock = DockStyle.Fill;
            cmbVariables.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbVariables.FormattingEnabled = true;
            cmbVariables.Location = new Point(59, 2);
            cmbVariables.Name = "cmbVariables";
            cmbVariables.Size = new Size(140, 23);
            cmbVariables.TabIndex = 1;
            cmbVariables.SelectedIndexChanged += cmbVariables_SelectedIndexChanged;
            // IMPORTANT: Hook Format Event
            cmbVariables.Format += cmbVariables_Format;
            // 
            // chkShowLeafsOnly
            // 
            chkShowLeafsOnly.Dock = DockStyle.Right;
            chkShowLeafsOnly.AutoSize = true;
            chkShowLeafsOnly.Location = new Point(200, 2);
            chkShowLeafsOnly.Name = "chkShowLeafsOnly";
            chkShowLeafsOnly.Padding = new Padding(5, 0, 0, 0);
            chkShowLeafsOnly.Size = new Size(95, 23);
            chkShowLeafsOnly.TabIndex = 2;
            chkShowLeafsOnly.Text = "Show Leafs Only";
            chkShowLeafsOnly.UseVisualStyleBackColor = true;
            // 
            // lblVariable
            // 
            lblVariable.AutoSize = true;
            lblVariable.Dock = DockStyle.Left;
            lblVariable.Location = new Point(5, 2);
            lblVariable.Name = "lblVariable";
            lblVariable.Padding = new Padding(0, 4, 0, 0);
            lblVariable.Size = new Size(54, 19);
            lblVariable.TabIndex = 0;
            lblVariable.Text = "Variable: ";
            // 
            // panelRowAddress
            // 
            panelRowAddress.Controls.Add(cmbNotes);
            panelRowAddress.Controls.Add(lblSelect);
            panelRowAddress.Dock = DockStyle.Top;
            panelRowAddress.Location = new Point(0, 32);
            panelRowAddress.Name = "panelRowAddress";
            panelRowAddress.Padding = new Padding(5, 5, 5, 2);
            panelRowAddress.Size = new Size(300, 32);
            panelRowAddress.TabIndex = 1;
            // 
            // cmbNotes
            // 
            cmbNotes.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cmbNotes.AutoCompleteSource = AutoCompleteSource.ListItems;
            cmbNotes.Dock = DockStyle.Fill;
            cmbNotes.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbNotes.FormattingEnabled = true;
            cmbNotes.Location = new Point(60, 5);
            cmbNotes.Name = "cmbNotes";
            cmbNotes.Size = new Size(235, 23);
            cmbNotes.TabIndex = 1;
            cmbNotes.SelectedIndexChanged += cmbNotes_SelectedIndexChanged;
            cmbNotes.Format += cmbNotes_Format;
            // 
            // lblSelect
            // 
            lblSelect.AutoSize = true;
            lblSelect.Dock = DockStyle.Left;
            lblSelect.Location = new Point(5, 5);
            lblSelect.Name = "lblSelect";
            lblSelect.Padding = new Padding(0, 4, 0, 0);
            lblSelect.Size = new Size(55, 19);
            lblSelect.TabIndex = 0;
            lblSelect.Text = "Address: ";
            // 
            // panelRowSearch
            // 
            panelRowSearch.Controls.Add(txtSearch);
            panelRowSearch.Controls.Add(btnReload);
            panelRowSearch.Controls.Add(lblSearch);
            panelRowSearch.Dock = DockStyle.Top;
            panelRowSearch.Location = new Point(0, 0);
            panelRowSearch.Name = "panelRowSearch";
            panelRowSearch.Padding = new Padding(5, 5, 5, 2);
            panelRowSearch.Size = new Size(300, 32);
            panelRowSearch.TabIndex = 0;
            // 
            // txtSearch
            // 
            txtSearch.Dock = DockStyle.Fill;
            txtSearch.Location = new Point(53, 5);
            txtSearch.Name = "txtSearch";
            txtSearch.Size = new Size(182, 23);
            txtSearch.TabIndex = 1;
            txtSearch.TextChanged += txtSearch_TextChanged;
            // 
            // btnReload
            // 
            btnReload.Dock = DockStyle.Right;
            btnReload.Location = new Point(235, 5);
            btnReload.Name = "btnReload";
            btnReload.Size = new Size(60, 25);
            btnReload.TabIndex = 2;
            btnReload.Text = "Reload";
            btnReload.UseVisualStyleBackColor = true;
            btnReload.Click += btnReload_Click;
            // 
            // lblSearch
            // 
            lblSearch.AutoSize = true;
            lblSearch.Dock = DockStyle.Left;
            lblSearch.Location = new Point(5, 5);
            lblSearch.Name = "lblSearch";
            lblSearch.Padding = new Padding(0, 4, 0, 0);
            lblSearch.Size = new Size(48, 19);
            lblSearch.TabIndex = 0;
            lblSearch.Text = "Search: ";
            // 
            // txtNoteBody
            // 
            txtNoteBody.AcceptsReturn = true;
            txtNoteBody.AcceptsTab = true;
            txtNoteBody.Dock = DockStyle.Fill;
            txtNoteBody.Font = new Font("Consolas", 9.75F);
            txtNoteBody.Location = new Point(0, 96);
            txtNoteBody.Multiline = true;
            txtNoteBody.Name = "txtNoteBody";
            txtNoteBody.ScrollBars = ScrollBars.Vertical;
            txtNoteBody.Size = new Size(300, 282);
            txtNoteBody.TabIndex = 1;
            txtNoteBody.TextChanged += txtNoteBody_TextChanged;
            // 
            // statusStripBottom
            // 
            statusStripBottom.Items.AddRange(new ToolStripItem[] { lblOffsetChain });
            statusStripBottom.Location = new Point(0, 378);
            statusStripBottom.Name = "statusStripBottom";
            statusStripBottom.Size = new Size(300, 22);
            statusStripBottom.TabIndex = 2;
            statusStripBottom.Text = "statusStrip1";
            // 
            // lblOffsetChain
            // 
            lblOffsetChain.Name = "lblOffsetChain";
            lblOffsetChain.Size = new Size(39, 17);
            lblOffsetChain.Text = "Ready";
            // 
            // CodeNoteEditorControl
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(txtNoteBody);
            Controls.Add(statusStripBottom);
            Controls.Add(panelTop);
            Name = "CodeNoteEditorControl";
            Size = new Size(300, 400);
            panelTop.ResumeLayout(false);
            panelRowVariable.ResumeLayout(false);
            panelRowVariable.PerformLayout();
            panelRowAddress.ResumeLayout(false);
            panelRowAddress.PerformLayout();
            panelRowSearch.ResumeLayout(false);
            panelRowSearch.PerformLayout();
            statusStripBottom.ResumeLayout(false);
            statusStripBottom.PerformLayout();
            ResumeLayout(false);
            PerformLayout();

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
    }
}