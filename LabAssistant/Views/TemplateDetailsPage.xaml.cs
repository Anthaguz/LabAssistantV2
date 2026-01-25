using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Templates;
using LabAssistant.Services.Catalog;

namespace LabAssistant.Views;

public partial class TemplateDetailsPage : Page
{
    private static readonly string CatalogPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "LabAssistant", "catalog", "vhdx-catalog.json");

    private readonly LabTemplate _template;
    private readonly VhdxCatalogLoader _catalogLoader = new VhdxCatalogLoader();

    public TemplateDetailsPage(LabTemplate template)
    {
        _template = template;
        InitializeComponent();
        TemplateNameText.Text = _template.Name;
        TemplateDescriptionText.Text = _template.Description ?? string.Empty;
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
            VmListView.Items.Refresh();
        }
    }

    private List<VhdxCatalogItem> LoadCatalogItems()
    {
        if (!File.Exists(CatalogPath))
        {
            System.Windows.MessageBox.Show($"Catalog file not found: {CatalogPath}", "Select VHDX", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return new List<VhdxCatalogItem>();
        }

        var result = _catalogLoader.Load(CatalogPath);
        if (result.Errors.Count > 0)
        {
            System.Windows.MessageBox.Show(string.Join(Environment.NewLine, result.Errors), "Catalog Errors", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }

        return result.Items;
    }
}
