namespace RALogicEditor.Controls
{
    // Subclass of DataGridView that enables Double Buffering to stop flickering
    public class DoubleBufferedDataGridView : DataGridView
    {
        public DoubleBufferedDataGridView()
        {
            this.DoubleBuffered = true;
        }

        // Override standard Copy/Paste keys to work during edit mode
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (IsCurrentCellInEditMode && EditingControl is TextBox editingControl)
            {
                switch (keyData)
                {
                    case Keys.Control | Keys.C:
                        editingControl.Copy();
                        return true;
                    case Keys.Control | Keys.V:
                        editingControl.Paste();
                        return true;
                    case Keys.Control | Keys.X:
                        editingControl.Cut();
                        return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}