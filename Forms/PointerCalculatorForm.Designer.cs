namespace RALogicEditor.Forms
{
    partial class PointerCalculatorForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.lblBase = new System.Windows.Forms.Label();
            this.txtBaseAddress = new System.Windows.Forms.TextBox();
            this.lblOffsets = new System.Windows.Forms.Label();
            this.txtOffsets = new System.Windows.Forms.TextBox();
            this.btnCalculate = new System.Windows.Forms.Button();
            this.txtResult = new System.Windows.Forms.TextBox();
            this.lblResult = new System.Windows.Forms.Label();
            this.btnCopy = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lblBase
            // 
            this.lblBase.AutoSize = true;
            this.lblBase.Location = new System.Drawing.Point(12, 15);
            this.lblBase.Name = "lblBase";
            this.lblBase.Size = new System.Drawing.Size(79, 15);
            this.lblBase.TabIndex = 0;
            this.lblBase.Text = "Base Address:";
            // 
            // txtBaseAddress
            // 
            this.txtBaseAddress.Location = new System.Drawing.Point(97, 12);
            this.txtBaseAddress.Name = "txtBaseAddress";
            this.txtBaseAddress.Size = new System.Drawing.Size(120, 23);
            this.txtBaseAddress.TabIndex = 1;
            // 
            // lblOffsets
            // 
            this.lblOffsets.AutoSize = true;
            this.lblOffsets.Location = new System.Drawing.Point(12, 45);
            this.lblOffsets.Name = "lblOffsets";
            this.lblOffsets.Size = new System.Drawing.Size(117, 15);
            this.lblOffsets.TabIndex = 2;
            this.lblOffsets.Text = "Offsets (One per line):";
            // 
            // txtOffsets
            // 
            this.txtOffsets.AcceptsReturn = true;
            this.txtOffsets.Location = new System.Drawing.Point(12, 63);
            this.txtOffsets.Multiline = true;
            this.txtOffsets.Name = "txtOffsets";
            this.txtOffsets.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtOffsets.Size = new System.Drawing.Size(260, 100);
            this.txtOffsets.TabIndex = 3;
            // 
            // btnCalculate
            // 
            this.btnCalculate.Location = new System.Drawing.Point(12, 169);
            this.btnCalculate.Name = "btnCalculate";
            this.btnCalculate.Size = new System.Drawing.Size(260, 23);
            this.btnCalculate.TabIndex = 4;
            this.btnCalculate.Text = "Generate Logic String";
            this.btnCalculate.UseVisualStyleBackColor = true;
            this.btnCalculate.Click += new System.EventHandler(this.btnCalculate_Click);
            // 
            // txtResult
            // 
            this.txtResult.Location = new System.Drawing.Point(12, 213);
            this.txtResult.Name = "txtResult";
            this.txtResult.ReadOnly = true;
            this.txtResult.Size = new System.Drawing.Size(200, 23);
            this.txtResult.TabIndex = 5;
            // 
            // lblResult
            // 
            this.lblResult.AutoSize = true;
            this.lblResult.Location = new System.Drawing.Point(12, 195);
            this.lblResult.Name = "lblResult";
            this.lblResult.Size = new System.Drawing.Size(42, 15);
            this.lblResult.TabIndex = 6;
            this.lblResult.Text = "Result:";
            // 
            // btnCopy
            // 
            this.btnCopy.Location = new System.Drawing.Point(218, 213);
            this.btnCopy.Name = "btnCopy";
            this.btnCopy.Size = new System.Drawing.Size(54, 23);
            this.btnCopy.TabIndex = 7;
            this.btnCopy.Text = "Copy";
            this.btnCopy.UseVisualStyleBackColor = true;
            this.btnCopy.Click += new System.EventHandler(this.btnCopy_Click);
            // 
            // PointerCalculatorForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 251);
            this.Controls.Add(this.btnCopy);
            this.Controls.Add(this.lblResult);
            this.Controls.Add(this.txtResult);
            this.Controls.Add(this.btnCalculate);
            this.Controls.Add(this.txtOffsets);
            this.Controls.Add(this.lblOffsets);
            this.Controls.Add(this.txtBaseAddress);
            this.Controls.Add(this.lblBase);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "PointerCalculatorForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Pointer Calculator";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private System.Windows.Forms.Label lblBase;
        private System.Windows.Forms.TextBox txtBaseAddress;
        private System.Windows.Forms.Label lblOffsets;
        private System.Windows.Forms.TextBox txtOffsets;
        private System.Windows.Forms.Button btnCalculate;
        private System.Windows.Forms.TextBox txtResult;
        private System.Windows.Forms.Label lblResult;
        private System.Windows.Forms.Button btnCopy;
    }
}