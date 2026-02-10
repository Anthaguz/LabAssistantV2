using System;
using System.Collections.Generic;
using System.Linq;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Templates;

namespace LabAssistant.Business.Templates;

public sealed class TemplateSelectionService
{
    private readonly IAppSettingsStore _settingsStore;

    public TemplateSelectionService(IAppSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public TemplateVhdxSelection? GetSelection(string templateKey, string vmName)
    {
        return _settingsStore.Settings.TemplateSelections.FirstOrDefault(item =>
            string.Equals(item.TemplateId, templateKey, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.VmName, vmName, StringComparison.OrdinalIgnoreCase));
    }

    public void SaveSelection(string templateKey, VmTemplate vm)
    {
        var selections = _settingsStore.Settings.TemplateSelections;
        selections.RemoveAll(item =>
            string.Equals(item.TemplateId, templateKey, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.VmName, vm.Name, StringComparison.OrdinalIgnoreCase));

        selections.Add(new TemplateVhdxSelection
        {
            TemplateId = templateKey,
            VmName = vm.Name,
            VhdxId = vm.VhdxId ?? string.Empty,
            VhdPath = vm.VhdPath ?? string.Empty
        });

        _settingsStore.Save();
    }

    public void ApplySelections(string templateKey, LabTemplate template, IEnumerable<VhdxCatalogItem> catalogItems)
    {
        var selections = _settingsStore.Settings.TemplateSelections;
        if (selections.Count == 0)
        {
            return;
        }

        var catalogById = catalogItems.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var vm in template.VmTemplates)
        {
            var selection = selections.FirstOrDefault(item =>
                string.Equals(item.TemplateId, templateKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.VmName, vm.Name, StringComparison.OrdinalIgnoreCase));

            if (selection == null)
            {
                continue;
            }

            vm.VhdxId = selection.VhdxId;
            if (!string.IsNullOrWhiteSpace(selection.VhdxId) && catalogById.TryGetValue(selection.VhdxId, out var catalogItem))
            {
                vm.VhdPath = catalogItem.Path;
                vm.VhdxSignature = catalogItem.Signature;
            }
            else
            {
                vm.VhdPath = selection.VhdPath;
            }
        }
    }
}
