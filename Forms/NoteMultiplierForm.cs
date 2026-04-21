using RALogicEditor.Helpers;
using System;
using System.Globalization;
using System.Windows.Forms;

namespace RALogicEditor.Forms
{
    public partial class NoteMultiplierForm : Form
    {
        // Static fields to persist state across session
        private static int _lastCopies = 1;
        private static string _lastStride = "0x0";
        private static int _lastStartNum = 2;
        private static int _lastNamingIdx = 1;
        private static string _lastSearch = "";
        private static string _lastReplace = "";
        private static int _lastTargetIdx = 0;

        public int Copies { get; private set; }
        public long Stride { get; private set; }
        public NamingStyle Naming { get; private set; }
        public string SearchText { get; private set; } = "";
        public string ReplaceText { get; private set; } = "";
        public int StartNumber { get; private set; }
        public StrideTarget TargetMode { get; private set; }

        public NoteMultiplierForm()
        {
            InitializeComponent();

            // Load persisted state
            txtCopies.Text = _lastCopies.ToString();
            txtStride.Text = _lastStride;
            txtStartNum.Text = _lastStartNum.ToString();
            txtSearch.Text = _lastSearch;
            txtReplace.Text = _lastReplace;
            cmbNaming.SelectedIndex = _lastNamingIdx;
            cmbStrideTarget.SelectedIndex = _lastTargetIdx;

            this.Text = "Multiply Selected Text";
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
            TargetMode = (StrideTarget)cmbStrideTarget.SelectedIndex;

            // Save state for next use
            _lastCopies = Copies;
            _lastStride = txtStride.Text.Trim();
            _lastStartNum = StartNumber;
            _lastNamingIdx = cmbNaming.SelectedIndex;
            _lastSearch = SearchText;
            _lastReplace = ReplaceText;
            _lastTargetIdx = cmbStrideTarget.SelectedIndex;

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