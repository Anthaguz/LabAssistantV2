using System.Windows;
using System.Windows.Controls;
using Forms = System.Windows.Forms;
using LabAssistant.Services;


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
            TemplatePathsList.ItemsSource = null;
            TemplatePathsList.ItemsSource = SettingsManager.Current.TemplatePaths;
            LogsPathBox.Text = SettingsManager.Current.LogsPath;
        }

        private void AddPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SettingsManager.Current.TemplatePaths.Add(dialog.SelectedPath);
                LoadSettingsIntoUI();
            }
        }

        private void RemovePath_Click(object sender, RoutedEventArgs e)
        {
            if (TemplatePathsList.SelectedItem is string selectedPath)
            {
                SettingsManager.Current.TemplatePaths.Remove(selectedPath);
                LoadSettingsIntoUI();
            }
        }

        private void ChangeLogsPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SettingsManager.Current.LogsPath = dialog.SelectedPath;
                LogsPathBox.Text = dialog.SelectedPath;
            }
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.SaveSettings();
            System.Windows.MessageBox.Show("Settings saved successfully!", "Lab Assistant", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ReloadSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.ReloadSettings();
            LoadSettingsIntoUI();
        }
    }
}
