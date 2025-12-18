using RALogicEditor.Helpers;
using RALogicEditor.Logic;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace RALogicEditor.Forms
{
    public partial class SettingsForm : Form
    {
        public SettingsForm()
        {
            InitializeComponent();
            LoadSettings();
            UpdatePreview();

            // Wire up events for real-time preview updates
            txtPrefix.TextChanged += (s, e) => UpdatePreview();
            txtSuffix.TextChanged += (s, e) => UpdatePreview();
            chkAlign.CheckedChanged += (s, e) => UpdatePreview();
            chkLastLineOnly.CheckedChanged += (s, e) => UpdatePreview();
        }

        private void LoadSettings()
        {
            txtPrefix.Text = AppSettings.Current.NotePrefix;
            txtSuffix.Text = AppSettings.Current.NoteSuffix;
            chkAlign.Checked = AppSettings.Current.NoteAlignSuffixes;
            chkLastLineOnly.Checked = AppSettings.Current.NoteSuffixOnLastLineOnly;
        }

        private void UpdatePreview()
        {
            // Dummy data for the preview
            var offsets = new List<int> { 0x64, 0x1A8, 0x14 };
            string preview = CodeNoteFormatter.BuildCodeNote(
                offsets,
                txtPrefix.Text,
                txtSuffix.Text,
                chkAlign.Checked,
                chkLastLineOnly.Checked,
                "32-bit",
                "Player Health"
            );
            txtPreview.Text = preview;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            AppSettings.Current.NotePrefix = txtPrefix.Text;
            AppSettings.Current.NoteSuffix = txtSuffix.Text;
            AppSettings.Current.NoteAlignSuffixes = chkAlign.Checked;
            AppSettings.Current.NoteSuffixOnLastLineOnly = chkLastLineOnly.Checked;
            AppSettings.Save();
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}