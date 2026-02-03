using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace LabAssistant.Views.Controls
{
    public partial class VmConfigPanel : System.Windows.Controls.UserControl
    {
        private readonly IVhdxCatalogStore _catalogStore;
        private readonly IAppSettingsStore _settingsStore;
        private List<VhdxCatalogItem> _catalogItems = new();

        public VmConfigPanel()
        {
            InitializeComponent();
            _catalogStore = App.Services.GetRequiredService<IVhdxCatalogStore>();
            _settingsStore = App.Services.GetRequiredService<IAppSettingsStore>();
            Loaded += (_, _) => UpdateSelectedVhdxDisplay();
            DataContextChanged += (_, _) => UpdateSelectedVhdxDisplay();
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
        }
    }
}
