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

/// <summary>
/// Behaviour tests for the per-anchor honest-skip reader (<see cref="DeployStepLogProbe.FindTrustCleanupSkipDetail"/>)
/// the rollback verb uses to prove the moot-skip cleanup terminal is honest rather than a silent no-op. Mirrors the
/// locked #924 emit contract: the single deploy.forest-trust.cleanup.end (result=skipped) terminal carries
/// sourceAnchorOutcome / targetAnchorOutcome (each skipped|success|failed) + skipReason in its nested context. All
/// parses run against synthetic structured-event lines - no app, no Hyper-V.
/// </summary>
public sealed class DeployStepLogProbeTrustSkipDetailTests
{
    private static readonly DateTimeOffset Window = DateTimeOffset.Parse(
        "2026-07-28T13:00:00.0000000Z", null, System.Globalization.DateTimeStyles.RoundtripKind);

    // A cleanup.end terminal carrying the per-anchor honest-skip context fields.
    private static string SkipTerminal(string ts, string result, string? source, string? target, string? reason)
    {
        var parts = new System.Collections.Generic.List<string>
        {
            "\"executionEngine\":\"V2\"",
            "\"trustId\":\"trust-alpha-beta\"",
            "\"stepKey\":\"v2.cleanupForestTrust\""
        };
        if (source is not null) parts.Add($"\"sourceAnchorOutcome\":\"{source}\"");
        if (target is not null) parts.Add($"\"targetAnchorOutcome\":\"{target}\"");
        if (reason is not null) parts.Add($"\"skipReason\":\"{reason}\"");
        return $"{{\"ts\":\"{ts}\",\"level\":\"info\",\"event\":\"{DeployStepLogProbe.TrustCleanupEndEvent}\"," +
               $"\"operationId\":\"op1\",\"result\":\"{result}\",\"context\":{{{string.Join(",", parts)}}}}}";
    }

    private static TrustCleanupSkipDetail? Find(params string[] lines)
        => DeployStepLogProbe.FindTrustCleanupSkipDetail(lines, Window);

    [Fact]
    public void Both_anchors_skipped_with_reason_are_read()
    {
        var detail = Find(SkipTerminal(
            "2026-07-28T13:07:00.0000000Z", "skipped", "skipped", "skipped", "anchor VM being torn down"));
        Assert.NotNull(detail);
        Assert.Equal("skipped", detail!.SourceAnchorOutcome);
        Assert.Equal("skipped", detail.TargetAnchorOutcome);
        Assert.Equal("anchor VM being torn down", detail.SkipReason);
    }

    [Fact]
    public void Mixed_surviving_anchor_outcomes_are_read()
    {
        // (B) surviving-anchor: one side ran (success), the torn-down side was skipped. The reader reports both
        // verbatim so a future mixed verb can assert them; #921 does not exercise this shape.
        var detail = Find(SkipTerminal(
            "2026-07-28T13:07:00.0000000Z", "skipped", "success", "skipped", "one anchor survives"));
        Assert.NotNull(detail);
        Assert.Equal("success", detail!.SourceAnchorOutcome);
        Assert.Equal("skipped", detail.TargetAnchorOutcome);
    }

    [Fact]
    public void Skipped_terminal_without_anchor_context_yields_null_fields()
    {
        // A skipped terminal with no honest-skip detail still parses (non-null detail) so the caller can tell
        // "skipped terminal, incomplete telemetry" apart from "no skipped terminal at all".
        var detail = Find(SkipTerminal("2026-07-28T13:07:00.0000000Z", "skipped", null, null, null));
        Assert.NotNull(detail);
        Assert.Null(detail!.SourceAnchorOutcome);
        Assert.Null(detail.TargetAnchorOutcome);
        Assert.Null(detail.SkipReason);
    }

    [Fact]
    public void Non_skipped_terminals_are_ignored()
    {
        Assert.Null(Find(SkipTerminal("2026-07-28T13:07:00.0000000Z", "success", "skipped", "skipped", "r")));
        Assert.Null(Find(SkipTerminal("2026-07-28T13:07:00.0000000Z", "failed", "skipped", "skipped", "r")));
    }

    [Fact]
    public void Skip_terminals_before_the_window_are_ignored()
        => Assert.Null(Find(SkipTerminal(
            "2026-07-28T12:59:59.0000000Z", "skipped", "skipped", "skipped", "r")));

    [Fact]
    public void Latest_skipped_terminal_wins()
    {
        var detail = Find(
            SkipTerminal("2026-07-28T13:06:00.0000000Z", "skipped", "skipped", "skipped", "first"),
            SkipTerminal("2026-07-28T13:08:00.0000000Z", "skipped", "skipped", "skipped", "second"));
        Assert.Equal("second", detail!.SkipReason);
    }

    [Fact]
    public void A_skipped_step_run_end_is_not_a_trust_skip()
    {
        // A generic deploy.step.run.end with result=skipped must NOT satisfy the trust cleanup skip query.
        var stepEnd = "{\"ts\":\"2026-07-28T13:07:00.0000000Z\",\"level\":\"info\",\"event\":\"deploy.step.run.end\"," +
                      "\"operationId\":\"op1\",\"result\":\"skipped\"," +
                      "\"context\":{\"vmName\":\"LAT-dc\",\"stepKey\":\"v2.cleanupForestTrust\"}}";
        Assert.Null(Find(stepEnd));
    }

    [Fact]
    public void Malformed_lines_are_skipped_and_the_good_terminal_is_found()
    {
        var detail = Find(
            "this is not json",
            "",
            SkipTerminal("2026-07-28T13:07:00.0000000Z", "skipped", "skipped", "skipped", "ok"));
        Assert.NotNull(detail);
        Assert.Equal("ok", detail!.SkipReason);
    }

    [Fact]
    public void Absent_skipped_terminal_reads_as_null()
        => Assert.Null(Find(SkipTerminal("2026-07-28T13:07:00.0000000Z", "success", "success", "success", "r")));
}
