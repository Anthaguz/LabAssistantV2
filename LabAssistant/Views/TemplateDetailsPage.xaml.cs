using System;
using System.Windows;
using System.Windows.Controls;
using LabAssistant.Business.Catalog;
using LabAssistant.Models.Templates;
using LabAssistant.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace LabAssistant.Views;

public partial class TemplateDetailsPage : Page
{
    private readonly LabTemplate _template;
    private readonly TemplateDetailsViewModel _viewModel;

    public TemplateDetailsPage(LabTemplate template)
    {
        _template = template;
        _viewModel = App.Services.GetRequiredService<TemplateDetailsViewModel>();
        _viewModel.Initialize(_template);
        InitializeComponent();
        TemplateNameText.Text = _template.Name;
        TemplateDescriptionText.Text = _template.Description ?? string.Empty;
        ResolveMissingVhdxSelections();
        VmListView.ItemsSource = _template.VmTemplates;
    }

    private void SelectVhdx_Click(object sender, RoutedEventArgs e)
    {
        if (VmListView.SelectedItem is not VmTemplate selectedVm)
        {
            System.Windows.MessageBox.Show("Select a VM first.", "Select VHDX", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }

        var catalog = _viewModel.LoadCatalogItems();
        if (catalog.Errors.Count > 0)
        {
            System.Windows.MessageBox.Show(string.Join(Environment.NewLine, catalog.Errors), "Catalog Errors", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }

        if (catalog.Items.Count == 0)
        {
            System.Windows.MessageBox.Show("No VHDX catalog items found.", "Select VHDX", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        var dialog = new VhdxSelectorDialog(catalog.Items);
        if (dialog.ShowDialog() == true && dialog.SelectedItem != null)
        {
            _viewModel.SaveSelection(selectedVm, dialog.SelectedItem);
            VmListView.Items.Refresh();
        }
    }

    private void ShowCompatibilityWarnings()
    {
        var catalog = _viewModel.LoadCatalogItems();
        var warnings = _viewModel.BuildCompatibilityWarnings(_template, catalog.Items);
        if (warnings.Count == 0)
        {
            return;
        }

        System.Windows.MessageBox.Show(string.Join(Environment.NewLine, warnings), "VHDX Compatibility Warnings", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void ResolveMissingVhdxSelections()
    {
        var resolution = _viewModel.ResolveMissingVhdxForDialog(_template);
        if (resolution.MissingReferences.Count == 0)
        {
            return;
        }

        if (resolution.CatalogErrors.Count > 0)
        {
            System.Windows.MessageBox.Show(
                string.Join(Environment.NewLine, resolution.CatalogErrors),
                "Catalog Errors",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
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
                "Missing VHDX mappings were not resolved. You can import new catalog entries and try again.",
                "Missing VHDX",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        _viewModel.ApplyResolvedMissingVhdx(dialog.GetResolvedSelections());
    }
}
