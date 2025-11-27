using System;
using System.Windows.Forms;

namespace RALogicEditor.Forms
{
    public partial class DebugChainForm : Form
    {
        public DebugChainForm(string log)
        {
            InitializeComponent();
            txtLog.Text = log;
            txtLog.SelectionStart = 0;
            txtLog.SelectionLength = 0;
        }
    }
}