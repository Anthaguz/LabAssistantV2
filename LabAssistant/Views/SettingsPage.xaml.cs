using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Services.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace LabAssistant.Views
{
    public partial class SettingsPage : Page
    {
        private readonly IAppSettingsStore _settingsStore;

        public SettingsPage()
        {
            InitializeComponent();
            _settingsStore = App.Services.GetRequiredService<IAppSettingsStore>();
            LoadSettingsIntoUI();
        }

        private void LoadSettingsIntoUI()
        {
            TemplateFolderBox.Text = _settingsStore.Settings.TemplateFolder;
            LogsPathBox.Text = _settingsStore.Settings.LogFolder;
            PerVmFailFastBox.IsChecked = _settingsStore.Settings.PerVmFailFast;
            StopAllOnAnyVmFailureBox.IsChecked = _settingsStore.Settings.StopAllOnAnyVmFailure;

            var nonBlocking = _settingsStore.Settings.NonBlockingOptionalSteps ?? new List<string>();
            OptionalSetTimeZoneBox.IsChecked = nonBlocking.Contains(DeploymentStepKeys.SetTimeZone);
            OptionalInstallSoftwareBox.IsChecked = nonBlocking.Contains(DeploymentStepKeys.InstallSoftware);
            OptionalInstallRoleBox.IsChecked = nonBlocking.Contains(DeploymentStepKeys.InstallRole);
            OptionalConfigureNetworkBox.IsChecked = nonBlocking.Contains(DeploymentStepKeys.ConfigureNetworkInformation);
        }

        private void ChangeTemplateFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _settingsStore.Settings.TemplateFolder = dialog.SelectedPath;
                TemplateFolderBox.Text = dialog.SelectedPath;
            }
        }

        private void ChangeLogsPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _settingsStore.Settings.LogFolder = dialog.SelectedPath;
                LogsPathBox.Text = dialog.SelectedPath;
                DebugLogger.SetLogFolder(dialog.SelectedPath);
            }
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            _settingsStore.Settings.PerVmFailFast = PerVmFailFastBox.IsChecked == true;
            _settingsStore.Settings.StopAllOnAnyVmFailure = StopAllOnAnyVmFailureBox.IsChecked == true;

            var nonBlocking = new List<string>();
            if (OptionalSetTimeZoneBox.IsChecked == true) nonBlocking.Add(DeploymentStepKeys.SetTimeZone);
            if (OptionalInstallSoftwareBox.IsChecked == true) nonBlocking.Add(DeploymentStepKeys.InstallSoftware);
            if (OptionalInstallRoleBox.IsChecked == true) nonBlocking.Add(DeploymentStepKeys.InstallRole);
            if (OptionalConfigureNetworkBox.IsChecked == true) nonBlocking.Add(DeploymentStepKeys.ConfigureNetworkInformation);
            _settingsStore.Settings.NonBlockingOptionalSteps = nonBlocking;

            _settingsStore.Save();
            System.Windows.MessageBox.Show("Settings saved successfully!", "Lab Assistant", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ReloadSettings_Click(object sender, RoutedEventArgs e)
        {
            _settingsStore.Reload();
            LoadSettingsIntoUI();
        }
    }
}
