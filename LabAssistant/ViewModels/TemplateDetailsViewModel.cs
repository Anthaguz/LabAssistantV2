using System;
using System.Collections.Generic;
using System.Linq;
using LabAssistant.Business.Catalog;
using LabAssistant.Business.Compatibility;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Templates;

namespace LabAssistant.ViewModels;

public sealed class TemplateDetailsViewModel
{
    private readonly CatalogService _catalogService;
    private readonly MissingVhdxResolutionService _missingVhdxResolutionService;
    private readonly TemplateSelectionService _templateSelectionService;
    private readonly Dictionary<string, string> _requiredVhdxIdsByVmName = new(StringComparer.OrdinalIgnoreCase);
    private string _templateKey = string.Empty;

    public TemplateDetailsViewModel(
        CatalogService catalogService,
        MissingVhdxResolutionService missingVhdxResolutionService,
        TemplateSelectionService templateSelectionService)
    {
        _catalogService = catalogService;
        _missingVhdxResolutionService = missingVhdxResolutionService;
        _templateSelectionService = templateSelectionService;
    }

    public void Initialize(LabTemplate template)
    {
        _templateKey = string.IsNullOrWhiteSpace(template.Id) ? template.Name : template.Id;
        CaptureRequiredVhdxIds(template);
        ApplyPersistedSelections(template);
    }

    public CatalogLoadState LoadCatalogItems()
    {
        if (string.IsNullOrWhiteSpace(_catalogService.CatalogPath))
        {
            return new CatalogLoadState(new List<VhdxCatalogItem>(), new List<string> { "Catalog path is not configured." });
        }

        var result = _catalogService.LoadCatalog();
        return new CatalogLoadState(result.Items, result.Errors);
    }

    public void SaveSelection(VmTemplate vm, VhdxCatalogItem item)
    {
        vm.VhdxId = item.Id;
        vm.VhdPath = item.Path;
        _templateSelectionService.SaveSelection(_templateKey, vm);
    }

    public MissingVhdxDialogState ResolveMissingVhdxForDialog(LabTemplate template)
    {
        var resolution = _missingVhdxResolutionService.ResolveMissingVhdx(template);
        var missing = resolution.MissingVms
            .Where(vm => !string.IsNullOrWhiteSpace(vm.VhdxId))
            .Select(vm => new MissingVhdxReference(vm, vm.VhdxId!))
            .ToList();

        return new MissingVhdxDialogState(
            resolution.CatalogErrors,
            missing);
    }

    public void ApplyResolvedMissingVhdx(IEnumerable<ResolvedVhdxSelection> selections)
    {
        foreach (var selection in selections)
        {
            selection.Vm.VhdxId = selection.Item.Id;
            selection.Vm.VhdPath = selection.Item.Path;
            selection.Vm.VhdxSignature = VhdxSignature.Build(selection.Item);
            _templateSelectionService.SaveSelection(_templateKey, selection.Vm);
        }
    }

    public List<string> BuildCompatibilityWarnings(LabTemplate template, IReadOnlyList<VhdxCatalogItem> catalogItems)
    {
        if (_requiredVhdxIdsByVmName.Count == 0 || catalogItems.Count == 0)
        {
            return new List<string>();
        }

        var catalogById = catalogItems.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();

        foreach (var vm in template.VmTemplates)
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

        return warnings;
    }

    private void CaptureRequiredVhdxIds(LabTemplate template)
    {
        _requiredVhdxIdsByVmName.Clear();
        foreach (var vm in template.VmTemplates)
        {
            if (!string.IsNullOrWhiteSpace(vm.VhdxId))
            {
                _requiredVhdxIdsByVmName[vm.Name] = vm.VhdxId!;
            }
        }
    }

    private void ApplyPersistedSelections(LabTemplate template)
    {
        var result = _catalogService.LoadCatalog();
        _templateSelectionService.ApplySelections(_templateKey, template, result.Items);
    }
}

public sealed class CatalogLoadState
{
    public CatalogLoadState(IReadOnlyList<VhdxCatalogItem> items, IReadOnlyList<string> errors)
    {
        Items = items;
        Errors = errors;
    }

    public IReadOnlyList<VhdxCatalogItem> Items { get; }

    public IReadOnlyList<string> Errors { get; }
}

public sealed class MissingVhdxDialogState
{
    public MissingVhdxDialogState(
        IReadOnlyList<string> catalogErrors,
        IReadOnlyList<MissingVhdxReference> missingReferences)
    {
        CatalogErrors = catalogErrors;
        MissingReferences = missingReferences;
    }

    public IReadOnlyList<string> CatalogErrors { get; }

    public IReadOnlyList<MissingVhdxReference> MissingReferences { get; }
}

public sealed class ResolvedVhdxSelection
{
    public ResolvedVhdxSelection(VmTemplate vm, VhdxCatalogItem item)
    {
        Vm = vm;
        Item = item;
    }

    public VmTemplate Vm { get; }

    public VhdxCatalogItem Item { get; }
}
