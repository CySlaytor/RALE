namespace RALogicEditor.Forms
{
    partial class TextDiffForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.RichTextBox rtbLeft;
        private System.Windows.Forms.RichTextBox rtbRight;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.rtbLeft = new System.Windows.Forms.RichTextBox();
            this.rtbRight = new System.Windows.Forms.RichTextBox();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.rtbLeft);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.rtbRight);
            this.splitContainer1.Size = new System.Drawing.Size(800, 450);
            this.splitContainer1.SplitterDistance = 390;
            this.splitContainer1.TabIndex = 0;
            // 
            // rtbLeft
            // 
            this.rtbLeft.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtbLeft.Location = new System.Drawing.Point(0, 0);
            this.rtbLeft.Name = "rtbLeft";
            this.rtbLeft.ReadOnly = true;
            this.rtbLeft.Size = new System.Drawing.Size(390, 450);
            this.rtbLeft.TabIndex = 0;
            this.rtbLeft.Text = "";
            this.rtbLeft.WordWrap = false;
            // 
            // rtbRight
            // 
            this.rtbRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtbRight.Location = new System.Drawing.Point(0, 0);
            this.rtbRight.Name = "rtbRight";
            this.rtbRight.ReadOnly = true;
            this.rtbRight.Size = new System.Drawing.Size(406, 450);
            this.rtbRight.TabIndex = 0;
            this.rtbRight.Text = "";
            this.rtbRight.WordWrap = false;
            // 
            // TextDiffForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.splitContainer1);
            this.Name = "TextDiffForm";
            this.Text = "Text Diff";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);
        }
    }
}