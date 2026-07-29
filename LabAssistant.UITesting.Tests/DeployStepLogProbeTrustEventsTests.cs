using LabAssistant.UITesting.Infrastructure;
using Xunit;

namespace LabAssistant.UITesting.Tests;

/// <summary>
/// Behaviour tests for the forest-trust lifecycle reader added to <see cref="DeployStepLogProbe"/> for the
/// rollback verb (finding 79). The parse runs entirely against synthetic structured-event lines (no app, no
/// Hyper-V), mirroring the real EmitTrustEvent schema: event = deploy.forest-trust.&lt;op&gt;.&lt;phase&gt;,
/// ts, result in {started, success, failed}, context carries trustId / stepKey but NO vmName.
///
/// This pins the contract the rollback scenario relies on: the create-start trigger and the authoritative
/// cleanup-ran markers are matched by event-name + result, events outside the window or facility are ignored,
/// the latest matching timestamp wins, and malformed lines never throw.
/// </summary>
public sealed class DeployStepLogProbeTrustEventsTests
{
    private static readonly DateTimeOffset Window = DateTimeOffset.Parse(
        "2026-07-28T13:00:00.0000000Z", null, System.Globalization.DateTimeStyles.RoundtripKind);

    // Mirrors EmitTrustEvent: a code-based event whose dotted name is the event field, result verbatim, and a
    // context with trustId/stepKey but no vmName.
    private static string Trust(string ts, string eventName, string result, string stepKey)
        => $"{{\"ts\":\"{ts}\",\"level\":\"info\",\"event\":\"{eventName}\"," +
           $"\"operationId\":\"op1\",\"result\":\"{result}\"," +
           $"\"context\":{{\"executionEngine\":\"V2\",\"trustId\":\"trust-alpha-beta\"," +
           $"\"sourceDomainId\":\"domain-alpha\",\"targetDomainId\":\"domain-beta\",\"stepKey\":\"{stepKey}\"}}}}";

    private static DateTimeOffset? Find(string eventName, string result, params string[] lines)
        => DeployStepLogProbe.FindTrustEventTimestamp(lines, eventName, result, Window);

    [Fact]
    public void Create_start_started_is_found()
    {
        var ts = Find(
            DeployStepLogProbe.TrustCreateStartEvent, "started",
            Trust("2026-07-28T13:05:00.0000000Z", DeployStepLogProbe.TrustCreateStartEvent, "started", "v2.createForestTrust"));
        Assert.NotNull(ts);
    }

    [Fact]
    public void Cleanup_start_and_success_are_found()
    {
        var lines = new[]
        {
            Trust("2026-07-28T13:06:00.0000000Z", DeployStepLogProbe.TrustCleanupStartEvent, "started", "v2.cleanupForestTrust"),
            Trust("2026-07-28T13:06:30.0000000Z", DeployStepLogProbe.TrustCleanupEndEvent, "success", "v2.cleanupForestTrust")
        };
        Assert.NotNull(Find(DeployStepLogProbe.TrustCleanupStartEvent, "started", lines));
        Assert.NotNull(Find(DeployStepLogProbe.TrustCleanupEndEvent, "success", lines));
    }

    [Fact]
    public void Cleanup_end_failed_is_distinguished_from_success()
    {
        var line = Trust("2026-07-28T13:06:30.0000000Z", DeployStepLogProbe.TrustCleanupEndEvent, "failed", "v2.cleanupForestTrust");
        // A residual (failed) cleanup end must NOT read as a success end, and must read as a failed end.
        Assert.Null(Find(DeployStepLogProbe.TrustCleanupEndEvent, "success", line));
        Assert.NotNull(Find(DeployStepLogProbe.TrustCleanupEndEvent, "failed", line));
    }

    [Fact]
    public void Wrong_result_for_same_event_is_ignored()
        => Assert.Null(Find(
            DeployStepLogProbe.TrustCreateStartEvent, "started",
            Trust("2026-07-28T13:05:00.0000000Z", DeployStepLogProbe.TrustCreateStartEvent, "success", "v2.createForestTrust")));

    [Fact]
    public void Events_before_the_window_are_ignored()
        => Assert.Null(Find(
            DeployStepLogProbe.TrustCleanupStartEvent, "started",
            Trust("2026-07-28T12:59:59.0000000Z", DeployStepLogProbe.TrustCleanupStartEvent, "started", "v2.cleanupForestTrust")));

    [Fact]
    public void Non_trust_events_are_ignored()
    {
        // A deploy.step.run.end line for the create step must NOT satisfy a trust-event query.
        var stepEnd = "{\"ts\":\"2026-07-28T13:05:00.0000000Z\",\"level\":\"info\",\"event\":\"deploy.step.run.end\"," +
                      "\"operationId\":\"op1\",\"result\":\"skipped\"," +
                      "\"context\":{\"vmName\":\"LAT-dc\",\"stepKey\":\"v2.createForestTrust\"}}";
        Assert.Null(Find(DeployStepLogProbe.TrustCreateStartEvent, "started", stepEnd));
    }

    [Fact]
    public void Latest_matching_timestamp_wins()
    {
        // A retried cleanup could emit two starts; the latest timestamp must be returned.
        var ts = Find(
            DeployStepLogProbe.TrustCleanupStartEvent, "started",
            Trust("2026-07-28T13:06:00.0000000Z", DeployStepLogProbe.TrustCleanupStartEvent, "started", "v2.cleanupForestTrust"),
            Trust("2026-07-28T13:07:00.0000000Z", DeployStepLogProbe.TrustCleanupStartEvent, "started", "v2.cleanupForestTrust"));
        Assert.Equal(
            DateTimeOffset.Parse("2026-07-28T13:07:00.0000000Z", null, System.Globalization.DateTimeStyles.RoundtripKind),
            ts);
    }

    [Fact]
    public void Malformed_lines_are_skipped_and_the_good_event_is_found()
    {
        var ts = Find(
            DeployStepLogProbe.TrustCleanupEndEvent, "success",
            "this is not json",
            "{\"event\":\"deploy.forest-trust.cleanup.end\"}",   // no ts / result
            "",
            Trust("2026-07-28T13:06:30.0000000Z", DeployStepLogProbe.TrustCleanupEndEvent, "success", "v2.cleanupForestTrust"));
        Assert.NotNull(ts);
    }

    [Fact]
    public void Absent_event_reads_as_null()
        => Assert.Null(Find(
            DeployStepLogProbe.TrustValidateEndEvent, "success",
            Trust("2026-07-28T13:05:00.0000000Z", DeployStepLogProbe.TrustCreateStartEvent, "started", "v2.createForestTrust")));
}
