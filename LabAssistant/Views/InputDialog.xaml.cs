using System.Windows;

namespace LabAssistant
{
    public partial class InputDialog : Window
    {
        public string ResponseText { get; private set; } = string.Empty;

        public InputDialog(string message)
        {
            InitializeComponent();
            MessageText.Text = message;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ResponseText = ResponseBox.Text ?? string.Empty;
            this.DialogResult = true;
            this.Close();
        }
    }
}
