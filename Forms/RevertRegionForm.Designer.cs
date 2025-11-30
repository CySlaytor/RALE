namespace RALogicEditor.Forms
{
    partial class RevertRegionForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.ComboBox cmbRegions;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Label label1;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.cmbRegions = new System.Windows.Forms.ComboBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();

            // cmbRegions
            this.cmbRegions.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbRegions.FormattingEnabled = true;
            this.cmbRegions.Location = new System.Drawing.Point(12, 27);
            this.cmbRegions.Name = "cmbRegions";
            this.cmbRegions.Size = new System.Drawing.Size(200, 23);
            this.cmbRegions.TabIndex = 0;

            // btnOK
            this.btnOK.Location = new System.Drawing.Point(218, 27);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 1;
            this.btnOK.Text = "Revert";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);

            // label1
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(186, 15);
            this.label1.TabIndex = 2;
            this.label1.Text = "Select Region to Keep (Base):";

            // RevertRegionForm
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(305, 66);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.cmbRegions);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "RevertRegionForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Revert Multi-Region";
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}