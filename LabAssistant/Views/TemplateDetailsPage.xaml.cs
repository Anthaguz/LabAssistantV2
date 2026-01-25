using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Templates;
using LabAssistant.Models.Configuration;
using LabAssistant.Services.Catalog;
using LabAssistant.Services.Configuration;

namespace LabAssistant.Views;

public partial class TemplateDetailsPage : Page
{
    private readonly LabTemplate _template;
    private readonly string _templateKey;
    public TemplateDetailsPage(LabTemplate template)
    {
        _template = template;
        _templateKey = string.IsNullOrWhiteSpace(_template.Id) ? _template.Name : _template.Id;
        InitializeComponent();
        TemplateNameText.Text = _template.Name;
        TemplateDescriptionText.Text = _template.Description ?? string.Empty;
        ApplyPersistedSelections();
        VmListView.ItemsSource = _template.VmTemplates;
    }

    private void SelectVhdx_Click(object sender, RoutedEventArgs e)
    {
        if (VmListView.SelectedItem is not VmTemplate selectedVm)
        {
            System.Windows.MessageBox.Show("Select a VM first.", "Select VHDX", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }

        var catalogItems = LoadCatalogItems();
        if (catalogItems.Count == 0)
        {
            System.Windows.MessageBox.Show("No VHDX catalog items found.", "Select VHDX", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        var dialog = new VhdxSelectorDialog(catalogItems);
        if (dialog.ShowDialog() == true && dialog.SelectedItem != null)
        {
            selectedVm.VhdxId = dialog.SelectedItem.Id;
            selectedVm.VhdPath = dialog.SelectedItem.Path;
            SaveSelection(selectedVm);
            VmListView.Items.Refresh();
        }
    }

    private List<VhdxCatalogItem> LoadCatalogItems(bool silent = false)
    {
        var catalogPath = SettingsManager.Settings.CatalogPath;
        if (string.IsNullOrWhiteSpace(catalogPath))
        {
            if (!silent)
            {
                System.Windows.MessageBox.Show("Catalog path is not configured.", "Select VHDX", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            return new List<VhdxCatalogItem>();
        }

        var store = new VhdxCatalogStore();
        var result = store.Load(catalogPath);
        if (result.Errors.Count > 0)
        {
            if (!silent)
            {
                System.Windows.MessageBox.Show(string.Join(Environment.NewLine, result.Errors), "Catalog Errors", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }

        return result.Items;
    }

    private void ApplyPersistedSelections()
    {
        var selections = SettingsManager.Settings.TemplateSelections;
        if (selections.Count == 0)
        {
            return;
        }

        var catalogItems = LoadCatalogItems(silent: true);
        var catalogById = catalogItems.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var vm in _template.VmTemplates)
        {
            var selection = selections.FirstOrDefault(item =>
                string.Equals(item.TemplateId, _templateKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.VmName, vm.Name, StringComparison.OrdinalIgnoreCase));

            if (selection != null)
            {
                vm.VhdxId = selection.VhdxId;
                if (!string.IsNullOrWhiteSpace(selection.VhdxId) && catalogById.TryGetValue(selection.VhdxId, out var catalogItem))
                {
                    vm.VhdPath = catalogItem.Path;
                }
                else
                {
                    vm.VhdPath = selection.VhdPath;
                }
            }
        }
    }

    private void SaveSelection(VmTemplate vm)
    {
        var selections = SettingsManager.Settings.TemplateSelections;
        selections.RemoveAll(item =>
            string.Equals(item.TemplateId, _templateKey, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.VmName, vm.Name, StringComparison.OrdinalIgnoreCase));

        selections.Add(new TemplateVhdxSelection
        {
            TemplateId = _templateKey,
            VmName = vm.Name,
            VhdxId = vm.VhdxId ?? string.Empty,
            VhdPath = vm.VhdPath ?? string.Empty
        });

        SettingsManager.Save();
    }
}
