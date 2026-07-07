using System.Linq;
using LabAssistant.Deployment.Harness;
using LabAssistant.Models.Templates;
using Xunit;
using Xunit.Abstractions;

namespace LabAssistant.Deployment.SmokeTests;

/// <summary>
/// Coverage for the single first-domain-controller topology. The fast <see cref="Fact"/> builds the V2 plan
/// offline (no Hyper-V, no elevation) and asserts its structure: the Internal switch is created before the VM,
/// and the AD gate order (feature install -> promote -> domain ready) holds. The <see cref="HyperVFactAttribute"/>
/// test provisions the DC on real hardware and promotes a fresh forest, with guaranteed teardown so no VM or
/// host switch is left orphaned.
/// </summary>
public sealed class DomainControllerDeploymentSmokeTests
{
    private readonly ITestOutputHelper _output;

    public DomainControllerDeploymentSmokeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task DomainController_Plan_CreatesSwitchBeforeVm_AndHonorsAdGateOrder()
    {
        var options = HarnessOptions.FromEnvironment(useGraphScheduler: true);
        var scenario = LabScenarioLibrary.DomainController(options, vmName: "harness-plan-dc-01");

        using var environment = new IsolatedLabEnvironment(options);
        var harness = new LabDeploymentHarness(environment, _output.WriteLine);

        var plan = await harness.BuildPlanAsync(scenario);

        Assert.True(plan.Success, "DC plan should build successfully.");
        Assert.DoesNotContain(plan.Issues, i => i.Severity == V2PlanIssueSeverity.Blocking);

        // The Internal switch is host infrastructure the deploy must create before the VM that attaches to it.
        var switchNode = Assert.Single(plan.Nodes, n => n.Kind == V2PlanNodeKind.EnsureNetworkSwitch);
        var provisionNode = Assert.Single(plan.Nodes, n => n.Kind == V2PlanNodeKind.ProvisionVm);
        AssertGateEdge(plan, switchNode.NodeId, provisionNode.NodeId);

        // The forest promotion chain must be ordered feature-install -> promote -> domain-ready.
        var featureNode = Assert.Single(plan.Nodes, n => n.Kind == V2PlanNodeKind.InstallAdDomainServicesFeature);
        var promoteNode = Assert.Single(plan.Nodes, n => n.Kind == V2PlanNodeKind.PromoteFirstDomainController);
        var domainReadyNode = Assert.Single(plan.Nodes, n => n.Kind == V2PlanNodeKind.DomainReady);
        AssertReachableBefore(plan, featureNode.NodeId, promoteNode.NodeId);
        AssertReachableBefore(plan, promoteNode.NodeId, domainReadyNode.NodeId);

        // The only legitimate in-degree-0 entries are the switch and the VM provision; nothing else may be orphaned.
        var entryKinds = plan.Nodes
            .Where(n => plan.Dependencies.All(d => d.ToNodeId != n.NodeId))
            .Select(n => n.Kind)
            .ToHashSet();
        Assert.Subset(
            new HashSet<V2PlanNodeKind> { V2PlanNodeKind.EnsureNetworkSwitch, V2PlanNodeKind.ProvisionVm },
            entryKinds);
    }

    [HyperVFact]
    public async Task DomainController_Deploys_And_Promotes_A_New_Forest()
    {
        var options = HarnessOptions.FromEnvironment(useGraphScheduler: true);
        var scenario = LabScenarioLibrary.DomainController(options, vmName: "harness-smoke-dc-01");

        using var environment = new IsolatedLabEnvironment(options);
        var harness = new LabDeploymentHarness(environment, _output.WriteLine);

        try
        {
            // Promotion reboots the guest and re-homes the built-in Administrator into the new domain, so guest
            // probing timing is unreliable here. RuntimeSuccess already proves the whole graph (including
            // promotion and the DomainReady gate) completed, which is the assertion that matters.
            var result = await harness.DeployAsync(scenario, probeGuest: false);

            Assert.True(result.PlanDeployable, "DC plan should be deployable.");
            Assert.True(result.RuntimeSuccess, "V2 runtime should promote the forest and report success.");

            Assert.Contains(result.Plan.Nodes, n => n.Kind == V2PlanNodeKind.EnsureNetworkSwitch);
            Assert.Contains(result.Plan.Nodes, n => n.Kind == V2PlanNodeKind.PromoteFirstDomainController);
        }
        finally
        {
            await harness.TeardownAsync(scenario);
        }
    }

    private static void AssertGateEdge(V2PlanBuildResult plan, string fromNodeId, string toNodeId)
    {
        Assert.Contains(
            plan.Dependencies,
            d => d.FromNodeId == fromNodeId && d.ToNodeId == toNodeId && d.IsBlockingGate);
    }

    private static void AssertReachableBefore(V2PlanBuildResult plan, string fromNodeId, string toNodeId)
    {
        // Direct or transitive edge from -> to. The planner may route promotion ordering through intermediate
        // gates, so a reachability walk is more robust than asserting a single direct edge.
        var visited = new HashSet<string>();
        var stack = new Stack<string>();
        stack.Push(fromNodeId);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            foreach (var edge in plan.Dependencies.Where(d => d.FromNodeId == current))
            {
                if (edge.ToNodeId == toNodeId)
                {
                    return;
                }

                if (visited.Add(edge.ToNodeId))
                {
                    stack.Push(edge.ToNodeId);
                }
            }
        }

        Assert.Fail($"Expected a dependency path from '{fromNodeId}' to '{toNodeId}'.");
    }
}
