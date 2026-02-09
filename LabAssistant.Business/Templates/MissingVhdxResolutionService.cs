using System;
using System.Collections.Generic;
using System.Linq;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Templates;

namespace LabAssistant.Business.Templates;

public sealed class MissingVhdxResolutionService
{
    private readonly IVhdxCatalogStore _catalogStore;
    private readonly IAppSettingsStore _settingsStore;

    public MissingVhdxResolutionService(IVhdxCatalogStore catalogStore, IAppSettingsStore settingsStore)
    {
        _catalogStore = catalogStore;
        _settingsStore = settingsStore;
    }

    public MissingVhdxResolutionResult ResolveMissingVhdx(LabTemplate template)
    {
        var catalogResult = _catalogStore.Load(_settingsStore.Settings.CatalogPath);
        var catalogItems = catalogResult.Items;
        var catalogIds = new HashSet<string>(
            catalogItems.Select(item => item.Id),
            StringComparer.OrdinalIgnoreCase);

        var autoResolvedCount = 0;
        foreach (var vm in template.VmTemplates)
        {
            var hasId = !string.IsNullOrWhiteSpace(vm.VhdxId);
            var hasValidId = hasId && catalogIds.Contains(vm.VhdxId!);
            if (hasValidId)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(vm.VhdxSignature))
            {
                continue;
            }

            var matches = VhdxSignature.FindMatches(vm.VhdxSignature, catalogItems);
            if (matches.Count == 1)
            {
                var match = matches[0];
                vm.VhdxId = match.Id;
                vm.VhdPath = match.Path;
                vm.VhdxSignature = match.Signature;
                autoResolvedCount++;
            }
        }

        var missing = new List<VmTemplate>();
        foreach (var vm in template.VmTemplates)
        {
            if (string.IsNullOrWhiteSpace(vm.VhdxId))
            {
                continue;
            }

            if (!catalogIds.Contains(vm.VhdxId))
            {
                missing.Add(vm);
            }
        }

        return new MissingVhdxResolutionResult(
            autoResolvedCount,
            missing,
            catalogResult.Errors);
    }
}
