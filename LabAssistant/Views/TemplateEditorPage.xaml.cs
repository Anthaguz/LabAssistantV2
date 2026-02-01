using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LabAssistant.Models.Templates;
using LabAssistant.Services.Configuration;
using LabAssistant.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace LabAssistant.Views
{
    public partial class TemplateEditorPage : Page
    {
        private readonly TemplateEditorViewModel _viewModel;

        public TemplateEditorPage()
        {
            InitializeComponent();
            _viewModel = App.Services.GetRequiredService<TemplateEditorViewModel>();
            DataContext = _viewModel;
        }

        private void AddVm_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.AddVm();
        }

        private void RemoveVm_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button button || button.Tag is not VmTemplate selectedVm)
            {
                System.Windows.MessageBox.Show(
                    "Select a VM row to remove.",
                    "Remove VM",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var result = System.Windows.MessageBox.Show(
                $"Remove VM '{selectedVm.Name}'?",
                "Confirm Remove",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _viewModel.RemoveVm(selectedVm);
            }
        }

        private void OpenVmDetail_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button button || button.Tag is not VmTemplate selectedVm)
            {
                return;
            }

            VmListPanel.Visibility = Visibility.Collapsed;
            VmDetailFrame.Visibility = Visibility.Visible;
            VmDetailFrame.Navigate(new TemplateVmDetailPage(_viewModel, selectedVm, ShowVmList));
        }

        private void ShowVmList()
        {
            VmDetailFrame.Content = null;
            VmDetailFrame.Visibility = Visibility.Collapsed;
            VmListPanel.Visibility = Visibility.Visible;
        }

        private void OpenTemplate_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                InitialDirectory = GetInitialTemplateDirectory()
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _viewModel.LoadFromFile(dialog.FileName);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Failed to load template: {ex.Message}",
                        "Load Template",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void SaveTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_viewModel.CurrentTemplatePath))
            {
                SaveTemplateAs_Click(sender, e);
                return;
            }

            if (!ValidateBeforeSave())
            {
                return;
            }

            try
            {
                _viewModel.SaveToFile(_viewModel.CurrentTemplatePath);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to save template: {ex.Message}",
                    "Save Template",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void SaveTemplateAs_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                InitialDirectory = GetInitialTemplateDirectory(),
                FileName = GetDefaultTemplateFileName()
            };

            if (dialog.ShowDialog() == true)
            {
                if (!ValidateBeforeSave())
                {
                    return;
                }

                try
                {
                    _viewModel.SaveToFile(dialog.FileName);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Failed to save template: {ex.Message}",
                        "Save Template As",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private static string GetInitialTemplateDirectory()
        {
            var folder = SettingsManager.Settings.TemplateFolder;
            return string.IsNullOrWhiteSpace(folder) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : folder;
        }

        private string GetDefaultTemplateFileName()
        {
            if (!string.IsNullOrWhiteSpace(_viewModel.CurrentTemplatePath))
            {
                return Path.GetFileName(_viewModel.CurrentTemplatePath);
            }

            if (!string.IsNullOrWhiteSpace(_viewModel.Template.Id))
            {
                return $"{_viewModel.Template.Id}.json";
            }

            return "lab-template.json";
        }

        private bool ValidateBeforeSave()
        {
            var summary = _viewModel.ValidateForSave();
            if (summary.Errors.Count > 0)
            {
                var message = "Fix the following before saving:" + Environment.NewLine
                              + string.Join(Environment.NewLine, summary.Errors.Select(error => $"- {error}"));
                System.Windows.MessageBox.Show(
                    message,
                    "Validation Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            if (summary.Warnings.Count > 0)
            {
                var message = "Warnings:" + Environment.NewLine
                              + string.Join(Environment.NewLine, summary.Warnings.Select(warning => $"- {warning}"));
                System.Windows.MessageBox.Show(
                    message,
                    "Validation Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            return true;
        }
    }
}
