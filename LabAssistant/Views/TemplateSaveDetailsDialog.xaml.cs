using System.Windows;

namespace LabAssistant.Views
{
    public partial class TemplateSaveDetailsDialog : Window
    {
        public TemplateSaveDetailsDialog(string? defaultName, string? defaultDescription, string defaultVersion)
        {
            InitializeComponent();
            NameBox.Text = defaultName ?? string.Empty;
            DescriptionBox.Text = defaultDescription ?? string.Empty;
            VersionBox.Text = string.IsNullOrWhiteSpace(defaultVersion) ? "v0" : defaultVersion;
        }

        public string TemplateName => NameBox.Text.Trim();
        public string TemplateDescription => DescriptionBox.Text.Trim();
        public string TemplateVersion => VersionBox.Text.Trim();

        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TemplateName))
            {
                System.Windows.MessageBox.Show("Template name is required.", "Template Details", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(TemplateVersion))
            {
                System.Windows.MessageBox.Show("Template version is required.", "Template Details", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
