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
            this.splitDiff = new System.Windows.Forms.SplitContainer();
            this.rtbServer = new System.Windows.Forms.RichTextBox();
            this.lblServer = new System.Windows.Forms.Label();
            this.rtbLocal = new System.Windows.Forms.RichTextBox();
            this.lblLocal = new System.Windows.Forms.Label();
            this.lblStats = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.splitDiff)).BeginInit();
            this.splitDiff.Panel1.SuspendLayout();
            this.splitDiff.Panel2.SuspendLayout();
            this.splitDiff.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitDiff
            // 
            this.splitDiff.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitDiff.Location = new System.Drawing.Point(0, 0);
            this.splitDiff.Name = "splitDiff";
            // 
            // splitDiff.Panel1
            // 
            this.splitDiff.Panel1.Controls.Add(this.rtbServer);
            this.splitDiff.Panel1.Controls.Add(this.lblServer);
            this.splitDiff.Panel1.Padding = new System.Windows.Forms.Padding(5);
            // 
            // splitDiff.Panel2
            // 
            this.splitDiff.Panel2.Controls.Add(this.rtbLocal);
            this.splitDiff.Panel2.Controls.Add(this.lblLocal);
            this.splitDiff.Panel2.Padding = new System.Windows.Forms.Padding(5);
            this.splitDiff.Size = new System.Drawing.Size(900, 500);
            this.splitDiff.SplitterDistance = 448;
            this.splitDiff.TabIndex = 0;
            // 
            // rtbServer
            // 
            this.rtbServer.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.rtbServer.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.rtbServer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtbServer.Font = new System.Drawing.Font("Consolas", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.rtbServer.ForeColor = System.Drawing.Color.WhiteSmoke;
            this.rtbServer.Location = new System.Drawing.Point(5, 20);
            this.rtbServer.Name = "rtbServer";
            this.rtbServer.ReadOnly = true;
            this.rtbServer.Size = new System.Drawing.Size(438, 475);
            this.rtbServer.TabIndex = 1;
            this.rtbServer.Text = "";
            this.rtbServer.WordWrap = false;
            // 
            // lblServer
            // 
            this.lblServer.AutoSize = true;
            this.lblServer.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblServer.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblServer.Location = new System.Drawing.Point(5, 5);
            this.lblServer.Name = "lblServer";
            this.lblServer.Size = new System.Drawing.Size(73, 15);
            this.lblServer.TabIndex = 0;
            this.lblServer.Text = "Server Data";
            // 
            // rtbLocal
            // 
            this.rtbLocal.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.rtbLocal.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.rtbLocal.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rtbLocal.Font = new System.Drawing.Font("Consolas", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.rtbLocal.ForeColor = System.Drawing.Color.WhiteSmoke;
            this.rtbLocal.Location = new System.Drawing.Point(5, 20);
            this.rtbLocal.Name = "rtbLocal";
            this.rtbLocal.ReadOnly = true;
            this.rtbLocal.Size = new System.Drawing.Size(438, 475);
            this.rtbLocal.TabIndex = 1;
            this.rtbLocal.Text = "";
            this.rtbLocal.WordWrap = false;
            // 
            // lblLocal
            // 
            this.lblLocal.AutoSize = true;
            this.lblLocal.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblLocal.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblLocal.Location = new System.Drawing.Point(5, 5);
            this.lblLocal.Name = "lblLocal";
            this.lblLocal.Size = new System.Drawing.Size(64, 15);
            this.lblLocal.TabIndex = 0;
            this.lblLocal.Text = "Local Data";
            // 
            // lblStats
            // 
            this.lblStats.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblStats.AutoSize = true;
            this.lblStats.BackColor = System.Drawing.Color.Transparent;
            this.lblStats.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.lblStats.Location = new System.Drawing.Point(12, 476);
            this.lblStats.Name = "lblStats";
            this.lblStats.Size = new System.Drawing.Size(99, 15);
            this.lblStats.TabIndex = 2;
            this.lblStats.Text = "Changes: +0 / -0";
            // 
            // AssetDiffForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(900, 500);
            this.Controls.Add(this.lblStats);
            this.Controls.Add(this.splitDiff);
            this.Name = "AssetDiffForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Asset Diff Viewer";
            this.splitDiff.Panel1.ResumeLayout(false);
            this.splitDiff.Panel1.PerformLayout();
            this.splitDiff.Panel2.ResumeLayout(false);
            this.splitDiff.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitDiff)).EndInit();
            this.splitDiff.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private System.Windows.Forms.SplitContainer splitDiff;
        private System.Windows.Forms.RichTextBox rtbServer;
        private System.Windows.Forms.Label lblServer;
        private System.Windows.Forms.RichTextBox rtbLocal;
        private System.Windows.Forms.Label lblLocal;
        private System.Windows.Forms.Label lblStats;
    }
}