using LabAssistant.Business.Planning;
using LabAssistant.Business.Runtime.Scheduling;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Templates;
using LabAssistant.Services.Logging;
using LabAssistant.WinUI.ViewModels.Templates;
using LabAssistant.WinUI.ViewModels.Templates.Builder;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// End-to-end OFFLINE golden net that crosses the seam every other test stops short of: a topology is authored
/// exactly as the Builder UI authors it (suggested draft + the real topology / machine authoring engines +
/// network reconciler), saved through the real draft -&gt; <see cref="LabTemplate"/> mapper, assembled into the
/// plan request the deploy flow assembles from a saved template, and run through the real V2 planner and the
/// generic <see cref="V2PlanScheduler"/>. It asserts the dependency graph the planner emits for a Builder-authored
/// template is correct and schedulable - no Hyper-V, so a wrong edge fails on every build.
///
/// The existing interaction tests prove the persisted template SHAPE round-trips; the existing scheduler golden
/// tests prove the scheduler honors edges for HAND-BUILT plans. Neither proves a Builder-authored template PLANS
/// to a correct graph. This closes that loop - the exact thing the AI-built dependency graph was distrusted for.
/// </summary>
public sealed class TemplatesBuilderPlanRoundTripGoldenTests
{
    private const string BootstrapSlotRef = "slot-local";

    private readonly IV2PlanningCapabilityService _planner = new V2PlanningCapabilityService();

    [Fact]
    public async Task SuggestedContosoLab_PlansAndSchedules_MemberJoinAdmittedAfterDomainReady()
    {
        // The Builder's out-of-the-box suggested lab (one forest: a root DC + a domain member). Authored, saved
        // through the mapper, and planned exactly as the deploy flow would. It must plan cleanly and the member's
        // domain join must be admitted only after the DC reports the domain ready and stabilizes DNS.
        var (plan, _) = await PlanFromDraftAsync(SuggestedDraft());

        Assert.True(plan.Success, DescribeIssues(plan));

        var result = await RunGraphSchedulerAsync(plan);
        Assert.True(result.Success);
        AssertAllEdgesHonored(plan, result);

        // VM lifecycle floor for the DC: provision -> start -> guest transport ready.
        AssertAdmittedBefore(result, NodeId(plan, V2PlanNodeKind.ProvisionVm, "vm-dc01"), NodeId(plan, V2PlanNodeKind.StartVm, "vm-dc01"));
        AssertAdmittedBefore(result, NodeId(plan, V2PlanNodeKind.StartVm, "vm-dc01"), NodeId(plan, V2PlanNodeKind.GuestTransportReady, "vm-dc01"));

        // The distrusted edge: the member cannot join until the DC's domain is ready and DNS has stabilized.
        var domainReady = NodeId(plan, V2PlanNodeKind.DomainReady, "vm-dc01");
        var stabilizeDns = NodeId(plan, V2PlanNodeKind.StabilizeDomainDns, "vm-dc01");
        var join = NodeId(plan, V2PlanNodeKind.JoinDomain, "vm-member01");
        AssertAdmittedBefore(result, domainReady, join);
        AssertAdmittedBefore(result, stabilizeDns, join);
    }

    [Fact]
    public async Task ChildDomainLab_PlansAndSchedules_BothDomainsPromoteAndRouterBridgesSwitches()
    {
        // Adding a child domain gives it its own DC and its own switch, so the model auto-creates a router to
        // bridge the two switches. The whole thing must still plan and schedule as a valid DAG: two domain
        // controllers promote, and the router's feature/route/NAT chain stays in order.
        var suggested = SuggestedDraft();
        var childDraft = TemplatesBuilderTopologyAuthoring
            .AddChildDomain(suggested, suggested.Domains[0].DomainId).Draft;

        var (plan, template) = await PlanFromDraftAsync(childDraft);

        Assert.True(plan.Success, DescribeIssues(plan));
        Assert.Equal(2, plan.Nodes.Count(node => node.Kind == V2PlanNodeKind.DomainReady));

        var result = await RunGraphSchedulerAsync(plan);
        Assert.True(result.Success);
        AssertAllEdgesHonored(plan, result);

        // A router exists (two switches) and its configuration chain is honored end to end.
        var routerVmId = template.VmTemplates.Single(vm => vm.TopologyRole == "Router").VmId;
        AssertAdmittedBefore(result, NodeId(plan, V2PlanNodeKind.PrepareRouterNetwork, routerVmId), NodeId(plan, V2PlanNodeKind.EnableRouterRouting, routerVmId));
        AssertAdmittedBefore(result, NodeId(plan, V2PlanNodeKind.EnableRouterRouting, routerVmId), NodeId(plan, V2PlanNodeKind.ConfigureRouterNat, routerVmId));
    }

    [Fact]
    public async Task TwoForestTrustLab_PlansAndSchedules_TrustChainWaitsForBothForestsReady()
    {
        // Two forests joined by a forest trust - the exact topology whose auto-generated dependency graph was
        // most distrusted. Both forests' root DCs must be domain-ready before the trust DNS/create/validate chain
        // runs, and that chain must stay strictly ordered.
        var twoForests = TemplatesBuilderTopologyAuthoring.AddForest(SuggestedDraft()).Draft;
        var withTrust = TemplatesBuilderTopologyAuthoring.AddForestTrust(twoForests, 0, 1).Draft;

        var (plan, _) = await PlanFromDraftAsync(withTrust);

        Assert.True(plan.Success, DescribeIssues(plan));

        var result = await RunGraphSchedulerAsync(plan);
        Assert.True(result.Success);
        AssertAllEdgesHonored(plan, result);

        // Exactly one trust chain, anchored on the source forest's root DC.
        var createTrust = Assert.Single(plan.Nodes, node => node.Kind == V2PlanNodeKind.CreateForestTrust).NodeId;
        var prepareDns = Assert.Single(plan.Nodes, node => node.Kind == V2PlanNodeKind.PrepareForestTrustDns).NodeId;
        var validateTrust = Assert.Single(plan.Nodes, node => node.Kind == V2PlanNodeKind.ValidateForestTrust).NodeId;

        // Both forests reach domain-ready before the trust is prepared, and the chain stays ordered.
        foreach (var domainReady in plan.Nodes.Where(node => node.Kind == V2PlanNodeKind.DomainReady))
        {
            AssertAdmittedBefore(result, domainReady.NodeId, prepareDns);
        }

        AssertAdmittedBefore(result, prepareDns, createTrust);
        AssertAdmittedBefore(result, createTrust, validateTrust);
    }

    /// <summary>
    /// Reconciles a Builder draft (as the app does on every edit), saves it through the real draft -&gt;
    /// <see cref="LabTemplate"/> mapper, completes the base-disk selections the UI would require, assembles the
    /// plan request the deploy flow assembles, and plans it. Returns the plan together with the persisted template.
    /// </summary>
    private async Task<(V2PlanBuildResult Plan, LabTemplate Template)> PlanFromDraftAsync(TemplatesBuilderDraftSnapshot draft)
    {
        var reconciled = TemplatesBuilderNetworkReconciler.Reconcile(draft);
        var build = TemplatesBuilderDraftMapper.BuildDocument(reconciled, "template-golden", 1, "1.0.0", sourceFilePath: null);
        Assert.NotNull(build.Document);

        var template = build.Document!.Template;
        AssignMissingBaseDisks(template);

        var plan = await _planner.BuildPlanAsync(BuildPlanRequest(template));
        return (plan, template);
    }

    /// <summary>
    /// Fills in the base disk for any VM the topology authoring engine created without one (a newly added forest /
    /// child domain / tree is born with a bare DC). In the UI the user must pick a disk before Save is allowed; the
    /// test represents that completed selection so it exercises a valid, deployable template.
    /// </summary>
    private static void AssignMissingBaseDisks(LabTemplate template)
    {
        foreach (var vm in template.VmTemplates)
        {
            if (!string.IsNullOrWhiteSpace(vm.VhdxId))
            {
                continue;
            }

            var isServerRole = IsActiveDirectoryDomainController(vm.TopologyRole) ||
                string.Equals(vm.TopologyRole, "Router", StringComparison.OrdinalIgnoreCase);
            vm.VhdxId = isServerRole ? "disk-dc" : "disk-member";
        }
    }

    private static bool IsActiveDirectoryDomainController(string? topologyRole) =>
        string.Equals(topologyRole, "FirstDomainController", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(topologyRole, "ReplicaDomainController", StringComparison.OrdinalIgnoreCase);

    private static TemplatesBuilderDraftSnapshot SuggestedDraft()
    {
        var referenceData = new TemplatesBuilderReferenceData(
            ["vSwitch-Core"],
            [
                new TemplateVhdxCatalogOption("disk-dc", @"C:\base\disk-dc.vhdx", "Windows Server", "2022", 2, "sig-dc"),
                new TemplateVhdxCatalogOption("disk-member", @"C:\base\disk-member.vhdx", "Windows Server", "2022", 2, "sig-member")
            ]);

        return TemplatesBuilderDraftMapper.CreateSuggestedDraft(referenceData) with { TemplateName = "Golden Lab" };
    }

    /// <summary>
    /// Mirrors the production assembly of a plan request from a saved template (DeployFromTemplateWorkspaceHost +
    /// DeployV2ReviewWorkspaceController): the catalog + switch inventory are synthesised from what the Builder
    /// actually persisted, and the resolved credential slot keys mirror the deploy flow, which resolves whatever
    /// credential slots the template declares from the user's local credential store. Every catalog item carries a
    /// bootstrap profile so the planner's local-admin fallback resolves any slot the template leaves unbound.
    /// </summary>
    private static V2PlanBuildRequest BuildPlanRequest(LabTemplate template)
    {
        var catalog = template.VmTemplates
            .Where(vm => !string.IsNullOrWhiteSpace(vm.VhdxId))
            .GroupBy(vm => vm.VhdxId!, StringComparer.OrdinalIgnoreCase)
            .Select(group => new VhdxCatalogItem
            {
                Id = group.Key,
                Path = $@"C:\base\{group.Key}.vhdx",
                OsName = "Windows Server",
                OsVersion = "2022",
                Generation = 2,
                Signature = group
                    .Select(vm => vm.VhdxSignature)
                    .FirstOrDefault(signature => !string.IsNullOrWhiteSpace(signature)) ?? $"{group.Key}-sig",
                BootstrapProfile = new VhdxBootstrapProfile
                {
                    ExpectedLocalUser = "Administrator",
                    LocalCredentialSlotRef = BootstrapSlotRef,
                    GuestOsFamily = "windows",
                    GuestTransport = "powershell-direct"
                }
            })
            .ToList();

        var switchNames = template.VmTemplates
            .SelectMany(vm => new[] { vm.SwitchName }.Concat(vm.SwitchNames ?? Enumerable.Empty<string>()))
            .Concat((template.LabNetworks ?? new()).Select(network => network.SwitchName))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new V2PlanBuildRequest
        {
            Template = template,
            CatalogItems = catalog,
            AvailableSwitchNames = switchNames,
            AvailableSwitches = switchNames
                .Select(name => new V2AvailableSwitchInfo { Name = name, SwitchType = "Internal" })
                .ToList(),
            ResolvedCredentialSlotKeys = CollectDeclaredCredentialSlotKeys(template),
            DefaultDeploymentProfile = "Balanced"
        };
    }

    /// <summary>
    /// Every credential slot key the persisted template's VMs reference, plus the local bootstrap slot. The deploy
    /// review controller passes the keys the user's credential store resolves; a deployable template is one whose
    /// declared slots are all registered, so this represents that completed credential state.
    /// </summary>
    private static IReadOnlyCollection<string> CollectDeclaredCredentialSlotKeys(LabTemplate template)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { BootstrapSlotRef };
        foreach (var slots in template.VmTemplates.Select(vm => vm.CredentialSlots).Where(slots => slots is not null))
        {
            foreach (var key in new[]
                {
                    slots!.LocalBootstrap,
                    slots.DomainAdmin,
                    slots.DomainJoin,
                    slots.Dsrm,
                    slots.ParentDomainAdmin
                })
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    keys.Add(key);
                }
            }
        }

        return keys;
    }

    // ----- scheduler-run + admission-order helpers (copied from the Business.Tests golden partial; they are
    // private cross-project, so they cannot be reused directly) -----

    private static async Task<V2SchedulerRunResult> RunGraphSchedulerAsync(
        V2PlanBuildResult plan,
        V2DeploymentProfile profile = V2DeploymentProfile.Balanced)
    {
        var executors = plan.Nodes
            .Select(node => node.Kind)
            .Distinct()
            .Select(kind => (IV2NodeExecutor)new V2DelegatingNodeExecutor(kind, (_, _) => Task.CompletedTask))
            .ToList();
        var registry = new V2NodeExecutorRegistry(executors);

        return await new V2PlanScheduler().ExecuteAsync(
            plan,
            registry,
            V2SchedulerOptionsFactory.ForProfile(profile),
            NullStructuredLogger.Instance,
            CancellationToken.None);
    }

    private static void AssertAllEdgesHonored(V2PlanBuildResult plan, V2SchedulerRunResult result)
    {
        var admissionIndex = BuildAdmissionIndex(result);
        foreach (var dependency in plan.Dependencies)
        {
            Assert.True(
                admissionIndex.TryGetValue(dependency.FromNodeId, out var fromIndex),
                $"Dependency source '{dependency.FromNodeId}' was never admitted.");
            Assert.True(
                admissionIndex.TryGetValue(dependency.ToNodeId, out var toIndex),
                $"Dependency target '{dependency.ToNodeId}' was never admitted.");
            Assert.True(
                fromIndex < toIndex,
                $"Edge '{dependency.FromNodeId}' -> '{dependency.ToNodeId}' violated: dependent was admitted before its dependency.");
        }
    }

    private static void AssertAdmittedBefore(V2SchedulerRunResult result, string firstNodeId, string secondNodeId)
    {
        var admissionIndex = BuildAdmissionIndex(result);
        Assert.True(admissionIndex.TryGetValue(firstNodeId, out var firstIndex), $"Node '{firstNodeId}' was never admitted.");
        Assert.True(admissionIndex.TryGetValue(secondNodeId, out var secondIndex), $"Node '{secondNodeId}' was never admitted.");
        Assert.True(firstIndex < secondIndex, $"Node '{firstNodeId}' should be admitted before '{secondNodeId}'.");
    }

    private static Dictionary<string, int> BuildAdmissionIndex(V2SchedulerRunResult result)
    {
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var position = 0; position < result.AdmissionOrder.Count; position++)
        {
            index[result.AdmissionOrder[position]] = position;
        }

        return index;
    }

    private static string NodeId(V2PlanBuildResult plan, V2PlanNodeKind kind, string? vmId = null) =>
        plan.Nodes.Single(node =>
            node.Kind == kind &&
            (vmId is null || string.Equals(node.VmId, vmId, StringComparison.OrdinalIgnoreCase))).NodeId;

    private static string DescribeIssues(V2PlanBuildResult plan) =>
        plan.Issues.Count == 0
            ? "(no issues reported)"
            : string.Join(
                Environment.NewLine,
                plan.Issues.Select(issue => $"{issue.Severity} {issue.Code} [{issue.VmName}] {issue.Message}"));
}
