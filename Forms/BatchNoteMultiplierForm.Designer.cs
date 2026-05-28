namespace RALogicEditor.Forms
{
    partial class BatchNoteMultiplierForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(BatchNoteMultiplierForm));
            splitMain = new SplitContainer();
            clbNotes = new CheckedListBox();
            panelFilter = new Panel();
            btnSelectNone = new Button();
            btnSelectAll = new Button();
            txtFilter = new TextBox();
            lblFilter = new Label();
            groupBoxSettings = new GroupBox();
            lblStartNum = new Label();
            txtStartNum = new TextBox();
            lblReplaceHint = new Label();
            txtReplacePattern = new TextBox();
            lblReplacePattern = new Label();
            txtSearchKeyword = new TextBox();
            lblSearchKeyword = new Label();
            txtStride = new TextBox();
            lblStride = new Label();
            txtCopies = new TextBox();
            lblCopies = new Label();
            btnCancel = new Button();
            btnOK = new Button();
            ((System.ComponentModel.ISupportInitialize)splitMain).BeginInit();
            splitMain.Panel1.SuspendLayout();
            splitMain.Panel2.SuspendLayout();
            splitMain.SuspendLayout();
            panelFilter.SuspendLayout();
            groupBoxSettings.SuspendLayout();
            SuspendLayout();
            // 
            // splitMain
            // 
            splitMain.Dock = DockStyle.Fill;
            splitMain.FixedPanel = FixedPanel.Panel2;
            splitMain.Location = new Point(0, 0);
            splitMain.Name = "splitMain";
            // 
            // splitMain.Panel1
            // 
            splitMain.Panel1.Controls.Add(clbNotes);
            splitMain.Panel1.Controls.Add(panelFilter);
            splitMain.Panel1.Padding = new Padding(5);
            // 
            // splitMain.Panel2
            // 
            splitMain.Panel2.Controls.Add(groupBoxSettings);
            splitMain.Panel2.Controls.Add(btnCancel);
            splitMain.Panel2.Controls.Add(btnOK);
            splitMain.Panel2.Padding = new Padding(5);
            splitMain.Size = new Size(684, 361);
            splitMain.SplitterDistance = 384;
            splitMain.TabIndex = 0;
            // 
            // clbNotes
            // 
            clbNotes.CheckOnClick = true;
            clbNotes.Dock = DockStyle.Fill;
            clbNotes.FormattingEnabled = true;
            clbNotes.Location = new Point(5, 60);
            clbNotes.Name = "clbNotes";
            clbNotes.Size = new Size(374, 296);
            clbNotes.TabIndex = 1;
            // 
            // panelFilter
            // 
            panelFilter.Controls.Add(btnSelectNone);
            panelFilter.Controls.Add(btnSelectAll);
            panelFilter.Controls.Add(txtFilter);
            panelFilter.Controls.Add(lblFilter);
            panelFilter.Dock = DockStyle.Top;
            panelFilter.Location = new Point(5, 5);
            panelFilter.Name = "panelFilter";
            panelFilter.Size = new Size(374, 55);
            panelFilter.TabIndex = 0;
            // 
            // btnSelectNone
            // 
            btnSelectNone.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSelectNone.Location = new Point(286, 26);
            btnSelectNone.Name = "btnSelectNone";
            btnSelectNone.Size = new Size(85, 23);
            btnSelectNone.TabIndex = 3;
            btnSelectNone.Text = "Select None";
            btnSelectNone.UseVisualStyleBackColor = true;
            btnSelectNone.Click += btnSelectNone_Click;
            // 
            // btnSelectAll
            // 
            btnSelectAll.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnSelectAll.Location = new Point(205, 26);
            btnSelectAll.Name = "btnSelectAll";
            btnSelectAll.Size = new Size(75, 23);
            btnSelectAll.TabIndex = 2;
            btnSelectAll.Text = "Select All";
            btnSelectAll.UseVisualStyleBackColor = true;
            btnSelectAll.Click += btnSelectAll_Click;
            // 
            // txtFilter
            // 
            txtFilter.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtFilter.Location = new Point(82, 3);
            txtFilter.Name = "txtFilter";
            txtFilter.Size = new Size(289, 23);
            txtFilter.TabIndex = 1;
            txtFilter.TextChanged += txtFilter_TextChanged;
            // 
            // lblFilter
            // 
            lblFilter.AutoSize = true;
            lblFilter.Location = new Point(3, 6);
            lblFilter.Name = "lblFilter";
            lblFilter.Size = new Size(70, 15);
            lblFilter.TabIndex = 0;
            lblFilter.Text = "Filter Notes:";
            // 
            // groupBoxSettings
            // 
            groupBoxSettings.Controls.Add(lblStartNum);
            groupBoxSettings.Controls.Add(txtStartNum);
            groupBoxSettings.Controls.Add(lblReplaceHint);
            groupBoxSettings.Controls.Add(txtReplacePattern);
            groupBoxSettings.Controls.Add(lblReplacePattern);
            groupBoxSettings.Controls.Add(txtSearchKeyword);
            groupBoxSettings.Controls.Add(lblSearchKeyword);
            groupBoxSettings.Controls.Add(txtStride);
            groupBoxSettings.Controls.Add(lblStride);
            groupBoxSettings.Controls.Add(txtCopies);
            groupBoxSettings.Controls.Add(lblCopies);
            groupBoxSettings.Dock = DockStyle.Top;
            groupBoxSettings.Location = new Point(5, 5);
            groupBoxSettings.Name = "groupBoxSettings";
            groupBoxSettings.Size = new Size(286, 230);
            groupBoxSettings.TabIndex = 0;
            groupBoxSettings.TabStop = false;
            groupBoxSettings.Text = "Multiplication Settings";
            // 
            // lblStartNum
            // 
            lblStartNum.AutoSize = true;
            lblStartNum.Location = new Point(15, 87);
            lblStartNum.Name = "lblStartNum";
            lblStartNum.Size = new Size(98, 15);
            lblStartNum.TabIndex = 4;
            lblStartNum.Text = "Starting Number:";
            // 
            // txtStartNum
            // 
            txtStartNum.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtStartNum.Location = new Point(120, 84);
            txtStartNum.Name = "txtStartNum";
            txtStartNum.Size = new Size(150, 23);
            txtStartNum.TabIndex = 5;
            txtStartNum.Text = "2";
            // 
            // lblReplaceHint
            // 
            lblReplaceHint.AutoSize = true;
            lblReplaceHint.ForeColor = SystemColors.GrayText;
            lblReplaceHint.Location = new Point(120, 199);
            lblReplaceHint.Name = "lblReplaceHint";
            lblReplaceHint.Size = new Size(139, 15);
            lblReplaceHint.TabIndex = 10;
            lblReplaceHint.Text = "(Use {N} for slot number)";
            // 
            // txtReplacePattern
            // 
            txtReplacePattern.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtReplacePattern.Location = new Point(120, 173);
            txtReplacePattern.Name = "txtReplacePattern";
            txtReplacePattern.Size = new Size(150, 23);
            txtReplacePattern.TabIndex = 9;
            // 
            // lblReplacePattern
            // 
            lblReplacePattern.AutoSize = true;
            lblReplacePattern.Location = new Point(15, 176);
            lblReplacePattern.Name = "lblReplacePattern";
            lblReplacePattern.Size = new Size(79, 15);
            lblReplacePattern.TabIndex = 8;
            lblReplacePattern.Text = "Replace With:";
            // 
            // txtSearchKeyword
            // 
            txtSearchKeyword.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtSearchKeyword.Location = new Point(120, 144);
            txtSearchKeyword.Name = "txtSearchKeyword";
            txtSearchKeyword.Size = new Size(150, 23);
            txtSearchKeyword.TabIndex = 7;
            // 
            // lblSearchKeyword
            // 
            lblSearchKeyword.AutoSize = true;
            lblSearchKeyword.Location = new Point(15, 147);
            lblSearchKeyword.Name = "lblSearchKeyword";
            lblSearchKeyword.Size = new Size(94, 15);
            lblSearchKeyword.TabIndex = 6;
            lblSearchKeyword.Text = "Search Keyword:";
            // 
            // txtStride
            // 
            txtStride.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtStride.Location = new Point(120, 55);
            txtStride.Name = "txtStride";
            txtStride.Size = new Size(150, 23);
            txtStride.TabIndex = 3;
            txtStride.Text = "0x0";
            // 
            // lblStride
            // 
            lblStride.AutoSize = true;
            lblStride.Location = new Point(15, 58);
            lblStride.Name = "lblStride";
            lblStride.Size = new Size(72, 15);
            lblStride.TabIndex = 2;
            lblStride.Text = "Stride (Hex):";
            // 
            // txtCopies
            // 
            txtCopies.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtCopies.Location = new Point(120, 26);
            txtCopies.Name = "txtCopies";
            txtCopies.Size = new Size(150, 23);
            txtCopies.TabIndex = 1;
            txtCopies.Text = "1";
            // 
            // lblCopies
            // 
            lblCopies.AutoSize = true;
            lblCopies.Location = new Point(15, 29);
            lblCopies.Name = "lblCopies";
            lblCopies.Size = new Size(46, 15);
            lblCopies.TabIndex = 0;
            lblCopies.Text = "Copies:";
            // 
            // btnCancel
            // 
            btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCancel.Location = new Point(206, 326);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(75, 25);
            btnCancel.TabIndex = 2;
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += btnCancel_Click;
            // 
            // btnOK
            // 
            btnOK.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnOK.Location = new Point(125, 326);
            btnOK.Name = "btnOK";
            btnOK.Size = new Size(75, 25);
            btnOK.TabIndex = 1;
            btnOK.Text = "OK";
            btnOK.UseVisualStyleBackColor = true;
            btnOK.Click += btnOK_Click;
            // 
            // BatchNoteMultiplierForm
            // 
            AcceptButton = btnOK;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = btnCancel;
            ClientSize = new Size(684, 361);
            Controls.Add(splitMain);
            Icon = (Icon)resources.GetObject("$this.Icon");
            MinimumSize = new Size(600, 350);
            Name = "BatchNoteMultiplierForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Batch Note Multiplier";
            splitMain.Panel1.ResumeLayout(false);
            splitMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitMain).EndInit();
            splitMain.ResumeLayout(false);
            panelFilter.ResumeLayout(false);
            panelFilter.PerformLayout();
            groupBoxSettings.ResumeLayout(false);
            groupBoxSettings.PerformLayout();
            ResumeLayout(false);

        }

        private System.Windows.Forms.SplitContainer splitMain;
        private System.Windows.Forms.CheckedListBox clbNotes;
        private System.Windows.Forms.Panel panelFilter;
        private System.Windows.Forms.TextBox txtFilter;
        private System.Windows.Forms.Label lblFilter;
        private System.Windows.Forms.Button btnSelectNone;
        private System.Windows.Forms.Button btnSelectAll;
        private System.Windows.Forms.GroupBox groupBoxSettings;
        private System.Windows.Forms.Label lblReplaceHint;
        private System.Windows.Forms.TextBox txtReplacePattern;
        private System.Windows.Forms.Label lblReplacePattern;
        private System.Windows.Forms.TextBox txtSearchKeyword;
        private System.Windows.Forms.Label lblSearchKeyword;
        private System.Windows.Forms.TextBox txtStride;
        private System.Windows.Forms.Label lblStride;
        private System.Windows.Forms.TextBox txtCopies;
        private System.Windows.Forms.Label lblCopies;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Label lblStartNum;
        private System.Windows.Forms.TextBox txtStartNum;
    }
}