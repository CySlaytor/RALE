using RALogicEditor.Helpers;
using System;
using System.Globalization;
using System.Windows.Forms;

namespace RALogicEditor.Forms
{
    public enum MultiplierMode { Text, Address }

    public partial class NoteMultiplierForm : Form
    {
        public int Copies { get; private set; }
        public long Stride { get; private set; }
        public NamingStyle Naming { get; private set; }
        public string SearchText { get; private set; } = "";
        public string ReplaceText { get; private set; } = "";
        public int StartNumber { get; private set; }
        public bool ApplyToChildren { get; private set; }

        public NoteMultiplierForm(MultiplierMode mode)
        {
            InitializeComponent();

            cmbNaming.SelectedIndex = 1; // Default to Replace "Slot 1"

            if (mode == MultiplierMode.Address)
            {
                this.Text = "Multiply Entire Note Address";
                chkApplyToChildren.Visible = false; // Irrelevant when creating base addresses
            }
            else
            {
                this.Text = "Multiply Selected Text";
                chkApplyToChildren.Visible = true;
            }
        }

        private void cmbNaming_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Show custom text boxes only if Custom pattern is selected
            pnlCustom.Visible = (cmbNaming.SelectedIndex == 3);
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(txtCopies.Text, out int copies) || copies <= 0)
            {
                MessageBox.Show("Please enter a valid number of copies (1 or more).");
                return;
            }

            string strideText = txtStride.Text.Trim();
            bool isHex = strideText.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            if (isHex) strideText = strideText.Substring(2);

            NumberStyles style = isHex ? NumberStyles.HexNumber : NumberStyles.Integer;

            if (!long.TryParse(strideText, style, CultureInfo.InvariantCulture, out long stride))
            {
                MessageBox.Show("Please enter a valid stride (Integers or 0xHex).");
                return;
            }

            if (!int.TryParse(txtStartNum.Text, out int startNum))
            {
                MessageBox.Show("Please enter a valid Starting Number.");
                return;
            }

            Copies = copies;
            Stride = stride;
            Naming = (NamingStyle)cmbNaming.SelectedIndex;
            SearchText = txtSearch.Text;
            ReplaceText = txtReplace.Text;
            StartNumber = startNum;
            ApplyToChildren = chkApplyToChildren.Checked;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}