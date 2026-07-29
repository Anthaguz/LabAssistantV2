using LabAssistant.UITesting.Scenarios;
using Xunit;

namespace LabAssistant.UITesting.Tests;

using RunOutcome = TemplateDeployForestTrustRollbackScenario.RunOutcome;

/// <summary>
/// Behaviour tests for the run-level terminal poll (finding 89b). On a cancel, the
/// <c>deploy.orchestration.run.end</c> orchestration terminal is emitted only AFTER all three cleanup stages
/// complete (trust cleanup.end -> VM teardown -> switch cleanup), so it lands LATER than the cleanup.end the
/// scenario already waited on. The old single, non-polling read sampled before it fired and mapped to
/// <see cref="RunOutcome.Unknown"/> -> a false InconclusiveRunOutcomeUnconfirmed on an otherwise-clean cancel.
/// <see cref="TemplateDeployForestTrustRollbackScenario.PollRunOutcome"/> polls while the terminal is still ABSENT
/// (raw read null), mapping and returning the instant any terminal string is present - so a cancel that lands
/// after several empty reads is picked up, and a present-but-unrecognized terminal resolves immediately instead of
/// stalling the whole budget. Only a genuinely absent terminal at budget expiry returns Unknown. The poll takes
/// injected clock/sleep so these run with no real waiting.
/// </summary>
public sealed class ForestTrustRollbackRunTerminalPollTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    // Polls a scripted sequence of raw run.end result strings (null = terminal not present yet), one per
    // iteration. The fake clock is advanced ONLY by the poll's own sleep, so the loop terminates deterministically
    // without real time. Returns the outcome plus the sleep count so tests can assert a present terminal
    // short-circuits without stalling the budget.
    private static (RunOutcome Outcome, int Sleeps) Poll(IReadOnlyList<string?> reads)
    {
        DateTime now = new(2026, 7, 29, 6, 0, 0, DateTimeKind.Utc);
        int index = 0;
        int sleeps = 0;
        RunOutcome outcome = TemplateDeployForestTrustRollbackScenario.PollRunOutcome(
            () => index < reads.Count ? reads[index++] : null,
            Budget,
            Interval,
            () => now,
            ts => { sleeps++; now += ts; });
        return (outcome, sleeps);
    }

    [Fact]
    public void Returns_the_terminal_immediately_when_present_on_first_read()
    {
        var (outcome, sleeps) = Poll(new[] { "cancelled" });
        Assert.Equal(RunOutcome.Cancelled, outcome);
        Assert.Equal(0, sleeps);
    }

    [Fact]
    public void Picks_up_a_cancel_that_lands_after_several_empty_reads()
    {
        // The exact finding-89b timing: the run terminal is absent for a few polls after cleanup.end, then fires.
        // The poll must not give up on the early nulls - it must keep polling and return Cancelled once it lands.
        var reads = new string?[] { null, null, null, null, null, null, "cancelled" };

        var (outcome, sleeps) = Poll(reads);

        Assert.Equal(RunOutcome.Cancelled, outcome);
        Assert.Equal(6, sleeps);
    }

    [Fact]
    public void Never_appearing_within_budget_stays_unknown()
    {
        // Genuine silence: the terminal never appears. The poll must exhaust the budget and return Unknown (the
        // honest InconclusiveRunOutcomeUnconfirmed lane), not hang and not fabricate an outcome.
        var (outcome, sleeps) = Poll(System.Array.Empty<string?>());

        Assert.Equal(RunOutcome.Unknown, outcome);
        Assert.Equal(150, sleeps); // 5 min / 2 s - proves it actually waited the full budget.
    }

    [Fact]
    public void A_residual_cancel_that_lands_late_is_still_reported_not_masked_as_unknown()
    {
        // The poll must not privilege the clean-cancel result: a late cancelled_with_residuals (the gating leak
        // signal) is a present terminal and must be returned so ClassifyRollback can gate-fail on it, never fall
        // through to a benign "unconfirmed" re-run.
        var reads = new string?[] { null, null, "cancelled_with_residuals" };

        Assert.Equal(RunOutcome.CancelledWithResiduals, Poll(reads).Outcome);
    }

    [Fact]
    public void A_late_completed_terminal_is_returned_for_the_too_late_inconclusive_lane()
    {
        var reads = new string?[] { null, "success" };
        Assert.Equal(RunOutcome.Completed, Poll(reads).Outcome);
    }

    [Fact]
    public void A_present_but_unrecognized_terminal_resolves_immediately_without_stalling_the_budget()
    {
        // A present-but-failure/unrecognized run.end variant must NOT be conflated with "not yet present": it maps
        // to Unknown and returns on the first read, rather than spinning for the whole 5-min budget (the reviewer's
        // finding-89b latency-regression guard). Verdict is still Unknown -> unconfirmed, but resolved instantly.
        var (outcome, sleeps) = Poll(new[] { "failed" });

        Assert.Equal(RunOutcome.Unknown, outcome);
        Assert.Equal(0, sleeps);
    }
}
