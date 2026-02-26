using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using Xunit;

namespace LabAssistant.Business.Tests;

public class GuestStepExecutionSelectionTests
{
    [Fact]
    public async Task OptionalImplementedStep_Disabled_IsExplicitlySkippedWithNotSelectedReason()
    {
        var step = new SetTimeZoneStep();
        var context = CreateContext();
        context.GuestServicesEnabled = true;
        context.ConfigureTimeZone = false;

        var events = new List<RecordedStepEvent>();
        context.StructuredEventEmitter = (eventName, level, result, payload) =>
            events.Add(new RecordedStepEvent(eventName, level, result, payload ?? new Dictionary<string, object?>()));

        await step.ExecuteAsync(context);

        var outcome = Assert.Single(context.GuestStepOutcomes);
        Assert.Equal(DeploymentStepKeys.SetTimeZone, outcome.StepKey);
        Assert.Equal(GuestStepOutcomeResults.Skipped, outcome.Result);
        Assert.Equal(GuestStepSkipReasons.NotSelected, outcome.SkipReason);

        var skippedEvent = Assert.Single(events.Where(e => e.EventName == "StepSkipped"));
        Assert.Equal("skipped", skippedEvent.Result);
        Assert.Equal(DeploymentStepKeys.SetTimeZone, skippedEvent.Context["stepKey"]?.ToString());
        Assert.Equal(GuestStepSkipReasons.NotSelected, skippedEvent.Context["skipReason"]?.ToString());
    }

    [Fact]
    public async Task OptionalImplementedStep_Enabled_ReachesExecutionPathAndRecordsExecutedOutcome()
    {
        var step = new InstallSoftwareStep();
        var context = CreateContext();
        context.GuestServicesEnabled = true;
        context.InstallSoftware = true;

        var events = new List<RecordedStepEvent>();
        context.StructuredEventEmitter = (eventName, level, result, payload) =>
            events.Add(new RecordedStepEvent(eventName, level, result, payload ?? new Dictionary<string, object?>()));

        await step.ExecuteAsync(context);

        var outcome = Assert.Single(context.GuestStepOutcomes);
        Assert.Equal(DeploymentStepKeys.InstallSoftware, outcome.StepKey);
        Assert.Equal(GuestStepOutcomeResults.Executed, outcome.Result);
        Assert.Null(outcome.SkipReason);
        Assert.Contains(context.Logs, log => log.Contains("Software installed in Guest OS.", StringComparison.Ordinal));
        Assert.DoesNotContain(events, e => e.EventName == "StepSkipped");
    }

    [Fact]
    public async Task PlaceholderStep_IsExplicitlySkippedWithNotImplementedReason()
    {
        var step = new ConfigureNetworkInformationStep();
        var context = CreateContext();
        context.GuestServicesEnabled = true;
        context.ConfigureNetworkInformation = true;

        var events = new List<RecordedStepEvent>();
        context.StructuredEventEmitter = (eventName, level, result, payload) =>
            events.Add(new RecordedStepEvent(eventName, level, result, payload ?? new Dictionary<string, object?>()));

        await step.ExecuteAsync(context);

        var outcome = Assert.Single(context.GuestStepOutcomes);
        Assert.Equal(DeploymentStepKeys.ConfigureNetworkInformation, outcome.StepKey);
        Assert.Equal(GuestStepOutcomeResults.Skipped, outcome.Result);
        Assert.Equal(GuestStepSkipReasons.NotImplemented, outcome.SkipReason);

        var skippedEvent = Assert.Single(events.Where(e => e.EventName == "StepSkipped"));
        Assert.Equal("skipped", skippedEvent.Result);
        Assert.Equal(GuestStepSkipReasons.NotImplemented, skippedEvent.Context["skipReason"]?.ToString());
    }

    [Fact]
    public async Task GuestStep_RespectsCancellationBoundary_WithoutRecordingOutcome()
    {
        var step = new InstallRoleStep();
        var context = CreateContext();
        context.GuestServicesEnabled = true;
        context.InstallRole = true;
        context.ShouldAbort = () => true;

        await step.ExecuteAsync(context);

        Assert.True(context.WasCancelled);
        Assert.Empty(context.GuestStepOutcomes);
    }

    private static VmDeploymentContext CreateContext()
    {
        return new VmDeploymentContext
        {
            VmId = Guid.NewGuid(),
            VmName = "vm1",
            VmPath = @"C:\vm\vm1",
            VhdPath = @"C:\vm\vm1\vm1.vhdx",
            BaseVhdPath = @"D:\base\parent.vhdx"
        };
    }

    private sealed record RecordedStepEvent(
        string EventName,
        string Level,
        string? Result,
        IReadOnlyDictionary<string, object?> Context);
}
