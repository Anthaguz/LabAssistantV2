using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Templates;
using LabAssistant.Services.Catalog;
using LabAssistant.Services.Configuration;
using LabAssistant.ViewModels;

namespace LabAssistant.Views
{
    public partial class TemplateVmDetailPage : Page
    {
        private readonly VhdxCatalogStore _catalogStore = new();
        private readonly Action _onBack;
        private readonly VmTemplate _vmTemplate;
        private List<VhdxCatalogItem> _catalogItems = new();

        public TemplateVmDetailPage(TemplateEditorViewModel editorViewModel, VmTemplate vmTemplate, Action onBack)
        {
            InitializeComponent();
            _vmTemplate = vmTemplate;
            DataContext = new TemplateVmDetailViewModel(editorViewModel, vmTemplate);
            _onBack = onBack;
            LoadCatalog(showErrors: false);
            UpdateSelectedVhdxDisplay();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            _onBack();
        }

        private void SelectVhdx_Click(object sender, RoutedEventArgs e)
        {
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
            var dialog = new VhdxCatalogEditDialog
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            LoadCatalog(showErrors: false);
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
            _vmTemplate.VhdxId = item.Id;
            _vmTemplate.VhdPath = item.Path;
            UpdateSelectedVhdxDisplay();
        }

        private void LoadCatalog(bool showErrors)
        {
            var result = _catalogStore.Load(SettingsManager.Settings.CatalogPath);
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
            var result = _catalogStore.Save(SettingsManager.Settings.CatalogPath, _catalogItems);
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

        private void UpdateSelectedVhdxDisplay()
        {
            var selected = _catalogItems.FirstOrDefault(item =>
                !string.IsNullOrWhiteSpace(_vmTemplate.VhdxId) &&
                string.Equals(item.Id, _vmTemplate.VhdxId, StringComparison.OrdinalIgnoreCase));

            if (selected != null)
            {
                SelectedVhdxText.Text = $"{selected.OsName} {selected.OsVersion} (Gen {selected.Generation})";
                SelectedVhdxPathText.Text = selected.Path;
                return;
            }

            if (!string.IsNullOrWhiteSpace(_vmTemplate.VhdPath))
            {
                SelectedVhdxText.Text = "Custom VHDX path";
                SelectedVhdxPathText.Text = _vmTemplate.VhdPath!;
                return;
            }

            SelectedVhdxText.Text = "No VHDX selected";
            SelectedVhdxPathText.Text = string.Empty;
        }
    }
}
