using RALogicEditor.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace RALogicEditor.Forms
{
    // A dialog to capture Copies, Stride, and select multiple Addresses for the Logic Generator
    public partial class GeneratorConfigForm : Form
    {
        // Static fields to persist state across the session
        private static string _lastCopies = "1";
        private static string _lastStride = "0x0";
        private static bool _lastIsArray = false;

        public int Copies { get; private set; }
        public int Stride { get; private set; }
        public bool IsArray { get; private set; }

        // List of addresses the user selected to apply the generator rule to
        public List<string> SelectedAddresses { get; private set; } = new();

        // ViewModel to handle hierarchy and display formatting
        public class GeneratorItemViewModel
        {
            public string Address { get; set; } = "";
            public string Description { get; set; } = "";
            public List<GeneratorItemViewModel> Children { get; set; } = new();

            public override string ToString()
            {
                if (string.IsNullOrEmpty(Description)) return Address;

                // Strip brackets [32-bit] etc for cleaner display, as requested
                string cleanDesc = Regex.Replace(Description, @"\[.*?\]", "").Trim();

                if (string.IsNullOrEmpty(cleanDesc)) return Address;
                return $"{Address} ({cleanDesc})";
            }
        }

        // Constructor now accepts a hierarchical list of items
        public GeneratorConfigForm(List<GeneratorItemViewModel> rootItems, int defaultStride = 0, GeneratorDefinition? existingDef = null)
        {
            InitializeComponent();
            InitializeTreeView(rootItems);

            // 1. Apply Session Persistence Defaults First
            txtCopies.Text = _lastCopies;
            txtStride.Text = _lastStride;
            chkGenerateAsArray.Checked = _lastIsArray;

            // 2. Pre-fill Stride if auto-detected (Overrides session default)
            if (defaultStride > 0)
            {
                txtStride.Text = "0x" + defaultStride.ToString("X");
            }

            // 3. If we have existing rules, try to pre-fill the form checks (Overrides everything)
            if (existingDef != null)
            {
                txtCopies.Text = existingDef.Copies.ToString();
                chkGenerateAsArray.Checked = existingDef.IsArray;

                if (existingDef.Rules.Count > 0)
                {
                    int foundStride = existingDef.Rules[0].Stride;
                    txtStride.Text = "0x" + foundStride.ToString("X");

                    // Check addresses that are already in the rules
                    var rulesSet = existingDef.Rules.Select(r => r.SourceAddress).ToHashSet();
                    CheckNodesRecursive(tvAddresses.Nodes, rulesSet);
                }
            }
            else if (tvAddresses.Nodes.Count == 1)
            {
                // Select single root item by default for convenience
                tvAddresses.Nodes[0].Checked = true;
                // Propagate check to children
                SetChildrenChecked(tvAddresses.Nodes[0], true);
            }
        }

        private void InitializeTreeView(List<GeneratorItemViewModel> items)
        {
            tvAddresses.Nodes.Clear();
            foreach (var item in items)
            {
                tvAddresses.Nodes.Add(CreateTreeNode(item));
            }
            tvAddresses.ExpandAll();
        }

        private TreeNode CreateTreeNode(GeneratorItemViewModel item)
        {
            var node = new TreeNode(item.ToString());
            node.Tag = item.Address; // Store raw address in Tag for retrieval

            foreach (var child in item.Children)
            {
                node.Nodes.Add(CreateTreeNode(child));
            }
            return node;
        }

        private void CheckNodesRecursive(TreeNodeCollection nodes, HashSet<string> addressesToCheck)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag is string addr && addressesToCheck.Contains(addr))
                {
                    node.Checked = true;
                    // Ensure parents are expanded to show checked items
                    TreeNode parent = node.Parent;
                    while (parent != null)
                    {
                        parent.Expand();
                        parent = parent.Parent;
                    }
                }
                CheckNodesRecursive(node.Nodes, addressesToCheck);
            }
        }

        // Cascade checking to children
        private void tvAddresses_AfterCheck(object sender, TreeViewEventArgs e)
        {
            // Only process user actions or code triggers, avoid infinite recursion loops by checking strict action if needed
            // But simple recursion is fine here as long as we don't re-trigger for same state
            // Added null check for e.Node
            if (e.Action != TreeViewAction.Unknown && e.Node != null)
            {
                SetChildrenChecked(e.Node, e.Node.Checked);
            }
        }

        private void SetChildrenChecked(TreeNode node, bool isChecked)
        {
            foreach (TreeNode child in node.Nodes)
            {
                if (child.Checked != isChecked)
                {
                    child.Checked = isChecked;
                    SetChildrenChecked(child, isChecked);
                }
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

            // Collect selected addresses from tree
            SelectedAddresses.Clear();
            CollectCheckedAddresses(tvAddresses.Nodes);

            // Validate Selection
            if (SelectedAddresses.Count == 0)
            {
                MessageBox.Show("Please select at least one address to increment.");
                return;
            }

            Copies = c;
            Stride = s;
            IsArray = chkGenerateAsArray.Checked;

            // Save state for next use in the same session
            _lastCopies = txtCopies.Text;
            _lastStride = txtStride.Text;
            _lastIsArray = chkGenerateAsArray.Checked;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void CollectCheckedAddresses(TreeNodeCollection nodes)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Checked && node.Tag is string addr)
                {
                    if (!SelectedAddresses.Contains(addr))
                        SelectedAddresses.Add(addr);
                }
                CollectCheckedAddresses(node.Nodes);
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void btnSelectAll_Click(object sender, EventArgs e)
        {
            bool anyUnchecked = false;
            foreach (TreeNode node in tvAddresses.Nodes)
            {
                if (!IsNodeTreeChecked(node)) { anyUnchecked = true; break; }
            }

            bool newState = anyUnchecked;
            foreach (TreeNode node in tvAddresses.Nodes)
            {
                node.Checked = newState;
                SetChildrenChecked(node, newState);
            }
        }

        private bool IsNodeTreeChecked(TreeNode node)
        {
            if (!node.Checked) return false;
            foreach (TreeNode child in node.Nodes)
            {
                if (!IsNodeTreeChecked(child)) return false;
            }
            return true;
        }
    }
}