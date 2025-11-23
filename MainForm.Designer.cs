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
            components = new System.ComponentModel.Container();
            mainMenu = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem();
            openToolStripMenuItem = new ToolStripMenuItem();
            saveToolStripMenuItem = new ToolStripMenuItem();
            recentToolStripMenuItem = new ToolStripMenuItem();
            editToolStripMenuItem = new ToolStripMenuItem();
            undoToolStripMenuItem = new ToolStripMenuItem();
            redoToolStripMenuItem = new ToolStripMenuItem();
            renumberLocalIDsToolStripMenuItem = new ToolStripMenuItem();
            splitMain = new SplitContainer();
            achievementGrid = new DataGridView();
            colID = new DataGridViewTextBoxColumn();
            colTitle = new DataGridViewTextBoxColumn();
            assetContextMenu = new ContextMenuStrip(components);
            newAssetToolStripMenuItem = new ToolStripMenuItem();
            cloneAssetToolStripMenuItem = new ToolStripMenuItem();
            deleteAssetToolStripMenuItem = new ToolStripMenuItem();
            resetAssetToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparatorAsset = new ToolStripSeparator();
            moveUpAssetToolStripMenuItem = new ToolStripMenuItem();
            moveDownAssetToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparatorRenumber = new ToolStripSeparator();
            renumberContextToolStripMenuItem = new ToolStripMenuItem();
            viewDiffToolStripMenuItem = new ToolStripMenuItem();
            panelGridBottom = new Panel();
            btnDeleteAsset = new Button();
            btnDuplicateAsset = new Button();
            btnAddAsset = new Button();
            panelGridTop = new Panel();
            txtAssetSearch = new TextBox();
            lblAssetSearch = new Label();
            cmbSource = new ComboBox();
            cmbAssetType = new ComboBox();
            splitEditor = new SplitContainer();
            triggerEditor = new RALogicEditor.Controls.TriggerEditorControl();
            codeNoteEditor = new RALogicEditor.Controls.CodeNoteEditorControl();
            mainMenu.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitMain).BeginInit();
            splitMain.Panel1.SuspendLayout();
            splitMain.Panel2.SuspendLayout();
            splitMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)achievementGrid).BeginInit();
            assetContextMenu.SuspendLayout();
            panelGridBottom.SuspendLayout();
            panelGridTop.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitEditor).BeginInit();
            splitEditor.Panel1.SuspendLayout();
            splitEditor.Panel2.SuspendLayout();
            splitEditor.SuspendLayout();
            SuspendLayout();
            // 
            // mainMenu
            // 
            mainMenu.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, editToolStripMenuItem });
            mainMenu.Location = new Point(0, 0);
            mainMenu.Name = "mainMenu";
            mainMenu.Size = new Size(1200, 24);
            mainMenu.TabIndex = 0;
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { openToolStripMenuItem, saveToolStripMenuItem, recentToolStripMenuItem });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new Size(37, 20);
            fileToolStripMenuItem.Text = "&File";
            // 
            // openToolStripMenuItem
            // 
            openToolStripMenuItem.Name = "openToolStripMenuItem";
            openToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.O;
            openToolStripMenuItem.Size = new Size(185, 22);
            openToolStripMenuItem.Text = "&Open...";
            openToolStripMenuItem.Click += openToolStripMenuItem_Click;
            // 
            // saveToolStripMenuItem
            // 
            saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            saveToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.S;
            saveToolStripMenuItem.Size = new Size(185, 22);
            saveToolStripMenuItem.Text = "&Save User File";
            saveToolStripMenuItem.Click += saveToolStripMenuItem_Click;
            // 
            // recentToolStripMenuItem
            // 
            recentToolStripMenuItem.Name = "recentToolStripMenuItem";
            recentToolStripMenuItem.Size = new Size(185, 22);
            recentToolStripMenuItem.Text = "Recent Files";
            // 
            // editToolStripMenuItem
            // 
            editToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { undoToolStripMenuItem, redoToolStripMenuItem, new ToolStripSeparator(), renumberLocalIDsToolStripMenuItem });
            editToolStripMenuItem.Name = "editToolStripMenuItem";
            editToolStripMenuItem.Size = new Size(39, 20);
            editToolStripMenuItem.Text = "&Edit";
            // 
            // undoToolStripMenuItem
            // 
            undoToolStripMenuItem.Name = "undoToolStripMenuItem";
            undoToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.Z;
            undoToolStripMenuItem.Size = new Size(144, 22);
            undoToolStripMenuItem.Text = "Undo";
            undoToolStripMenuItem.Click += Undo_Click;
            // 
            // redoToolStripMenuItem
            // 
            redoToolStripMenuItem.Name = "redoToolStripMenuItem";
            redoToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.Y;
            redoToolStripMenuItem.Size = new Size(144, 22);
            redoToolStripMenuItem.Text = "Redo";
            redoToolStripMenuItem.Click += Redo_Click;
            // 
            // renumberLocalIDsToolStripMenuItem
            // 
            renumberLocalIDsToolStripMenuItem.Name = "renumberLocalIDsToolStripMenuItem";
            renumberLocalIDsToolStripMenuItem.Size = new Size(180, 22);
            renumberLocalIDsToolStripMenuItem.Text = "Renumber Local IDs";
            // 
            // splitMain
            // 
            splitMain.Dock = DockStyle.Fill;
            splitMain.Location = new Point(0, 24);
            splitMain.Name = "splitMain";
            // 
            // splitMain.Panel1
            // 
            splitMain.Panel1.Controls.Add(achievementGrid);
            splitMain.Panel1.Controls.Add(panelGridBottom);
            splitMain.Panel1.Controls.Add(panelGridTop);
            splitMain.Panel1MinSize = 240;
            // 
            // splitMain.Panel2
            // 
            splitMain.Panel2.Controls.Add(splitEditor);
            splitMain.Panel2MinSize = 600;
            splitMain.Size = new Size(1200, 650);
            splitMain.SplitterDistance = 260;
            splitMain.TabIndex = 1;
            // 
            // achievementGrid
            // 
            achievementGrid.AllowUserToAddRows = false;
            achievementGrid.AllowUserToDeleteRows = false;
            achievementGrid.AllowUserToResizeRows = false;
            achievementGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            achievementGrid.Columns.AddRange(new DataGridViewColumn[] { colID, colTitle });
            achievementGrid.ContextMenuStrip = assetContextMenu;
            achievementGrid.Dock = DockStyle.Fill;
            achievementGrid.Location = new Point(0, 60);
            achievementGrid.Name = "achievementGrid";
            achievementGrid.ReadOnly = true;
            achievementGrid.RowHeadersVisible = false;
            achievementGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            achievementGrid.Size = new Size(260, 555);
            achievementGrid.TabIndex = 0;
            achievementGrid.SelectionChanged += achievementGrid_SelectionChanged;
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
            // assetContextMenu
            // 
            assetContextMenu.Items.AddRange(new ToolStripItem[] { newAssetToolStripMenuItem, cloneAssetToolStripMenuItem, deleteAssetToolStripMenuItem, resetAssetToolStripMenuItem, toolStripSeparatorAsset, moveUpAssetToolStripMenuItem, moveDownAssetToolStripMenuItem, toolStripSeparatorRenumber, renumberContextToolStripMenuItem, viewDiffToolStripMenuItem });
            assetContextMenu.Name = "assetContextMenu";
            assetContextMenu.Size = new Size(200, 208);
            // 
            // newAssetToolStripMenuItem
            // 
            newAssetToolStripMenuItem.Name = "newAssetToolStripMenuItem";
            newAssetToolStripMenuItem.Size = new Size(199, 22);
            newAssetToolStripMenuItem.Text = "New";
            // 
            // cloneAssetToolStripMenuItem
            // 
            cloneAssetToolStripMenuItem.Name = "cloneAssetToolStripMenuItem";
            cloneAssetToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.D;
            cloneAssetToolStripMenuItem.Size = new Size(199, 22);
            cloneAssetToolStripMenuItem.Text = "Duplicate";
            // 
            // deleteAssetToolStripMenuItem
            // 
            deleteAssetToolStripMenuItem.Name = "deleteAssetToolStripMenuItem";
            deleteAssetToolStripMenuItem.ShortcutKeys = Keys.Delete;
            deleteAssetToolStripMenuItem.Size = new Size(199, 22);
            deleteAssetToolStripMenuItem.Text = "Delete";
            // 
            // resetAssetToolStripMenuItem
            // 
            resetAssetToolStripMenuItem.Name = "resetAssetToolStripMenuItem";
            resetAssetToolStripMenuItem.Size = new Size(199, 22);
            resetAssetToolStripMenuItem.Text = "Reset";
            // 
            // toolStripSeparatorAsset
            // 
            toolStripSeparatorAsset.Name = "toolStripSeparatorAsset";
            toolStripSeparatorAsset.Size = new Size(196, 6);
            // 
            // moveUpAssetToolStripMenuItem
            // 
            moveUpAssetToolStripMenuItem.Name = "moveUpAssetToolStripMenuItem";
            moveUpAssetToolStripMenuItem.ShortcutKeys = Keys.Alt | Keys.Up;
            moveUpAssetToolStripMenuItem.Size = new Size(199, 22);
            moveUpAssetToolStripMenuItem.Text = "Move Up";
            // 
            // moveDownAssetToolStripMenuItem
            // 
            moveDownAssetToolStripMenuItem.Name = "moveDownAssetToolStripMenuItem";
            moveDownAssetToolStripMenuItem.ShortcutKeys = Keys.Alt | Keys.Down;
            moveDownAssetToolStripMenuItem.Size = new Size(199, 22);
            moveDownAssetToolStripMenuItem.Text = "Move Down";
            // 
            // toolStripSeparatorRenumber
            // 
            toolStripSeparatorRenumber.Name = "toolStripSeparatorRenumber";
            toolStripSeparatorRenumber.Size = new Size(196, 6);
            // 
            // renumberContextToolStripMenuItem
            // 
            renumberContextToolStripMenuItem.Name = "renumberContextToolStripMenuItem";
            renumberContextToolStripMenuItem.Size = new Size(199, 22);
            renumberContextToolStripMenuItem.Text = "Renumber Local IDs";
            // 
            // viewDiffToolStripMenuItem
            // 
            viewDiffToolStripMenuItem.Name = "viewDiffToolStripMenuItem";
            viewDiffToolStripMenuItem.Size = new Size(199, 22);
            viewDiffToolStripMenuItem.Text = "View Diff (Server/Local)";
            viewDiffToolStripMenuItem.Click += viewDiffToolStripMenuItem_Click;
            // 
            // panelGridBottom
            // 
            panelGridBottom.Controls.Add(btnDeleteAsset);
            panelGridBottom.Controls.Add(btnDuplicateAsset);
            panelGridBottom.Controls.Add(btnAddAsset);
            panelGridBottom.Dock = DockStyle.Bottom;
            panelGridBottom.Location = new Point(0, 615);
            panelGridBottom.Name = "panelGridBottom";
            panelGridBottom.Size = new Size(260, 35);
            panelGridBottom.TabIndex = 2;
            // 
            // btnDeleteAsset
            // 
            btnDeleteAsset.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnDeleteAsset.Enabled = false;
            btnDeleteAsset.Location = new Point(182, 6);
            btnDeleteAsset.Name = "btnDeleteAsset";
            btnDeleteAsset.Size = new Size(75, 23);
            btnDeleteAsset.TabIndex = 2;
            btnDeleteAsset.Text = "Delete";
            btnDeleteAsset.UseVisualStyleBackColor = true;
            // 
            // btnDuplicateAsset
            // 
            btnDuplicateAsset.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnDuplicateAsset.Enabled = false;
            btnDuplicateAsset.Location = new Point(101, 6);
            btnDuplicateAsset.Name = "btnDuplicateAsset";
            btnDuplicateAsset.Size = new Size(75, 23);
            btnDuplicateAsset.TabIndex = 1;
            btnDuplicateAsset.Text = "Duplicate";
            btnDuplicateAsset.UseVisualStyleBackColor = true;
            // 
            // btnAddAsset
            // 
            btnAddAsset.Location = new Point(3, 6);
            btnAddAsset.Name = "btnAddAsset";
            btnAddAsset.Size = new Size(73, 23);
            btnAddAsset.TabIndex = 0;
            btnAddAsset.Text = "Add";
            btnAddAsset.UseVisualStyleBackColor = true;
            // 
            // panelGridTop
            // 
            panelGridTop.Controls.Add(txtAssetSearch);
            panelGridTop.Controls.Add(lblAssetSearch);
            panelGridTop.Controls.Add(cmbSource);
            panelGridTop.Controls.Add(cmbAssetType);
            panelGridTop.Dock = DockStyle.Top;
            panelGridTop.Location = new Point(0, 0);
            panelGridTop.Name = "panelGridTop";
            panelGridTop.Size = new Size(260, 60);
            panelGridTop.TabIndex = 1;
            // 
            // txtAssetSearch
            // 
            txtAssetSearch.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtAssetSearch.Location = new Point(55, 32);
            txtAssetSearch.Name = "txtAssetSearch";
            txtAssetSearch.Size = new Size(174, 23);
            txtAssetSearch.TabIndex = 3;
            // 
            // lblAssetSearch
            // 
            lblAssetSearch.AutoSize = true;
            lblAssetSearch.Location = new Point(3, 35);
            lblAssetSearch.Name = "lblAssetSearch";
            lblAssetSearch.Size = new Size(45, 15);
            lblAssetSearch.TabIndex = 2;
            lblAssetSearch.Text = "Search:";
            // 
            // cmbSource
            // 
            cmbSource.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbSource.FormattingEnabled = true;
            cmbSource.Items.AddRange(new object[] { "Local", "JSON", "New" });
            cmbSource.Location = new Point(149, 3);
            cmbSource.Name = "cmbSource";
            cmbSource.Size = new Size(80, 23);
            cmbSource.TabIndex = 1;
            // 
            // cmbAssetType
            // 
            cmbAssetType.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbAssetType.FormattingEnabled = true;
            cmbAssetType.Items.AddRange(new object[] { "Achievements", "Leaderboards" });
            cmbAssetType.Location = new Point(3, 3);
            cmbAssetType.Name = "cmbAssetType";
            cmbAssetType.Size = new Size(140, 23);
            cmbAssetType.TabIndex = 0;
            // 
            // splitEditor
            // 
            splitEditor.Dock = DockStyle.Fill;
            splitEditor.Location = new Point(0, 0);
            splitEditor.Name = "splitEditor";
            // 
            // splitEditor.Panel1
            // 
            splitEditor.Panel1.Controls.Add(triggerEditor);
            splitEditor.Panel1MinSize = 400;
            // 
            // splitEditor.Panel2
            // 
            splitEditor.Panel2.Controls.Add(codeNoteEditor);
            splitEditor.Panel2MinSize = 200;
            splitEditor.Size = new Size(936, 650);
            splitEditor.SplitterDistance = 703;
            splitEditor.TabIndex = 0;
            // 
            // triggerEditor
            // 
            triggerEditor.Dock = DockStyle.Fill;
            triggerEditor.GroupSeparator = 'S';
            triggerEditor.Location = new Point(0, 0);
            triggerEditor.Name = "triggerEditor";
            triggerEditor.Size = new Size(703, 650);
            triggerEditor.TabIndex = 0;
            // 
            // codeNoteEditor
            // 
            codeNoteEditor.Dock = DockStyle.Fill;
            codeNoteEditor.Location = new Point(0, 0);
            codeNoteEditor.Name = "codeNoteEditor";
            codeNoteEditor.Size = new Size(229, 650);
            codeNoteEditor.TabIndex = 0;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1200, 674);
            Controls.Add(splitMain);
            Controls.Add(mainMenu);
            MainMenuStrip = mainMenu;
            MinimumSize = new Size(1000, 650);
            Name = "MainForm";
            Text = "RA Logic Editor";
            mainMenu.ResumeLayout(false);
            mainMenu.PerformLayout();
            splitMain.Panel1.ResumeLayout(false);
            splitMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitMain).EndInit();
            splitMain.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)achievementGrid).EndInit();
            assetContextMenu.ResumeLayout(false);
            panelGridBottom.ResumeLayout(false);
            panelGridTop.ResumeLayout(false);
            panelGridTop.PerformLayout();
            splitEditor.Panel1.ResumeLayout(false);
            splitEditor.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitEditor).EndInit();
            splitEditor.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();

        }

        private System.Windows.Forms.MenuStrip mainMenu;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem recentToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem editToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem undoToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem redoToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem renumberLocalIDsToolStripMenuItem;
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
        private System.Windows.Forms.ToolStripSeparator toolStripSeparatorRenumber;
        private System.Windows.Forms.ToolStripMenuItem renumberContextToolStripMenuItem;
        private System.Windows.Forms.Panel panelGridBottom;
        private System.Windows.Forms.Button btnAddAsset;
        private System.Windows.Forms.Button btnDuplicateAsset;
        private System.Windows.Forms.Button btnDeleteAsset;
        private System.Windows.Forms.Label lblAssetSearch;
        private System.Windows.Forms.TextBox txtAssetSearch;
        private System.Windows.Forms.ToolStripMenuItem viewDiffToolStripMenuItem;
    }
}