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
            this.splitMain = new System.Windows.Forms.SplitContainer();
            this.clbNotes = new System.Windows.Forms.CheckedListBox();
            this.panelFilter = new System.Windows.Forms.Panel();
            this.btnSelectNone = new System.Windows.Forms.Button();
            this.btnSelectAll = new System.Windows.Forms.Button();
            this.txtFilter = new System.Windows.Forms.TextBox();
            this.lblFilter = new System.Windows.Forms.Label();
            this.groupBoxSettings = new System.Windows.Forms.GroupBox();
            this.lblStartNum = new System.Windows.Forms.Label();
            this.txtStartNum = new System.Windows.Forms.TextBox();
            this.lblReplaceHint = new System.Windows.Forms.Label();
            this.txtReplacePattern = new System.Windows.Forms.TextBox();
            this.lblReplacePattern = new System.Windows.Forms.Label();
            this.txtSearchKeyword = new System.Windows.Forms.TextBox();
            this.lblSearchKeyword = new System.Windows.Forms.Label();
            this.txtStride = new System.Windows.Forms.TextBox();
            this.lblStride = new System.Windows.Forms.Label();
            this.txtCopies = new System.Windows.Forms.TextBox();
            this.lblCopies = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOK = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).BeginInit();
            this.splitMain.Panel1.SuspendLayout();
            this.splitMain.Panel2.SuspendLayout();
            this.splitMain.SuspendLayout();
            this.panelFilter.SuspendLayout();
            this.groupBoxSettings.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitMain
            // 
            this.splitMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitMain.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitMain.Location = new System.Drawing.Point(0, 0);
            this.splitMain.Name = "splitMain";
            // 
            // splitMain.Panel1
            // 
            this.splitMain.Panel1.Controls.Add(this.clbNotes);
            this.splitMain.Panel1.Controls.Add(this.panelFilter);
            this.splitMain.Panel1.Padding = new System.Windows.Forms.Padding(5);
            // 
            // splitMain.Panel2
            // 
            this.splitMain.Panel2.Controls.Add(this.groupBoxSettings);
            this.splitMain.Panel2.Controls.Add(this.btnCancel);
            this.splitMain.Panel2.Controls.Add(this.btnOK);
            this.splitMain.Panel2.Padding = new System.Windows.Forms.Padding(5);
            this.splitMain.Size = new System.Drawing.Size(684, 361);
            this.splitMain.SplitterDistance = 384;
            this.splitMain.TabIndex = 0;
            // 
            // clbNotes
            // 
            this.clbNotes.CheckOnClick = true;
            this.clbNotes.Dock = System.Windows.Forms.DockStyle.Fill;
            this.clbNotes.FormattingEnabled = true;
            this.clbNotes.Location = new System.Drawing.Point(5, 60);
            this.clbNotes.Name = "clbNotes";
            this.clbNotes.Size = new System.Drawing.Size(374, 296);
            this.clbNotes.TabIndex = 1;
            // 
            // panelFilter
            // 
            this.panelFilter.Controls.Add(this.btnSelectNone);
            this.panelFilter.Controls.Add(this.btnSelectAll);
            this.panelFilter.Controls.Add(this.txtFilter);
            this.panelFilter.Controls.Add(this.lblFilter);
            this.panelFilter.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelFilter.Location = new System.Drawing.Point(5, 5);
            this.panelFilter.Name = "panelFilter";
            this.panelFilter.Size = new System.Drawing.Size(374, 55);
            this.panelFilter.TabIndex = 0;
            // 
            // btnSelectNone
            // 
            this.btnSelectNone.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSelectNone.Location = new System.Drawing.Point(286, 26);
            this.btnSelectNone.Name = "btnSelectNone";
            this.btnSelectNone.Size = new System.Drawing.Size(85, 23);
            this.btnSelectNone.TabIndex = 3;
            this.btnSelectNone.Text = "Select None";
            this.btnSelectNone.UseVisualStyleBackColor = true;
            this.btnSelectNone.Click += new System.EventHandler(this.btnSelectNone_Click);
            // 
            // btnSelectAll
            // 
            this.btnSelectAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSelectAll.Location = new System.Drawing.Point(205, 26);
            this.btnSelectAll.Name = "btnSelectAll";
            this.btnSelectAll.Size = new System.Drawing.Size(75, 23);
            this.btnSelectAll.TabIndex = 2;
            this.btnSelectAll.Text = "Select All";
            this.btnSelectAll.UseVisualStyleBackColor = true;
            this.btnSelectAll.Click += new System.EventHandler(this.btnSelectAll_Click);
            // 
            // txtFilter
            // 
            this.txtFilter.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtFilter.Location = new System.Drawing.Point(82, 3);
            this.txtFilter.Name = "txtFilter";
            this.txtFilter.Size = new System.Drawing.Size(289, 23);
            this.txtFilter.TabIndex = 1;
            this.txtFilter.TextChanged += new System.EventHandler(this.txtFilter_TextChanged);
            // 
            // lblFilter
            // 
            this.lblFilter.AutoSize = true;
            this.lblFilter.Location = new System.Drawing.Point(3, 6);
            this.lblFilter.Name = "lblFilter";
            this.lblFilter.Size = new System.Drawing.Size(73, 15);
            this.lblFilter.TabIndex = 0;
            this.lblFilter.Text = "Filter Notes:";
            // 
            // groupBoxSettings
            // 
            this.groupBoxSettings.Controls.Add(this.lblStartNum);
            this.groupBoxSettings.Controls.Add(this.txtStartNum);
            this.groupBoxSettings.Controls.Add(this.lblReplaceHint);
            this.groupBoxSettings.Controls.Add(this.txtReplacePattern);
            this.groupBoxSettings.Controls.Add(this.lblReplacePattern);
            this.groupBoxSettings.Controls.Add(this.txtSearchKeyword);
            this.groupBoxSettings.Controls.Add(this.lblSearchKeyword);
            this.groupBoxSettings.Controls.Add(this.txtStride);
            this.groupBoxSettings.Controls.Add(this.lblStride);
            this.groupBoxSettings.Controls.Add(this.txtCopies);
            this.groupBoxSettings.Controls.Add(this.lblCopies);
            this.groupBoxSettings.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBoxSettings.Location = new System.Drawing.Point(5, 5);
            this.groupBoxSettings.Name = "groupBoxSettings";
            this.groupBoxSettings.Size = new System.Drawing.Size(286, 230);
            this.groupBoxSettings.TabIndex = 0;
            this.groupBoxSettings.TabStop = false;
            this.groupBoxSettings.Text = "Multiplication Settings";
            // 
            // lblStartNum
            // 
            this.lblStartNum.AutoSize = true;
            this.lblStartNum.Location = new System.Drawing.Point(15, 87);
            this.lblStartNum.Name = "lblStartNum";
            this.lblStartNum.Size = new System.Drawing.Size(99, 15);
            this.lblStartNum.TabIndex = 4;
            this.lblStartNum.Text = "Starting Number:";
            // 
            // txtStartNum
            // 
            this.txtStartNum.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtStartNum.Location = new System.Drawing.Point(120, 84);
            this.txtStartNum.Name = "txtStartNum";
            this.txtStartNum.Size = new System.Drawing.Size(150, 23);
            this.txtStartNum.TabIndex = 5;
            this.txtStartNum.Text = "2";
            // 
            // lblReplaceHint
            // 
            this.lblReplaceHint.AutoSize = true;
            this.lblReplaceHint.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblReplaceHint.Location = new System.Drawing.Point(120, 199);
            this.lblReplaceHint.Name = "lblReplaceHint";
            this.lblReplaceHint.Size = new System.Drawing.Size(147, 15);
            this.lblReplaceHint.TabIndex = 10;
            this.lblReplaceHint.Text = "(Use {N} for slot number)";
            // 
            // txtReplacePattern
            // 
            this.txtReplacePattern.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtReplacePattern.Location = new System.Drawing.Point(120, 173);
            this.txtReplacePattern.Name = "txtReplacePattern";
            this.txtReplacePattern.Size = new System.Drawing.Size(150, 23);
            this.txtReplacePattern.TabIndex = 9;
            // 
            // lblReplacePattern
            // 
            this.lblReplacePattern.AutoSize = true;
            this.lblReplacePattern.Location = new System.Drawing.Point(15, 176);
            this.lblReplacePattern.Name = "lblReplacePattern";
            this.lblReplacePattern.Size = new System.Drawing.Size(78, 15);
            this.lblReplacePattern.TabIndex = 8;
            this.lblReplacePattern.Text = "Replace With:";
            // 
            // txtSearchKeyword
            // 
            this.txtSearchKeyword.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtSearchKeyword.Location = new System.Drawing.Point(120, 144);
            this.txtSearchKeyword.Name = "txtSearchKeyword";
            this.txtSearchKeyword.Size = new System.Drawing.Size(150, 23);
            this.txtSearchKeyword.TabIndex = 7;
            // 
            // lblSearchKeyword
            // 
            this.lblSearchKeyword.AutoSize = true;
            this.lblSearchKeyword.Location = new System.Drawing.Point(15, 147);
            this.lblSearchKeyword.Name = "lblSearchKeyword";
            this.lblSearchKeyword.Size = new System.Drawing.Size(100, 15);
            this.lblSearchKeyword.TabIndex = 6;
            this.lblSearchKeyword.Text = "Search Keyword:";
            // 
            // txtStride
            // 
            this.txtStride.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtStride.Location = new System.Drawing.Point(120, 55);
            this.txtStride.Name = "txtStride";
            this.txtStride.Size = new System.Drawing.Size(150, 23);
            this.txtStride.TabIndex = 3;
            this.txtStride.Text = "0x0";
            // 
            // lblStride
            // 
            this.lblStride.AutoSize = true;
            this.lblStride.Location = new System.Drawing.Point(15, 58);
            this.lblStride.Name = "lblStride";
            this.lblStride.Size = new System.Drawing.Size(73, 15);
            this.lblStride.TabIndex = 2;
            this.lblStride.Text = "Stride (Hex):";
            // 
            // txtCopies
            // 
            this.txtCopies.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtCopies.Location = new System.Drawing.Point(120, 26);
            this.txtCopies.Name = "txtCopies";
            this.txtCopies.Size = new System.Drawing.Size(150, 23);
            this.txtCopies.TabIndex = 1;
            this.txtCopies.Text = "1";
            // 
            // lblCopies
            // 
            this.lblCopies.AutoSize = true;
            this.lblCopies.Location = new System.Drawing.Point(15, 29);
            this.lblCopies.Name = "lblCopies";
            this.lblCopies.Size = new System.Drawing.Size(46, 15);
            this.lblCopies.TabIndex = 0;
            this.lblCopies.Text = "Copies:";
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.Location = new System.Drawing.Point(206, 326);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 25);
            this.btnCancel.TabIndex = 2;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(125, 326);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 25);
            this.btnOK.TabIndex = 1;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // BatchNoteMultiplierForm
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(684, 361);
            this.Controls.Add(this.splitMain);
            this.MinimumSize = new System.Drawing.Size(600, 350);
            this.Name = "BatchNoteMultiplierForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Batch Note Multiplier";
            this.splitMain.Panel1.ResumeLayout(false);
            this.splitMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).EndInit();
            this.splitMain.ResumeLayout(false);
            this.panelFilter.ResumeLayout(false);
            this.panelFilter.PerformLayout();
            this.groupBoxSettings.ResumeLayout(false);
            this.groupBoxSettings.PerformLayout();
            this.ResumeLayout(false);

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