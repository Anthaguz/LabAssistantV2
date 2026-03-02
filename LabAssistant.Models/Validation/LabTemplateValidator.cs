using System;
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

        if (string.IsNullOrWhiteSpace(template.SchemaVersion))
        {
            result.Errors.Add("Template schemaVersion is required.");
        }
        else if (!Version.TryParse(template.SchemaVersion, out _))
        {
            result.Errors.Add("Template schemaVersion must be a valid semantic version (e.g. 1.0.0).");
        }

        if (string.IsNullOrWhiteSpace(template.Id))
        {
            result.Errors.Add("Template id is required.");
        }

        if (string.IsNullOrWhiteSpace(template.Name))
        {
            result.Errors.Add("Template name is required.");
        }

        if (template.TemplateRevision <= 0)
        {
            result.Errors.Add("Template templateRevision must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(template.CreatedWithAppVersion))
        {
            result.Errors.Add("Template createdWithAppVersion is required.");
        }
        else if (!Version.TryParse(template.CreatedWithAppVersion, out _))
        {
            result.Errors.Add("Template createdWithAppVersion must be a valid semantic version (e.g. 1.0.0).");
        }

        if (!string.Equals(template.TemplateType, LabTemplate.SupportedTemplateType, StringComparison.OrdinalIgnoreCase))
        {
            result.Errors.Add($"Template templateType must be '{LabTemplate.SupportedTemplateType}'.");
        }

        if (template.VmTemplates.Count == 0)
        {
            result.Errors.Add("At least one VM template is required.");
        }

        var vmNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var vm in template.VmTemplates)
        {
            var vmName = string.IsNullOrWhiteSpace(vm.Name) ? "<unnamed VM>" : vm.Name;

            if (string.IsNullOrWhiteSpace(vm.VmId))
            {
                result.Errors.Add($"VM '{vmName}' vmId is required.");
            }

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
                result.Errors.Add($"VM '{vmName}' memory must be positive.");
            }

            if (vm.CpuCount <= 0)
            {
                result.Errors.Add($"VM '{vmName}' CPU count must be positive.");
            }

            var hasVhdxId = !string.IsNullOrWhiteSpace(vm.VhdxId);
            var hasVhdPath = !string.IsNullOrWhiteSpace(vm.VhdPath);
            if (!hasVhdxId && !hasVhdPath)
            {
                result.Errors.Add($"VM '{vmName}' must specify vhdxId or vhdPath.");
            }

            if (hasVhdxId && !catalogIds.Contains(vm.VhdxId!))
            {
                result.MissingVhdxIds.Add(vm.VhdxId!);
            }

            if (vm.SwitchNames is not null)
            {
                if (vm.SwitchNames.Any(string.IsNullOrWhiteSpace))
                {
                    result.Errors.Add($"VM '{vmName}' switchNames must not contain empty values.");
                }

                var distinctSwitches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var switchName in vm.SwitchNames.Where(name => !string.IsNullOrWhiteSpace(name)))
                {
                    if (!distinctSwitches.Add(switchName.Trim()))
                    {
                        result.Errors.Add($"VM '{vmName}' switchNames must not contain duplicates.");
                        break;
                    }
                }
            }

            ValidateGuestStepConfig(vm, result);
        }

        return result;
    }

    private static void ValidateGuestStepConfig(VmTemplate vm, LabTemplateValidationResult result)
    {
        var vmName = string.IsNullOrWhiteSpace(vm.Name) ? "<unnamed VM>" : vm.Name;

        if (vm.SoftwareConfig?.Packages != null && vm.SoftwareConfig.Packages.Any(string.IsNullOrWhiteSpace))
        {
            result.Errors.Add($"VM '{vmName}' softwareConfig.packages must not contain empty values.");
        }

        if (vm.RoleConfig?.Roles != null && vm.RoleConfig.Roles.Any(string.IsNullOrWhiteSpace))
        {
            result.Errors.Add($"VM '{vmName}' roleConfig.roles must not contain empty values.");
        }

        if (vm.GuestNetworkConfig?.DnsServers != null && vm.GuestNetworkConfig.DnsServers.Any(string.IsNullOrWhiteSpace))
        {
            result.Errors.Add($"VM '{vmName}' guestNetworkConfig.dnsServers must not contain empty values.");
        }
    }
}
