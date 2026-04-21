namespace RALogicEditor.Forms
{
    partial class NoteMultiplierForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.lblCopies = new System.Windows.Forms.Label();
            this.txtCopies = new System.Windows.Forms.TextBox();
            this.lblStride = new System.Windows.Forms.Label();
            this.txtStride = new System.Windows.Forms.TextBox();
            this.lblStartNum = new System.Windows.Forms.Label();
            this.txtStartNum = new System.Windows.Forms.TextBox();
            this.lblNaming = new System.Windows.Forms.Label();
            this.cmbNaming = new System.Windows.Forms.ComboBox();
            this.pnlCustom = new System.Windows.Forms.Panel();
            this.lblSearch = new System.Windows.Forms.Label();
            this.txtSearch = new System.Windows.Forms.TextBox();
            this.lblReplace = new System.Windows.Forms.Label();
            this.txtReplace = new System.Windows.Forms.TextBox();
            this.lblHelp = new System.Windows.Forms.Label();
            this.lblStrideTarget = new System.Windows.Forms.Label();
            this.cmbStrideTarget = new System.Windows.Forms.ComboBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.pnlCustom.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblCopies
            // 
            this.lblCopies.AutoSize = true;
            this.lblCopies.Location = new System.Drawing.Point(12, 15);
            this.lblCopies.Name = "lblCopies";
            this.lblCopies.Size = new System.Drawing.Size(105, 15);
            this.lblCopies.TabIndex = 0;
            this.lblCopies.Text = "Number of Copies:";
            // 
            // txtCopies
            // 
            this.txtCopies.Location = new System.Drawing.Point(140, 12);
            this.txtCopies.Name = "txtCopies";
            this.txtCopies.Size = new System.Drawing.Size(100, 23);
            this.txtCopies.TabIndex = 1;
            this.txtCopies.Text = "1";
            // 
            // lblStride
            // 
            this.lblStride.AutoSize = true;
            this.lblStride.Location = new System.Drawing.Point(12, 44);
            this.lblStride.Name = "lblStride";
            this.lblStride.Size = new System.Drawing.Size(95, 15);
            this.lblStride.TabIndex = 2;
            this.lblStride.Text = "Stride (Address):";
            // 
            // txtStride
            // 
            this.txtStride.Location = new System.Drawing.Point(140, 41);
            this.txtStride.Name = "txtStride";
            this.txtStride.Size = new System.Drawing.Size(100, 23);
            this.txtStride.TabIndex = 3;
            this.txtStride.Text = "0x0";
            // 
            // lblStartNum
            // 
            this.lblStartNum.AutoSize = true;
            this.lblStartNum.Location = new System.Drawing.Point(12, 73);
            this.lblStartNum.Name = "lblStartNum";
            this.lblStartNum.Size = new System.Drawing.Size(99, 15);
            this.lblStartNum.TabIndex = 4;
            this.lblStartNum.Text = "Starting Number:";
            // 
            // txtStartNum
            // 
            this.txtStartNum.Location = new System.Drawing.Point(140, 70);
            this.txtStartNum.Name = "txtStartNum";
            this.txtStartNum.Size = new System.Drawing.Size(100, 23);
            this.txtStartNum.TabIndex = 5;
            this.txtStartNum.Text = "2";
            // 
            // lblNaming
            // 
            this.lblNaming.AutoSize = true;
            this.lblNaming.Location = new System.Drawing.Point(12, 102);
            this.lblNaming.Name = "lblNaming";
            this.lblNaming.Size = new System.Drawing.Size(83, 15);
            this.lblNaming.TabIndex = 6;
            this.lblNaming.Text = "Naming Style:";
            // 
            // cmbNaming
            // 
            this.cmbNaming.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbNaming.FormattingEnabled = true;
            this.cmbNaming.Items.AddRange(new object[] {
            "Append \" (Slot N)\"",
            "Replace \"Slot 1\" -> \"Slot N\"",
            "Replace \"Player 1\" -> \"Player N\"",
            "Custom Pattern..."});
            this.cmbNaming.Location = new System.Drawing.Point(140, 99);
            this.cmbNaming.Name = "cmbNaming";
            this.cmbNaming.Size = new System.Drawing.Size(252, 23);
            this.cmbNaming.TabIndex = 7;
            this.cmbNaming.SelectedIndexChanged += new System.EventHandler(this.cmbNaming_SelectedIndexChanged);
            // 
            // pnlCustom
            // 
            this.pnlCustom.Controls.Add(this.lblSearch);
            this.pnlCustom.Controls.Add(this.txtSearch);
            this.pnlCustom.Controls.Add(this.lblReplace);
            this.pnlCustom.Controls.Add(this.txtReplace);
            this.pnlCustom.Controls.Add(this.lblHelp);
            this.pnlCustom.Location = new System.Drawing.Point(12, 128);
            this.pnlCustom.Name = "pnlCustom";
            this.pnlCustom.Size = new System.Drawing.Size(380, 85);
            this.pnlCustom.TabIndex = 8;
            this.pnlCustom.Visible = false;
            // 
            // lblSearch
            // 
            this.lblSearch.AutoSize = true;
            this.lblSearch.Location = new System.Drawing.Point(0, 5);
            this.lblSearch.Name = "lblSearch";
            this.lblSearch.Size = new System.Drawing.Size(73, 15);
            this.lblSearch.TabIndex = 0;
            this.lblSearch.Text = "Text to Find:";
            // 
            // txtSearch
            // 
            this.txtSearch.Location = new System.Drawing.Point(128, 2);
            this.txtSearch.Name = "txtSearch";
            this.txtSearch.Size = new System.Drawing.Size(252, 23);
            this.txtSearch.TabIndex = 1;
            // 
            // lblReplace
            // 
            this.lblReplace.AutoSize = true;
            this.lblReplace.Location = new System.Drawing.Point(0, 34);
            this.lblReplace.Name = "lblReplace";
            this.lblReplace.Size = new System.Drawing.Size(86, 15);
            this.lblReplace.TabIndex = 2;
            this.lblReplace.Text = "Replace Name:";
            // 
            // txtReplace
            // 
            this.txtReplace.Location = new System.Drawing.Point(128, 31);
            this.txtReplace.Name = "txtReplace";
            this.txtReplace.Size = new System.Drawing.Size(252, 23);
            this.txtReplace.TabIndex = 3;
            // 
            // lblHelp
            // 
            this.lblHelp.AutoSize = true;
            this.lblHelp.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblHelp.Location = new System.Drawing.Point(125, 57);
            this.lblHelp.Name = "lblHelp";
            this.lblHelp.Size = new System.Drawing.Size(236, 15);
            this.lblHelp.TabIndex = 4;
            this.lblHelp.Text = "Tip: Use {N} for the target slot/player number.";
            // 
            // lblStrideTarget
            // 
            this.lblStrideTarget.AutoSize = true;
            this.lblStrideTarget.Location = new System.Drawing.Point(12, 220);
            this.lblStrideTarget.Name = "lblStrideTarget";
            this.lblStrideTarget.Size = new System.Drawing.Size(90, 15);
            this.lblStrideTarget.TabIndex = 9;
            this.lblStrideTarget.Text = "Apply Stride To:";
            // 
            // cmbStrideTarget
            // 
            this.cmbStrideTarget.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbStrideTarget.FormattingEnabled = true;
            this.cmbStrideTarget.Items.AddRange(new object[] {
            "All Offsets (Array of Structs)",
            "Parent Offsets Only (Array of Pointers)",
            "Child Offsets Only (Array inside a Struct)"});
            this.cmbStrideTarget.Location = new System.Drawing.Point(140, 217);
            this.cmbStrideTarget.Name = "cmbStrideTarget";
            this.cmbStrideTarget.Size = new System.Drawing.Size(252, 23);
            this.cmbStrideTarget.TabIndex = 10;
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(236, 256);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 11;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.Location = new System.Drawing.Point(317, 256);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 12;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // NoteMultiplierForm
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(404, 291);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.cmbStrideTarget);
            this.Controls.Add(this.lblStrideTarget);
            this.Controls.Add(this.pnlCustom);
            this.Controls.Add(this.cmbNaming);
            this.Controls.Add(this.lblNaming);
            this.Controls.Add(this.txtStartNum);
            this.Controls.Add(this.lblStartNum);
            this.Controls.Add(this.txtStride);
            this.Controls.Add(this.lblStride);
            this.Controls.Add(this.txtCopies);
            this.Controls.Add(this.lblCopies);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "NoteMultiplierForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Multiply";
            this.pnlCustom.ResumeLayout(false);
            this.pnlCustom.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private System.Windows.Forms.Label lblCopies;
        private System.Windows.Forms.TextBox txtCopies;
        private System.Windows.Forms.Label lblStride;
        private System.Windows.Forms.TextBox txtStride;
        private System.Windows.Forms.Label lblStartNum;
        private System.Windows.Forms.TextBox txtStartNum;
        private System.Windows.Forms.Label lblNaming;
        private System.Windows.Forms.ComboBox cmbNaming;
        private System.Windows.Forms.Panel pnlCustom;
        private System.Windows.Forms.Label lblSearch;
        private System.Windows.Forms.TextBox txtSearch;
        private System.Windows.Forms.Label lblReplace;
        private System.Windows.Forms.TextBox txtReplace;
        private System.Windows.Forms.Label lblHelp;
        private System.Windows.Forms.Label lblStrideTarget;
        private System.Windows.Forms.ComboBox cmbStrideTarget;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
    }
}