namespace RALogicEditor.Controls
{
    partial class ConditionPreviewControl
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.rtbPreview = new System.Windows.Forms.RichTextBox();
            this.SuspendLayout();
            // 
            // rtbPreview
            // 
            this.rtbPreview.BackColor = System.Drawing.SystemColors.Control;
            this.rtbPreview.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.rtbPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtbPreview.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.rtbPreview.Location = new System.Drawing.Point(5, 5);
            this.rtbPreview.Name = "rtbPreview";
            this.rtbPreview.ReadOnly = true;
            this.rtbPreview.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.None;
            this.rtbPreview.Size = new System.Drawing.Size(690, 40);
            this.rtbPreview.TabIndex = 0;
            this.rtbPreview.Text = "";
            // 
            // ConditionPreviewControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.Controls.Add(this.rtbPreview);
            this.Name = "ConditionPreviewControl";
            this.Padding = new System.Windows.Forms.Padding(5);
            this.Size = new System.Drawing.Size(700, 50);
            this.ResumeLayout(false);

        }

        private System.Windows.Forms.RichTextBox rtbPreview;
    }
}