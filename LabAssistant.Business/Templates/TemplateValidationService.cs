using System;
using System.Collections.Generic;
using System.Linq;
using LabAssistant.Business.Catalog;
using LabAssistant.Models.Templates;
using LabAssistant.Models.Validation;

namespace LabAssistant.Business.Templates;

public sealed class TemplateValidationService
{
    private readonly CatalogService _catalogService;

    public TemplateValidationService(CatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public TemplateValidationSummary ValidateForSave(LabTemplate template)
    {
        var warnings = new List<string>();
        var errors = new List<string>();

        var catalogResult = _catalogService.LoadCatalog();
        if (catalogResult.Errors.Count > 0)
        {
            warnings.AddRange(catalogResult.Errors.Select(error => $"Catalog: {error}"));
        }

        var validation = LabTemplateValidator.Validate(template, catalogResult.Items);
        errors.AddRange(validation.Errors);

        var catalogIds = new HashSet<string>(
            catalogResult.Items.Select(item => item.Id),
            StringComparer.OrdinalIgnoreCase);

        foreach (var vm in template.VmTemplates)
        {
            if (string.IsNullOrWhiteSpace(vm.VhdxId))
            {
                continue;
            }

            if (!catalogIds.Contains(vm.VhdxId))
            {
                var vmName = string.IsNullOrWhiteSpace(vm.Name) ? "<unnamed VM>" : vm.Name;
                errors.Add($"VM '{vmName}' references missing VHDX id '{vm.VhdxId}'.");
            }
        }

        return new TemplateValidationSummary(errors, warnings);
    }
}
