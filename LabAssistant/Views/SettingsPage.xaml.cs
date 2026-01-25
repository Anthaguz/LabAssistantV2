using System.Windows;
using System.Windows.Controls;
using LabAssistant.Services.Configuration;


namespace LabAssistant.Views
{
    public partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            InitializeComponent();
            LoadSettingsIntoUI();
        }

        private void LoadSettingsIntoUI()
        {
            TemplateFolderBox.Text = SettingsManager.Settings.TemplateFolder;
            LogsPathBox.Text = SettingsManager.Settings.LogFolder;
        }

        private void ChangeTemplateFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SettingsManager.Settings.TemplateFolder = dialog.SelectedPath;
                TemplateFolderBox.Text = dialog.SelectedPath;
            }
        }

        private void ChangeLogsPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SettingsManager.Settings.LogFolder = dialog.SelectedPath;
                LogsPathBox.Text = dialog.SelectedPath;
            }
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Save();
            System.Windows.MessageBox.Show("Settings saved successfully!", "Lab Assistant", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ReloadSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Reload();
            LoadSettingsIntoUI();
        }
    }
}
