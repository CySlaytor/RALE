using RALogicEditor.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace RALogicEditor.Forms
{
    public partial class BatchNoteMultiplierForm : Form
    {
        private List<CodeNote> _allNotes;
        private List<CodeNote> _filteredNotes = new();

        // Persisted state
        private static int _lastCopies = 1;
        private static string _lastStride = "0x0";
        private static int _lastStartNum = 2;
        private static string _lastSearch = "";
        private static string _lastReplace = "";

        public List<CodeNote> SelectedNotes { get; private set; } = new();
        public int Copies { get; private set; }
        public long Stride { get; private set; }
        public int StartNumber { get; private set; }
        public string SearchKeyword { get; private set; } = "";
        public string ReplacePattern { get; private set; } = "";

        public BatchNoteMultiplierForm(List<CodeNote> allNotes)
        {
            InitializeComponent();
            _allNotes = allNotes;

            // Load persisted state
            txtCopies.Text = _lastCopies.ToString();
            txtStride.Text = _lastStride;
            txtStartNum.Text = _lastStartNum.ToString();
            txtSearchKeyword.Text = _lastSearch;
            txtReplacePattern.Text = _lastReplace;

            PopulateList("");
        }

        private void txtFilter_TextChanged(object sender, EventArgs e)
        {
            PopulateList(txtFilter.Text);

            // Auto-fill the replacement Search Keyword if the user is typing a filter
            if (!string.IsNullOrWhiteSpace(txtFilter.Text) && string.IsNullOrWhiteSpace(txtSearchKeyword.Text))
            {
                txtSearchKeyword.Text = txtFilter.Text;
            }
        }

        private void PopulateList(string filter)
        {
            clbNotes.Items.Clear();
            _filteredNotes.Clear();

            bool autoCheck = !string.IsNullOrWhiteSpace(filter);

            foreach (var note in _allNotes)
            {
                string header = note.Note.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";

                if (string.IsNullOrWhiteSpace(filter) || header.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _filteredNotes.Add(note);
                    clbNotes.Items.Add($"{note.Address} | {header}", autoCheck);
                }
            }
        }

        private void btnSelectAll_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < clbNotes.Items.Count; i++)
            {
                clbNotes.SetItemChecked(i, true);
            }
        }

        private void btnSelectNone_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < clbNotes.Items.Count; i++)
            {
                clbNotes.SetItemChecked(i, false);
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            // Validate Selection
            if (clbNotes.CheckedIndices.Count == 0)
            {
                MessageBox.Show("Please select at least one Code Note to duplicate.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Validate Copies
            if (!int.TryParse(txtCopies.Text, out int copies) || copies <= 0)
            {
                MessageBox.Show("Please enter a valid number of copies (1 or more).");
                return;
            }

            // Validate Stride
            string strideText = txtStride.Text.Trim();
            bool isHex = strideText.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            if (isHex) strideText = strideText.Substring(2);

            NumberStyles style = isHex ? NumberStyles.HexNumber : NumberStyles.Integer;
            if (!long.TryParse(strideText, style, CultureInfo.InvariantCulture, out long stride))
            {
                MessageBox.Show("Please enter a valid stride (Integers or 0xHex).");
                return;
            }

            // Validate Start Num
            if (!int.TryParse(txtStartNum.Text, out int startNum))
            {
                MessageBox.Show("Please enter a valid Starting Number.");
                return;
            }

            // Collect Data
            foreach (int index in clbNotes.CheckedIndices)
            {
                SelectedNotes.Add(_filteredNotes[index]);
            }

            Copies = copies;
            Stride = stride;
            StartNumber = startNum;
            SearchKeyword = txtSearchKeyword.Text;
            ReplacePattern = txtReplacePattern.Text;

            // Save persisted state
            _lastCopies = Copies;
            _lastStride = txtStride.Text;
            _lastStartNum = StartNumber;
            _lastSearch = SearchKeyword;
            _lastReplace = ReplacePattern;

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