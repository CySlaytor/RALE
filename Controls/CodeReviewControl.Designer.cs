namespace RALogicEditor.Controls
{
    partial class CodeReviewControl
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.panelHeader = new System.Windows.Forms.Panel();
            this.lblStatus = new System.Windows.Forms.Label();
            this.lblHeader = new System.Windows.Forms.Label();
            this.cmbFilter = new System.Windows.Forms.ComboBox();
            this.listIssues = new System.Windows.Forms.ListView();
            this.panelHeader.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelHeader
            // 
            this.panelHeader.BackColor = System.Drawing.SystemColors.ControlLight;
            this.panelHeader.Controls.Add(this.cmbFilter);
            this.panelHeader.Controls.Add(this.lblStatus);
            this.panelHeader.Controls.Add(this.lblHeader);
            this.panelHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelHeader.Location = new System.Drawing.Point(0, 0);
            this.panelHeader.Name = "panelHeader";
            this.panelHeader.Size = new System.Drawing.Size(400, 30);
            this.panelHeader.TabIndex = 0;
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.Dock = System.Windows.Forms.DockStyle.Right;
            this.lblStatus.Location = new System.Drawing.Point(309, 0);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Padding = new System.Windows.Forms.Padding(0, 8, 5, 0);
            this.lblStatus.Size = new System.Drawing.Size(91, 23);
            this.lblStatus.TabIndex = 1;
            this.lblStatus.Text = "No issues found.";
            this.lblStatus.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // lblHeader
            // 
            this.lblHeader.AutoSize = true;
            this.lblHeader.Dock = System.Windows.Forms.DockStyle.Left;
            this.lblHeader.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblHeader.Location = new System.Drawing.Point(0, 0);
            this.lblHeader.Name = "lblHeader";
            this.lblHeader.Padding = new System.Windows.Forms.Padding(5, 8, 0, 0);
            this.lblHeader.Size = new System.Drawing.Size(81, 23);
            this.lblHeader.TabIndex = 0;
            this.lblHeader.Text = "Code Review";
            // 
            // cmbFilter
            // 
            this.cmbFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbFilter.FormattingEnabled = true;
            this.cmbFilter.Location = new System.Drawing.Point(90, 4);
            this.cmbFilter.Name = "cmbFilter";
            this.cmbFilter.Size = new System.Drawing.Size(160, 23);
            this.cmbFilter.TabIndex = 2;
            // 
            // listIssues
            // 
            this.listIssues.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.listIssues.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listIssues.HideSelection = false;
            this.listIssues.Location = new System.Drawing.Point(0, 30);
            this.listIssues.Name = "listIssues";
            this.listIssues.Size = new System.Drawing.Size(400, 170);
            this.listIssues.TabIndex = 1;
            this.listIssues.UseCompatibleStateImageBehavior = false;
            // 
            // CodeReviewControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.listIssues);
            this.Controls.Add(this.panelHeader);
            this.Name = "CodeReviewControl";
            this.Size = new System.Drawing.Size(400, 200);
            this.panelHeader.ResumeLayout(false);
            this.panelHeader.PerformLayout();
            this.ResumeLayout(false);

        }

        private System.Windows.Forms.Panel panelHeader;
        private System.Windows.Forms.Label lblHeader;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.ListView listIssues;
        private System.Windows.Forms.ComboBox cmbFilter;
    }
}