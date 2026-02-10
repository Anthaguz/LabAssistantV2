using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LabAssistant.Business.Catalog;
using LabAssistant.Business.Compatibility;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Templates;
using LabAssistant.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace LabAssistant.Views;

public partial class TemplateDetailsPage : Page
{
    private readonly LabTemplate _template;
    private readonly string _templateKey;
    private readonly CatalogService _catalogService;
    private readonly MissingVhdxResolutionService _missingVhdxResolutionService;
    private readonly TemplateSelectionService _templateSelectionService;
    private readonly Dictionary<string, string> _requiredVhdxIdsByVmName = new(StringComparer.OrdinalIgnoreCase);

    public TemplateDetailsPage(LabTemplate template)
    {
        _template = template;
        _templateKey = string.IsNullOrWhiteSpace(_template.Id) ? _template.Name : _template.Id;
        _catalogService = App.Services.GetRequiredService<CatalogService>();
        _missingVhdxResolutionService = App.Services.GetRequiredService<MissingVhdxResolutionService>();
        _templateSelectionService = App.Services.GetRequiredService<TemplateSelectionService>();
        CaptureRequiredVhdxIds();
        InitializeComponent();
        TemplateNameText.Text = _template.Name;
        TemplateDescriptionText.Text = _template.Description ?? string.Empty;
        ApplyPersistedSelections();
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
            _templateSelectionService.SaveSelection(_templateKey, selectedVm);
            VmListView.Items.Refresh();
        }
    }

    private List<VhdxCatalogItem> LoadCatalogItems(bool silent = false)
    {
        var catalogPath = _catalogService.CatalogPath;
        if (string.IsNullOrWhiteSpace(catalogPath))
        {
            if (!silent)
            {
                System.Windows.MessageBox.Show("Catalog path is not configured.", "Select VHDX", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            return new List<VhdxCatalogItem>();
        }

        var result = _catalogService.LoadCatalog();
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
        var catalogItems = LoadCatalogItems(silent: true);
        _templateSelectionService.ApplySelections(_templateKey, _template, catalogItems);
    }

    private void CaptureRequiredVhdxIds()
    {
        foreach (var vm in _template.VmTemplates)
        {
            if (!string.IsNullOrWhiteSpace(vm.VhdxId))
            {
                _requiredVhdxIdsByVmName[vm.Name] = vm.VhdxId!;
            }
        }
    }

    private void ShowCompatibilityWarnings()
    {
        if (_requiredVhdxIdsByVmName.Count == 0)
        {
            return;
        }

        var catalogItems = LoadCatalogItems(silent: true);
        if (catalogItems.Count == 0)
        {
            return;
        }

        var catalogById = catalogItems.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();

        foreach (var vm in _template.VmTemplates)
        {
            if (!_requiredVhdxIdsByVmName.TryGetValue(vm.Name, out var requiredId))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(vm.VhdxId) ||
                !catalogById.TryGetValue(requiredId, out var required) ||
                !catalogById.TryGetValue(vm.VhdxId, out var selected))
            {
                continue;
            }

            warnings.AddRange(VhdxCompatibilityChecker.Check(required, selected, vm.Name));
        }

        if (warnings.Count > 0)
        {
            System.Windows.MessageBox.Show(string.Join(Environment.NewLine, warnings), "VHDX Compatibility Warnings", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void ResolveMissingVhdxSelections()
    {
        var resolution = _missingVhdxResolutionService.ResolveMissingVhdx(_template);
        if (resolution.MissingVms.Count == 0)
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

        var missing = resolution.MissingVms
            .Where(vm => !string.IsNullOrWhiteSpace(vm.VhdxId))
            .Select(vm => new MissingVhdxReference(vm, vm.VhdxId!))
            .ToList();

        var dialog = new MissingVhdxResolutionDialog(missing, _catalogService)
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

        foreach (var item in dialog.Items)
        {
            if (item.SelectedOption == null)
            {
                continue;
            }

            item.Reference.Vm.VhdxId = item.SelectedOption.Item.Id;
            item.Reference.Vm.VhdPath = item.SelectedOption.Item.Path;
            item.Reference.Vm.VhdxSignature = VhdxSignature.Build(item.SelectedOption.Item);
            _templateSelectionService.SaveSelection(_templateKey, item.Reference.Vm);
        }
    }
}
