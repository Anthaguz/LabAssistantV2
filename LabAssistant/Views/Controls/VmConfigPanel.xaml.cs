using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace LabAssistant.Views.Controls
{
    public partial class VmConfigPanel : System.Windows.Controls.UserControl
    {
        private readonly IVhdxCatalogStore _catalogStore;
        private readonly IAppSettingsStore _settingsStore;
        private List<VhdxCatalogItem> _catalogItems = new();
        private INotifyPropertyChanged? _contextNotifier;
        private readonly Dictionary<System.Windows.Controls.Control, (MediaBrush brush, Thickness thickness)> _borderDefaults = new();
        private static readonly string[] MandatorySteps =
        [
            "Check Hyper-V",
            "Create VM Folder",
            "Create Differencing Disk",
            "Create VM",
            "Add NIC",
            "Configure VM",
            "Enable Guest Services",
            "Disable Checkpoints",
            "Start VM"
        ];

        private static readonly Dictionary<string, string> OptionalSteps = new()
        {
            [DeploymentStepKeys.SetTimeZone] = "Set Time Zone",
            [DeploymentStepKeys.InstallSoftware] = "Install Software",
            [DeploymentStepKeys.InstallRole] = "Install Role",
            [DeploymentStepKeys.ConfigureNetworkInformation] = "Configure Network Information"
        };

        public VmConfigPanel()
        {
            InitializeComponent();
            _catalogStore = App.Services.GetRequiredService<IVhdxCatalogStore>();
            _settingsStore = App.Services.GetRequiredService<IAppSettingsStore>();
            Loaded += (_, _) =>
            {
                UpdateSelectedVhdxDisplay();
                UpdateDeploymentStepsDisplay();
            };
            DataContextChanged += (_, _) =>
            {
                AttachContextHandlers();
                UpdateSelectedVhdxDisplay();
                UpdateValidationIndicators();
                UpdateDeploymentStepsDisplay();
            };
        }

        public void FocusNameField()
        {
            FocusTextBox(NameBox);
        }

        public void FocusMemoryField()
        {
            FocusTextBox(MemoryBox);
        }

        public void FocusCpuField()
        {
            FocusTextBox(CpuBox);
        }

        public void FocusSwitchField()
        {
            SwitchBox.Focus();
        }

        public void FocusVhdxField()
        {
            SelectVhdxButton.Focus();
        }

        private IVmConfigContext? Context => DataContext as IVmConfigContext;

        private void SelectVhdx_Click(object sender, RoutedEventArgs e)
        {
            if (Context == null)
            {
                return;
            }

            LoadCatalog(showErrors: true);
            if (_catalogItems.Count == 0)
            {
                System.Windows.MessageBox.Show(
                    "The VHDX catalog is empty. Add a VHDX first.",
                    "Select VHDX",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var dialog = new VhdxSelectorDialog(_catalogItems)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true && dialog.SelectedItem != null)
            {
                ApplyCatalogSelection(dialog.SelectedItem);
            }
        }

        private void AddVhdx_Click(object sender, RoutedEventArgs e)
        {
            if (Context == null)
            {
                return;
            }

            var dialog = new VhdxCatalogEditDialog
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            LoadCatalog(showErrors: false);
            var duplicatePath = FindDuplicatePath(dialog.Item.Path);
            if (duplicatePath != null)
            {
                var choice = System.Windows.MessageBox.Show(
                    $"A catalog entry already exists for this path:{Environment.NewLine}{duplicatePath.Path}{Environment.NewLine}{Environment.NewLine}Use the existing entry instead?",
                    "Duplicate VHDX Path",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (choice == MessageBoxResult.Yes)
                {
                    ApplyCatalogSelection(duplicatePath);
                    return;
                }

                if (choice != MessageBoxResult.No)
                {
                    return;
                }
            }

            if (HasDuplicateId(dialog.Item.Id))
            {
                System.Windows.MessageBox.Show(
                    "Catalog id must be unique.",
                    "VHDX Catalog",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            _catalogItems.Add(dialog.Item);
            if (!SaveCatalog())
            {
                _catalogItems.Remove(dialog.Item);
                return;
            }

            ApplyCatalogSelection(dialog.Item);
        }

        private void ApplyCatalogSelection(VhdxCatalogItem item)
        {
            if (Context == null)
            {
                return;
            }

            Context.VhdxId = item.Id;
            Context.VhdPath = item.Path;
            Context.VhdxSignature = VhdxSignature.Build(item);
            UpdateSelectedVhdxDisplay();
        }

        private void LoadCatalog(bool showErrors)
        {
            var result = _catalogStore.Load(_settingsStore.Settings.CatalogPath);
            _catalogItems = result.Items.ToList();

            if (showErrors && result.Errors.Count > 0)
            {
                System.Windows.MessageBox.Show(
                    string.Join(Environment.NewLine, result.Errors),
                    "Catalog Errors",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private bool SaveCatalog()
        {
            var result = _catalogStore.Save(_settingsStore.Settings.CatalogPath, _catalogItems);
            if (!result.IsValid)
            {
                System.Windows.MessageBox.Show(
                    string.Join(Environment.NewLine, result.Errors),
                    "Catalog Save Errors",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private bool HasDuplicateId(string id)
        {
            return _catalogItems.Any(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        private VhdxCatalogItem? FindDuplicatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            return _catalogItems.FirstOrDefault(item =>
                string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase));
        }

        private void UpdateSelectedVhdxDisplay()
        {
            if (Context == null)
            {
                return;
            }

            LoadCatalog(showErrors: false);
            var selected = _catalogItems.FirstOrDefault(item =>
                !string.IsNullOrWhiteSpace(Context.VhdxId) &&
                string.Equals(item.Id, Context.VhdxId, StringComparison.OrdinalIgnoreCase));

            if (selected != null)
            {
                SelectedVhdxText.Text = $"{selected.OsName} {selected.OsVersion} (Gen {selected.Generation})";
                SelectedVhdxPathText.Text = selected.Path;
                return;
            }

            if (!string.IsNullOrWhiteSpace(Context.VhdPath))
            {
                SelectedVhdxText.Text = "Custom VHDX path";
                SelectedVhdxPathText.Text = Context.VhdPath!;
                return;
            }

            SelectedVhdxText.Text = "No VHDX selected";
            SelectedVhdxPathText.Text = string.Empty;
            UpdateValidationIndicators();
        }

        private void UpdateDeploymentStepsDisplay()
        {
            MandatoryStepsList.ItemsSource = MandatorySteps;
            var nonBlocking = _settingsStore.Settings.NonBlockingOptionalSteps ?? new List<string>();
            OptionalStepsList.ItemsSource = OptionalSteps
                .Select(kvp =>
                    nonBlocking.Contains(kvp.Key)
                        ? $"{kvp.Value} (non-blocking)"
                        : $"{kvp.Value} (blocking)")
                .ToList();
        }

        private void AttachContextHandlers()
        {
            if (_contextNotifier != null)
            {
                _contextNotifier.PropertyChanged -= ContextOnPropertyChanged;
                _contextNotifier = null;
            }

            _contextNotifier = Context as INotifyPropertyChanged;
            if (_contextNotifier != null)
            {
                _contextNotifier.PropertyChanged += ContextOnPropertyChanged;
            }
        }

        private void ContextOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(IVmConfigContext.Name)
                or nameof(IVmConfigContext.MemoryMb)
                or nameof(IVmConfigContext.CpuCount)
                or nameof(IVmConfigContext.SwitchName)
                or nameof(IVmConfigContext.VhdxId)
                or nameof(IVmConfigContext.VhdPath))
            {
                UpdateValidationIndicators();
            }
        }

        private void UpdateValidationIndicators()
        {
            if (Context == null)
            {
                return;
            }

            var nameError = string.IsNullOrWhiteSpace(Context.Name)
                ? "VM name is required."
                : string.Empty;
            SetWarning(NameBox, NameWarningText, nameError, isError: true);

            var memoryError = Context.MemoryMb <= 0
                ? "Memory must be a positive number."
                : string.Empty;
            SetWarning(MemoryBox, MemoryWarningText, memoryError, isError: true);

            var cpuError = Context.CpuCount <= 0
                ? "CPU count must be a positive number."
                : string.Empty;
            SetWarning(CpuBox, CpuWarningText, cpuError, isError: true);

            var switchWarning = string.Empty;
            if (Context.HasSwitches && string.IsNullOrWhiteSpace(Context.SwitchName))
            {
                switchWarning = "Select a virtual switch.";
            }
            SetWarning(SwitchBox, SwitchWarningText, switchWarning, isError: false);

            var vhdxWarning = string.Empty;
            if (string.IsNullOrWhiteSpace(Context.VhdxId) && string.IsNullOrWhiteSpace(Context.VhdPath))
            {
                vhdxWarning = "Select a base VHDX before deployment.";
            }
            else if (!string.IsNullOrWhiteSpace(Context.VhdxId))
            {
                LoadCatalog(showErrors: false);
                var hasMatch = _catalogItems.Any(item =>
                    string.Equals(item.Id, Context.VhdxId, StringComparison.OrdinalIgnoreCase));
                if (!hasMatch)
                {
                    vhdxWarning = "Selected VHDX is not in the local catalog.";
                }
            }

            SetWarning(VhdxWarningText, vhdxWarning, isError: false);
        }

        private void SetWarning(TextBlock target, string message, bool isError)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                target.Visibility = Visibility.Collapsed;
                target.Text = string.Empty;
                return;
            }

            target.Text = message;
            target.Foreground = isError ? MediaBrushes.IndianRed : MediaBrushes.DarkOrange;
            target.Visibility = Visibility.Visible;
        }

        private void SetWarning(System.Windows.Controls.Control control, TextBlock target, string message, bool isError)
        {
            SetWarning(target, message, isError);
            SetControlBorder(control, !string.IsNullOrWhiteSpace(message), isError ? MediaBrushes.IndianRed : MediaBrushes.DarkOrange);
        }

        private void SetControlBorder(System.Windows.Controls.Control control, bool highlight, MediaBrush brush)
        {
            if (!_borderDefaults.TryGetValue(control, out var defaults))
            {
                defaults = (control.BorderBrush, control.BorderThickness);
                _borderDefaults[control] = defaults;
            }

            if (!highlight)
            {
                control.BorderBrush = defaults.brush;
                control.BorderThickness = defaults.thickness;
                return;
            }

            control.BorderBrush = brush;
            control.BorderThickness = new Thickness(1.5);
        }

        private static void FocusTextBox(System.Windows.Controls.TextBox textBox)
        {
            textBox.Focus();
            textBox.SelectAll();
        }
    }
}
