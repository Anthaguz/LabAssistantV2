using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Templates;
using LabAssistant.Models.Deployment;
using LabAssistant.WinUI.Models.Deploy;

namespace LabAssistant.WinUI.ViewModels.Deploy;

internal static class DeployContextBuilder
{
    public static DeployContextBuildResult Build(
        LabTemplate template,
        AppSettings settings,
        IReadOnlyList<VhdxCatalogItem> catalogItems,
        IReadOnlyList<string> availableSwitches)
    {
        if (template.ExecutionEngine == TemplateExecutionEngine.V2UnifiedPlanning ||
            TemplateSchemaVersionCatalog.Classify(template.SchemaVersion) == TemplateExecutionEngine.V2UnifiedPlanning)
        {
            throw new InvalidOperationException(
                $"Template '{template.Name}' uses V2 schema '{template.SchemaVersion}' and must be routed through the V2 planner instead of the legacy deployment engine.");
        }

        var contexts = new List<VmDeploymentContext>();
        var compatibilityIssues = new List<DeployCompatibilityIssue>();

        foreach (var vmTemplate in template.VmTemplates)
        {
            var vmName = string.IsNullOrWhiteSpace(vmTemplate.Name) ? "Unnamed-VM" : vmTemplate.Name.Trim();
            var vmId = Guid.TryParse(vmTemplate.VmId, out var parsedVmId) ? parsedVmId : Guid.NewGuid();

            var diskResolution = ResolveDeployDiskIdentity(vmTemplate, catalogItems);
            compatibilityIssues.AddRange(diskResolution.Issues.Select(issue => issue with { VmName = vmName }));

            var switchResolution = ResolveDeploySwitches(vmTemplate, availableSwitches);
            compatibilityIssues.AddRange(switchResolution.Issues.Select(issue => issue with { VmName = vmName }));

            var vmPath = Path.Combine(settings.VmBasePath, vmName);
            var vhdPath = Path.Combine(vmPath, $"{vmName}.vhdx");

            contexts.Add(new VmDeploymentContext
            {
                VmId = vmId,
                VmName = vmName,
                MemoryMb = vmTemplate.MemoryMb > 0 ? vmTemplate.MemoryMb : settings.DefaultVmMemoryMb,
                CpuCount = vmTemplate.CpuCount > 0 ? vmTemplate.CpuCount : settings.DefaultCpuCount,
                VmPath = vmPath,
                VhdPath = vhdPath,
                BaseVhdPath = diskResolution.EffectiveBasePath,
                VhdxId = diskResolution.EffectiveId,
                VhdxSignature = diskResolution.EffectiveSignature,
                VirtualSwitchName = switchResolution.EffectiveSwitches.FirstOrDefault() ?? string.Empty,
                VirtualSwitchNames = switchResolution.EffectiveSwitches.ToList(),
                PerVmFailFast = settings.PerVmFailFast,
                NonBlockingOptionalSteps = new List<string>(settings.NonBlockingOptionalSteps ?? []),
                ConfigureTimeZone = vmTemplate.TimeZoneConfig?.Enabled == true,
                InstallSoftware = vmTemplate.SoftwareConfig?.Enabled == true,
                InstallRole = vmTemplate.RoleConfig?.Enabled == true,
                ConfigureNetworkInformation = vmTemplate.GuestNetworkConfig?.Enabled == true,
                TimeZoneConfig = vmTemplate.TimeZoneConfig,
                SoftwareConfig = vmTemplate.SoftwareConfig,
                RoleConfig = vmTemplate.RoleConfig,
                GuestNetworkConfig = vmTemplate.GuestNetworkConfig
            });
        }

        return new DeployContextBuildResult(
            new MultiVmDeploymentContext
            {
                VmContexts = contexts,
                StopAllOnAnyVmFailure = settings.StopAllOnAnyVmFailure
            },
            compatibilityIssues);
    }

    private static DeployDiskResolution ResolveDeployDiskIdentity(VmTemplate vmTemplate, IReadOnlyList<VhdxCatalogItem> catalogItems)
    {
        var issues = new List<DeployCompatibilityIssue>();
        var idMatch = string.IsNullOrWhiteSpace(vmTemplate.VhdxId)
            ? null
            : catalogItems.FirstOrDefault(item => string.Equals(item.Id, vmTemplate.VhdxId, StringComparison.OrdinalIgnoreCase));

        var signatureMatches = string.IsNullOrWhiteSpace(vmTemplate.VhdxSignature)
            ? []
            : VhdxSignature.FindMatches(vmTemplate.VhdxSignature, catalogItems).ToList();

        var pathMatch = string.IsNullOrWhiteSpace(vmTemplate.VhdPath)
            ? null
            : catalogItems.FirstOrDefault(item => string.Equals(item.Path, vmTemplate.VhdPath, StringComparison.OrdinalIgnoreCase));

        if (idMatch is not null)
        {
            var pathConflict = !string.IsNullOrWhiteSpace(vmTemplate.VhdPath) &&
                               !string.Equals(vmTemplate.VhdPath, idMatch.Path, StringComparison.OrdinalIgnoreCase);
            var signatureConflict = !string.IsNullOrWhiteSpace(vmTemplate.VhdxSignature) &&
                                    !string.IsNullOrWhiteSpace(idMatch.Signature) &&
                                    !string.Equals(vmTemplate.VhdxSignature, idMatch.Signature, StringComparison.OrdinalIgnoreCase);
            if (pathConflict || signatureConflict)
            {
                issues.Add(new DeployCompatibilityIssue(string.Empty, true, "Disk identity conflict detected. Resolve in Templates editor.", "Select one catalog-backed identity and save template."));
                return new DeployDiskResolution(string.Empty, vmTemplate.VhdxId, vmTemplate.VhdxSignature, issues);
            }

            return new DeployDiskResolution(idMatch.Path, idMatch.Id, idMatch.Signature, issues);
        }

        if (!string.IsNullOrWhiteSpace(vmTemplate.VhdxId))
        {
            issues.Add(new DeployCompatibilityIssue(string.Empty, true, $"Catalog entry '{vmTemplate.VhdxId}' is missing.", "Open in Templates editor and select a valid catalog disk."));
            return new DeployDiskResolution(string.Empty, vmTemplate.VhdxId, vmTemplate.VhdxSignature, issues);
        }

        if (signatureMatches.Count > 1)
        {
            issues.Add(new DeployCompatibilityIssue(string.Empty, true, "Disk signature maps to multiple catalog entries.", "Resolve ambiguous disk selection in Templates editor."));
            return new DeployDiskResolution(string.Empty, vmTemplate.VhdxId, vmTemplate.VhdxSignature, issues);
        }

        if (signatureMatches.Count == 1)
        {
            var match = signatureMatches[0];
            return new DeployDiskResolution(match.Path, match.Id, match.Signature, issues);
        }

        if (pathMatch is not null)
        {
            issues.Add(new DeployCompatibilityIssue(string.Empty, false, "Using legacy path-based disk match.", "Use resolve suggestions or Templates editor to normalize to catalog id."));
            return new DeployDiskResolution(pathMatch.Path, pathMatch.Id, pathMatch.Signature, issues);
        }

        if (!string.IsNullOrWhiteSpace(vmTemplate.VhdPath))
        {
            issues.Add(new DeployCompatibilityIssue(string.Empty, true, "Disk path does not match catalog entries.", "Open in Templates editor and choose a valid catalog base disk."));
            return new DeployDiskResolution(vmTemplate.VhdPath, vmTemplate.VhdxId, vmTemplate.VhdxSignature, issues);
        }

        issues.Add(new DeployCompatibilityIssue(string.Empty, true, "No disk identity configured.", "Open in Templates editor and select a base disk."));
        return new DeployDiskResolution(string.Empty, vmTemplate.VhdxId, vmTemplate.VhdxSignature, issues);
    }

    private static DeploySwitchResolution ResolveDeploySwitches(VmTemplate vmTemplate, IReadOnlyList<string> availableSwitches)
    {
        var issues = new List<DeployCompatibilityIssue>();
        var switches = vmTemplate.SwitchNames?
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        if (switches.Count == 0 && !string.IsNullOrWhiteSpace(vmTemplate.SwitchName))
        {
            switches.Add(vmTemplate.SwitchName);
        }

        if (switches.Count == 0)
        {
            issues.Add(new DeployCompatibilityIssue(string.Empty, false, "No switch assigned.", "Assign a switch in Quick Deploy VM properties (or Templates editor) if networking is required."));
            return new DeploySwitchResolution(Array.Empty<string>(), issues);
        }

        var available = new List<string>();
        var missing = new List<string>();
        foreach (var switchName in switches)
        {
            if (availableSwitches.Contains(switchName, StringComparer.OrdinalIgnoreCase))
            {
                available.Add(switchName);
            }
            else
            {
                missing.Add(switchName);
            }
        }

        if (missing.Count > 0)
        {
            issues.Add(new DeployCompatibilityIssue(string.Empty, true, $"Assigned switch mapping missing ({string.Join(", ", missing)}).", "Update switch mapping in Quick Deploy VM properties (or Templates editor)."));
        }

        return new DeploySwitchResolution(available, issues);
    }
}
