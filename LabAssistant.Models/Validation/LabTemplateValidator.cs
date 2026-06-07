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
        var executionEngine = template.ExecutionEngine != default
            ? template.ExecutionEngine
            : TemplateSchemaVersionCatalog.Classify(template.SchemaVersion);

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

        if (executionEngine == TemplateExecutionEngine.V2UnifiedPlanning)
        {
            ValidateV2DeploymentProfile(template, result);
            ValidateV2Networks(template, result);
            ValidateV2ExtendedTopology(template, result);
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

            if (executionEngine == TemplateExecutionEngine.V2UnifiedPlanning)
            {
                ValidateV2VmShape(vm, result);
            }

            ValidateGuestStepConfig(vm, result);
        }

        return result;
    }

    private static void ValidateV2Networks(LabTemplate template, LabTemplateValidationResult result)
    {
        if (template.LabNetworks == null)
        {
            return;
        }

        var networkIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var network in template.LabNetworks)
        {
            if (string.IsNullOrWhiteSpace(network.NetworkId))
            {
                result.Errors.Add("V2 labNetwork.networkId is required when labNetworks are provided.");
                continue;
            }

            if (!networkIds.Add(network.NetworkId.Trim()))
            {
                result.Errors.Add($"Duplicate V2 lab network id: {network.NetworkId}.");
            }
        }
    }

    private static void ValidateV2DeploymentProfile(LabTemplate template, LabTemplateValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(template.DeploymentProfile))
        {
            return;
        }

        if (!V2SchedulerPolicyCatalog.IsSupportedProfile(template.DeploymentProfile))
        {
            var allowedProfiles = string.Join(", ", V2SchedulerPolicyCatalog.SupportedProfileNames);
            result.Errors.Add($"V2 deploymentProfile must be one of: {allowedProfiles}.");
        }
    }

    private static void ValidateV2ExtendedTopology(LabTemplate template, LabTemplateValidationResult result)
    {
        if (template.ExtendedTopology == null)
        {
            return;
        }

        var vmIds = template.VmTemplates
            .Where(vm => !string.IsNullOrWhiteSpace(vm.VmId))
            .Select(vm => vm.VmId.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        ValidateChildDomains(template.ExtendedTopology.ChildDomains, vmIds, result);
        ValidateAdditionalForests(template.ExtendedTopology.AdditionalForests, vmIds, result);
        ValidateTreeDomains(template.ExtendedTopology.TreeDomains, result);
    }

    private static void ValidateChildDomains(
        List<V2ChildDomainTemplate>? childDomains,
        ISet<string> vmIds,
        LabTemplateValidationResult result)
    {
        if (childDomains == null)
        {
            return;
        }

        var topologyIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var childDomain in childDomains)
        {
            if (string.IsNullOrWhiteSpace(childDomain.TopologyId))
            {
                result.Errors.Add("V2 extendedTopology.childDomains.topologyId is required.");
            }
            else if (!topologyIds.Add(childDomain.TopologyId.Trim()))
            {
                result.Errors.Add($"Duplicate V2 child domain topology id: {childDomain.TopologyId}.");
            }

            if (string.IsNullOrWhiteSpace(childDomain.ParentDomainRef))
            {
                result.Errors.Add($"V2 child domain '{childDomain.TopologyId}' parentDomainRef is required.");
            }

            if (string.IsNullOrWhiteSpace(childDomain.ChildLabel))
            {
                result.Errors.Add($"V2 child domain '{childDomain.TopologyId}' childLabel is required.");
            }

            if (string.IsNullOrWhiteSpace(childDomain.FirstDomainControllerVmId))
            {
                result.Errors.Add($"V2 child domain '{childDomain.TopologyId}' firstDomainControllerVmId is required.");
            }
            else if (!vmIds.Contains(childDomain.FirstDomainControllerVmId.Trim()))
            {
                result.Errors.Add($"V2 child domain '{childDomain.TopologyId}' references unknown VM id '{childDomain.FirstDomainControllerVmId}'.");
            }
        }
    }

    private static void ValidateAdditionalForests(
        List<V2AdditionalForestTemplate>? additionalForests,
        ISet<string> vmIds,
        LabTemplateValidationResult result)
    {
        if (additionalForests == null)
        {
            return;
        }

        var topologyIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var forest in additionalForests)
        {
            if (string.IsNullOrWhiteSpace(forest.TopologyId))
            {
                result.Errors.Add("V2 extendedTopology.additionalForests.topologyId is required.");
            }
            else if (!topologyIds.Add(forest.TopologyId.Trim()))
            {
                result.Errors.Add($"Duplicate V2 additional forest topology id: {forest.TopologyId}.");
            }

            if (string.IsNullOrWhiteSpace(forest.ForestRootDomainFqdn))
            {
                result.Errors.Add($"V2 additional forest '{forest.TopologyId}' forestRootDomainFqdn is required.");
            }

            if (string.IsNullOrWhiteSpace(forest.FirstDomainControllerVmId))
            {
                result.Errors.Add($"V2 additional forest '{forest.TopologyId}' firstDomainControllerVmId is required.");
            }
            else if (!vmIds.Contains(forest.FirstDomainControllerVmId.Trim()))
            {
                result.Errors.Add($"V2 additional forest '{forest.TopologyId}' references unknown VM id '{forest.FirstDomainControllerVmId}'.");
            }
        }
    }

    private static void ValidateTreeDomains(
        List<V2TreeDomainTemplate>? treeDomains,
        LabTemplateValidationResult result)
    {
        if (treeDomains == null)
        {
            return;
        }

        var topologyIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var treeDomain in treeDomains)
        {
            if (string.IsNullOrWhiteSpace(treeDomain.TopologyId))
            {
                result.Errors.Add("V2 extendedTopology.treeDomains.topologyId is required.");
            }
            else if (!topologyIds.Add(treeDomain.TopologyId.Trim()))
            {
                result.Errors.Add($"Duplicate V2 tree domain topology id: {treeDomain.TopologyId}.");
            }
        }
    }

    private static void ValidateV2VmShape(VmTemplate vm, LabTemplateValidationResult result)
    {
        var vmName = string.IsNullOrWhiteSpace(vm.Name) ? "<unnamed VM>" : vm.Name;

        if (vm.CapabilityRoles != null)
        {
            if (vm.CapabilityRoles.Any(string.IsNullOrWhiteSpace))
            {
                result.Errors.Add($"VM '{vmName}' capabilityRoles must not contain empty values.");
            }

            var distinctRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var role in vm.CapabilityRoles.Where(role => !string.IsNullOrWhiteSpace(role)))
            {
                if (!distinctRoles.Add(role.Trim()))
                {
                    result.Errors.Add($"VM '{vmName}' capabilityRoles must not contain duplicates.");
                    break;
                }
            }
        }

        if (vm.DependsOn != null && vm.DependsOn.Any(string.IsNullOrWhiteSpace))
        {
            result.Errors.Add($"VM '{vmName}' dependsOn must not contain empty values.");
        }

        ValidateCredentialSlots(vm, result);

        if (vm.Nics == null)
        {
            return;
        }

        var nicIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var nic in vm.Nics)
        {
            if (string.IsNullOrWhiteSpace(nic.NicId))
            {
                result.Errors.Add($"VM '{vmName}' V2 nicId is required when nics are provided.");
                continue;
            }

            if (!nicIds.Add(nic.NicId.Trim()))
            {
                result.Errors.Add($"VM '{vmName}' contains duplicate V2 nicId '{nic.NicId}'.");
            }

            if (nic.DnsServers != null && nic.DnsServers.Any(string.IsNullOrWhiteSpace))
            {
                result.Errors.Add($"VM '{vmName}' V2 nic '{nic.NicId}' dnsServers must not contain empty values.");
            }
        }
    }

    private static void ValidateCredentialSlots(VmTemplate vm, LabTemplateValidationResult result)
    {
        var vmName = string.IsNullOrWhiteSpace(vm.Name) ? "<unnamed VM>" : vm.Name;
        if (vm.CredentialSlots == null)
        {
            return;
        }

        if (vm.CredentialSlots.LocalBootstrap != null && string.IsNullOrWhiteSpace(vm.CredentialSlots.LocalBootstrap))
        {
            result.Errors.Add($"VM '{vmName}' credentialSlots.localBootstrap must not be empty.");
        }

        if (vm.CredentialSlots.DomainAdmin != null && string.IsNullOrWhiteSpace(vm.CredentialSlots.DomainAdmin))
        {
            result.Errors.Add($"VM '{vmName}' credentialSlots.domainAdmin must not be empty.");
        }

        if (vm.CredentialSlots.DomainJoin != null && string.IsNullOrWhiteSpace(vm.CredentialSlots.DomainJoin))
        {
            result.Errors.Add($"VM '{vmName}' credentialSlots.domainJoin must not be empty.");
        }
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
