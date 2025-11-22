using System;
using System.Globalization;
using System.Windows.Forms;

namespace RALogicEditor.Forms
{
    // A simple dialog to capture Copies and Stride for the Logic Generator
    public partial class GeneratorConfigForm : Form
    {
        public int Copies { get; private set; }
        public int Stride { get; private set; }

        public GeneratorConfigForm(string targetAddress)
        {
            InitializeComponent();
            lblTarget.Text = $"Target Address: {targetAddress}";
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            // Validate Copies
            if (!int.TryParse(txtCopies.Text, out int c) || c <= 0)
            {
                MessageBox.Show("Please enter a valid number of copies (1 or more).");
                return;
            }

            // Validate Stride (allows 0x Hex format)
            string strideText = txtStride.Text.Trim();
            bool isHex = strideText.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            if (isHex) strideText = strideText.Substring(2);

            NumberStyles style = isHex ? NumberStyles.HexNumber : NumberStyles.Integer;

            if (!int.TryParse(strideText, style, CultureInfo.InvariantCulture, out int s))
            {
                MessageBox.Show("Please enter a valid stride (Integers or 0xHex).");
                return;
            }

            Copies = c;
            Stride = s;

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