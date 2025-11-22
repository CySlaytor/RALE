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
            this.lblTarget = new System.Windows.Forms.Label();
            this.lblStrideHint = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // lblCopies
            // 
            this.lblCopies.AutoSize = true;
            this.lblCopies.Location = new System.Drawing.Point(12, 38);
            this.lblCopies.Name = "lblCopies";
            this.lblCopies.Size = new System.Drawing.Size(105, 15);
            this.lblCopies.TabIndex = 0;
            this.lblCopies.Text = "Number of Copies:";
            // 
            // txtCopies
            // 
            this.txtCopies.Location = new System.Drawing.Point(123, 35);
            this.txtCopies.Name = "txtCopies";
            this.txtCopies.Size = new System.Drawing.Size(100, 23);
            this.txtCopies.TabIndex = 1;
            this.txtCopies.Text = "1";
            // 
            // lblStride
            // 
            this.lblStride.AutoSize = true;
            this.lblStride.Location = new System.Drawing.Point(12, 70);
            this.lblStride.Name = "lblStride";
            this.lblStride.Size = new System.Drawing.Size(95, 15);
            this.lblStride.TabIndex = 2;
            this.lblStride.Text = "Stride (Address):";
            // 
            // txtStride
            // 
            this.txtStride.Location = new System.Drawing.Point(123, 67);
            this.txtStride.Name = "txtStride";
            this.txtStride.Size = new System.Drawing.Size(100, 23);
            this.txtStride.TabIndex = 3;
            this.txtStride.Text = "0x0";
            // 
            // btnOK
            // 
            this.btnOK.Location = new System.Drawing.Point(67, 126);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 4;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(148, 126);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 5;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // lblTarget
            // 
            this.lblTarget.AutoSize = true;
            this.lblTarget.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblTarget.Location = new System.Drawing.Point(12, 9);
            this.lblTarget.Name = "lblTarget";
            this.lblTarget.Size = new System.Drawing.Size(90, 15);
            this.lblTarget.TabIndex = 6;
            this.lblTarget.Text = "Target: 0x0000";
            // 
            // lblStrideHint
            // 
            this.lblStrideHint.AutoSize = true;
            this.lblStrideHint.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.lblStrideHint.ForeColor = System.Drawing.SystemColors.GrayText;
            this.lblStrideHint.Location = new System.Drawing.Point(123, 93);
            this.lblStrideHint.Name = "lblStrideHint";
            this.lblStrideHint.Size = new System.Drawing.Size(108, 13);
            this.lblStrideHint.TabIndex = 7;
            this.lblStrideHint.Text = "Hex supported (0x)";
            // 
            // GeneratorConfigForm
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(284, 161);
            this.Controls.Add(this.lblStrideHint);
            this.Controls.Add(this.lblTarget);
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
        private System.Windows.Forms.Label lblTarget;
        private System.Windows.Forms.Label lblStrideHint;
    }
}