using RALogicEditor.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace RALogicEditor.Forms
{
    public partial class PointerCalculatorForm : Form
    {
        public PointerCalculatorForm()
        {
            InitializeComponent();
        }

        private void btnCalculate_Click(object sender, EventArgs e)
        {
            // 1. Parse Base Address
            string baseStr = txtBaseAddress.Text.Trim();
            if (string.IsNullOrEmpty(baseStr))
            {
                txtResult.Text = "Error: Base address required.";
                return;
            }

            if (baseStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) baseStr = baseStr.Substring(2);
            if (!long.TryParse(baseStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long _))
            {
                txtResult.Text = "Error: Invalid hex base address.";
                return;
            }

            // 2. Parse Offsets
            var offsets = new List<string>();
            foreach (string line in txtOffsets.Lines)
            {
                string clean = line.Trim();
                if (string.IsNullOrEmpty(clean)) continue;

                // Strip comments or extra text, just look for hex
                if (clean.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) clean = clean.Substring(2);

                // Allow simple math like "+10"
                clean = clean.TrimStart('+');

                if (long.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long _))
                {
                    offsets.Add(clean);
                }
            }

            // 3. Construct Logic String
            // Format: I:0xBase_I:0xOffset1_0xOffset2
            // The last item is usually the read target, previous are "Add Address"

            StringBuilder sb = new StringBuilder();

            // Add Base
            // If there are offsets, the base is an "Add Address" (I:)
            // If no offsets, it's just the address
            if (offsets.Count > 0)
            {
                sb.Append($"I:0x{baseStr}");
            }
            else
            {
                sb.Append($"0x{baseStr}");
            }

            // Add intermediates
            for (int i = 0; i < offsets.Count; i++)
            {
                sb.Append("_");
                // If it's not the last one, it's an "Add Address" (I:)
                // If it IS the last one, it's the actual read (Mem)
                if (i < offsets.Count - 1)
                {
                    sb.Append($"I:0x{offsets[i]}");
                }
                else
                {
                    // Final offset
                    sb.Append($"0x{offsets[i]}");
                }
            }

            txtResult.Text = sb.ToString();
        }

        private void btnCopy_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(txtResult.Text) && !txtResult.Text.StartsWith("Error"))
            {
                Clipboard.SetText(txtResult.Text);
                MessageBox.Show("Logic string copied to clipboard.\nYou can now 'Paste Logic' into the editor.", "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}