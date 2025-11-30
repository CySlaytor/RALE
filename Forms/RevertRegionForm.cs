using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace RALogicEditor.Forms
{
    public partial class RevertRegionForm : Form
    {
        public string SelectedRegion { get; private set; } = "";

        public RevertRegionForm(List<string> detectedRegions)
        {
            InitializeComponent();
            cmbRegions.Items.Clear();
            foreach (var r in detectedRegions) cmbRegions.Items.Add(r);
            if (cmbRegions.Items.Count > 0) cmbRegions.SelectedIndex = 0;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            SelectedRegion = cmbRegions.SelectedItem?.ToString() ?? "";
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}