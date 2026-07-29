using LabAssistant.UITesting.Infrastructure;
using Xunit;

namespace LabAssistant.UITesting.Tests;

/// <summary>
/// Behaviour tests for the deploy step-log reader that backs the router-tail completion assertions.
/// The parse runs entirely against synthetic structured-event lines (no app, no Hyper-V), mirroring
/// the real deploy.step.run.end schema: event / ts / result / context.vmName / context.stepKey.
///
/// This pins the contract the router scenario relies on: an absent step reads as null (keep waiting),
/// success/skipped/failed map to their outcomes, and only terminal run.end events for the right VM
/// inside the window are considered - so a skipped validation can never be misread as a completed one,
/// and a failed step is never silently dropped.
/// </summary>
public sealed class DeployStepLogProbeTests
{
    private const string Vm = "LAT-20260728-071810-rtr";
    private const string Step = "v2.enableRouterRouting";
    private static readonly DateTimeOffset Window = DateTimeOffset.Parse(
        "2026-07-28T13:00:00.0000000Z", null, System.Globalization.DateTimeStyles.RoundtripKind);

    private static string End(string ts, string vm, string stepKey, string result)
        => $"{{\"ts\":\"{ts}\",\"level\":\"info\",\"event\":\"deploy.step.run.end\"," +
           $"\"operationId\":\"op1\",\"result\":\"{result}\"," +
           $"\"context\":{{\"vmName\":\"{vm}\",\"stepKey\":\"{stepKey}\"}}}}";

    private static string Start(string ts, string vm, string stepKey)
        => $"{{\"ts\":\"{ts}\",\"level\":\"info\",\"event\":\"deploy.step.run.start\"," +
           $"\"operationId\":\"op1\",\"context\":{{\"vmName\":\"{vm}\",\"stepKey\":\"{stepKey}\"}}}}";

    private static DeployStepOutcome? Find(params string[] lines)
        => DeployStepLogProbe.FindStepTerminalOutcome(lines, Vm, Step, Window);

    [Fact]
    public void Success_result_maps_to_Success()
        => Assert.Equal(DeployStepOutcome.Success, Find(End("2026-07-28T13:05:00.0000000Z", Vm, Step, "success")));

    [Fact]
    public void Skipped_result_maps_to_Skipped()
        => Assert.Equal(DeployStepOutcome.Skipped, Find(End("2026-07-28T13:05:00.0000000Z", Vm, Step, "skipped")));

    [Fact]
    public void Failed_result_maps_to_Failed()
        => Assert.Equal(DeployStepOutcome.Failed, Find(End("2026-07-28T13:05:00.0000000Z", Vm, Step, "failed")));

    [Fact]
    public void Absent_step_reads_as_null()
        => Assert.Null(Find(End("2026-07-28T13:05:00.0000000Z", Vm, "v2.configureRouterNat", "success")));

    [Fact]
    public void Other_vm_is_ignored()
        => Assert.Null(Find(End("2026-07-28T13:05:00.0000000Z", "some-other-vm", Step, "success")));

    [Fact]
    public void Events_before_the_window_are_ignored()
        => Assert.Null(Find(End("2026-07-28T12:59:59.0000000Z", Vm, Step, "success")));

    [Fact]
    public void Non_run_end_events_are_ignored()
        => Assert.Null(Find(Start("2026-07-28T13:05:00.0000000Z", Vm, Step)));

    [Fact]
    public void Latest_terminal_by_timestamp_wins()
    {
        // A retried step could emit failed then success; the final outcome must be authoritative.
        var outcome = Find(
            End("2026-07-28T13:05:00.0000000Z", Vm, Step, "failed"),
            End("2026-07-28T13:06:00.0000000Z", Vm, Step, "success"));
        Assert.Equal(DeployStepOutcome.Success, outcome);
    }

    [Fact]
    public void Malformed_lines_are_skipped_and_the_good_terminal_is_returned()
    {
        var outcome = Find(
            "this is not json",
            "{\"event\":\"deploy.step.run.end\"}",     // no ts / result / context
            "",
            End("2026-07-28T13:05:00.0000000Z", Vm, Step, "success"));
        Assert.Equal(DeployStepOutcome.Success, outcome);
    }

    [Fact]
    public void Unknown_result_value_is_ignored()
        => Assert.Null(Find(End("2026-07-28T13:05:00.0000000Z", Vm, Step, "retrying")));
}
