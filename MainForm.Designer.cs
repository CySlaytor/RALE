namespace RALogicEditor
{
    partial class MainForm
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
            this.mainMenu = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.recentToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.undoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.redoToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.splitMain = new System.Windows.Forms.SplitContainer();
            this.achievementGrid = new System.Windows.Forms.DataGridView();
            this.colID = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTitle = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.assetContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.newAssetToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.cloneAssetToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.deleteAssetToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.resetAssetToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparatorAsset = new System.Windows.Forms.ToolStripSeparator();
            this.moveUpAssetToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.moveDownAssetToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.panelGridBottom = new System.Windows.Forms.Panel();
            this.btnDeleteAsset = new System.Windows.Forms.Button();
            this.btnDuplicateAsset = new System.Windows.Forms.Button();
            this.btnAddAsset = new System.Windows.Forms.Button();
            this.panelGridTop = new System.Windows.Forms.Panel();
            this.cmbSource = new System.Windows.Forms.ComboBox();
            this.cmbAssetType = new System.Windows.Forms.ComboBox();
            this.splitEditor = new System.Windows.Forms.SplitContainer();
            this.triggerEditor = new RALogicEditor.Controls.TriggerEditorControl();
            this.codeNoteEditor = new RALogicEditor.Controls.CodeNoteEditorControl();
            this.mainMenu.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).BeginInit();
            this.splitMain.Panel1.SuspendLayout();
            this.splitMain.Panel2.SuspendLayout();
            this.splitMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.achievementGrid)).BeginInit();
            this.assetContextMenu.SuspendLayout();
            this.panelGridBottom.SuspendLayout();
            this.panelGridTop.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitEditor)).BeginInit();
            this.splitEditor.Panel1.SuspendLayout();
            this.splitEditor.Panel2.SuspendLayout();
            this.splitEditor.SuspendLayout();
            this.SuspendLayout();
            // 
            // mainMenu
            // 
            this.mainMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.editToolStripMenuItem});
            this.mainMenu.Location = new System.Drawing.Point(0, 0);
            this.mainMenu.Name = "mainMenu";
            this.mainMenu.Size = new System.Drawing.Size(1200, 24);
            this.mainMenu.TabIndex = 0;
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openToolStripMenuItem,
            this.saveToolStripMenuItem,
            this.recentToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Text = "&File";
            // 
            // openToolStripMenuItem
            // 
            this.openToolStripMenuItem.Name = "openToolStripMenuItem";
            this.openToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O)));
            this.openToolStripMenuItem.Text = "&Open...";
            this.openToolStripMenuItem.Click += new System.EventHandler(this.openToolStripMenuItem_Click);
            // 
            // saveToolStripMenuItem
            // 
            this.saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            this.saveToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
            this.saveToolStripMenuItem.Text = "&Save User File";
            this.saveToolStripMenuItem.Click += new System.EventHandler(this.saveToolStripMenuItem_Click);
            // 
            // recentToolStripMenuItem
            // 
            this.recentToolStripMenuItem.Name = "recentToolStripMenuItem";
            this.recentToolStripMenuItem.Text = "Recent Files";
            // 
            // editToolStripMenuItem
            // 
            this.editToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.undoToolStripMenuItem,
            this.redoToolStripMenuItem});
            this.editToolStripMenuItem.Name = "editToolStripMenuItem";
            this.editToolStripMenuItem.Text = "&Edit";
            // 
            // undoToolStripMenuItem
            // 
            this.undoToolStripMenuItem.Name = "undoToolStripMenuItem";
            this.undoToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Z)));
            this.undoToolStripMenuItem.Text = "Undo";
            this.undoToolStripMenuItem.Click += new System.EventHandler(this.Undo_Click);
            // 
            // redoToolStripMenuItem
            // 
            this.redoToolStripMenuItem.Name = "redoToolStripMenuItem";
            this.redoToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Y)));
            this.redoToolStripMenuItem.Text = "Redo";
            this.redoToolStripMenuItem.Click += new System.EventHandler(this.Redo_Click);
            // 
            // splitMain
            // 
            this.splitMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitMain.Location = new System.Drawing.Point(0, 24);
            this.splitMain.Name = "splitMain";
            // 
            // splitMain.Panel1
            // 
            // Add the Grid FIRST. In WinForms, controls added LAST are at the Top of Z-Order.
            // Controls at the Top of Z-Order take layout priority.
            // We want the Top/Bottom Panels to claim their space first, so they must be added LAST.
            this.splitMain.Panel1.Controls.Add(this.achievementGrid);
            this.splitMain.Panel1.Controls.Add(this.panelGridBottom);
            this.splitMain.Panel1.Controls.Add(this.panelGridTop);
            // 
            // splitMain.Panel2
            // 
            this.splitMain.Panel2.Controls.Add(this.splitEditor);
            this.splitMain.Size = new System.Drawing.Size(1200, 650);
            this.splitMain.SplitterDistance = 300;
            this.splitMain.TabIndex = 1;
            // 
            // achievementGrid
            // 
            this.achievementGrid.AllowUserToAddRows = false;
            this.achievementGrid.AllowUserToDeleteRows = false;
            this.achievementGrid.AllowUserToResizeRows = false;
            this.achievementGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.achievementGrid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colID,
            this.colTitle});
            this.achievementGrid.ContextMenuStrip = this.assetContextMenu;
            this.achievementGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.achievementGrid.Location = new System.Drawing.Point(0, 0);
            this.achievementGrid.MultiSelect = true;
            this.achievementGrid.Name = "achievementGrid";
            this.achievementGrid.ReadOnly = true;
            this.achievementGrid.RowHeadersVisible = false;
            this.achievementGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.achievementGrid.Size = new System.Drawing.Size(300, 650);
            this.achievementGrid.TabIndex = 0;
            this.achievementGrid.SelectionChanged += new System.EventHandler(this.achievementGrid_SelectionChanged);
            // 
            // colID
            // 
            this.colID.HeaderText = "ID";
            this.colID.Width = 80;
            this.colID.Name = "colID";
            // 
            // colTitle
            // 
            this.colTitle.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.colTitle.HeaderText = "Title";
            this.colTitle.Name = "colTitle";
            // 
            // assetContextMenu
            // 
            this.assetContextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.newAssetToolStripMenuItem,
            this.cloneAssetToolStripMenuItem,
            this.deleteAssetToolStripMenuItem,
            this.resetAssetToolStripMenuItem,
            this.toolStripSeparatorAsset,
            this.moveUpAssetToolStripMenuItem,
            this.moveDownAssetToolStripMenuItem});
            this.assetContextMenu.Name = "assetContextMenu";
            this.assetContextMenu.Size = new System.Drawing.Size(205, 142);
            // 
            // newAssetToolStripMenuItem
            // 
            this.newAssetToolStripMenuItem.Name = "newAssetToolStripMenuItem";
            this.newAssetToolStripMenuItem.Size = new System.Drawing.Size(204, 22);
            this.newAssetToolStripMenuItem.Text = "New";
            // 
            // cloneAssetToolStripMenuItem
            // 
            this.cloneAssetToolStripMenuItem.Name = "cloneAssetToolStripMenuItem";
            this.cloneAssetToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.D)));
            this.cloneAssetToolStripMenuItem.Size = new System.Drawing.Size(204, 22);
            this.cloneAssetToolStripMenuItem.Text = "Duplicate";
            // 
            // deleteAssetToolStripMenuItem
            // 
            this.deleteAssetToolStripMenuItem.Name = "deleteAssetToolStripMenuItem";
            this.deleteAssetToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.Delete;
            this.deleteAssetToolStripMenuItem.Size = new System.Drawing.Size(204, 22);
            this.deleteAssetToolStripMenuItem.Text = "Delete";
            // 
            // resetAssetToolStripMenuItem
            // 
            this.resetAssetToolStripMenuItem.Name = "resetAssetToolStripMenuItem";
            this.resetAssetToolStripMenuItem.Size = new System.Drawing.Size(204, 22);
            this.resetAssetToolStripMenuItem.Text = "Reset";
            // 
            // toolStripSeparatorAsset
            // 
            this.toolStripSeparatorAsset.Name = "toolStripSeparatorAsset";
            this.toolStripSeparatorAsset.Size = new System.Drawing.Size(201, 6);
            // 
            // moveUpAssetToolStripMenuItem
            // 
            this.moveUpAssetToolStripMenuItem.Name = "moveUpAssetToolStripMenuItem";
            this.moveUpAssetToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.Up)));
            this.moveUpAssetToolStripMenuItem.Size = new System.Drawing.Size(204, 22);
            this.moveUpAssetToolStripMenuItem.Text = "Move Up";
            // 
            // moveDownAssetToolStripMenuItem
            // 
            this.moveDownAssetToolStripMenuItem.Name = "moveDownAssetToolStripMenuItem";
            this.moveDownAssetToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.Down)));
            this.moveDownAssetToolStripMenuItem.Size = new System.Drawing.Size(204, 22);
            this.moveDownAssetToolStripMenuItem.Text = "Move Down";
            // 
            // panelGridBottom
            // 
            this.panelGridBottom.Controls.Add(this.btnDeleteAsset);
            this.panelGridBottom.Controls.Add(this.btnDuplicateAsset);
            this.panelGridBottom.Controls.Add(this.btnAddAsset);
            this.panelGridBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelGridBottom.Location = new System.Drawing.Point(0, 615);
            this.panelGridBottom.Name = "panelGridBottom";
            this.panelGridBottom.Size = new System.Drawing.Size(300, 35);
            this.panelGridBottom.TabIndex = 2;
            // 
            // btnDeleteAsset
            // 
            this.btnDeleteAsset.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnDeleteAsset.Enabled = false;
            this.btnDeleteAsset.Location = new System.Drawing.Point(222, 6);
            this.btnDeleteAsset.Name = "btnDeleteAsset";
            this.btnDeleteAsset.Size = new System.Drawing.Size(75, 23);
            this.btnDeleteAsset.TabIndex = 2;
            this.btnDeleteAsset.Text = "Delete";
            this.btnDeleteAsset.UseVisualStyleBackColor = true;
            // 
            // btnDuplicateAsset
            // 
            this.btnDuplicateAsset.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnDuplicateAsset.Enabled = false;
            this.btnDuplicateAsset.Location = new System.Drawing.Point(141, 6);
            this.btnDuplicateAsset.Name = "btnDuplicateAsset";
            this.btnDuplicateAsset.Size = new System.Drawing.Size(75, 23);
            this.btnDuplicateAsset.TabIndex = 1;
            this.btnDuplicateAsset.Text = "Duplicate";
            this.btnDuplicateAsset.UseVisualStyleBackColor = true;
            // 
            // btnAddAsset
            // 
            this.btnAddAsset.Location = new System.Drawing.Point(3, 6);
            this.btnAddAsset.Name = "btnAddAsset";
            this.btnAddAsset.Size = new System.Drawing.Size(73, 23);
            this.btnAddAsset.TabIndex = 0;
            this.btnAddAsset.Text = "Add";
            this.btnAddAsset.UseVisualStyleBackColor = true;
            // 
            // panelGridTop
            // 
            this.panelGridTop.Controls.Add(this.cmbSource);
            this.panelGridTop.Controls.Add(this.cmbAssetType);
            this.panelGridTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelGridTop.Location = new System.Drawing.Point(0, 0);
            this.panelGridTop.Name = "panelGridTop";
            this.panelGridTop.Size = new System.Drawing.Size(300, 30);
            this.panelGridTop.TabIndex = 1;
            // 
            // cmbSource
            // 
            this.cmbSource.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbSource.FormattingEnabled = true;
            this.cmbSource.Items.AddRange(new object[] {
            "Local",
            "JSON",
            "New"});
            this.cmbSource.Location = new System.Drawing.Point(149, 3);
            this.cmbSource.Name = "cmbSource";
            this.cmbSource.Size = new System.Drawing.Size(80, 23);
            this.cmbSource.TabIndex = 1;
            // 
            // cmbAssetType
            // 
            this.cmbAssetType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbAssetType.FormattingEnabled = true;
            this.cmbAssetType.Items.AddRange(new object[] {
            "Achievements",
            "Leaderboards"});
            this.cmbAssetType.Location = new System.Drawing.Point(3, 3);
            this.cmbAssetType.Name = "cmbAssetType";
            this.cmbAssetType.Size = new System.Drawing.Size(140, 23);
            this.cmbAssetType.TabIndex = 0;
            // 
            // splitEditor
            // 
            this.splitEditor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitEditor.Location = new System.Drawing.Point(0, 0);
            this.splitEditor.Name = "splitEditor";
            // 
            // splitEditor.Panel1
            // 
            this.splitEditor.Panel1.Controls.Add(this.triggerEditor);
            // 
            // splitEditor.Panel2
            // 
            this.splitEditor.Panel2.Controls.Add(this.codeNoteEditor);
            this.splitEditor.Size = new System.Drawing.Size(896, 650);
            this.splitEditor.SplitterDistance = 600;
            this.splitEditor.TabIndex = 0;
            // 
            // triggerEditor
            // 
            this.triggerEditor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.triggerEditor.GroupSeparator = 'S';
            this.triggerEditor.Location = new System.Drawing.Point(0, 0);
            this.triggerEditor.Name = "triggerEditor";
            this.triggerEditor.Size = new System.Drawing.Size(600, 650);
            this.triggerEditor.TabIndex = 0;
            // 
            // codeNoteEditor
            // 
            this.codeNoteEditor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.codeNoteEditor.Location = new System.Drawing.Point(0, 0);
            this.codeNoteEditor.Name = "codeNoteEditor";
            this.codeNoteEditor.Size = new System.Drawing.Size(292, 650);
            this.codeNoteEditor.TabIndex = 0;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1200, 674);
            this.Controls.Add(this.splitMain);
            this.Controls.Add(this.mainMenu);
            this.MainMenuStrip = this.mainMenu;
            this.Text = "RA Logic Editor";
            this.mainMenu.ResumeLayout(false);
            this.mainMenu.PerformLayout();
            this.splitMain.Panel1.ResumeLayout(false);
            this.splitMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).EndInit();
            this.splitMain.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.achievementGrid)).EndInit();
            this.assetContextMenu.ResumeLayout(false);
            this.panelGridBottom.ResumeLayout(false);
            this.panelGridTop.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitEditor)).EndInit();
            this.splitEditor.Panel1.ResumeLayout(false);
            this.splitEditor.Panel2.ResumeLayout(false);
            this.splitEditor.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private System.Windows.Forms.MenuStrip mainMenu;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem recentToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem undoToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem redoToolStripMenuItem;
        private System.Windows.Forms.SplitContainer splitMain;
        private System.Windows.Forms.SplitContainer splitEditor;
        private System.Windows.Forms.DataGridView achievementGrid;
        private System.Windows.Forms.DataGridViewTextBoxColumn colID;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTitle;
        private System.Windows.Forms.Panel panelGridTop;
        private System.Windows.Forms.ComboBox cmbAssetType;
        private System.Windows.Forms.ComboBox cmbSource;
        private RALogicEditor.Controls.TriggerEditorControl triggerEditor;
        private RALogicEditor.Controls.CodeNoteEditorControl codeNoteEditor;
        private System.Windows.Forms.ContextMenuStrip assetContextMenu;
        private System.Windows.Forms.ToolStripMenuItem newAssetToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem cloneAssetToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem deleteAssetToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparatorAsset;
        private System.Windows.Forms.ToolStripMenuItem moveUpAssetToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem moveDownAssetToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem resetAssetToolStripMenuItem;
        private System.Windows.Forms.Panel panelGridBottom;
        private System.Windows.Forms.Button btnAddAsset;
        private System.Windows.Forms.Button btnDuplicateAsset;
        private System.Windows.Forms.Button btnDeleteAsset;
    }
}