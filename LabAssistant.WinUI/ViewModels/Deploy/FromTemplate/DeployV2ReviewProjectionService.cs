using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Models.Deploy;

namespace LabAssistant.WinUI.ViewModels.Deploy;

internal sealed class DeployV2ReviewProjectionService
{
    public DeployV2ReviewProjectionResult Build(
        LabTemplate template,
        V2PlanBuildResult plan,
        IReadOnlyList<LocalCredentialSlotDefinition> localSlotDefinitions,
        IReadOnlyDictionary<string, V2RuntimeCredential> resolvedCredentialSlotValues)
    {
        var summary = new DeployV2PlanSummaryRow(
            TemplateName: string.IsNullOrWhiteSpace(template.Name) ? "Unnamed Template" : template.Name,
            ExecutionEngine: "V2 Unified Planning",
            DeploymentProfile: plan.Context.ResolvedDeploymentProfileName,
            VmCount: plan.Context.Vms.Count,
            NodeCount: plan.Nodes.Count,
            UnresolvedRequirementCount: plan.UnresolvedRequirements.Count,
            RouterSummary: plan.Context.RouterSemanticsRequired ? "Router-aware" : "No router gating",
            DomainSummary: plan.Context.DomainSemanticsRequired ? "Domain-aware" : "No domain semantics",
            StartabilitySummary: plan.Success ? "Startable" : "Blocked");

        var vmNamesById = plan.Context.Vms
            .GroupBy(vm => vm.VmId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().VmName, StringComparer.OrdinalIgnoreCase);

        var blockers = new List<DeployV2BlockerRow>();
        blockers.AddRange(plan.Issues.Select(issue => new DeployV2BlockerRow(
            Severity: issue.Severity == V2PlanIssueSeverity.Blocking ? "Block" : "Warn",
            Scope: ResolveScope(issue.VmId, issue.VmName),
            Message: string.IsNullOrWhiteSpace(issue.SuggestedAction)
                ? issue.Message
                : $"{issue.Message} {issue.SuggestedAction}".Trim())));

        foreach (var requirement in plan.UnresolvedRequirements)
        {
            var scope = requirement.AffectedVmIds.Count == 0
                ? "Global"
                : string.Join(", ", requirement.AffectedVmIds
                    .Select(id => vmNamesById.TryGetValue(id, out var vmName) ? vmName : id)
                    .Distinct(StringComparer.OrdinalIgnoreCase));

            var prefix = requirement.Kind == V2UnresolvedRequirementKind.CredentialSlot
                ? "Resolve credential slot locally."
                : requirement.Kind == V2UnresolvedRequirementKind.BootstrapProfile
                    ? "Fix bootstrap metadata in the template or base-image catalog."
                    : "Resolve the planning requirement before starting deployment.";
            blockers.Add(new DeployV2BlockerRow("Block", scope, $"{requirement.Description} {prefix}".Trim()));
        }

        var localSlotsByKey = localSlotDefinitions.ToDictionary(definition => definition.SlotKey, StringComparer.OrdinalIgnoreCase);
        var credentialRows = plan.UnresolvedRequirements
            .Where(requirement => requirement.Kind == V2UnresolvedRequirementKind.CredentialSlot)
            .GroupBy(requirement => requirement.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                localSlotsByKey.TryGetValue(group.Key, out var localDefinition);
                resolvedCredentialSlotValues.TryGetValue(group.Key, out var resolvedValue);
                var affectedVmSummary = string.Join(", ",
                    group.SelectMany(item => item.AffectedVmIds)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Select(id => vmNamesById.TryGetValue(id, out var vmName) ? vmName : id));

                return new DeployV2CredentialSlotRow(
                    SlotKey: group.Key,
                    PurposeSummary: BuildPurposeSummary(group.Select(item => item.Description)),
                    AffectedVmSummary: string.IsNullOrWhiteSpace(affectedVmSummary) ? "Affects current V2 plan" : affectedVmSummary,
                    ExistingUsername: resolvedValue?.Username ?? localDefinition?.Username ?? string.Empty,
                    HasStoredValue: resolvedValue is not null || localDefinition is not null);
            })
            .OrderBy(row => row.SlotKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var waves = plan.Waves
            .OrderBy(wave => wave.WaveNumber)
            .Select(wave => new DeployV2WaveRow(
                WaveNumber: wave.WaveNumber,
                DisplayName: wave.DisplayName,
                NodeCount: wave.NodeIds.Count,
                Summary: wave.Summary))
            .ToList();

        var diagnostics = new List<DeployV2DiagnosticRow>
        {
            new("Nodes", $"{plan.Nodes.Count} node(s)"),
            new("Dependencies", $"{plan.Dependencies.Count} dependency edge(s)"),
            new("Warnings", $"{plan.Issues.Count(issue => issue.Severity == V2PlanIssueSeverity.Warning)} warning(s)")
        };

        diagnostics.AddRange(plan.Issues
            .Where(issue => issue.Severity == V2PlanIssueSeverity.Warning)
            .Select(issue => new DeployV2DiagnosticRow(issue.Code, issue.Message)));

        return new DeployV2ReviewProjectionResult(summary, blockers, credentialRows, waves, diagnostics);
    }

    private static string ResolveScope(string? vmId, string? vmName)
    {
        if (!string.IsNullOrWhiteSpace(vmName))
        {
            return vmName;
        }

        return string.IsNullOrWhiteSpace(vmId) ? "Global" : vmId;
    }

    private static string BuildPurposeSummary(IEnumerable<string> descriptions)
    {
        return string.Join(" | ",
            descriptions
                .Select(description => description.Trim())
                .Where(description => !string.IsNullOrWhiteSpace(description))
                .Distinct(StringComparer.OrdinalIgnoreCase));
    }
}

internal sealed record DeployV2ReviewProjectionResult(
    DeployV2PlanSummaryRow Summary,
    IReadOnlyList<DeployV2BlockerRow> Blockers,
    IReadOnlyList<DeployV2CredentialSlotRow> CredentialSlots,
    IReadOnlyList<DeployV2WaveRow> Waves,
    IReadOnlyList<DeployV2DiagnosticRow> Diagnostics);
