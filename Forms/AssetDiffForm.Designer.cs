namespace RALogicEditor.Forms
{
    partial class AssetDiffForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AssetDiffForm));
            splitDiff = new SplitContainer();
            rtbServer = new RichTextBox();
            lblServer = new Label();
            rtbLocal = new RichTextBox();
            lblLocal = new Label();
            lblStats = new Label();
            ((System.ComponentModel.ISupportInitialize)splitDiff).BeginInit();
            splitDiff.Panel1.SuspendLayout();
            splitDiff.Panel2.SuspendLayout();
            splitDiff.SuspendLayout();
            SuspendLayout();
            // 
            // splitDiff
            // 
            splitDiff.Dock = DockStyle.Fill;
            splitDiff.Location = new Point(0, 0);
            splitDiff.Name = "splitDiff";
            // 
            // splitDiff.Panel1
            // 
            splitDiff.Panel1.Controls.Add(rtbServer);
            splitDiff.Panel1.Controls.Add(lblServer);
            splitDiff.Panel1.Padding = new Padding(5);
            // 
            // splitDiff.Panel2
            // 
            splitDiff.Panel2.Controls.Add(rtbLocal);
            splitDiff.Panel2.Controls.Add(lblLocal);
            splitDiff.Panel2.Padding = new Padding(5);
            splitDiff.Size = new Size(900, 500);
            splitDiff.SplitterDistance = 446;
            splitDiff.TabIndex = 0;
            // 
            // rtbServer
            // 
            rtbServer.BackColor = Color.FromArgb(30, 30, 30);
            rtbServer.BorderStyle = BorderStyle.None;
            rtbServer.Dock = DockStyle.Fill;
            rtbServer.Font = new Font("Consolas", 10F);
            rtbServer.ForeColor = Color.WhiteSmoke;
            rtbServer.Location = new Point(5, 20);
            rtbServer.Name = "rtbServer";
            rtbServer.ReadOnly = true;
            rtbServer.Size = new Size(436, 475);
            rtbServer.TabIndex = 1;
            rtbServer.Text = "";
            rtbServer.WordWrap = false;
            // 
            // lblServer
            // 
            lblServer.AutoSize = true;
            lblServer.Dock = DockStyle.Top;
            lblServer.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblServer.Location = new Point(5, 5);
            lblServer.Name = "lblServer";
            lblServer.Size = new Size(74, 15);
            lblServer.TabIndex = 0;
            lblServer.Text = "Server Data";
            // 
            // rtbLocal
            // 
            rtbLocal.BackColor = Color.FromArgb(30, 30, 30);
            rtbLocal.BorderStyle = BorderStyle.None;
            rtbLocal.Dock = DockStyle.Fill;
            rtbLocal.Font = new Font("Consolas", 10F);
            rtbLocal.ForeColor = Color.WhiteSmoke;
            rtbLocal.Location = new Point(5, 20);
            rtbLocal.Name = "rtbLocal";
            rtbLocal.ReadOnly = true;
            rtbLocal.Size = new Size(440, 475);
            rtbLocal.TabIndex = 1;
            rtbLocal.Text = "";
            rtbLocal.WordWrap = false;
            // 
            // lblLocal
            // 
            lblLocal.AutoSize = true;
            lblLocal.Dock = DockStyle.Top;
            lblLocal.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblLocal.Location = new Point(5, 5);
            lblLocal.Name = "lblLocal";
            lblLocal.Size = new Size(64, 15);
            lblLocal.TabIndex = 0;
            lblLocal.Text = "Local Data";
            // 
            // lblStats
            // 
            lblStats.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblStats.AutoSize = true;
            lblStats.BackColor = Color.Transparent;
            lblStats.Font = new Font("Segoe UI", 9F);
            lblStats.Location = new Point(12, 476);
            lblStats.Name = "lblStats";
            lblStats.Size = new Size(95, 15);
            lblStats.TabIndex = 2;
            lblStats.Text = "Changes: +0 / -0";
            // 
            // AssetDiffForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(900, 500);
            Controls.Add(lblStats);
            Controls.Add(splitDiff);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "AssetDiffForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Asset Diff Viewer";
            splitDiff.Panel1.ResumeLayout(false);
            splitDiff.Panel1.PerformLayout();
            splitDiff.Panel2.ResumeLayout(false);
            splitDiff.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)splitDiff).EndInit();
            splitDiff.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();

        }

        private System.Windows.Forms.SplitContainer splitDiff;
        private System.Windows.Forms.RichTextBox rtbServer;
        private System.Windows.Forms.Label lblServer;
        private System.Windows.Forms.RichTextBox rtbLocal;
        private System.Windows.Forms.Label lblLocal;
        private System.Windows.Forms.Label lblStats;
    }
}