using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LabAssistant.Business.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Templates;
using LabAssistant.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace LabAssistant.Views
{
    public partial class TemplateEditorPage : Page
    {
        private readonly TemplateEditorViewModel _viewModel;
        private readonly IAppSettingsStore _settingsStore;
        private readonly IAppPaths _appPaths;
        private VmValidationField? _pendingFieldFocus;
        private List<VmValidationDisplayItem> _validationItems = new();
        private bool _isValidationCollapsed;
        private double _vmListScrollOffset;

        public TemplateEditorPage()
        {
            InitializeComponent();
            _viewModel = App.Services.GetRequiredService<TemplateEditorViewModel>();
            _settingsStore = App.Services.GetRequiredService<IAppSettingsStore>();
            _appPaths = App.Services.GetRequiredService<IAppPaths>();
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

            UpdateValidationPanel();
            _vmListScrollOffset = VmListScrollViewer.VerticalOffset;
            VmListPanel.Visibility = Visibility.Collapsed;
            VmDetailFrame.Visibility = Visibility.Visible;
            BeginPanelFade(VmDetailFrame, fadeIn: true);
            VmDetailFrame.Navigate(new TemplateVmDetailPage(_viewModel, selectedVm, ShowVmList, () =>
            {
                var focus = _pendingFieldFocus;
                _pendingFieldFocus = null;
                if (focus.HasValue)
                {
                    FocusField(focus.Value);
                }
            }));
        }

        private void ShowVmList()
        {
            UpdateValidationPanel();
            _vmListScrollOffset = VmListScrollViewer.VerticalOffset;
            VmDetailFrame.Content = null;
            VmDetailFrame.Visibility = Visibility.Collapsed;
            VmListPanel.Visibility = Visibility.Visible;
            BeginPanelFade(VmListPanel, fadeIn: true);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                VmListScrollViewer.ScrollToVerticalOffset(_vmListScrollOffset);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
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
                    ResolveMissingVhdxReferences();
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

        private string GetInitialTemplateDirectory()
        {
            var folder = _settingsStore.Settings.TemplateFolder;
            return string.IsNullOrWhiteSpace(folder)
                ? _appPaths.TemplatesFolder
                : folder;
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
            var state = _viewModel.BuildSaveValidationState();
            if (state.VmIssues.Count > 0)
            {
                UpdateValidationPanel();
                return false;
            }

            if (state.MissingReferences.Count > 0)
            {
                var message = "Resolve missing VHDX mappings before saving:" + Environment.NewLine
                              + string.Join(Environment.NewLine, state.MissingReferences.Select(item => $"- {item.VmName} missing '{item.MissingId}'"));
                System.Windows.MessageBox.Show(
                    message,
                    "Missing VHDX",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (state.Summary.Errors.Count > 0)
            {
                var message = "Fix the following before saving:" + Environment.NewLine
                              + string.Join(Environment.NewLine, state.Summary.Errors.Select(error => $"- {error}"));
                System.Windows.MessageBox.Show(
                    message,
                    "Validation Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            if (state.Summary.Warnings.Count > 0)
            {
                var message = "Warnings:" + Environment.NewLine
                              + string.Join(Environment.NewLine, state.Summary.Warnings.Select(warning => $"- {warning}"));
                System.Windows.MessageBox.Show(
                    message,
                    "Validation Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            return true;
        }

        private void UpdateValidationPanel()
        {
            var issues = _viewModel.BuildVmValidationDisplayItems();
            if (issues.Count == 0)
            {
                ValidationPanel.Visibility = Visibility.Collapsed;
                ValidationItemsControl.ItemsSource = null;
                return;
            }

            _validationItems = issues;
            ApplyValidationFilter();
            ValidationPanel.Visibility = Visibility.Visible;
            ValidationContentPanel.Visibility = _isValidationCollapsed ? Visibility.Collapsed : Visibility.Visible;
            ValidationToggleButton.Content = _isValidationCollapsed ? "Expand" : "Collapse";
        }

        private void ValidationItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button button || button.Tag is not VmValidationDisplayItem item)
            {
                return;
            }

            UpdateValidationPanel();
            _pendingFieldFocus = item.Field;

            _vmListScrollOffset = VmListScrollViewer.VerticalOffset;
            VmListPanel.Visibility = Visibility.Collapsed;
            VmDetailFrame.Visibility = Visibility.Visible;
            BeginPanelFade(VmDetailFrame, fadeIn: true);
            VmDetailFrame.Navigate(new TemplateVmDetailPage(_viewModel, item.Vm, ShowVmList, () => FocusField(item.Field)));
        }

        private void BeginPanelFade(UIElement element, bool fadeIn)
        {
            if (element == null)
            {
                return;
            }

            var storyboard = (System.Windows.Media.Animation.Storyboard)Resources[fadeIn ? "FadeIn" : "FadeOut"];
            if (element is FrameworkElement frameworkElement)
            {
                frameworkElement.BeginStoryboard(storyboard);
            }
        }

        private void ValidationToggle_Click(object sender, RoutedEventArgs e)
        {
            _isValidationCollapsed = !_isValidationCollapsed;
            ValidationContentPanel.Visibility = _isValidationCollapsed ? Visibility.Collapsed : Visibility.Visible;
            ValidationToggleButton.Content = _isValidationCollapsed ? "Expand" : "Collapse";
        }

        private void ValidationFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyValidationFilter();
        }

        private void ApplyValidationFilter()
        {
            if (_validationItems.Count == 0)
            {
                ValidationItemsControl.ItemsSource = null;
                return;
            }

            var filtered = _viewModel.FilterVmValidationDisplayItems(
                _validationItems,
                ValidationFilterBox.Text);
            ValidationItemsControl.ItemsSource = filtered;
        }

        private void FocusField(VmValidationField field)
        {
            if (VmDetailFrame.Content is not TemplateVmDetailPage page)
            {
                return;
            }

            switch (field)
            {
                case VmValidationField.Name:
                    page.FocusField(VmDetailField.Name);
                    break;
                case VmValidationField.MemoryMb:
                    page.FocusField(VmDetailField.Memory);
                    break;
                case VmValidationField.CpuCount:
                    page.FocusField(VmDetailField.Cpu);
                    break;
                case VmValidationField.SwitchName:
                    page.FocusField(VmDetailField.Switch);
                    break;
                case VmValidationField.Vhdx:
                    page.FocusField(VmDetailField.Vhdx);
                    break;
            }
        }

        private void ResolveMissingVhdxReferences()
        {
            var resolution = _viewModel.ResolveMissingVhdxForDialog();
            if (resolution.MissingReferences.Count == 0)
            {
                return;
            }

            if (resolution.CatalogErrors.Count > 0)
            {
                System.Windows.MessageBox.Show(
                    string.Join(Environment.NewLine, resolution.CatalogErrors),
                    "Catalog Errors",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            var dialog = new MissingVhdxResolutionDialog(
                resolution.MissingReferences,
                App.Services.GetRequiredService<CatalogService>())
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() != true)
            {
                System.Windows.MessageBox.Show(
                    "Missing VHDX mappings were not resolved. Saving and deployment will be blocked until resolved.",
                    "Missing VHDX",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            _viewModel.ApplyResolvedMissingVhdx(dialog.GetResolvedSelections());
        }
    }

}
