using LabAssistant.Business.Runtime.Scheduling;
using LabAssistant.Models.Templates;
using LabAssistant.Services.Logging;
using Xunit;

namespace LabAssistant.Business.Tests.Tests;

/// <summary>
/// Golden-plan structural tests: feed real planner output for known topologies into the generic scheduler and assert its
/// admission order honors every declared dependency edge and gate. These prove structural correctness only - they run no
/// Hyper-V and make no claim about real deployment behavior. They live in the planning-test partial so they can reuse the
/// same topology request builders the planner tests are validated against, keeping the two views in lock-step.
/// </summary>
public sealed partial class V2PlanningCapabilityServiceTests
{
    [Fact]
    public async Task Scheduler_SingleVmPlan_HonorsVmLifecycleEdges()
    {
        var plan = await _service.BuildPlanAsync(CreateSingleVmStandaloneRequest());
        Assert.True(plan.Success);

        var result = await RunGraphSchedulerAsync(plan);

        Assert.True(result.Success);
        AssertAllEdgesHonored(plan, result);
        AssertAdmittedBefore(result, NodeId(plan, V2PlanNodeKind.ProvisionVm), NodeId(plan, V2PlanNodeKind.StartVm));
        AssertAdmittedBefore(result, NodeId(plan, V2PlanNodeKind.StartVm), NodeId(plan, V2PlanNodeKind.GuestTransportReady));
    }

    [Fact]
    public async Task Scheduler_ClientAndDomainControllerPlan_AdmitsJoinAfterDomainReadyGates()
    {
        var plan = await _service.BuildPlanAsync(
            CreateAdCoreRequest("Balanced", ["slot-local", "slot-join", "slot-admin", "slot-dsrm"]));
        Assert.True(plan.Success);

        var result = await RunGraphSchedulerAsync(plan);

        Assert.True(result.Success);
        AssertAllEdgesHonored(plan, result);

        var domainReady = NodeId(plan, V2PlanNodeKind.DomainReady, "vm-dc01");
        var stabilizeDns = NodeId(plan, V2PlanNodeKind.StabilizeDomainDns, "vm-dc01");
        var join = NodeId(plan, V2PlanNodeKind.JoinDomain, "vm-member01");
        AssertAdmittedBefore(result, domainReady, join);
        AssertAdmittedBefore(result, stabilizeDns, join);
    }

    [Fact]
    public async Task Scheduler_DomainControllerAndReplicaPlan_AdmitsReplicaAfterRootDomainReady()
    {
        var request = CreateAdCoreRequest("Balanced", ["slot-local", "slot-join", "slot-admin", "slot-dsrm"]);
        request.Template.VmTemplates.Insert(1, new VmTemplate
        {
            VmId = "vm-replica01",
            Name = "replica01",
            MemoryMb = 4096,
            CpuCount = 2,
            VhdxId = "disk-replica",
            TopologyRole = "ReplicaDomainController",
            DomainId = "domain-contoso",
            CredentialSlots = new VmCredentialSlotBindings
            {
                LocalBootstrap = "slot-local",
                DomainAdmin = "slot-admin",
                Dsrm = "slot-dsrm"
            },
            Nics =
            [
                new VmNetworkInterfaceTemplate
                {
                    NicId = "nic-replica",
                    NetworkId = "lab-core",
                    IpAddress = "10.0.0.11",
                    PrefixLength = 24,
                    DefaultGateway = "10.0.0.1",
                    DnsServers = ["10.0.0.10", "8.8.8.8"]
                }
            ]
        });
        request.CatalogItems = request.CatalogItems.Concat([CreateCatalogItem("disk-replica", "slot-local")]).ToArray();

        var plan = await _service.BuildPlanAsync(request);
        Assert.True(plan.Success);

        var result = await RunGraphSchedulerAsync(plan);

        Assert.True(result.Success);
        AssertAllEdgesHonored(plan, result);

        var rootDomainReady = NodeId(plan, V2PlanNodeKind.DomainReady, "vm-dc01");
        var promoteReplica = NodeId(plan, V2PlanNodeKind.PromoteReplicaDomainController, "vm-replica01");
        var replicaReady = NodeId(plan, V2PlanNodeKind.ReplicaDomainReady, "vm-replica01");
        AssertAdmittedBefore(result, rootDomainReady, promoteReplica);
        AssertAdmittedBefore(result, promoteReplica, replicaReady);
    }

    [Fact]
    public async Task Scheduler_RouterWithTwoSubnetsPlan_HonorsRouterChainAndCrossSwitchGates()
    {
        var plan = await _service.BuildPlanAsync(CreateCrossSwitchRequest(includeRouter: true));
        Assert.True(plan.Success);

        var result = await RunGraphSchedulerAsync(plan);

        Assert.True(result.Success);
        AssertAllEdgesHonored(plan, result);

        var routerVm = plan.Context.Vms.Single(vm => vm.TopologyRole == "Router").VmId;
        AssertAdmittedBefore(
            result,
            NodeId(plan, V2PlanNodeKind.PrepareRouterNetwork, routerVm),
            NodeId(plan, V2PlanNodeKind.EnableRouterRouting, routerVm));
        AssertAdmittedBefore(
            result,
            NodeId(plan, V2PlanNodeKind.EnableRouterRouting, routerVm),
            NodeId(plan, V2PlanNodeKind.ConfigureRouterNat, routerVm));
    }

    [Fact]
    public async Task Scheduler_TwoForestTrustPlan_AdmitsTrustNodesAfterBothForestsReady()
    {
        var plan = await _service.BuildPlanAsync(
            CreateManagedForestTrustRequest(["slot-local", "slot-admin", "slot-dsrm", "slot-fabrikam-admin"]));
        Assert.True(plan.Success);

        var result = await RunGraphSchedulerAsync(plan);

        Assert.True(result.Success);
        AssertAllEdgesHonored(plan, result);

        var contosoReady = NodeId(plan, V2PlanNodeKind.DomainReady, "vm-dc01");
        var fabrikamReady = NodeId(plan, V2PlanNodeKind.DomainReady, "vm-fabrikamdc01");
        var prepareDns = NodeId(plan, V2PlanNodeKind.PrepareForestTrustDns);
        var createTrust = NodeId(plan, V2PlanNodeKind.CreateForestTrust);
        var validateTrust = NodeId(plan, V2PlanNodeKind.ValidateForestTrust);

        AssertAdmittedBefore(result, contosoReady, prepareDns);
        AssertAdmittedBefore(result, fabrikamReady, prepareDns);
        AssertAdmittedBefore(result, prepareDns, createTrust);
        AssertAdmittedBefore(result, createTrust, validateTrust);
    }

    /// <summary>
    /// Runs the plan through the generic scheduler with a recording registry that registers a trivially-succeeding
    /// executor for every node kind present (including <see cref="V2PlanNodeKind.ApplyCapabilityRole"/>) so validation
    /// passes and the run exercises ordering only.
    /// </summary>
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
}
