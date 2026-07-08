using LabAssistant.Deployment.Harness;
using LabAssistant.Models.Templates;

namespace LabAssistant.Deployment.Runner;

/// <summary>
/// Thin console over the deployment harness. Drives the standalone smoke scenario through the V2 planner and
/// (with <c>--deploy</c>) the V2 runtime, using an isolated throwaway config root so it never touches the real
/// <c>%APPDATA%\LabAssistant</c>. Replaces the ad-hoc external deploy runner with a versioned, in-repo tool.
/// </summary>
/// <remarks>
/// Modes:
///   (default)     build and print the V2 plan only - safe, fast, non-elevated, no Hyper-V.
///   --deploy      provision the scenario through the runtime - requires elevation + Hyper-V + admin password env.
///   --probe       after --deploy, probe the guest over PowerShell Direct.
///   --teardown    after --deploy, stop and delete the scenario VMs (no orphans).
///   --no-graph    disable the ready-set graph scheduler (use the legacy fan-out).
///   --scenario S  select the topology: standalone (default) or dc (single first domain controller).
///   --vm NAME     override the VM name (default depends on scenario).
/// </remarks>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var planOnly = !Has(args, "--deploy");
        var probe = Has(args, "--probe");
        var teardown = Has(args, "--teardown");
        var useGraph = !Has(args, "--no-graph");
        var scenarioName = (ArgValue(args, "--scenario") ?? "standalone").ToLowerInvariant();

        using var log = new RunLog();
        log.Line("=== LabAssistant deployment runner (harness) ===");

        var options = HarnessOptions.FromEnvironment(useGraph);
        log.Line($"Base image: {options.BaseImagePath} (id {options.BaseImageId})");
        log.Line($"Graph scheduler: {(useGraph ? "ENABLED" : "disabled (legacy)")}  mode={(planOnly ? "plan-only" : "deploy")} probe={probe} teardown={teardown} scenario={scenarioName}");

        if (!planOnly)
        {
            var skipReason = HarnessCapabilities.DescribeSkip(options);
            if (skipReason is not null)
            {
                log.Line($"Cannot deploy: {skipReason}");
                log.Line("RESULT: preconditions-not-met");
                return 2;
            }
        }

        try
        {
            await using var env = new IsolatedLabEnvironment(options);
            log.Line($"Isolated config root: {env.AppRoot}");

            var harness = new LabDeploymentHarness(env, log.Line);
            LabScenario scenario;
            switch (scenarioName)
            {
                case "standalone":
                    scenario = LabScenarioLibrary.Standalone(options, ArgValue(args, "--vm") ?? "harness-standalone-01");
                    break;
                case "dc":
                case "domaincontroller":
                    scenario = LabScenarioLibrary.DomainController(options, ArgValue(args, "--vm") ?? "harness-dc-01");
                    break;
                case "dc-member":
                case "domainmember":
                    scenario = LabScenarioLibrary.DomainMember(options);
                    break;
                case "replica-dc":
                case "replicadomaincontroller":
                    scenario = LabScenarioLibrary.ReplicaDomainController(options);
                    break;
                case "child-domain":
                case "childdomain":
                    scenario = LabScenarioLibrary.ChildDomain(options);
                    break;
                default:
                    log.Line($"Unknown scenario '{scenarioName}'. Valid: standalone, dc, dc-member, replica-dc, child-domain.");
                    log.Line("RESULT: unknown-scenario");
                    return 3;
            }
            log.Line($"Scenario: {scenario.Name}  expected VMs: {string.Join(", ", scenario.ExpectedVmNames)}");

            if (planOnly)
            {
                var plan = await harness.BuildPlanAsync(scenario);
                log.Line(PlanTextFormatter.Format(plan));
                var deployable = plan.Success
                    && plan.Issues.All(i => i.Severity != V2PlanIssueSeverity.Blocking)
                    && plan.UnresolvedRequirements.Count == 0;
                log.Line(deployable ? "RESULT: plan-deployable" : "RESULT: plan-not-deployable");
                return deployable ? 0 : 6;
            }

            try
            {
                var result = await harness.DeployAsync(scenario, probeGuest: probe);
                if (!result.PlanDeployable)
                {
                    log.Line("RESULT: plan-not-deployable");
                    return 6;
                }

                var probeSuffix = probe ? $" guest-probe={(result.GuestProbeSucceeded ? "ok" : "unreachable")}" : string.Empty;
                log.Line($"RESULT: {(result.RuntimeSuccess ? "deploy-success" : "deploy-failed")}{probeSuffix}");

                if (result.GuestProbeSucceeded && result.GuestProbeOutput is not null)
                {
                    foreach (var line in result.GuestProbeOutput.Split('\n'))
                    {
                        var trimmed = line.TrimEnd('\r');
                        if (!string.IsNullOrWhiteSpace(trimmed))
                        {
                            log.Line($"  > {trimmed}");
                        }
                    }
                }

                return result.RuntimeSuccess ? 0 : 5;
            }
            finally
            {
                if (teardown)
                {
                    log.Line("Tearing down scenario VMs...");
                    await harness.TeardownAsync(scenario);
                }
            }
        }
        catch (Exception ex)
        {
            log.Line("FATAL EXCEPTION:");
            log.Line(ex.ToString());
            return 1;
        }
    }

    private static bool Has(string[] args, string name) =>
        args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

    private static string? ArgValue(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
