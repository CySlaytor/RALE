namespace RALogicEditor.Forms
{
    partial class SettingsForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.lblPrefix = new System.Windows.Forms.Label();
            this.txtPrefix = new System.Windows.Forms.TextBox();
            this.lblSuffix = new System.Windows.Forms.Label();
            this.txtSuffix = new System.Windows.Forms.TextBox();
            this.chkAlign = new System.Windows.Forms.CheckBox();
            this.chkLastLineOnly = new System.Windows.Forms.CheckBox();
            this.groupPreview = new System.Windows.Forms.GroupBox();
            this.txtPreview = new System.Windows.Forms.TextBox();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.groupPreview.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblPrefix
            // 
            this.lblPrefix.AutoSize = true;
            this.lblPrefix.Location = new System.Drawing.Point(12, 15);
            this.lblPrefix.Name = "lblPrefix";
            this.lblPrefix.Size = new System.Drawing.Size(107, 15);
            this.lblPrefix.TabIndex = 0;
            this.lblPrefix.Text = "Indentation Prefix:";
            // 
            // txtPrefix
            // 
            this.txtPrefix.Location = new System.Drawing.Point(125, 12);
            this.txtPrefix.Name = "txtPrefix";
            this.txtPrefix.Size = new System.Drawing.Size(50, 23);
            this.txtPrefix.TabIndex = 1;
            // 
            // lblSuffix
            // 
            this.lblSuffix.AutoSize = true;
            this.lblSuffix.Location = new System.Drawing.Point(12, 44);
            this.lblSuffix.Name = "lblSuffix";
            this.lblSuffix.Size = new System.Drawing.Size(66, 15);
            this.lblSuffix.TabIndex = 2;
            this.lblSuffix.Text = "Line Suffix:";
            // 
            // txtSuffix
            // 
            this.txtSuffix.Location = new System.Drawing.Point(125, 41);
            this.txtSuffix.Name = "txtSuffix";
            this.txtSuffix.Size = new System.Drawing.Size(50, 23);
            this.txtSuffix.TabIndex = 3;
            // 
            // chkAlign
            // 
            this.chkAlign.AutoSize = true;
            this.chkAlign.Location = new System.Drawing.Point(12, 75);
            this.chkAlign.Name = "chkAlign";
            this.chkAlign.Size = new System.Drawing.Size(155, 19);
            this.chkAlign.TabIndex = 4;
            this.chkAlign.Text = "Align Suffixes Vertically";
            this.chkAlign.UseVisualStyleBackColor = true;
            // 
            // chkLastLineOnly
            // 
            this.chkLastLineOnly.AutoSize = true;
            this.chkLastLineOnly.Location = new System.Drawing.Point(12, 100);
            this.chkLastLineOnly.Name = "chkLastLineOnly";
            this.chkLastLineOnly.Size = new System.Drawing.Size(175, 19);
            this.chkLastLineOnly.TabIndex = 5;
            this.chkLastLineOnly.Text = "Apply Suffix to Last Line Only";
            this.chkLastLineOnly.UseVisualStyleBackColor = true;
            // 
            // groupPreview
            // 
            this.groupPreview.Controls.Add(this.txtPreview);
            this.groupPreview.Location = new System.Drawing.Point(200, 12);
            this.groupPreview.Name = "groupPreview";
            this.groupPreview.Size = new System.Drawing.Size(250, 120);
            this.groupPreview.TabIndex = 6;
            this.groupPreview.TabStop = false;
            this.groupPreview.Text = "Live Preview";
            // 
            // txtPreview
            // 
            this.txtPreview.BackColor = System.Drawing.SystemColors.Info;
            this.txtPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtPreview.Font = new System.Drawing.Font("Consolas", 9F);
            this.txtPreview.Location = new System.Drawing.Point(3, 19);
            this.txtPreview.Multiline = true;
            this.txtPreview.Name = "txtPreview";
            this.txtPreview.ReadOnly = true;
            this.txtPreview.Size = new System.Drawing.Size(244, 98);
            this.txtPreview.TabIndex = 0;
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(294, 138);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(75, 23);
            this.btnSave.TabIndex = 7;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(375, 138);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 8;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // SettingsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(464, 171);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.groupPreview);
            this.Controls.Add(this.chkLastLineOnly);
            this.Controls.Add(this.chkAlign);
            this.Controls.Add(this.txtSuffix);
            this.Controls.Add(this.lblSuffix);
            this.Controls.Add(this.txtPrefix);
            this.Controls.Add(this.lblPrefix);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SettingsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Code Note Settings";
            this.groupPreview.ResumeLayout(false);
            this.groupPreview.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private System.Windows.Forms.Label lblPrefix;
        private System.Windows.Forms.TextBox txtPrefix;
        private System.Windows.Forms.Label lblSuffix;
        private System.Windows.Forms.TextBox txtSuffix;
        private System.Windows.Forms.CheckBox chkAlign;
        private System.Windows.Forms.CheckBox chkLastLineOnly;
        private System.Windows.Forms.GroupBox groupPreview;
        private System.Windows.Forms.TextBox txtPreview;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnCancel;
    }
}