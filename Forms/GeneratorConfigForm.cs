using RALogicEditor.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace RALogicEditor.Forms
{
    // A dialog to capture Copies, Stride, and select multiple Addresses for the Logic Generator
    public partial class GeneratorConfigForm : Form
    {
        public int Copies { get; private set; }
        public int Stride { get; private set; }

        // List of addresses the user selected to apply the generator rule to
        public List<string> SelectedAddresses { get; private set; } = new();

        // Constructor accepts a dictionary for readable labels: Key=Address, Value=Description
        public GeneratorConfigForm(Dictionary<string, string> addressLabels, int defaultStride = 0, GeneratorDefinition existingDef = null)
        {
            InitializeComponent();

            // Populate the list
            clbAddresses.Items.Clear();
            foreach (var kvp in addressLabels)
            {
                // Display: "0x1234 (HP)" or just "0x1234"
                string label = string.IsNullOrEmpty(kvp.Value) ? kvp.Key : $"{kvp.Key} ({kvp.Value})";
                // We store the raw address in the Item but display the label? 
                // Checklistbox items are objects. We can wrap them or just parse string.
                // Simplest way: Store a custom object.
                clbAddresses.Items.Add(new AddressItem(kvp.Key, label), false);
            }

            // Pre-fill Stride
            if (defaultStride > 0)
            {
                txtStride.Text = "0x" + defaultStride.ToString("X");
                chkAutoStride.Checked = true; // Visually indicate it was auto-detected
            }

            // If we have existing rules, try to pre-fill the form
            if (existingDef != null)
            {
                txtCopies.Text = existingDef.Copies.ToString();

                if (existingDef.Rules.Count > 0)
                {
                    int foundStride = existingDef.Rules[0].Stride;
                    txtStride.Text = "0x" + foundStride.ToString("X");

                    // Check addresses that are already in the rules
                    for (int i = 0; i < clbAddresses.Items.Count; i++)
                    {
                        if (clbAddresses.Items[i] is AddressItem item)
                        {
                            if (existingDef.Rules.Any(r => r.SourceAddress == item.Address))
                            {
                                clbAddresses.SetItemChecked(i, true);
                            }
                        }
                    }
                }
            }
            else if (clbAddresses.Items.Count == 1)
            {
                // Select single item by default for convenience
                clbAddresses.SetItemChecked(0, true);
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            // Validate Copies
            if (!int.TryParse(txtCopies.Text, out int c) || c <= 0)
            {
                MessageBox.Show("Please enter a valid number of copies (1 or more).");
                return;
            }

            // Validate Stride
            string strideText = txtStride.Text.Trim();
            bool isHex = strideText.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            if (isHex) strideText = strideText.Substring(2);

            NumberStyles style = isHex ? NumberStyles.HexNumber : NumberStyles.Integer;

            if (!int.TryParse(strideText, style, CultureInfo.InvariantCulture, out int s))
            {
                MessageBox.Show("Please enter a valid stride (Integers or 0xHex).");
                return;
            }

            // Validate Selection
            if (clbAddresses.CheckedItems.Count == 0)
            {
                MessageBox.Show("Please select at least one address to increment.");
                return;
            }

            Copies = c;
            Stride = s;

            SelectedAddresses.Clear();
            foreach (var item in clbAddresses.CheckedItems)
            {
                if (item is AddressItem ai)
                    SelectedAddresses.Add(ai.Address);
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void btnSelectAll_Click(object sender, EventArgs e)
        {
            bool state = true;
            if (clbAddresses.CheckedItems.Count == clbAddresses.Items.Count)
                state = false;

            for (int i = 0; i < clbAddresses.Items.Count; i++)
            {
                clbAddresses.SetItemChecked(i, state);
            }
        }

        // Wrapper class for list display
        private class AddressItem
        {
            public string Address { get; }
            public string Display { get; }
            public AddressItem(string addr, string disp) { Address = addr; Display = disp; }
            public override string ToString() => Display;
        }
    }
}