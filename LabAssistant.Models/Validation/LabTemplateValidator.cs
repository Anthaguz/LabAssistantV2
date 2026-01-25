using LabAssistant.Models.Catalog;
using LabAssistant.Models.Templates;

namespace LabAssistant.Models.Validation;

/// <summary>
/// Validates lab templates and their VM definitions.
/// </summary>
public static class LabTemplateValidator
{
    public static LabTemplateValidationResult Validate(LabTemplate template, IEnumerable<VhdxCatalogItem> catalogItems)
    {
        var result = new LabTemplateValidationResult();
        var catalogIds = new HashSet<string>(catalogItems.Select(item => item.Id), StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(template.Version))
        {
            result.Errors.Add("Template version is required.");
        }

        if (string.IsNullOrWhiteSpace(template.Id))
        {
            result.Errors.Add("Template id is required.");
        }

        if (string.IsNullOrWhiteSpace(template.Name))
        {
            result.Errors.Add("Template name is required.");
        }

        if (template.VmTemplates.Count == 0)
        {
            result.Errors.Add("At least one VM template is required.");
        }

        var vmNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var vm in template.VmTemplates)
        {
            if (string.IsNullOrWhiteSpace(vm.Name))
            {
                result.Errors.Add("VM name is required.");
            }
            else if (!vmNames.Add(vm.Name))
            {
                result.Errors.Add($"Duplicate VM name: {vm.Name}.");
            }

            if (vm.MemoryMb <= 0)
            {
                result.Errors.Add($"VM '{vm.Name}' memory must be positive.");
            }

            if (vm.CpuCount <= 0)
            {
                result.Errors.Add($"VM '{vm.Name}' CPU count must be positive.");
            }

            var hasVhdxId = !string.IsNullOrWhiteSpace(vm.VhdxId);
            var hasVhdPath = !string.IsNullOrWhiteSpace(vm.VhdPath);
            if (!hasVhdxId && !hasVhdPath)
            {
                result.Errors.Add($"VM '{vm.Name}' must specify vhdxId or vhdPath.");
            }

            if (hasVhdxId && !catalogIds.Contains(vm.VhdxId!))
            {
                result.MissingVhdxIds.Add(vm.VhdxId!);
            }
        }

        return result;
    }
}
