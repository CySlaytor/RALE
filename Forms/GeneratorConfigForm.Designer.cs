namespace RALogicEditor.Forms
{
    partial class GeneratorConfigForm
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
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.lblStrideHint = new System.Windows.Forms.Label();
            this.tvAddresses = new System.Windows.Forms.TreeView();
            this.lblInstructions = new System.Windows.Forms.Label();
            this.btnSelectAll = new System.Windows.Forms.Button();
            this.chkAutoStride = new System.Windows.Forms.CheckBox();
            this.chkGenerateAsArray = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // lblCopies
            // 
            this.lblCopies.AutoSize = true;
            this.lblCopies.Location = new System.Drawing.Point(12, 237);
            this.lblCopies.Name = "lblCopies";
            this.lblCopies.Size = new System.Drawing.Size(105, 15);
            this.lblCopies.TabIndex = 0;
            this.lblCopies.Text = "Number of Copies:";
            // 
            // txtCopies
            // 
            this.txtCopies.Location = new System.Drawing.Point(123, 234);
            this.txtCopies.Name = "txtCopies";
            this.txtCopies.Size = new System.Drawing.Size(100, 23);
            this.txtCopies.TabIndex = 1;
            this.txtCopies.Text = "1";
            // 
            // lblStride
            // 
            this.lblStride.AutoSize = true;
            this.lblStride.Location = new System.Drawing.Point(12, 269);
            this.lblStride.Name = "lblStride";
            this.lblStride.Size = new System.Drawing.Size(95, 15);
            this.lblStride.TabIndex = 2;
            this.lblStride.Text = "Stride (Address):";
            // 
            // txtStride
            // 
            this.txtStride.Location = new System.Drawing.Point(123, 266);
            this.txtStride.Name = "txtStride";
            this.txtStride.Size = new System.Drawing.Size(100, 23);
            this.txtStride.TabIndex = 3;
            this.txtStride.Text = "0x0";
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(196, 335);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 4;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.Location = new System.Drawing.Point(277, 335);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 5;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // lblStrideHint
            // 
            this.lblStrideHint.AutoSize = true;
            this.lblStrideHint.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.lblStrideHint.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblStrideHint.Location = new System.Drawing.Point(123, 292);
            this.lblStrideHint.Name = "lblStrideHint";
            this.lblStrideHint.Size = new System.Drawing.Size(108, 13);
            this.lblStrideHint.TabIndex = 7;
            this.lblStrideHint.Text = "Hex supported (0x)";
            // 
            // tvAddresses
            // 
            this.tvAddresses.CheckBoxes = true;
            this.tvAddresses.Location = new System.Drawing.Point(12, 27);
            this.tvAddresses.Name = "tvAddresses";
            this.tvAddresses.Size = new System.Drawing.Size(340, 184);
            this.tvAddresses.TabIndex = 8;
            this.tvAddresses.AfterCheck += new System.Windows.Forms.TreeViewEventHandler(this.tvAddresses_AfterCheck);
            // 
            // lblInstructions
            // 
            this.lblInstructions.AutoSize = true;
            this.lblInstructions.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblInstructions.Location = new System.Drawing.Point(12, 9);
            this.lblInstructions.Name = "lblInstructions";
            this.lblInstructions.Size = new System.Drawing.Size(188, 15);
            this.lblInstructions.TabIndex = 9;
            this.lblInstructions.Text = "Select Base Addresses to Expand:";
            // 
            // btnSelectAll
            // 
            this.btnSelectAll.Location = new System.Drawing.Point(277, 217);
            this.btnSelectAll.Name = "btnSelectAll";
            this.btnSelectAll.Size = new System.Drawing.Size(75, 23);
            this.btnSelectAll.TabIndex = 10;
            this.btnSelectAll.Text = "Select All";
            this.btnSelectAll.UseVisualStyleBackColor = true;
            this.btnSelectAll.Click += new System.EventHandler(this.btnSelectAll_Click);
            // 
            // chkAutoStride
            // 
            this.chkAutoStride.AutoSize = true;
            this.chkAutoStride.Enabled = true;
            this.chkAutoStride.Location = new System.Drawing.Point(237, 268);
            this.chkAutoStride.Name = "chkAutoStride";
            this.chkAutoStride.Size = new System.Drawing.Size(97, 19);
            this.chkAutoStride.TabIndex = 11;
            this.chkAutoStride.Text = "Auto Detect";
            this.chkAutoStride.UseVisualStyleBackColor = true;
            // 
            // chkGenerateAsArray
            // 
            this.chkGenerateAsArray.AutoSize = true;
            this.chkGenerateAsArray.Location = new System.Drawing.Point(12, 311);
            this.chkGenerateAsArray.Name = "chkGenerateAsArray";
            this.chkGenerateAsArray.Size = new System.Drawing.Size(325, 19);
            this.chkGenerateAsArray.TabIndex = 12;
            this.chkGenerateAsArray.Text = "Generate as Array (Append to this Group, don't create Alts)";
            this.chkGenerateAsArray.UseVisualStyleBackColor = true;
            // 
            // GeneratorConfigForm
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(364, 370);
            this.Controls.Add(this.chkGenerateAsArray);
            this.Controls.Add(this.chkAutoStride);
            this.Controls.Add(this.btnSelectAll);
            this.Controls.Add(this.lblInstructions);
            this.Controls.Add(this.tvAddresses);
            this.Controls.Add(this.lblStrideHint);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.txtStride);
            this.Controls.Add(this.lblStride);
            this.Controls.Add(this.txtCopies);
            this.Controls.Add(this.lblCopies);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "GeneratorConfigForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Create Generator";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private System.Windows.Forms.Label lblCopies;
        private System.Windows.Forms.TextBox txtCopies;
        private System.Windows.Forms.Label lblStride;
        private System.Windows.Forms.TextBox txtStride;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label lblStrideHint;
        private System.Windows.Forms.TreeView tvAddresses;
        private System.Windows.Forms.Label lblInstructions;
        private System.Windows.Forms.Button btnSelectAll;
        private System.Windows.Forms.CheckBox chkAutoStride;
        private System.Windows.Forms.CheckBox chkGenerateAsArray;
    }
}