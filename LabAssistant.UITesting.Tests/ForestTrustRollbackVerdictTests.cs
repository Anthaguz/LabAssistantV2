using LabAssistant.UITesting.Scenarios;
using Xunit;

namespace LabAssistant.UITesting.Tests;

using Verdict = TemplateDeployForestTrustRollbackScenario.RollbackVerdict;

/// <summary>
/// Behaviour tests for the pure rollback verdict (finding 79, reframed for the findings-86/87 moot-skip product
/// behaviour). The classifier decides PASS / gating-fail / non-gating-inconclusive from the log-derived facts
/// PLUS the host-side zero-orphans facts, with no live deploy. The load-bearing invariants pinned here:
///   - PASS requires ALL of: a fully-created trust (create.end=success), an HONEST cleanup terminal
///     (skipped-as-moot OR success), AND zero orphans before the backstop (no tagged leftovers AND teardown
///     within budget). "cleanup ran" alone is never a pass; a leak or a blown teardown budget is never a pass.
///   - skipped-as-moot is a PASS terminal, exactly like success - it is the EXPECTED terminal when both DC
///     anchors are run-created (the #921 both-run-created case).
///   - A cleanup that reported residual (cleanup.end=failed) or hung (started, no terminal) is a GATING failure;
///     a runtime that left orphans or blew the teardown budget on the cancel path is the GATING finding-86/87
///     failure.
///   - Every remaining ambiguity is a non-gating INCONCLUSIVE (never a false pass, never a false fail): a READY
///     trust, no confirmed create, an honest cleanup terminal without a confirmed create, or a silent cleanup
///     stage that nonetheless left zero orphans (defence-in-depth on the emit path).
/// </summary>
public sealed class ForestTrustRollbackVerdictTests
{
    private static Verdict Classify(
        bool cleanupStarted,
        bool cleanupSucceeded,
        bool cleanupSkipped,
        bool cleanupResidual,
        bool createEnded,
        bool trustValidated,
        bool noTaggedLeftovers,
        bool teardownWithinBudget)
        => TemplateDeployForestTrustRollbackScenario.ClassifyRollback(
            cleanupStarted, cleanupSucceeded, cleanupSkipped, cleanupResidual, createEnded, trustValidated,
            noTaggedLeftovers, teardownWithinBudget);

    [Fact]
    public void Skipped_as_moot_on_a_fully_created_trust_with_zero_orphans_is_the_authoritative_pass()
        => Assert.Equal(
            Verdict.Pass,
            Classify(cleanupStarted: true, cleanupSucceeded: false, cleanupSkipped: true, cleanupResidual: false,
                createEnded: true, trustValidated: false, noTaggedLeftovers: true, teardownWithinBudget: true));

    [Fact]
    public void Cleanup_success_on_a_fully_created_trust_with_zero_orphans_is_also_a_pass()
        => Assert.Equal(
            Verdict.Pass,
            Classify(cleanupStarted: true, cleanupSucceeded: true, cleanupSkipped: false, cleanupResidual: false,
                createEnded: true, trustValidated: false, noTaggedLeftovers: true, teardownWithinBudget: true));

    [Fact]
    public void Honest_terminal_on_a_created_trust_but_leftover_orphans_is_the_gating_finding_86_87_failure()
        => Assert.Equal(
            Verdict.FailOrphansLeaked,
            Classify(cleanupStarted: true, cleanupSucceeded: false, cleanupSkipped: true, cleanupResidual: false,
                createEnded: true, trustValidated: false, noTaggedLeftovers: false, teardownWithinBudget: true));

    [Fact]
    public void Honest_terminal_on_a_created_trust_but_teardown_over_budget_is_the_gating_finding_87_failure()
        => Assert.Equal(
            Verdict.FailOrphansLeaked,
            Classify(cleanupStarted: true, cleanupSucceeded: true, cleanupSkipped: false, cleanupResidual: false,
                createEnded: true, trustValidated: false, noTaggedLeftovers: true, teardownWithinBudget: false));

    [Fact]
    public void Honest_terminal_without_a_confirmed_create_is_inconclusive_not_pass()
        => Assert.Equal(
            Verdict.InconclusiveCleanupWithoutCreateEnd,
            Classify(cleanupStarted: true, cleanupSucceeded: false, cleanupSkipped: true, cleanupResidual: false,
                createEnded: false, trustValidated: false, noTaggedLeftovers: true, teardownWithinBudget: true));

    [Fact]
    public void Cleanup_residual_end_is_a_gating_failure_regardless_of_create_or_orphans()
    {
        Assert.Equal(
            Verdict.FailCleanupResidual,
            Classify(cleanupStarted: true, cleanupSucceeded: false, cleanupSkipped: false, cleanupResidual: true,
                createEnded: true, trustValidated: false, noTaggedLeftovers: true, teardownWithinBudget: true));
        // A residual must never be masked by a co-occurring skipped flag.
        Assert.Equal(
            Verdict.FailCleanupResidual,
            Classify(cleanupStarted: true, cleanupSucceeded: false, cleanupSkipped: true, cleanupResidual: true,
                createEnded: true, trustValidated: false, noTaggedLeftovers: true, teardownWithinBudget: true));
    }

    [Fact]
    public void Cleanup_started_without_any_terminal_is_a_gating_hung_failure()
        => Assert.Equal(
            Verdict.FailCleanupHung,
            Classify(cleanupStarted: true, cleanupSucceeded: false, cleanupSkipped: false, cleanupResidual: false,
                createEnded: true, trustValidated: false, noTaggedLeftovers: true, teardownWithinBudget: true));

    [Fact]
    public void A_success_terminal_wins_over_a_stale_residual_flag_and_still_requires_create_and_zero_orphans()
    {
        // Defensive: the live path sets exactly one terminal, but if both are seen a success terminal must win
        // (still gated on create.end AND zero orphans for the PASS claim).
        Assert.Equal(
            Verdict.Pass,
            Classify(cleanupStarted: true, cleanupSucceeded: true, cleanupSkipped: false, cleanupResidual: true,
                createEnded: true, trustValidated: false, noTaggedLeftovers: true, teardownWithinBudget: true));
    }

    [Fact]
    public void A_full_create_cancelled_with_a_silent_cleanup_but_zero_orphans_is_inconclusive_not_fail()
        => Assert.Equal(
            Verdict.InconclusiveSilentButClean,
            Classify(cleanupStarted: false, cleanupSucceeded: false, cleanupSkipped: false, cleanupResidual: false,
                createEnded: true, trustValidated: false, noTaggedLeftovers: true, teardownWithinBudget: true));

    [Fact]
    public void A_full_create_cancelled_with_a_silent_cleanup_AND_orphans_is_the_gating_leak_failure()
        => Assert.Equal(
            Verdict.FailOrphansLeaked,
            Classify(cleanupStarted: false, cleanupSucceeded: false, cleanupSkipped: false, cleanupResidual: false,
                createEnded: true, trustValidated: false, noTaggedLeftovers: false, teardownWithinBudget: true));

    [Fact]
    public void A_validated_ready_trust_with_no_cleanup_is_inconclusive_even_if_vms_survive()
    {
        // A ready trust means the deploy finished, so surviving VMs are a successful deploy - not a leak; the
        // orphan facts do not gate this lane.
        Assert.Equal(
            Verdict.InconclusiveReadyTrust,
            Classify(cleanupStarted: false, cleanupSucceeded: false, cleanupSkipped: false, cleanupResidual: false,
                createEnded: true, trustValidated: true, noTaggedLeftovers: false, teardownWithinBudget: false));
        // validate.end=success dominates even if create.end happened to fall outside the window.
        Assert.Equal(
            Verdict.InconclusiveReadyTrust,
            Classify(cleanupStarted: false, cleanupSucceeded: false, cleanupSkipped: false, cleanupResidual: false,
                createEnded: false, trustValidated: true, noTaggedLeftovers: true, teardownWithinBudget: true));
    }

    [Fact]
    public void No_create_and_no_cleanup_is_the_too_early_inconclusive()
        => Assert.Equal(
            Verdict.InconclusiveNoCreate,
            Classify(cleanupStarted: false, cleanupSucceeded: false, cleanupSkipped: false, cleanupResidual: false,
                createEnded: false, trustValidated: false, noTaggedLeftovers: true, teardownWithinBudget: true));

    [Fact]
    public void No_inputs_ever_produce_a_pass_without_create_end_an_honest_terminal_and_zero_orphans()
    {
        // Exhaustive guard over all 256 fact combinations: PASS is reachable ONLY when create.end=success AND a
        // cleanup start with an honest terminal (success or skipped) AND zero orphans (no tagged leftovers AND
        // teardown within budget) were all observed. This is the core no-false-pass invariant.
        for (int mask = 0; mask < 256; mask++)
        {
            bool cleanupStarted = (mask & 1) != 0;
            bool cleanupSucceeded = (mask & 2) != 0;
            bool cleanupSkipped = (mask & 4) != 0;
            bool cleanupResidual = (mask & 8) != 0;
            bool createEnded = (mask & 16) != 0;
            bool trustValidated = (mask & 32) != 0;
            bool noTaggedLeftovers = (mask & 64) != 0;
            bool teardownWithinBudget = (mask & 128) != 0;

            var verdict = Classify(cleanupStarted, cleanupSucceeded, cleanupSkipped, cleanupResidual, createEnded,
                trustValidated, noTaggedLeftovers, teardownWithinBudget);
            if (verdict == Verdict.Pass)
            {
                Assert.True(
                    cleanupStarted && (cleanupSucceeded || cleanupSkipped) && createEnded && noTaggedLeftovers && teardownWithinBudget,
                    $"PASS returned for mask {mask} without create.end + honest terminal + zero orphans (started={cleanupStarted}, " +
                    $"succeeded={cleanupSucceeded}, skipped={cleanupSkipped}, createEnded={createEnded}, " +
                    $"noTaggedLeftovers={noTaggedLeftovers}, teardownWithinBudget={teardownWithinBudget}).");
            }
        }
    }

    [Fact]
    public void Any_leak_or_blown_budget_on_the_cancel_path_never_produces_a_pass()
    {
        // Complementary to the PASS guard: whenever there ARE orphans (a tagged leftover or a blown teardown
        // budget), the verdict must never be PASS, across every log-fact combination.
        for (int mask = 0; mask < 64; mask++)
        {
            bool cleanupStarted = (mask & 1) != 0;
            bool cleanupSucceeded = (mask & 2) != 0;
            bool cleanupSkipped = (mask & 4) != 0;
            bool cleanupResidual = (mask & 8) != 0;
            bool createEnded = (mask & 16) != 0;
            bool trustValidated = (mask & 32) != 0;

            // noTaggedLeftovers=false (a real leak) with teardown within budget.
            Assert.NotEqual(
                Verdict.Pass,
                Classify(cleanupStarted, cleanupSucceeded, cleanupSkipped, cleanupResidual, createEnded,
                    trustValidated, noTaggedLeftovers: false, teardownWithinBudget: true));
            // teardownWithinBudget=false (finding-87 timing regression) with no other leftovers.
            Assert.NotEqual(
                Verdict.Pass,
                Classify(cleanupStarted, cleanupSucceeded, cleanupSkipped, cleanupResidual, createEnded,
                    trustValidated, noTaggedLeftovers: true, teardownWithinBudget: false));
        }
    }
}
