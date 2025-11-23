using RALogicEditor.Services;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace RALogicEditor.Forms
{
    public partial class SnippetSelectForm : Form
    {
        public Snippet? SelectedSnippet { get; private set; }

        public SnippetSelectForm()
        {
            InitializeComponent();
            LoadSnippets();
        }

        private void LoadSnippets()
        {
            lstSnippets.Items.Clear();
            var snippets = SnippetManager.Load();
            foreach (var snip in snippets)
            {
                lstSnippets.Items.Add(snip);
            }
        }

        private void lstSnippets_Format(object sender, ListControlConvertEventArgs e)
        {
            if (e.ListItem is Snippet s)
            {
                e.Value = s.Name;
            }
        }

        private void lstSnippets_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstSnippets.SelectedItem is Snippet s)
            {
                txtPreview.Text = s.LogicCode;
                btnInsert.Enabled = true;
                btnDelete.Enabled = true;
            }
            else
            {
                txtPreview.Text = "";
                btnInsert.Enabled = false;
                btnDelete.Enabled = false;
            }
        }

        private void btnInsert_Click(object sender, EventArgs e)
        {
            if (lstSnippets.SelectedItem is Snippet s)
            {
                SelectedSnippet = s;
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (lstSnippets.SelectedItem is Snippet s)
            {
                if (MessageBox.Show($"Delete snippet '{s.Name}'?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    SnippetManager.DeleteSnippet(s);
                    LoadSnippets();
                    txtPreview.Text = "";
                }
            }
        }

        private void lstSnippets_DoubleClick(object sender, EventArgs e)
        {
            if (lstSnippets.SelectedItem != null) btnInsert_Click(sender, e);
        }
    }
}