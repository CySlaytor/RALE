namespace RALogicEditor.Controls
{
    partial class TriggerEditorControl
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
            this.panelHeader = new System.Windows.Forms.Panel();
            this.btnAddNote = new System.Windows.Forms.Button();
            this.btnReset = new System.Windows.Forms.Button();
            this.lvLeaderboardComponents = new System.Windows.Forms.ListView();
            this.colLbComp = new System.Windows.Forms.ColumnHeader();
            this.lblAuthor = new System.Windows.Forms.Label();
            this.cmbType = new System.Windows.Forms.ComboBox();
            this.cmbPoints = new System.Windows.Forms.ComboBox();
            this.txtDescription = new System.Windows.Forms.TextBox();
            this.txtTitle = new System.Windows.Forms.TextBox();
            this.pbBadge = new System.Windows.Forms.PictureBox();
            this.splitGroups = new System.Windows.Forms.SplitContainer();
            this.groupsList = new System.Windows.Forms.ListView();
            this.colGroups = new System.Windows.Forms.ColumnHeader();
            this.groupsContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.addGroupToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.removeGroupToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.conditionGrid = new RALogicEditor.Controls.DoubleBufferedDataGridView();
            this.gridContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.moveUpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.moveDownToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.addConditionToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.duplicateToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.deleteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.copyLogicToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copyRowToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pasteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statusStripEditor = new System.Windows.Forms.StatusStrip();
            this.lblEditorStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.panelHeader.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pbBadge)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitGroups)).BeginInit();
            this.splitGroups.Panel1.SuspendLayout();
            this.splitGroups.Panel2.SuspendLayout();
            this.splitGroups.SuspendLayout();
            this.groupsContextMenu.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.conditionGrid)).BeginInit();
            this.gridContextMenu.SuspendLayout();
            this.statusStripEditor.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelHeader
            // 
            this.panelHeader.BackColor = System.Drawing.SystemColors.Control;
            this.panelHeader.Controls.Add(this.btnAddNote);
            this.panelHeader.Controls.Add(this.btnReset);
            this.panelHeader.Controls.Add(this.lvLeaderboardComponents);
            this.panelHeader.Controls.Add(this.lblAuthor);
            this.panelHeader.Controls.Add(this.cmbType);
            this.panelHeader.Controls.Add(this.cmbPoints);
            this.panelHeader.Controls.Add(this.txtDescription);
            this.panelHeader.Controls.Add(this.txtTitle);
            this.panelHeader.Controls.Add(this.pbBadge);
            this.panelHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelHeader.Location = new System.Drawing.Point(0, 0);
            this.panelHeader.Name = "panelHeader";
            this.panelHeader.Padding = new System.Windows.Forms.Padding(5);
            this.panelHeader.Size = new System.Drawing.Size(700, 85);
            this.panelHeader.TabIndex = 0;
            // 
            // btnAddNote
            // 
            this.btnAddNote.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAddNote.Location = new System.Drawing.Point(537, 58);
            this.btnAddNote.Name = "btnAddNote";
            this.btnAddNote.Size = new System.Drawing.Size(75, 23);
            this.btnAddNote.TabIndex = 8;
            this.btnAddNote.Text = "Add Note";
            this.btnAddNote.UseVisualStyleBackColor = true;
            this.btnAddNote.Click += new System.EventHandler(this.btnAddNote_Click);
            // 
            // btnReset
            // 
            this.btnReset.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnReset.Location = new System.Drawing.Point(618, 58);
            this.btnReset.Name = "btnReset";
            this.btnReset.Size = new System.Drawing.Size(75, 23);
            this.btnReset.TabIndex = 7;
            this.btnReset.Text = "Reset";
            this.btnReset.UseVisualStyleBackColor = true;
            this.btnReset.Click += new System.EventHandler(this.btnReset_Click);
            // 
            // lvLeaderboardComponents
            // 
            this.lvLeaderboardComponents.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colLbComp});
            this.lvLeaderboardComponents.FullRowSelect = true;
            this.lvLeaderboardComponents.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.None;
            this.lvLeaderboardComponents.HideSelection = false;
            this.lvLeaderboardComponents.Location = new System.Drawing.Point(5, 5);
            this.lvLeaderboardComponents.MultiSelect = false;
            this.lvLeaderboardComponents.Name = "lvLeaderboardComponents";
            this.lvLeaderboardComponents.Scrollable = false;
            this.lvLeaderboardComponents.Size = new System.Drawing.Size(80, 75);
            this.lvLeaderboardComponents.TabIndex = 6;
            this.lvLeaderboardComponents.UseCompatibleStateImageBehavior = false;
            this.lvLeaderboardComponents.View = System.Windows.Forms.View.Details;
            this.lvLeaderboardComponents.Visible = false;
            // 
            // colLbComp
            // 
            this.colLbComp.Text = "Type";
            this.colLbComp.Width = 76;
            // 
            // lblAuthor
            // 
            this.lblAuthor.AutoSize = true;
            this.lblAuthor.ForeColor = System.Drawing.Color.Gray;
            this.lblAuthor.Location = new System.Drawing.Point(232, 63);
            this.lblAuthor.Name = "lblAuthor";
            this.lblAuthor.Size = new System.Drawing.Size(96, 15);
            this.lblAuthor.TabIndex = 5;
            this.lblAuthor.Text = "Author: Unknown";
            // 
            // cmbType
            // 
            this.cmbType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbType.FormattingEnabled = true;
            this.cmbType.Items.AddRange(new object[] {
            "",
            "Progression",
            "Win",
            "Missable"});
            this.cmbType.Location = new System.Drawing.Point(126, 59);
            this.cmbType.Name = "cmbType";
            this.cmbType.Size = new System.Drawing.Size(100, 23);
            this.cmbType.TabIndex = 4;
            // 
            // cmbPoints
            // 
            this.cmbPoints.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbPoints.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.cmbPoints.FormattingEnabled = true;
            this.cmbPoints.Items.AddRange(new object[] {
            "0",
            "1",
            "2",
            "3",
            "4",
            "5",
            "10",
            "25",
            "50",
            "100"});
            this.cmbPoints.Location = new System.Drawing.Point(75, 59);
            this.cmbPoints.Name = "cmbPoints";
            this.cmbPoints.Size = new System.Drawing.Size(45, 23);
            this.cmbPoints.TabIndex = 3;
            // 
            // txtDescription
            // 
            this.txtDescription.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtDescription.Location = new System.Drawing.Point(75, 34);
            this.txtDescription.Multiline = true;
            this.txtDescription.Name = "txtDescription";
            this.txtDescription.ScrollBars = System.Windows.Forms.ScrollBars.None; // Changed from Vertical to None
            this.txtDescription.Size = new System.Drawing.Size(617, 23);
            this.txtDescription.TabIndex = 2;
            // 
            // txtTitle
            // 
            this.txtTitle.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtTitle.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.txtTitle.Location = new System.Drawing.Point(75, 5);
            this.txtTitle.Name = "txtTitle";
            this.txtTitle.Size = new System.Drawing.Size(617, 27);
            this.txtTitle.TabIndex = 1;
            // 
            // pbBadge
            // 
            this.pbBadge.Dock = System.Windows.Forms.DockStyle.Left;
            this.pbBadge.Location = new System.Drawing.Point(5, 5);
            this.pbBadge.Name = "pbBadge";
            this.pbBadge.Size = new System.Drawing.Size(64, 75);
            this.pbBadge.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pbBadge.TabIndex = 0;
            this.pbBadge.TabStop = false;
            // 
            // splitGroups
            // 
            this.splitGroups.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitGroups.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitGroups.Location = new System.Drawing.Point(0, 85);
            this.splitGroups.Name = "splitGroups";
            // 
            // splitGroups.Panel1
            // 
            this.splitGroups.Panel1.Controls.Add(this.groupsList);
            // 
            // splitGroups.Panel2
            // 
            this.splitGroups.Panel2.Controls.Add(this.conditionGrid);
            this.splitGroups.Size = new System.Drawing.Size(700, 293);
            this.splitGroups.SplitterDistance = 75;
            this.splitGroups.SplitterWidth = 1;
            this.splitGroups.TabIndex = 1;
            // 
            // groupsList
            // 
            this.groupsList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colGroups});
            this.groupsList.ContextMenuStrip = this.groupsContextMenu;
            this.groupsList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupsList.FullRowSelect = true;
            this.groupsList.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.groupsList.HideSelection = false;
            this.groupsList.Location = new System.Drawing.Point(0, 0);
            this.groupsList.MultiSelect = false;
            this.groupsList.Name = "groupsList";
            this.groupsList.Size = new System.Drawing.Size(75, 293);
            this.groupsList.TabIndex = 0;
            this.groupsList.UseCompatibleStateImageBehavior = false;
            this.groupsList.View = System.Windows.Forms.View.Details;
            // 
            // colGroups
            // 
            this.colGroups.Text = "Groups";
            this.colGroups.Width = 70;
            // 
            // groupsContextMenu
            // 
            this.groupsContextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.addGroupToolStripMenuItem,
            this.removeGroupToolStripMenuItem});
            this.groupsContextMenu.Name = "groupsContextMenu";
            this.groupsContextMenu.Size = new System.Drawing.Size(154, 48);
            // 
            // addGroupToolStripMenuItem
            // 
            this.addGroupToolStripMenuItem.Name = "addGroupToolStripMenuItem";
            this.addGroupToolStripMenuItem.Size = new System.Drawing.Size(153, 22);
            this.addGroupToolStripMenuItem.Text = "Add Group";
            this.addGroupToolStripMenuItem.Click += new System.EventHandler(this.addGroupToolStripMenuItem_Click);
            // 
            // removeGroupToolStripMenuItem
            // 
            this.removeGroupToolStripMenuItem.Name = "removeGroupToolStripMenuItem";
            this.removeGroupToolStripMenuItem.Size = new System.Drawing.Size(153, 22);
            this.removeGroupToolStripMenuItem.Text = "Remove Group";
            this.removeGroupToolStripMenuItem.Click += new System.EventHandler(this.removeGroupToolStripMenuItem_Click);
            // 
            // conditionGrid
            // 
            this.conditionGrid.AllowUserToAddRows = false;
            this.conditionGrid.AllowUserToDeleteRows = false;
            this.conditionGrid.AllowUserToResizeRows = false;
            this.conditionGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.conditionGrid.ContextMenuStrip = this.gridContextMenu;
            this.conditionGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.conditionGrid.Location = new System.Drawing.Point(0, 0);
            this.conditionGrid.Name = "conditionGrid";
            this.conditionGrid.RowHeadersVisible = false;
            this.conditionGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.conditionGrid.Size = new System.Drawing.Size(624, 293);
            this.conditionGrid.TabIndex = 0;
            // 
            // gridContextMenu
            // 
            this.gridContextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.moveUpToolStripMenuItem,
            this.moveDownToolStripMenuItem,
            this.toolStripSeparator1,
            this.addConditionToolStripMenuItem,
            this.duplicateToolStripMenuItem,
            this.deleteToolStripMenuItem,
            this.toolStripSeparator2,
            this.copyLogicToolStripMenuItem,
            this.copyRowToolStripMenuItem,
            this.pasteToolStripMenuItem});
            this.gridContextMenu.Name = "gridContextMenu";
            this.gridContextMenu.Size = new System.Drawing.Size(153, 192);
            // 
            // moveUpToolStripMenuItem
            // 
            this.moveUpToolStripMenuItem.Name = "moveUpToolStripMenuItem";
            this.moveUpToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.Up)));
            this.moveUpToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.moveUpToolStripMenuItem.Text = "Move Up";
            this.moveUpToolStripMenuItem.Click += new System.EventHandler(this.moveUpToolStripMenuItem_Click);
            // 
            // moveDownToolStripMenuItem
            // 
            this.moveDownToolStripMenuItem.Name = "moveDownToolStripMenuItem";
            this.moveDownToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Alt | System.Windows.Forms.Keys.Down)));
            this.moveDownToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.moveDownToolStripMenuItem.Text = "Move Down";
            this.moveDownToolStripMenuItem.Click += new System.EventHandler(this.moveDownToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(149, 6);
            // 
            // addConditionToolStripMenuItem
            // 
            this.addConditionToolStripMenuItem.Name = "addConditionToolStripMenuItem";
            this.addConditionToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.addConditionToolStripMenuItem.Text = "Add Condition";
            this.addConditionToolStripMenuItem.Click += new System.EventHandler(this.addConditionToolStripMenuItem_Click);
            // 
            // duplicateToolStripMenuItem
            // 
            this.duplicateToolStripMenuItem.Name = "duplicateToolStripMenuItem";
            this.duplicateToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.D)));
            this.duplicateToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.duplicateToolStripMenuItem.Text = "Duplicate";
            this.duplicateToolStripMenuItem.Click += new System.EventHandler(this.duplicateToolStripMenuItem_Click);
            // 
            // deleteToolStripMenuItem
            // 
            this.deleteToolStripMenuItem.Name = "deleteToolStripMenuItem";
            this.deleteToolStripMenuItem.ShortcutKeys = System.Windows.Forms.Keys.Delete;
            this.deleteToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.deleteToolStripMenuItem.Text = "Delete";
            this.deleteToolStripMenuItem.Click += new System.EventHandler(this.deleteToolStripMenuItem_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(149, 6);
            // 
            // copyLogicToolStripMenuItem
            // 
            this.copyLogicToolStripMenuItem.Name = "copyLogicToolStripMenuItem";
            this.copyLogicToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift)
            | System.Windows.Forms.Keys.C)));
            this.copyLogicToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.copyLogicToolStripMenuItem.Text = "Copy Logic";
            this.copyLogicToolStripMenuItem.Click += new System.EventHandler(this.copyLogicToolStripMenuItem_Click);
            // 
            // copyRowToolStripMenuItem
            // 
            this.copyRowToolStripMenuItem.Name = "copyRowToolStripMenuItem";
            this.copyRowToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.C)));
            this.copyRowToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.copyRowToolStripMenuItem.Text = "Copy Row";
            this.copyRowToolStripMenuItem.Click += new System.EventHandler(this.copyRowToolStripMenuItem_Click);
            // 
            // pasteToolStripMenuItem
            // 
            this.pasteToolStripMenuItem.Name = "pasteToolStripMenuItem";
            this.pasteToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.V)));
            this.pasteToolStripMenuItem.Size = new System.Drawing.Size(152, 22);
            this.pasteToolStripMenuItem.Text = "Paste Logic";
            this.pasteToolStripMenuItem.Click += new System.EventHandler(this.pasteToolStripMenuItem_Click);
            // 
            // statusStripEditor
            // 
            this.statusStripEditor.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblEditorStatus});
            this.statusStripEditor.Location = new System.Drawing.Point(0, 378);
            this.statusStripEditor.Name = "statusStripEditor";
            this.statusStripEditor.Size = new System.Drawing.Size(700, 22);
            this.statusStripEditor.SizingGrip = false;
            this.statusStripEditor.TabIndex = 2;
            this.statusStripEditor.Text = "statusStrip1";
            // 
            // lblEditorStatus
            // 
            this.lblEditorStatus.Name = "lblEditorStatus";
            this.lblEditorStatus.Size = new System.Drawing.Size(39, 17);
            this.lblEditorStatus.Text = "Ready";
            // 
            // TriggerEditorControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.splitGroups);
            this.Controls.Add(this.statusStripEditor);
            this.Controls.Add(this.panelHeader);
            this.Name = "TriggerEditorControl";
            this.Size = new System.Drawing.Size(700, 400);
            this.panelHeader.ResumeLayout(false);
            this.panelHeader.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pbBadge)).EndInit();
            this.splitGroups.Panel1.ResumeLayout(false);
            this.splitGroups.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitGroups)).EndInit();
            this.splitGroups.ResumeLayout(false);
            this.groupsContextMenu.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.conditionGrid)).EndInit();
            this.gridContextMenu.ResumeLayout(false);
            this.statusStripEditor.ResumeLayout(false);
            this.statusStripEditor.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private System.Windows.Forms.Panel panelHeader;
        private System.Windows.Forms.PictureBox pbBadge;
        private System.Windows.Forms.TextBox txtTitle;
        private System.Windows.Forms.TextBox txtDescription;
        private System.Windows.Forms.ComboBox cmbPoints;
        private System.Windows.Forms.ComboBox cmbType;
        private System.Windows.Forms.Label lblAuthor;
        private System.Windows.Forms.SplitContainer splitGroups;
        private System.Windows.Forms.ListView groupsList;
        private System.Windows.Forms.ColumnHeader colGroups;
        private RALogicEditor.Controls.DoubleBufferedDataGridView conditionGrid;
        private System.Windows.Forms.ContextMenuStrip gridContextMenu;
        private System.Windows.Forms.ToolStripMenuItem moveUpToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem moveDownToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem addConditionToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem duplicateToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem deleteToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem copyLogicToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyRowToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pasteToolStripMenuItem;
        private System.Windows.Forms.ContextMenuStrip groupsContextMenu;
        private System.Windows.Forms.ToolStripMenuItem addGroupToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem removeGroupToolStripMenuItem;
        private System.Windows.Forms.ListView lvLeaderboardComponents;
        private System.Windows.Forms.ColumnHeader colLbComp;
        private System.Windows.Forms.Button btnReset;
        private System.Windows.Forms.Button btnAddNote;
        private System.Windows.Forms.StatusStrip statusStripEditor;
        private System.Windows.Forms.ToolStripStatusLabel lblEditorStatus;
    }
}