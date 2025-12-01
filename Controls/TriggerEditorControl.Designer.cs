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
            components = new System.ComponentModel.Container();
            panelHeader = new Panel();
            btnCopyDef = new Button();
            lblAssetState = new Label();
            btnTools = new Button();
            btnAddNote = new Button();
            btnReset = new Button();
            lvLeaderboardComponents = new ListView();
            colLbComp = new ColumnHeader();
            lblAuthor = new Label();
            cmbType = new ComboBox();
            cmbPoints = new ComboBox();
            txtDescription = new TextBox();
            txtTitle = new TextBox();
            pbBadge = new PictureBox();
            toolsContextMenu = new ContextMenuStrip(components);
            createGeneratorToolStripMenuItem = new ToolStripMenuItem();
            commitGeneratorToolStripMenuItem = new ToolStripMenuItem();
            multiRegionToolStripMenuItem = new ToolStripMenuItem();
            logicOptimizerToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator5 = new ToolStripSeparator();
            explainLogicToolStripMenuItem = new ToolStripMenuItem();
            splitGroups = new SplitContainer();
            groupsList = new ListView();
            colGroups = new ColumnHeader();
            groupsContextMenu = new ContextMenuStrip(components);
            addGroupToolStripMenuItem = new ToolStripMenuItem();
            removeGroupToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator4 = new ToolStripSeparator();
            removeAllAltGroupsToolStripMenuItem = new ToolStripMenuItem();
            panelGroupButtons = new Panel();
            btnRemoveGroup = new Button();
            btnAddGroup = new Button();
            conditionGrid = new DoubleBufferedDataGridView();
            gridContextMenu = new ContextMenuStrip(components);
            moveUpToolStripMenuItem = new ToolStripMenuItem();
            moveDownToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator1 = new ToolStripSeparator();
            addConditionToolStripMenuItem = new ToolStripMenuItem();
            duplicateToolStripMenuItem = new ToolStripMenuItem();
            deleteToolStripMenuItem = new ToolStripMenuItem();
            toolStripSeparator2 = new ToolStripSeparator();

            // New / Reordered Logic Items
            cutToolStripMenuItem = new ToolStripMenuItem();
            copyLogicToolStripMenuItem = new ToolStripMenuItem();
            copyRowToolStripMenuItem = new ToolStripMenuItem();
            pasteToolStripMenuItem = new ToolStripMenuItem();

            toolStripSeparator3 = new ToolStripSeparator();
            saveAsSnippetToolStripMenuItem = new ToolStripMenuItem();
            insertSnippetToolStripMenuItem = new ToolStripMenuItem();
            statusStripEditor = new StatusStrip();
            lblEditorStatus = new ToolStripStatusLabel();
            chkShowDecimal = new CheckBox();
            chkAliasMode = new CheckBox();
            chkCollapseChains = new CheckBox();
            panelHeader.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pbBadge).BeginInit();
            toolsContextMenu.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitGroups).BeginInit();
            splitGroups.Panel1.SuspendLayout();
            splitGroups.Panel2.SuspendLayout();
            splitGroups.SuspendLayout();
            groupsContextMenu.SuspendLayout();
            panelGroupButtons.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)conditionGrid).BeginInit();
            gridContextMenu.SuspendLayout();
            statusStripEditor.SuspendLayout();
            SuspendLayout();
            // 
            // panelHeader
            // 
            panelHeader.BackColor = SystemColors.Control;
            panelHeader.Controls.Add(btnCopyDef);
            panelHeader.Controls.Add(lblAssetState);
            panelHeader.Controls.Add(btnTools);
            panelHeader.Controls.Add(btnAddNote);
            panelHeader.Controls.Add(btnReset);
            panelHeader.Controls.Add(lvLeaderboardComponents);
            panelHeader.Controls.Add(lblAuthor);
            panelHeader.Controls.Add(cmbType);
            panelHeader.Controls.Add(cmbPoints);
            panelHeader.Controls.Add(txtDescription);
            panelHeader.Controls.Add(txtTitle);
            panelHeader.Controls.Add(pbBadge);
            panelHeader.Dock = DockStyle.Top;
            panelHeader.Location = new Point(0, 0);
            panelHeader.Name = "panelHeader";
            panelHeader.Padding = new Padding(5);
            panelHeader.Size = new Size(700, 85);
            panelHeader.TabIndex = 0;
            // 
            // btnCopyDef
            // 
            btnCopyDef.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnCopyDef.Location = new Point(375, 58);
            btnCopyDef.Name = "btnCopyDef";
            btnCopyDef.Size = new Size(75, 23);
            btnCopyDef.TabIndex = 11;
            btnCopyDef.Text = "Copy Def";
            btnCopyDef.UseVisualStyleBackColor = true;
            btnCopyDef.Click += btnCopyDef_Click;
            // 
            // lblAssetState
            // 
            lblAssetState.AutoSize = true;
            lblAssetState.Font = new Font("Segoe UI", 9F, FontStyle.Italic, GraphicsUnit.Point, 0);
            lblAssetState.ForeColor = Color.DimGray;
            lblAssetState.Location = new Point(350, 63);
            lblAssetState.Name = "lblAssetState";
            lblAssetState.Size = new Size(32, 15);
            lblAssetState.TabIndex = 10;
            lblAssetState.Text = "Core";
            // 
            // btnTools
            // 
            btnTools.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnTools.Location = new Point(456, 58);
            btnTools.Name = "btnTools";
            btnTools.Size = new Size(75, 23);
            btnTools.TabIndex = 9;
            btnTools.Text = "Tools ▼";
            btnTools.UseVisualStyleBackColor = true;
            btnTools.Click += btnTools_Click;
            // 
            // btnAddNote
            // 
            btnAddNote.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnAddNote.Location = new Point(537, 58);
            btnAddNote.Name = "btnAddNote";
            btnAddNote.Size = new Size(75, 23);
            btnAddNote.TabIndex = 8;
            btnAddNote.Text = "Add Note";
            btnAddNote.UseVisualStyleBackColor = true;
            btnAddNote.Click += btnAddNote_Click;
            // 
            // btnReset
            // 
            btnReset.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnReset.Location = new Point(618, 58);
            btnReset.Name = "btnReset";
            btnReset.Size = new Size(75, 23);
            btnReset.TabIndex = 7;
            btnReset.Text = "Reset";
            btnReset.UseVisualStyleBackColor = true;
            btnReset.Click += btnReset_Click;
            // 
            // lvLeaderboardComponents
            // 
            lvLeaderboardComponents.Columns.AddRange(new ColumnHeader[] { colLbComp });
            lvLeaderboardComponents.FullRowSelect = true;
            lvLeaderboardComponents.HeaderStyle = ColumnHeaderStyle.None;
            lvLeaderboardComponents.Location = new Point(5, 5);
            lvLeaderboardComponents.MultiSelect = false;
            lvLeaderboardComponents.Name = "lvLeaderboardComponents";
            lvLeaderboardComponents.Scrollable = false;
            lvLeaderboardComponents.Size = new Size(88, 100);
            lvLeaderboardComponents.TabIndex = 6;
            lvLeaderboardComponents.UseCompatibleStateImageBehavior = false;
            lvLeaderboardComponents.View = View.Details;
            lvLeaderboardComponents.Visible = false;
            // 
            // colLbComp
            // 
            colLbComp.Text = "Type";
            colLbComp.Width = 84;
            // 
            // lblAuthor
            // 
            lblAuthor.AutoSize = true;
            lblAuthor.ForeColor = Color.Gray;
            lblAuthor.Location = new Point(232, 63);
            lblAuthor.Name = "lblAuthor";
            lblAuthor.Size = new Size(101, 15);
            lblAuthor.TabIndex = 5;
            lblAuthor.Text = "Author: Unknown";
            // 
            // cmbType
            // 
            cmbType.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbType.FormattingEnabled = true;
            cmbType.Items.AddRange(new object[] { "", "Progression", "Win", "Missable" });
            cmbType.Location = new Point(126, 59);
            cmbType.Name = "cmbType";
            cmbType.Size = new Size(100, 23);
            cmbType.TabIndex = 4;
            // 
            // cmbPoints
            // 
            cmbPoints.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbPoints.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            cmbPoints.FormattingEnabled = true;
            cmbPoints.Items.AddRange(new object[] { "0", "1", "2", "3", "4", "5", "10", "25", "50", "100" });
            cmbPoints.Location = new Point(75, 59);
            cmbPoints.Name = "cmbPoints";
            cmbPoints.Size = new Size(45, 23);
            cmbPoints.TabIndex = 3;
            // 
            // txtDescription
            // 
            txtDescription.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtDescription.Location = new Point(75, 34);
            txtDescription.Name = "txtDescription";
            txtDescription.Size = new Size(617, 23);
            txtDescription.TabIndex = 2;
            // 
            // txtTitle
            // 
            txtTitle.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtTitle.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            txtTitle.Location = new Point(75, 5);
            txtTitle.Name = "txtTitle";
            txtTitle.Size = new Size(617, 27);
            txtTitle.TabIndex = 1;
            // 
            // pbBadge
            // 
            pbBadge.Dock = DockStyle.Left;
            pbBadge.Location = new Point(5, 5);
            pbBadge.Name = "pbBadge";
            pbBadge.Size = new Size(64, 75);
            pbBadge.SizeMode = PictureBoxSizeMode.Zoom;
            pbBadge.TabIndex = 0;
            pbBadge.TabStop = false;
            // 
            // toolsContextMenu
            // 
            toolsContextMenu.Items.AddRange(new ToolStripItem[] { createGeneratorToolStripMenuItem, commitGeneratorToolStripMenuItem, multiRegionToolStripMenuItem, logicOptimizerToolStripMenuItem, toolStripSeparator5, explainLogicToolStripMenuItem });
            toolsContextMenu.Name = "toolsContextMenu";
            toolsContextMenu.Size = new Size(221, 142);
            // 
            // createGeneratorToolStripMenuItem
            // 
            createGeneratorToolStripMenuItem.Name = "createGeneratorToolStripMenuItem";
            createGeneratorToolStripMenuItem.Size = new Size(220, 22);
            createGeneratorToolStripMenuItem.Text = "Create Generator...";
            createGeneratorToolStripMenuItem.Click += createGeneratorToolStripMenuItem_Click;
            // 
            // commitGeneratorToolStripMenuItem
            // 
            commitGeneratorToolStripMenuItem.Name = "commitGeneratorToolStripMenuItem";
            commitGeneratorToolStripMenuItem.Size = new Size(220, 22);
            commitGeneratorToolStripMenuItem.Text = "Commit Generator (Bake)";
            commitGeneratorToolStripMenuItem.Click += commitGeneratorToolStripMenuItem_Click;
            // 
            // multiRegionToolStripMenuItem
            // 
            multiRegionToolStripMenuItem.Name = "multiRegionToolStripMenuItem";
            multiRegionToolStripMenuItem.Size = new Size(220, 22);
            multiRegionToolStripMenuItem.Text = "Multi-Region Support...";
            multiRegionToolStripMenuItem.Click += multiRegionToolStripMenuItem_Click;
            // 
            // logicOptimizerToolStripMenuItem
            // 
            logicOptimizerToolStripMenuItem.Name = "logicOptimizerToolStripMenuItem";
            logicOptimizerToolStripMenuItem.Size = new Size(220, 22);
            logicOptimizerToolStripMenuItem.Text = "Logic Optimizer (Length)...";
            logicOptimizerToolStripMenuItem.Click += logicOptimizerToolStripMenuItem_Click;
            // 
            // toolStripSeparator5
            // 
            toolStripSeparator5.Name = "toolStripSeparator5";
            toolStripSeparator5.Size = new Size(217, 6);
            // 
            // explainLogicToolStripMenuItem
            // 
            explainLogicToolStripMenuItem.Name = "explainLogicToolStripMenuItem";
            explainLogicToolStripMenuItem.Size = new Size(220, 22);
            explainLogicToolStripMenuItem.Text = "Explain Logic (Preview)";
            explainLogicToolStripMenuItem.Click += explainLogicToolStripMenuItem_Click;
            // 
            // splitGroups
            // 
            splitGroups.Dock = DockStyle.Fill;
            splitGroups.FixedPanel = FixedPanel.Panel1;
            splitGroups.Location = new Point(0, 85);
            splitGroups.Name = "splitGroups";
            // 
            // splitGroups.Panel1
            // 
            splitGroups.Panel1.Controls.Add(groupsList);
            splitGroups.Panel1.Controls.Add(panelGroupButtons);
            splitGroups.Panel1MinSize = 65;
            // 
            // splitGroups.Panel2
            // 
            splitGroups.Panel2.Controls.Add(conditionGrid);
            splitGroups.Size = new Size(700, 293);
            splitGroups.SplitterDistance = 65;
            splitGroups.SplitterWidth = 1;
            splitGroups.TabIndex = 1;
            // 
            // groupsList
            // 
            groupsList.Columns.AddRange(new ColumnHeader[] { colGroups });
            groupsList.ContextMenuStrip = groupsContextMenu;
            groupsList.Dock = DockStyle.Fill;
            groupsList.FullRowSelect = true;
            groupsList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            groupsList.Location = new Point(0, 0);
            groupsList.MultiSelect = false;
            groupsList.Name = "groupsList";
            groupsList.Size = new Size(65, 271);
            groupsList.TabIndex = 0;
            groupsList.UseCompatibleStateImageBehavior = false;
            groupsList.View = View.Details;
            groupsList.SelectedIndexChanged += GroupsList_SelectedIndexChanged;
            // 
            // colGroups
            // 
            colGroups.Text = "Groups";
            colGroups.Width = 66;
            // 
            // groupsContextMenu
            // 
            groupsContextMenu.Items.AddRange(new ToolStripItem[] { addGroupToolStripMenuItem, removeGroupToolStripMenuItem, toolStripSeparator4, removeAllAltGroupsToolStripMenuItem });
            groupsContextMenu.Name = "groupsContextMenu";
            groupsContextMenu.Size = new Size(194, 76);
            // 
            // addGroupToolStripMenuItem
            // 
            addGroupToolStripMenuItem.Name = "addGroupToolStripMenuItem";
            addGroupToolStripMenuItem.Size = new Size(193, 22);
            addGroupToolStripMenuItem.Text = "Add Group";
            addGroupToolStripMenuItem.Click += addGroupToolStripMenuItem_Click;
            // 
            // removeGroupToolStripMenuItem
            // 
            removeGroupToolStripMenuItem.Name = "removeGroupToolStripMenuItem";
            removeGroupToolStripMenuItem.Size = new Size(193, 22);
            removeGroupToolStripMenuItem.Text = "Remove Group";
            removeGroupToolStripMenuItem.Click += removeGroupToolStripMenuItem_Click;
            // 
            // toolStripSeparator4
            // 
            toolStripSeparator4.Name = "toolStripSeparator4";
            toolStripSeparator4.Size = new Size(190, 6);
            // 
            // removeAllAltGroupsToolStripMenuItem
            // 
            removeAllAltGroupsToolStripMenuItem.Name = "removeAllAltGroupsToolStripMenuItem";
            removeAllAltGroupsToolStripMenuItem.Size = new Size(193, 22);
            removeAllAltGroupsToolStripMenuItem.Text = "Remove All Alt Groups";
            removeAllAltGroupsToolStripMenuItem.Click += removeAllAltGroupsToolStripMenuItem_Click;
            // 
            // panelGroupButtons
            // 
            panelGroupButtons.Controls.Add(btnRemoveGroup);
            panelGroupButtons.Controls.Add(btnAddGroup);
            panelGroupButtons.Dock = DockStyle.Bottom;
            panelGroupButtons.Location = new Point(0, 271);
            panelGroupButtons.Name = "panelGroupButtons";
            panelGroupButtons.Size = new Size(65, 22);
            panelGroupButtons.TabIndex = 1;
            // 
            // btnRemoveGroup
            // 
            btnRemoveGroup.Dock = DockStyle.Right;
            btnRemoveGroup.Location = new Point(40, 0);
            btnRemoveGroup.Name = "btnRemoveGroup";
            btnRemoveGroup.Size = new Size(25, 22);
            btnRemoveGroup.TabIndex = 1;
            btnRemoveGroup.Text = "-";
            btnRemoveGroup.UseVisualStyleBackColor = true;
            btnRemoveGroup.Click += removeGroupToolStripMenuItem_Click;
            // 
            // btnAddGroup
            // 
            btnAddGroup.Dock = DockStyle.Left;
            btnAddGroup.Location = new Point(0, 0);
            btnAddGroup.Name = "btnAddGroup";
            btnAddGroup.Size = new Size(25, 22);
            btnAddGroup.TabIndex = 0;
            btnAddGroup.Text = "+";
            btnAddGroup.UseVisualStyleBackColor = true;
            btnAddGroup.Click += addGroupToolStripMenuItem_Click;
            // 
            // conditionGrid
            // 
            conditionGrid.AllowUserToAddRows = false;
            conditionGrid.AllowUserToDeleteRows = false;
            conditionGrid.AllowUserToResizeRows = false;
            conditionGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            conditionGrid.ContextMenuStrip = gridContextMenu;
            conditionGrid.Dock = DockStyle.Fill;
            conditionGrid.Location = new Point(0, 0);
            conditionGrid.Name = "conditionGrid";
            conditionGrid.RowHeadersVisible = true;
            conditionGrid.RowHeadersWidth = 25;
            conditionGrid.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            conditionGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            conditionGrid.Size = new Size(634, 293);
            conditionGrid.TabIndex = 0;
            // 
            // gridContextMenu
            // 
            gridContextMenu.Items.AddRange(new ToolStripItem[] { moveUpToolStripMenuItem, moveDownToolStripMenuItem, toolStripSeparator1, addConditionToolStripMenuItem, duplicateToolStripMenuItem, deleteToolStripMenuItem, toolStripSeparator2, cutToolStripMenuItem, copyLogicToolStripMenuItem, copyRowToolStripMenuItem, pasteToolStripMenuItem, toolStripSeparator3, saveAsSnippetToolStripMenuItem, insertSnippetToolStripMenuItem });
            gridContextMenu.Name = "gridContextMenu";
            gridContextMenu.Size = new Size(209, 270);
            // 
            // moveUpToolStripMenuItem
            // 
            moveUpToolStripMenuItem.Name = "moveUpToolStripMenuItem";
            moveUpToolStripMenuItem.ShortcutKeys = Keys.Alt | Keys.Up;
            moveUpToolStripMenuItem.Size = new Size(208, 22);
            moveUpToolStripMenuItem.Text = "Move Up";
            moveUpToolStripMenuItem.Click += moveUpToolStripMenuItem_Click;
            // 
            // moveDownToolStripMenuItem
            // 
            moveDownToolStripMenuItem.Name = "moveDownToolStripMenuItem";
            moveDownToolStripMenuItem.ShortcutKeys = Keys.Alt | Keys.Down;
            moveDownToolStripMenuItem.Size = new Size(208, 22);
            moveDownToolStripMenuItem.Text = "Move Down";
            moveDownToolStripMenuItem.Click += moveDownToolStripMenuItem_Click;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(205, 6);
            // 
            // addConditionToolStripMenuItem
            // 
            addConditionToolStripMenuItem.Name = "addConditionToolStripMenuItem";
            addConditionToolStripMenuItem.Size = new Size(208, 22);
            addConditionToolStripMenuItem.Text = "Add Condition";
            addConditionToolStripMenuItem.Click += addConditionToolStripMenuItem_Click;
            // 
            // duplicateToolStripMenuItem
            // 
            duplicateToolStripMenuItem.Name = "duplicateToolStripMenuItem";
            duplicateToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.D;
            duplicateToolStripMenuItem.Size = new Size(208, 22);
            duplicateToolStripMenuItem.Text = "Duplicate";
            duplicateToolStripMenuItem.Click += duplicateToolStripMenuItem_Click;
            // 
            // deleteToolStripMenuItem
            // 
            deleteToolStripMenuItem.Name = "deleteToolStripMenuItem";
            deleteToolStripMenuItem.ShortcutKeys = Keys.Delete;
            deleteToolStripMenuItem.Size = new Size(208, 22);
            deleteToolStripMenuItem.Text = "Delete";
            deleteToolStripMenuItem.Click += deleteToolStripMenuItem_Click;
            // 
            // toolStripSeparator2
            // 
            toolStripSeparator2.Name = "toolStripSeparator2";
            toolStripSeparator2.Size = new Size(205, 6);
            // 
            // cutToolStripMenuItem
            // 
            cutToolStripMenuItem.Name = "cutToolStripMenuItem";
            cutToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.X;
            cutToolStripMenuItem.Size = new Size(208, 22);
            cutToolStripMenuItem.Text = "Cut Logic";
            cutToolStripMenuItem.Click += cutToolStripMenuItem_Click;
            // 
            // copyLogicToolStripMenuItem
            // 
            copyLogicToolStripMenuItem.Name = "copyLogicToolStripMenuItem";
            copyLogicToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.C;
            copyLogicToolStripMenuItem.Size = new Size(208, 22);
            copyLogicToolStripMenuItem.Text = "Copy Logic";
            copyLogicToolStripMenuItem.Click += copyLogicToolStripMenuItem_Click;
            // 
            // copyRowToolStripMenuItem
            // 
            copyRowToolStripMenuItem.Name = "copyRowToolStripMenuItem";
            copyRowToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.C;
            copyRowToolStripMenuItem.Size = new Size(208, 22);
            copyRowToolStripMenuItem.Text = "Copy Row";
            copyRowToolStripMenuItem.Click += copyRowToolStripMenuItem_Click;
            // 
            // pasteToolStripMenuItem
            // 
            pasteToolStripMenuItem.Name = "pasteToolStripMenuItem";
            pasteToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.V;
            pasteToolStripMenuItem.Size = new Size(208, 22);
            pasteToolStripMenuItem.Text = "Paste Logic";
            pasteToolStripMenuItem.Click += pasteToolStripMenuItem_Click;
            // 
            // toolStripSeparator3
            // 
            toolStripSeparator3.Name = "toolStripSeparator3";
            toolStripSeparator3.Size = new Size(205, 6);
            // 
            // saveAsSnippetToolStripMenuItem
            // 
            saveAsSnippetToolStripMenuItem.Name = "saveAsSnippetToolStripMenuItem";
            saveAsSnippetToolStripMenuItem.Size = new Size(208, 22);
            saveAsSnippetToolStripMenuItem.Text = "Save Selection as Snippet";
            saveAsSnippetToolStripMenuItem.Click += saveAsSnippetToolStripMenuItem_Click;
            // 
            // insertSnippetToolStripMenuItem
            // 
            insertSnippetToolStripMenuItem.Name = "insertSnippetToolStripMenuItem";
            insertSnippetToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.I;
            insertSnippetToolStripMenuItem.Size = new Size(208, 22);
            insertSnippetToolStripMenuItem.Text = "Insert Snippet...";
            insertSnippetToolStripMenuItem.Click += insertSnippetToolStripMenuItem_Click;
            // 
            // statusStripEditor
            // 
            statusStripEditor.Items.AddRange(new ToolStripItem[] { lblEditorStatus });
            statusStripEditor.Location = new Point(0, 378);
            statusStripEditor.Name = "statusStripEditor";
            statusStripEditor.Size = new Size(700, 22);
            statusStripEditor.SizingGrip = false;
            statusStripEditor.TabIndex = 2;
            statusStripEditor.Text = "statusStrip1";
            // 
            // lblEditorStatus
            // 
            lblEditorStatus.Name = "lblEditorStatus";
            lblEditorStatus.Size = new Size(39, 17);
            lblEditorStatus.Text = "Ready";
            // 
            // chkShowDecimal
            // 
            chkShowDecimal.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            chkShowDecimal.AutoSize = true;
            chkShowDecimal.BackColor = SystemColors.Control;
            chkShowDecimal.Location = new Point(580, 381);
            chkShowDecimal.Name = "chkShowDecimal";
            chkShowDecimal.Size = new Size(101, 19);
            chkShowDecimal.TabIndex = 9;
            chkShowDecimal.Text = "Show Decimal";
            chkShowDecimal.UseVisualStyleBackColor = true;
            // 
            // chkAliasMode
            // 
            chkAliasMode.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            chkAliasMode.AutoSize = true;
            chkAliasMode.BackColor = SystemColors.Control;
            chkAliasMode.Location = new Point(481, 381);
            chkAliasMode.Name = "chkAliasMode";
            chkAliasMode.Size = new Size(94, 19);
            chkAliasMode.TabIndex = 10;
            chkAliasMode.Text = "Show Aliases";
            chkAliasMode.UseVisualStyleBackColor = true;
            // 
            // chkCollapseChains
            // 
            chkCollapseChains.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            chkCollapseChains.AutoSize = true;
            chkCollapseChains.BackColor = SystemColors.Control;
            chkCollapseChains.Location = new Point(362, 381);
            chkCollapseChains.Name = "chkCollapseChains";
            chkCollapseChains.Size = new Size(113, 19);
            chkCollapseChains.TabIndex = 11;
            chkCollapseChains.Text = "Collapse Chains";
            chkCollapseChains.UseVisualStyleBackColor = true;
            // 
            // TriggerEditorControl
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(chkCollapseChains);
            Controls.Add(chkAliasMode);
            Controls.Add(chkShowDecimal);
            Controls.Add(splitGroups);
            Controls.Add(statusStripEditor);
            Controls.Add(panelHeader);
            Name = "TriggerEditorControl";
            Size = new Size(700, 400);
            panelHeader.ResumeLayout(false);
            panelHeader.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)pbBadge).EndInit();
            toolsContextMenu.ResumeLayout(false);
            splitGroups.Panel1.ResumeLayout(false);
            splitGroups.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitGroups).EndInit();
            splitGroups.ResumeLayout(false);
            groupsContextMenu.ResumeLayout(false);
            panelGroupButtons.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)conditionGrid).EndInit();
            gridContextMenu.ResumeLayout(false);
            statusStripEditor.ResumeLayout(false);
            statusStripEditor.PerformLayout();
            ResumeLayout(false);
            PerformLayout();

        }

        private System.Windows.Forms.Panel panelHeader = null!;
        private System.Windows.Forms.PictureBox pbBadge = null!;
        private System.Windows.Forms.TextBox txtTitle = null!;
        private System.Windows.Forms.TextBox txtDescription = null!;
        private System.Windows.Forms.ComboBox cmbPoints = null!;
        private System.Windows.Forms.ComboBox cmbType = null!;
        private System.Windows.Forms.Label lblAuthor = null!;
        private System.Windows.Forms.Label lblAssetState = null!;
        private System.Windows.Forms.SplitContainer splitGroups = null!;
        private System.Windows.Forms.ListView groupsList = null!;
        private System.Windows.Forms.ColumnHeader colGroups = null!;
        private RALogicEditor.Controls.DoubleBufferedDataGridView conditionGrid = null!;
        private System.Windows.Forms.ContextMenuStrip gridContextMenu = null!;
        private System.Windows.Forms.ToolStripMenuItem moveUpToolStripMenuItem = null!;
        private System.Windows.Forms.ToolStripMenuItem moveDownToolStripMenuItem = null!;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1 = null!;
        private System.Windows.Forms.ToolStripMenuItem addConditionToolStripMenuItem = null!;
        private System.Windows.Forms.ToolStripMenuItem duplicateToolStripMenuItem = null!;
        private System.Windows.Forms.ToolStripMenuItem deleteToolStripMenuItem = null!;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2 = null!;
        private System.Windows.Forms.ToolStripMenuItem copyLogicToolStripMenuItem = null!;
        private System.Windows.Forms.ToolStripMenuItem copyRowToolStripMenuItem = null!;
        private System.Windows.Forms.ToolStripMenuItem cutToolStripMenuItem = null!; // New
        private System.Windows.Forms.ToolStripMenuItem pasteToolStripMenuItem = null!;
        private System.Windows.Forms.ContextMenuStrip groupsContextMenu = null!;
        private System.Windows.Forms.ToolStripMenuItem addGroupToolStripMenuItem = null!;
        private System.Windows.Forms.ToolStripMenuItem removeGroupToolStripMenuItem = null!;
        private System.Windows.Forms.ListView lvLeaderboardComponents = null!;
        private System.Windows.Forms.ColumnHeader colLbComp = null!;
        private System.Windows.Forms.Button btnReset = null!;
        private System.Windows.Forms.Button btnAddNote = null!;
        private System.Windows.Forms.StatusStrip statusStripEditor = null!;
        private System.Windows.Forms.ToolStripStatusLabel lblEditorStatus = null!;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3 = null!;
        private System.Windows.Forms.ToolStripMenuItem createGeneratorToolStripMenuItem = null!;
        private System.Windows.Forms.ToolStripMenuItem commitGeneratorToolStripMenuItem = null!;
        private System.Windows.Forms.CheckBox chkShowDecimal = null!;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator4 = null!;
        private System.Windows.Forms.ToolStripMenuItem removeAllAltGroupsToolStripMenuItem = null!;
        private System.Windows.Forms.Panel panelGroupButtons = null!;
        private System.Windows.Forms.Button btnRemoveGroup = null!;
        private System.Windows.Forms.Button btnAddGroup = null!;
        private System.Windows.Forms.CheckBox chkAliasMode = null!;
        private System.Windows.Forms.Button btnTools = null!;
        private System.Windows.Forms.ContextMenuStrip toolsContextMenu = null!;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator5 = null!;
        private System.Windows.Forms.ToolStripMenuItem saveAsSnippetToolStripMenuItem = null!;
        private System.Windows.Forms.ToolStripMenuItem insertSnippetToolStripMenuItem = null!;
        private System.Windows.Forms.ToolStripMenuItem explainLogicToolStripMenuItem = null!;
        private System.Windows.Forms.Button btnCopyDef = null!;
        private System.Windows.Forms.CheckBox chkCollapseChains = null!;
        private System.Windows.Forms.ToolStripMenuItem multiRegionToolStripMenuItem = null!;
        private System.Windows.Forms.ToolStripMenuItem logicOptimizerToolStripMenuItem = null!;
    }
}