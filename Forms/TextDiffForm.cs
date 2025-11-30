using System.Drawing;
using System.Windows.Forms;

namespace RALogicEditor.Forms
{
    public partial class TextDiffForm : Form
    {
        public TextDiffForm(string title, string leftText, string rightText)
        {
            InitializeComponent();
            this.Text = title;
            rtbLeft.Text = leftText;
            rtbRight.Text = rightText;

            if (System.Windows.Forms.SystemInformation.HighContrast || SystemColors.Window.R < 128)
            {
                this.BackColor = Color.FromArgb(45, 45, 48);
                rtbLeft.BackColor = Color.FromArgb(30, 30, 30);
                rtbLeft.ForeColor = Color.WhiteSmoke;
                rtbRight.BackColor = Color.FromArgb(30, 30, 30);
                rtbRight.ForeColor = Color.WhiteSmoke;
            }
        }
    }
}