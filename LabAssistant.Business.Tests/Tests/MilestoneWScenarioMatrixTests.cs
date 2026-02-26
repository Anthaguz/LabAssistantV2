using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using Xunit;

namespace LabAssistant.Business.Tests.Tests;

public class MilestoneWScenarioMatrixTests
{
    [Fact]
    public async Task GuestStepSelectionAndPlaceholderOutcomes_AreLoggedAndPreservedIntoPerVmSummary()
    {
        var vm = new VmDeploymentContext
        {
            VmId = Guid.NewGuid(),
            VmName = "VM1",
            GuestServicesEnabled = true,
            ConfigureTimeZone = false,
            InstallSoftware = true,
            ConfigureNetworkInformation = true
        };

        var events = new List<(string EventName, string? Result, IReadOnlyDictionary<string, object?> Context)>();
        vm.StructuredEventEmitter = (eventName, _, result, payload) =>
            events.Add((eventName, result, payload ?? new Dictionary<string, object?>()));

        await new SetTimeZoneStep().ExecuteAsync(vm);
        await new InstallSoftwareStep().ExecuteAsync(vm);
        await new ConfigureNetworkInformationStep().ExecuteAsync(vm);

        Assert.Equal(3, vm.GuestStepOutcomes.Count);
        Assert.Contains(vm.GuestStepOutcomes, o =>
            o.StepKey == DeploymentStepKeys.SetTimeZone &&
            o.Result == GuestStepOutcomeResults.Skipped &&
            o.SkipReason == GuestStepSkipReasons.NotSelected);
        Assert.Contains(vm.GuestStepOutcomes, o =>
            o.StepKey == DeploymentStepKeys.InstallSoftware &&
            o.Result == GuestStepOutcomeResults.Executed &&
            o.SkipReason == null);
        Assert.Contains(vm.GuestStepOutcomes, o =>
            o.StepKey == DeploymentStepKeys.ConfigureNetworkInformation &&
            o.Result == GuestStepOutcomeResults.Skipped &&
            o.SkipReason == GuestStepSkipReasons.NotImplemented);

        var skippedEvents = events.Where(e => e.EventName == "StepSkipped").ToList();
        Assert.Equal(2, skippedEvents.Count);
        Assert.All(skippedEvents, e => Assert.Equal("skipped", e.Result));
        Assert.Contains(skippedEvents, e =>
            e.Context["stepKey"]?.ToString() == DeploymentStepKeys.SetTimeZone &&
            e.Context["skipReason"]?.ToString() == GuestStepSkipReasons.NotSelected);
        Assert.Contains(skippedEvents, e =>
            e.Context["stepKey"]?.ToString() == DeploymentStepKeys.ConfigureNetworkInformation &&
            e.Context["skipReason"]?.ToString() == GuestStepSkipReasons.NotImplemented);

        var multi = new MultiVmDeploymentContext
        {
            VmContexts = { vm }
        };
        multi.MarkRunning();
        multi.CompleteTerminalState(hasFailures: false, hasCleanupResiduals: false);

        var summary = new DeploymentOutcomeSummaryBuilder().Build(multi);
        var vmSummary = Assert.Single(summary.VmOutcomes);
        Assert.Equal(3, vmSummary.GuestStepOutcomes.Count);
        Assert.Contains(vmSummary.GuestStepOutcomes, o => o.StepKey == DeploymentStepKeys.SetTimeZone && o.SkipReason == GuestStepSkipReasons.NotSelected);
        Assert.Contains(vmSummary.GuestStepOutcomes, o => o.StepKey == DeploymentStepKeys.ConfigureNetworkInformation && o.SkipReason == GuestStepSkipReasons.NotImplemented);
    }

    [Fact]
    public async Task GuestStepReadinessCompletenessMatrix_EnforcesBlockingOnlyForEnabledIncompleteImplementedSteps()
    {
        var service = new DeploymentPreflightService(
        [
            new GuestStepConfigurationCompletenessPreflightCheck()
        ]);

        var context = new MultiVmDeploymentContext
        {
            VmContexts =
            {
                new VmDeploymentContext
                {
                    VmName = "VM-TimeZone-Missing",
                    ConfigureTimeZone = true,
                    TimeZoneConfig = new TimeZoneStepConfig { Enabled = true }
                },
                new VmDeploymentContext
                {
                    VmName = "VM-Software-Disabled",
                    InstallSoftware = false,
                    SoftwareConfig = new SoftwareStepConfig { Enabled = false }
                },
                new VmDeploymentContext
                {
                    VmName = "VM-Role-Configured",
                    InstallRole = true,
                    RoleConfig = new RoleStepConfig { Enabled = true, Roles = new() { "WebServer" } }
                },
                new VmDeploymentContext
                {
                    VmName = "VM-Network-Placeholder",
                    ConfigureNetworkInformation = true,
                    GuestNetworkConfig = new GuestNetworkStepConfig { Enabled = false }
                }
            }
        };

        var quick = await service.RunAsync(context, DeploymentPreflightMode.Quick);
        var full = await service.RunAsync(context, DeploymentPreflightMode.Full);

        Assert.True(quick.HasBlockingFailures);
        Assert.False(quick.CanDeploy);
        Assert.True(full.HasBlockingFailures);
        Assert.False(full.CanDeploy);

        Assert.Contains(quick.Results, r =>
            r.Status == DeploymentReadinessStatus.Fail &&
            r.Category == DeploymentReadinessCategory.TemplateConfig &&
            r.Code == "GST.TIMEZONE.MISSING_CONFIG" &&
            r.AffectedVmNames.Contains("VM-TimeZone-Missing"));

        Assert.Contains(full.Results, r =>
            r.Status == DeploymentReadinessStatus.Pass &&
            r.Code == "GST.ROLE.CONFIG_OK" &&
            r.AffectedVmNames.Contains("VM-Role-Configured"));

        Assert.DoesNotContain(full.Results, r => r.Code.StartsWith("GST.SOFTWARE.", StringComparison.Ordinal) && r.Status == DeploymentReadinessStatus.Fail);
        Assert.DoesNotContain(full.Results, r => r.Code.StartsWith("GST.NETWORK", StringComparison.Ordinal));
    }
}
