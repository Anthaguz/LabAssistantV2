using LabAssistant.UITesting.Scenarios;
using Xunit;

namespace LabAssistant.UITesting.Tests;

using Verdict = TemplateDeployForestTrustRollbackScenario.RollbackVerdict;

/// <summary>
/// Behaviour tests for the pure rollback verdict (finding 79, retimed for the cancel-during-validate design).
/// The classifier decides PASS / gating-fail / non-gating-inconclusive from five log-derived facts, with no
/// live deploy. The load-bearing invariants pinned here:
///   - PASS requires BOTH a fully-created trust (create.end=success) AND a cleanup wrap that ran to a success
///     terminal - "cleanup ran" alone is never a pass (removes the old false-INCONCLUSIVE and, more importantly,
///     any false-PASS on a no-op/empty cleanup).
///   - A cleanup that ran but reported residual, or hung without a terminal, or never ran at all on a real
///     created trust, is a GATING failure.
///   - Every remaining ambiguity is a non-gating INCONCLUSIVE (never a false pass, never a false fail): the
///     deploy already reached a READY trust, or no full create was observed, or cleanup succeeded without a
///     confirmed create.
/// </summary>
public sealed class ForestTrustRollbackVerdictTests
{
    private static Verdict Classify(
        bool cleanupStarted,
        bool cleanupSucceeded,
        bool cleanupResidual,
        bool createEnded,
        bool trustValidated)
        => TemplateDeployForestTrustRollbackScenario.ClassifyRollback(
            cleanupStarted, cleanupSucceeded, cleanupResidual, createEnded, trustValidated);

    [Fact]
    public void Cleanup_success_on_a_fully_created_trust_is_the_authoritative_pass()
        => Assert.Equal(
            Verdict.Pass,
            Classify(cleanupStarted: true, cleanupSucceeded: true, cleanupResidual: false, createEnded: true, trustValidated: false));

    [Fact]
    public void Cleanup_success_without_a_confirmed_create_is_inconclusive_not_pass()
        => Assert.Equal(
            Verdict.InconclusiveCleanupWithoutCreateEnd,
            Classify(cleanupStarted: true, cleanupSucceeded: true, cleanupResidual: false, createEnded: false, trustValidated: false));

    [Fact]
    public void Cleanup_residual_end_is_a_gating_failure_regardless_of_create()
    {
        Assert.Equal(
            Verdict.FailCleanupResidual,
            Classify(cleanupStarted: true, cleanupSucceeded: false, cleanupResidual: true, createEnded: true, trustValidated: false));
        Assert.Equal(
            Verdict.FailCleanupResidual,
            Classify(cleanupStarted: true, cleanupSucceeded: false, cleanupResidual: true, createEnded: false, trustValidated: false));
    }

    [Fact]
    public void Cleanup_started_without_a_terminal_end_is_a_gating_hung_failure()
        => Assert.Equal(
            Verdict.FailCleanupHung,
            Classify(cleanupStarted: true, cleanupSucceeded: false, cleanupResidual: false, createEnded: true, trustValidated: false));

    [Fact]
    public void A_success_terminal_wins_over_a_stale_residual_flag_and_still_requires_create()
    {
        // Defensive: the live path sets exactly one terminal, but if both are seen a success terminal must win
        // (still gated on create.end for the PASS claim).
        Assert.Equal(
            Verdict.Pass,
            Classify(cleanupStarted: true, cleanupSucceeded: true, cleanupResidual: true, createEnded: true, trustValidated: false));
    }

    [Fact]
    public void A_full_create_that_was_cancelled_with_no_cleanup_is_the_gating_no_wrap_finding()
        => Assert.Equal(
            Verdict.FailCleanupDidNotRun,
            Classify(cleanupStarted: false, cleanupSucceeded: false, cleanupResidual: false, createEnded: true, trustValidated: false));

    [Fact]
    public void A_validated_ready_trust_with_no_cleanup_is_inconclusive()
    {
        Assert.Equal(
            Verdict.InconclusiveReadyTrust,
            Classify(cleanupStarted: false, cleanupSucceeded: false, cleanupResidual: false, createEnded: true, trustValidated: true));
        // validate.end=success dominates: a ready trust means the deploy finished, not a missing cleanup wrap,
        // even if create.end happened to fall outside the window.
        Assert.Equal(
            Verdict.InconclusiveReadyTrust,
            Classify(cleanupStarted: false, cleanupSucceeded: false, cleanupResidual: false, createEnded: false, trustValidated: true));
    }

    [Fact]
    public void No_create_and_no_cleanup_is_the_too_early_inconclusive()
        => Assert.Equal(
            Verdict.InconclusiveNoCreate,
            Classify(cleanupStarted: false, cleanupSucceeded: false, cleanupResidual: false, createEnded: false, trustValidated: false));

    [Fact]
    public void No_inputs_ever_produce_a_pass_without_both_create_end_and_cleanup_success()
    {
        // Exhaustive guard over all 32 fact combinations: PASS is reachable ONLY when create.end=success AND a
        // cleanup success terminal were both observed. This is the core no-false-pass invariant.
        for (int mask = 0; mask < 32; mask++)
        {
            bool cleanupStarted = (mask & 1) != 0;
            bool cleanupSucceeded = (mask & 2) != 0;
            bool cleanupResidual = (mask & 4) != 0;
            bool createEnded = (mask & 8) != 0;
            bool trustValidated = (mask & 16) != 0;

            var verdict = Classify(cleanupStarted, cleanupSucceeded, cleanupResidual, createEnded, trustValidated);
            if (verdict == Verdict.Pass)
            {
                Assert.True(cleanupStarted && cleanupSucceeded && createEnded,
                    $"PASS returned for mask {mask} without create.end + cleanup-success (started={cleanupStarted}, " +
                    $"succeeded={cleanupSucceeded}, createEnded={createEnded}).");
            }
        }
    }
}
