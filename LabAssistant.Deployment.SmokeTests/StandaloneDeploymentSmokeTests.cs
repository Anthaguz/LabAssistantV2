using LabAssistant.Deployment.Harness;
using LabAssistant.Models.Templates;
using Xunit;
using Xunit.Abstractions;

namespace LabAssistant.Deployment.SmokeTests;

/// <summary>
/// Real-hardware smoke test for the standalone topology: deploy one VM off the prepared base image through
/// the V2 planner + runtime + ready-set graph scheduler, verify it started, and prove PowerShell Direct into
/// the guest. Skipped unless the opt-in Hyper-V preconditions are met (see <see cref="HyperVFactAttribute"/>).
/// Teardown is guaranteed in a finally block so no VM is left orphaned regardless of outcome.
/// </summary>
public sealed class StandaloneDeploymentSmokeTests
{
    private readonly ITestOutputHelper _output;

    public StandaloneDeploymentSmokeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [HyperVFact]
    public async Task Standalone_Deploys_Starts_And_Guest_Is_Reachable()
    {
        var options = HarnessOptions.FromEnvironment(useGraphScheduler: true);
        var scenario = LabScenarioLibrary.Standalone(options, vmName: "harness-smoke-standalone-01");

        await using var environment = new IsolatedLabEnvironment(options);
        var harness = new LabDeploymentHarness(environment, _output.WriteLine);

        try
        {
            var result = await harness.DeployAsync(scenario, probeGuest: true);

            Assert.True(result.PlanDeployable, "Plan should be deployable for a bare standalone VM.");
            Assert.True(result.RuntimeSuccess, "V2 runtime should report success.");

            // The bare-switch standalone plan must be exactly ProvisionVm -> StartVm with no orphaned
            // guest-network node (the invariant PR #853 protects). Assert the guest-network kind is absent.
            Assert.DoesNotContain(
                result.Plan.Nodes,
                n => n.Kind == V2PlanNodeKind.PrepareGuestNetwork);

            Assert.True(result.GuestProbeSucceeded, "PowerShell Direct into the guest should succeed after boot.");
            Assert.False(string.IsNullOrWhiteSpace(result.GuestProbeOutput));
        }
        finally
        {
            await harness.TeardownAsync(scenario);
        }
    }
}
