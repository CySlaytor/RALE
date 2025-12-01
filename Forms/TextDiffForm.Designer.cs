namespace RALogicEditor.Forms
{
    partial class TextDiffForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.RichTextBox rtbLeft;
        private System.Windows.Forms.RichTextBox rtbRight;
        private System.Windows.Forms.Label lblStats;
        private System.Windows.Forms.Label lblLeft;
        private System.Windows.Forms.Label lblRight;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            splitContainer1 = new SplitContainer();
            rtbLeft = new RichTextBox();
            lblLeft = new Label();
            rtbRight = new RichTextBox();
            lblRight = new Label();
            lblStats = new Label();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            SuspendLayout();
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Location = new Point(0, 0);
            splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(rtbLeft);
            splitContainer1.Panel1.Controls.Add(lblLeft);
            splitContainer1.Panel1.Padding = new Padding(5);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(rtbRight);
            splitContainer1.Panel2.Controls.Add(lblRight);
            splitContainer1.Panel2.Padding = new Padding(5);
            splitContainer1.Size = new Size(900, 475);
            splitContainer1.SplitterDistance = 446;
            splitContainer1.TabIndex = 0;
            // 
            // rtbLeft
            // 
            rtbLeft.BorderStyle = BorderStyle.None;
            rtbLeft.Dock = DockStyle.Fill;
            rtbLeft.Font = new Font("Consolas", 10F);
            rtbLeft.Location = new Point(5, 20);
            rtbLeft.Name = "rtbLeft";
            rtbLeft.ReadOnly = true;
            rtbLeft.Size = new Size(436, 450);
            rtbLeft.TabIndex = 1;
            rtbLeft.Text = "";
            rtbLeft.WordWrap = false;
            // 
            // lblLeft
            // 
            lblLeft.AutoSize = true;
            lblLeft.Dock = DockStyle.Top;
            lblLeft.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblLeft.Location = new Point(5, 5);
            lblLeft.Name = "lblLeft";
            lblLeft.Size = new Size(80, 15);
            lblLeft.TabIndex = 0;
            lblLeft.Text = "Original (File)";
            // 
            // rtbRight
            // 
            rtbRight.BorderStyle = BorderStyle.None;
            rtbRight.Dock = DockStyle.Fill;
            rtbRight.Font = new Font("Consolas", 10F);
            rtbRight.Location = new Point(5, 20);
            rtbRight.Name = "rtbRight";
            rtbRight.ReadOnly = true;
            rtbRight.Size = new Size(440, 450);
            rtbRight.TabIndex = 1;
            rtbRight.Text = "";
            rtbRight.WordWrap = false;
            // 
            // lblRight
            // 
            lblRight.AutoSize = true;
            lblRight.Dock = DockStyle.Top;
            lblRight.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblRight.Location = new Point(5, 5);
            lblRight.Name = "lblRight";
            lblRight.Size = new Size(108, 15);
            lblRight.TabIndex = 0;
            lblRight.Text = "Current (Memory)";
            // 
            // lblStats
            // 
            lblStats.AutoSize = true;
            lblStats.Dock = DockStyle.Bottom;
            lblStats.Font = new Font("Segoe UI", 9F);
            lblStats.Location = new Point(0, 475);
            lblStats.Name = "lblStats";
            lblStats.Padding = new Padding(10, 5, 0, 5);
            lblStats.Size = new Size(105, 25);
            lblStats.TabIndex = 2;
            lblStats.Text = "Changes: +0 / -0";
            // 
            // TextDiffForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(900, 500);
            Controls.Add(splitContainer1);
            Controls.Add(lblStats);
            Name = "TextDiffForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Text Diff Viewer";
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel1.PerformLayout();
            splitContainer1.Panel2.ResumeLayout(false);
            splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }
    }
}