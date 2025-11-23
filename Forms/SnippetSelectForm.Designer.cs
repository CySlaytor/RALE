namespace RALogicEditor.Forms
{
    partial class SnippetSelectForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.lstSnippets = new System.Windows.Forms.ListBox();
            this.btnInsert = new System.Windows.Forms.Button();
            this.btnDelete = new System.Windows.Forms.Button();
            this.txtPreview = new System.Windows.Forms.TextBox();
            this.lblPreview = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // lstSnippets
            // 
            this.lstSnippets.FormattingEnabled = true;
            this.lstSnippets.ItemHeight = 15;
            this.lstSnippets.Location = new System.Drawing.Point(12, 12);
            this.lstSnippets.Name = "lstSnippets";
            this.lstSnippets.Size = new System.Drawing.Size(200, 244);
            this.lstSnippets.TabIndex = 0;
            this.lstSnippets.SelectedIndexChanged += new System.EventHandler(this.lstSnippets_SelectedIndexChanged);
            this.lstSnippets.Format += new System.Windows.Forms.ListControlConvertEventHandler(this.lstSnippets_Format);
            this.lstSnippets.DoubleClick += new System.EventHandler(this.lstSnippets_DoubleClick);
            // 
            // btnInsert
            // 
            this.btnInsert.Enabled = false;
            this.btnInsert.Location = new System.Drawing.Point(218, 12);
            this.btnInsert.Name = "btnInsert";
            this.btnInsert.Size = new System.Drawing.Size(154, 30);
            this.btnInsert.TabIndex = 1;
            this.btnInsert.Text = "Insert";
            this.btnInsert.UseVisualStyleBackColor = true;
            this.btnInsert.Click += new System.EventHandler(this.btnInsert_Click);
            // 
            // btnDelete
            // 
            this.btnDelete.Enabled = false;
            this.btnDelete.Location = new System.Drawing.Point(218, 228);
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(154, 28);
            this.btnDelete.TabIndex = 2;
            this.btnDelete.Text = "Delete Snippet";
            this.btnDelete.UseVisualStyleBackColor = true;
            this.btnDelete.Click += new System.EventHandler(this.btnDelete_Click);
            // 
            // txtPreview
            // 
            this.txtPreview.Location = new System.Drawing.Point(218, 65);
            this.txtPreview.Multiline = true;
            this.txtPreview.Name = "txtPreview";
            this.txtPreview.ReadOnly = true;
            this.txtPreview.Size = new System.Drawing.Size(154, 157);
            this.txtPreview.TabIndex = 3;
            // 
            // lblPreview
            // 
            this.lblPreview.AutoSize = true;
            this.lblPreview.Location = new System.Drawing.Point(218, 47);
            this.lblPreview.Name = "lblPreview";
            this.lblPreview.Size = new System.Drawing.Size(51, 15);
            this.lblPreview.TabIndex = 4;
            this.lblPreview.Text = "Preview:";
            // 
            // SnippetSelectForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(384, 268);
            this.Controls.Add(this.lblPreview);
            this.Controls.Add(this.txtPreview);
            this.Controls.Add(this.btnDelete);
            this.Controls.Add(this.btnInsert);
            this.Controls.Add(this.lstSnippets);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SnippetSelectForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Snippet Library";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private System.Windows.Forms.ListBox lstSnippets;
        private System.Windows.Forms.Button btnInsert;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.TextBox txtPreview;
        private System.Windows.Forms.Label lblPreview;
    }
}