namespace RALogicEditor.Forms
{
    partial class LogicExplainerForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.panelTop = new System.Windows.Forms.Panel();
            this.chkShowDecimal = new System.Windows.Forms.CheckBox();
            this.webBrowser = new System.Windows.Forms.WebBrowser();
            this.panelTop.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelTop
            // 
            this.panelTop.Controls.Add(this.chkShowDecimal);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Location = new System.Drawing.Point(10, 10);
            this.panelTop.Name = "panelTop";
            this.panelTop.Size = new System.Drawing.Size(564, 35);
            this.panelTop.TabIndex = 0;
            // 
            // chkShowDecimal
            // 
            this.chkShowDecimal.AutoSize = true;
            this.chkShowDecimal.ForeColor = System.Drawing.Color.Gainsboro;
            this.chkShowDecimal.Location = new System.Drawing.Point(3, 8);
            this.chkShowDecimal.Name = "chkShowDecimal";
            this.chkShowDecimal.Size = new System.Drawing.Size(148, 19);
            this.chkShowDecimal.TabIndex = 0;
            this.chkShowDecimal.Text = "Show Decimal Values";
            this.chkShowDecimal.UseVisualStyleBackColor = true;
            // 
            // webBrowser
            // 
            this.webBrowser.Dock = System.Windows.Forms.DockStyle.Fill;
            this.webBrowser.Location = new System.Drawing.Point(10, 45);
            this.webBrowser.MinimumSize = new System.Drawing.Size(20, 20);
            this.webBrowser.Name = "webBrowser";
            this.webBrowser.Size = new System.Drawing.Size(564, 406);
            this.webBrowser.TabIndex = 1;
            // 
            // LogicExplainerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 461);
            this.Controls.Add(this.webBrowser);
            this.Controls.Add(this.panelTop);
            this.Name = "LogicExplainerForm";
            this.Padding = new System.Windows.Forms.Padding(10);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Logic Explainer";
            this.panelTop.ResumeLayout(false);
            this.panelTop.PerformLayout();
            this.ResumeLayout(false);

        }

        private System.Windows.Forms.Panel panelTop;
        private System.Windows.Forms.CheckBox chkShowDecimal;
        private System.Windows.Forms.WebBrowser webBrowser;
    }
}