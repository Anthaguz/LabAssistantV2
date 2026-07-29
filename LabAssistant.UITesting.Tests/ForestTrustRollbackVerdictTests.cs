using LabAssistant.UITesting.Scenarios;
using Xunit;

namespace LabAssistant.UITesting.Tests;

using Verdict = TemplateDeployForestTrustRollbackScenario.RollbackVerdict;
using RunOutcome = TemplateDeployForestTrustRollbackScenario.RunOutcome;

/// <summary>
/// Behaviour tests for the pure rollback verdict (finding 79, reframed for the findings-86/87 moot-skip product
/// behaviour and keyed on the RUN-LEVEL orchestration terminal). The classifier decides PASS / gating-fail /
/// non-gating-inconclusive from the log-derived facts PLUS the host-side zero-orphans facts, with no live deploy.
/// The load-bearing invariants pinned here:
///   - PASS requires ALL of: a fully-created trust (create.end=success), the CLEAN run-level terminal
///     (deploy.orchestration.run.end=cancelled, NOT cancelled_with_residuals), an HONEST cleanup terminal
///     (skipped-as-moot OR success), AND zero orphans before the backstop (no tagged leftovers AND teardown
///     within budget). "cleanup ran" alone is never a pass; a leak, a blown teardown budget, a residual run
///     terminal, or an unconfirmed run terminal is never a pass.
///   - skipped-as-moot is a PASS terminal, exactly like success - it is the EXPECTED terminal when both DC
///     anchors are run-created (the #921 both-run-created case).
///   - The run terminal is the HEADLINE signal: result=cancelled_with_residuals is a GATING failure (findings
///     86/87 not fully closed); result=success means the deploy completed (cancel too late) = INCONCLUSIVE.
///   - A cleanup that reported residual (cleanup.end=failed) or hung (started, no terminal) is a GATING failure;
///     a runtime that left orphans or blew the teardown budget on the cancel path is the GATING finding-86/87
///     failure.
///   - Every remaining ambiguity is a non-gating INCONCLUSIVE (never a false pass, never a false fail): a
///     completed/ready trust, no confirmed create, an honest cleanup terminal without a confirmed create, a
///     silent cleanup stage that nonetheless left zero orphans, or a clean cleanup with zero orphans but an
///     unconfirmed run terminal (defence-in-depth on the emit path).
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
        bool teardownWithinBudget,
        RunOutcome runOutcome = RunOutcome.Cancelled)
        => TemplateDeployForestTrustRollbackScenario.ClassifyRollback(
            cleanupStarted, cleanupSucceeded, cleanupSkipped, cleanupResidual, createEnded, trustValidated,
            noTaggedLeftovers, teardownWithinBudget, runOutcome);

    [Fact]
    public void Skipped_as_moot_on_a_fully_created_trust_with_a_clean_cancel_and_zero_orphans_is_the_authoritative_pass()
        => Assert.Equal(
            Verdict.Pass,
            Classify(cleanupStarted: true, cleanupSucceeded: false, cleanupSkipped: true, cleanupResidual: false,
                createEnded: true, trustValidated: false, noTaggedLeftovers: true, teardownWithinBudget: true,
                runOutcome: RunOutcome.Cancelled));

    [Fact]
    public void Cleanup_success_on_a_fully_created_trust_with_a_clean_cancel_and_zero_orphans_is_also_a_pass()
        => Assert.Equal(
            Verdict.Pass,
            Classify(cleanupStarted: true, cleanupSucceeded: true, cleanupSkipped: false, cleanupResidual: false,
                createEnded: true, trustValidated: false, noTaggedLeftovers: true, teardownWithinBudget: true,
                runOutcome: RunOutcome.Cancelled));

    [Fact]
    public void A_cancelled_with_residuals_run_terminal_is_a_gating_failure_even_with_a_clean_cleanup_and_zero_orphans()
    {
        // The headline finding-86/87 signal: the run itself reported residuals remained. Gating regardless of a
        // clean cleanup terminal or a (racily) clean host-orphan read - never a false pass.
        Assert.Equal(
            Verdict.FailCancelledWithResiduals,
            Classify(cleanupStarted: true, cleanupSucceeded: false, cleanupSkipped: true, cleanupResidual: false,
                createEnded: true, trustValidated: false, noTaggedLeftovers: true, teardownWithinBudget: true,
                runOutcome: RunOutcome.CancelledWithResiduals));
        // Also gating when a cleanup success terminal co-occurs.
        Assert.Equal(
            Verdict.FailCancelledWithResiduals,
            Classify(cleanupStarted: true, cleanupSucceeded: true, cleanupSkipped: false, cleanupResidual: false,
                createEnded: true, trustValidated: false, noTaggedLeftovers: true, teardownWithinBudget: true,
                runOutcome: RunOutcome.CancelledWithResiduals));
    }

    [Fact]
    public void A_completed_run_terminal_is_the_ready_trust_inconclusive_cancel_landed_too_late()
    {
        // deploy.orchestration.run.end=success means the deploy finished, so nothing was rolled back; surviving
        // VMs are a successful deploy, not a leak. Re-run to land the cancel earlier.
        Assert.Equal(
            Verdict.InconclusiveReadyTrust,
            Classify(cleanupStarted: false, cleanupSucceeded: false, cleanupSkipped: false, cleanupResidual: false,
                createEnded: true, trustValidated: true, noTaggedLeftovers: false, teardownWithinBudget: false,
                runOutcome: RunOutcome.Completed));
        // The completed run terminal drives the verdict even if validate.end fell outside the window.
        Assert.Equal(
            Verdict.InconclusiveReadyTrust,
            Classify(cleanupStarted: false, cleanupSucceeded: false, cleanupSkipped: false, cleanupResidual: false,
                createEnded: true, trustValidated: false, noTaggedLeftovers: true, teardownWithinBudget: true,
                runOutcome: RunOutcome.Completed));
    }

    [Fact]
    public void A_clean_cleanup_with_zero_orphans_but_an_unconfirmed_run_terminal_is_inconclusive_not_pass()
        // create + honest terminal + zero orphans, but the run terminal was a failure variant or unreadable
        // (Unknown): the headline clean-cancel PASS criterion cannot be asserted, so re-run.
        => Assert.Equal(
            Verdict.InconclusiveRunOutcomeUnconfirmed,
            Classify(cleanupStarted: true, cleanupSucceeded: false, cleanupSkipped: true, cleanupResidual: false,
                createEnded: true, trustValidated: false, noTaggedLeftovers: true, teardownWithinBudget: true,
                runOutcome: RunOutcome.Unknown));

    [Fact]
    public void Honest_terminal_on_a_created_trust_but_leftover_orphans_is_the_gating_finding_86_87_failure()
        => Assert.Equal(
            Verdict.FailOrphansLeaked,
            Classify(cleanupStarted: true, cleanupSucceeded: false, cleanupSkipped: true, cleanupResidual: false,
                createEnded: true, trustValidated: false, noTaggedLeftovers: false, teardownWithinBudget: true,
                runOutcome: RunOutcome.Cancelled));

    [Fact]
    public void Honest_terminal_on_a_created_trust_but_teardown_over_budget_is_the_gating_finding_87_failure()
        => Assert.Equal(
            Verdict.FailOrphansLeaked,
            Classify(cleanupStarted: true, cleanupSucceeded: true, cleanupSkipped: false, cleanupResidual: false,
                createEnded: true, trustValidated: false, noTaggedLeftovers: true, teardownWithinBudget: false,
                runOutcome: RunOutcome.Cancelled));

    [Fact]
    public void Honest_terminal_without_a_confirmed_create_is_inconclusive_not_pass()
        => Assert.Equal(
            Verdict.InconclusiveCleanupWithoutCreateEnd,
            Classify(cleanupStarted: true, cleanupSucceeded: false, cleanupSkipped: true, cleanupResidual: false,
                createEnded: false, trustValidated: false, noTaggedLeftovers: true, teardownWithinBudget: true,
                runOutcome: RunOutcome.Cancelled));

    [Fact]
    public void Cleanup_residual_end_is_a_gating_failure_regardless_of_create_or_orphans_or_run_terminal()
    {
        Assert.Equal(
            Verdict.FailCleanupResidual,
            Classify(cleanupStarted: true, cleanupSucceeded: false, cleanupSkipped: false, cleanupResidual: true,
                createEnded: true, trustValidated: false, noTaggedLeftovers: true, teardownWithinBudget: true,
                runOutcome: RunOutcome.Cancelled));
        // A residual must never be masked by a co-occurring skipped flag.
        Assert.Equal(
            Verdict.FailCleanupResidual,
            Classify(cleanupStarted: true, cleanupSucceeded: false, cleanupSkipped: true, cleanupResidual: true,
                createEnded: true, trustValidated: false, noTaggedLeftovers: true, teardownWithinBudget: true,
                runOutcome: RunOutcome.Cancelled));
        // The specific cleanup-residual reason is surfaced even when the run terminal also reported residuals.
        Assert.Equal(
            Verdict.FailCleanupResidual,
            Classify(cleanupStarted: true, cleanupSucceeded: false, cleanupSkipped: false, cleanupResidual: true,
                createEnded: true, trustValidated: false, noTaggedLeftovers: true, teardownWithinBudget: true,
                runOutcome: RunOutcome.CancelledWithResiduals));
    }

    [Fact]
    public void Cleanup_started_without_any_terminal_is_a_gating_hung_failure()
        => Assert.Equal(
            Verdict.FailCleanupHung,
            Classify(cleanupStarted: true, cleanupSucceeded: false, cleanupSkipped: false, cleanupResidual: false,
                createEnded: true, trustValidated: false, noTaggedLeftovers: true, teardownWithinBudget: true,
                runOutcome: RunOutcome.Cancelled));

    [Fact]
    public void A_success_terminal_wins_over_a_stale_residual_flag_and_still_requires_create_clean_cancel_and_zero_orphans()
    {
        // Defensive: the live path sets exactly one terminal, but if both are seen a success terminal must win
        // (still gated on create.end AND a clean run-cancel AND zero orphans for the PASS claim).
        Assert.Equal(
            Verdict.Pass,
            Classify(cleanupStarted: true, cleanupSucceeded: true, cleanupSkipped: false, cleanupResidual: true,
                createEnded: true, trustValidated: false, noTaggedLeftovers: true, teardownWithinBudget: true,
                runOutcome: RunOutcome.Cancelled));
    }

    [Fact]
    public void A_full_create_cancelled_with_a_silent_cleanup_but_zero_orphans_is_inconclusive_not_fail()
        => Assert.Equal(
            Verdict.InconclusiveSilentButClean,
            Classify(cleanupStarted: false, cleanupSucceeded: false, cleanupSkipped: false, cleanupResidual: false,
                createEnded: true, trustValidated: false, noTaggedLeftovers: true, teardownWithinBudget: true,
                runOutcome: RunOutcome.Cancelled));

    [Fact]
    public void A_full_create_cancelled_with_a_silent_cleanup_AND_orphans_is_the_gating_leak_failure()
        => Assert.Equal(
            Verdict.FailOrphansLeaked,
            Classify(cleanupStarted: false, cleanupSucceeded: false, cleanupSkipped: false, cleanupResidual: false,
                createEnded: true, trustValidated: false, noTaggedLeftovers: false, teardownWithinBudget: true,
                runOutcome: RunOutcome.Cancelled));

    [Fact]
    public void A_validated_ready_trust_with_no_cleanup_is_inconclusive_even_if_vms_survive()
    {
        // A ready trust means the deploy finished, so surviving VMs are a successful deploy - not a leak; the
        // orphan facts do not gate this lane. validate.end=success is the conservative corroborator when the run
        // terminal was not itself the clean cancelled/completed signal (here Unknown).
        Assert.Equal(
            Verdict.InconclusiveReadyTrust,
            Classify(cleanupStarted: false, cleanupSucceeded: false, cleanupSkipped: false, cleanupResidual: false,
                createEnded: true, trustValidated: true, noTaggedLeftovers: false, teardownWithinBudget: false,
                runOutcome: RunOutcome.Unknown));
        // validate.end=success dominates even if create.end happened to fall outside the window.
        Assert.Equal(
            Verdict.InconclusiveReadyTrust,
            Classify(cleanupStarted: false, cleanupSucceeded: false, cleanupSkipped: false, cleanupResidual: false,
                createEnded: false, trustValidated: true, noTaggedLeftovers: true, teardownWithinBudget: true,
                runOutcome: RunOutcome.Unknown));
    }

    [Fact]
    public void No_create_and_no_cleanup_is_the_too_early_inconclusive()
        => Assert.Equal(
            Verdict.InconclusiveNoCreate,
            Classify(cleanupStarted: false, cleanupSucceeded: false, cleanupSkipped: false, cleanupResidual: false,
                createEnded: false, trustValidated: false, noTaggedLeftovers: true, teardownWithinBudget: true,
                runOutcome: RunOutcome.Cancelled));

    [Fact]
    public void A_cancelled_with_residuals_run_terminal_is_ALWAYS_gating_never_inconclusive_or_pass()
    {
        // The Coder subtlety: cancelled_with_residuals must never be masked as a benign INCONCLUSIVE re-run
        // (that would hide a real orphan-leak regression). Across every log/host fact combination, a residual
        // run terminal must resolve to a gating Fail* verdict - never Pass, never any Inconclusive*.
        var gating = new[]
        {
            Verdict.FailCancelledWithResiduals,
            Verdict.FailCleanupResidual,
            Verdict.FailCleanupHung,
            Verdict.FailOrphansLeaked
        };

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
                trustValidated, noTaggedLeftovers, teardownWithinBudget, RunOutcome.CancelledWithResiduals);

            Assert.Contains(verdict, gating);
        }
    }

    [Fact]
    public void No_inputs_ever_produce_a_pass_without_create_clean_cancel_an_honest_terminal_and_zero_orphans()
    {
        // Exhaustive guard over all 256 log/host fact combinations x every run outcome (1024 total): PASS is
        // reachable ONLY when create.end=success AND a cleanup start with an honest terminal (success or skipped)
        // that is NOT a live residual AND zero orphans (no tagged leftovers AND teardown within budget) AND the
        // run terminal is the clean cancelled signal. This is the core no-false-pass invariant.
        foreach (RunOutcome runOutcome in Enum.GetValues<RunOutcome>())
        {
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
                    trustValidated, noTaggedLeftovers, teardownWithinBudget, runOutcome);
                if (verdict == Verdict.Pass)
                {
                    bool honestTerminal = (cleanupSucceeded || cleanupSkipped) && !(cleanupResidual && !cleanupSucceeded);
                    Assert.True(
                        cleanupStarted && honestTerminal && createEnded && noTaggedLeftovers && teardownWithinBudget
                            && runOutcome == RunOutcome.Cancelled,
                        $"PASS returned for mask {mask} runOutcome {runOutcome} without create.end + clean-cancel + honest " +
                        $"terminal + zero orphans (started={cleanupStarted}, succeeded={cleanupSucceeded}, skipped={cleanupSkipped}, " +
                        $"residual={cleanupResidual}, createEnded={createEnded}, noTaggedLeftovers={noTaggedLeftovers}, " +
                        $"teardownWithinBudget={teardownWithinBudget}).");
                }
            }
        }
    }

    [Fact]
    public void Any_leak_blown_budget_or_residual_run_terminal_on_the_cancel_path_never_produces_a_pass()
    {
        // Complementary to the PASS guard: whenever there ARE orphans (a tagged leftover or a blown teardown
        // budget) OR the run terminal reported residuals, the verdict must never be PASS, across every log-fact
        // combination and every run outcome.
        foreach (RunOutcome runOutcome in Enum.GetValues<RunOutcome>())
        {
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
                        trustValidated, noTaggedLeftovers: false, teardownWithinBudget: true, runOutcome: runOutcome));
                // teardownWithinBudget=false (finding-87 timing regression) with no other leftovers.
                Assert.NotEqual(
                    Verdict.Pass,
                    Classify(cleanupStarted, cleanupSucceeded, cleanupSkipped, cleanupResidual, createEnded,
                        trustValidated, noTaggedLeftovers: true, teardownWithinBudget: false, runOutcome: runOutcome));
                // A residual run terminal must never pass even with an otherwise-clean host + cleanup.
                Assert.NotEqual(
                    Verdict.Pass,
                    Classify(cleanupStarted, cleanupSucceeded, cleanupSkipped, cleanupResidual, createEnded,
                        trustValidated, noTaggedLeftovers: true, teardownWithinBudget: true,
                        runOutcome: RunOutcome.CancelledWithResiduals));
            }
        }
    }
}
