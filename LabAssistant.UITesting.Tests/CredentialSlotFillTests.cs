using LabAssistant.UITesting.Infrastructure;
using Xunit;

namespace LabAssistant.UITesting.Tests;

/// <summary>
/// Covers <see cref="CredentialSlotFill.FillSlotWithRetry"/> - the persistence-verified,
/// focus-asserted retry loop behind the credential-slot fill automation.
///
/// Regression context: the fill entered the password as raw keystrokes into a WinUI PasswordBox
/// (no Value pattern). A focus miss typed an empty password, the Save no-oped, nothing persisted,
/// and the old loop still counted the slot "resolved" and spun to a false success - wedging live
/// deploys because the plan never became startable. These tests pin the fixed contract: only type
/// once focus lands, only count a Save once persistence is verified on a bounded poll, retry on
/// miss, and report focus-ever-landed so the caller can tell a harness fill-miss from a product
/// Save failure. The clock is fake and advanced by the poll's own sleep, so the tests are
/// deterministic and instant.
/// </summary>
public sealed class CredentialSlotFillTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);
    private const int MaxAttempts = 4;

    /// <summary>A monotonic fake clock advanced only by the poll's injected sleep.</summary>
    private sealed class FakeClock
    {
        private DateTime _now = new(2026, 7, 29, 0, 0, 0, DateTimeKind.Utc);
        public int SleepCount { get; private set; }
        public DateTime Now() => _now;
        public void Sleep(TimeSpan d)
        {
            _now += d;
            SleepCount++;
        }
    }

    [Fact]
    public void Persists_OnFirstAttempt_WhenFocusLandsAndSaveTakes()
    {
        var clock = new FakeClock();
        int typeCount = 0, saveCount = 0;

        var outcome = CredentialSlotFill.FillSlotWithRetry(
            attemptFocus: () => true,
            typePassword: () => typeCount++,
            save: () => saveCount++,
            hasPersisted: () => true,
            maxAttempts: MaxAttempts,
            persistBudget: Budget,
            pollInterval: Interval,
            utcNow: clock.Now,
            sleep: clock.Sleep);

        Assert.True(outcome.Persisted);
        Assert.Equal(1, outcome.Attempts);
        Assert.True(outcome.FocusEverLanded);
        Assert.Equal(1, typeCount);
        Assert.Equal(1, saveCount);
        Assert.Equal(0, clock.SleepCount); // recognized on the first poll, no waiting
    }

    [Fact]
    public void NeverTypesOrSaves_WhenFocusNeverLands()
    {
        var clock = new FakeClock();
        int typeCount = 0, saveCount = 0;

        var outcome = CredentialSlotFill.FillSlotWithRetry(
            attemptFocus: () => false,
            typePassword: () => typeCount++,
            save: () => saveCount++,
            hasPersisted: () => true,
            maxAttempts: MaxAttempts,
            persistBudget: Budget,
            pollInterval: Interval,
            utcNow: clock.Now,
            sleep: clock.Sleep);

        Assert.False(outcome.Persisted);
        Assert.Equal(MaxAttempts, outcome.Attempts);
        Assert.False(outcome.FocusEverLanded); // the harness-fill-miss signal
        Assert.Equal(0, typeCount);            // never type into the void
        Assert.Equal(0, saveCount);
    }

    [Fact]
    public void Retries_ThenPersists_WhenFocusLandsOnLaterAttempt()
    {
        var clock = new FakeClock();
        int focusCalls = 0, typeCount = 0;

        var outcome = CredentialSlotFill.FillSlotWithRetry(
            attemptFocus: () => ++focusCalls >= 3, // miss on attempts 1 and 2, land on 3
            typePassword: () => typeCount++,
            save: () => { },
            hasPersisted: () => true,
            maxAttempts: MaxAttempts,
            persistBudget: Budget,
            pollInterval: Interval,
            utcNow: clock.Now,
            sleep: clock.Sleep);

        Assert.True(outcome.Persisted);
        Assert.Equal(3, outcome.Attempts);
        Assert.True(outcome.FocusEverLanded);
        Assert.Equal(1, typeCount); // only the landing attempt types
    }

    [Fact]
    public void Fails_Loud_WhenFocusLandsButSaveNeverPersists()
    {
        var clock = new FakeClock();
        int saveCount = 0;

        var outcome = CredentialSlotFill.FillSlotWithRetry(
            attemptFocus: () => true,
            typePassword: () => { },
            save: () => saveCount++,
            hasPersisted: () => false, // Save fires but nothing ever persists
            maxAttempts: MaxAttempts,
            persistBudget: Budget,
            pollInterval: Interval,
            utcNow: clock.Now,
            sleep: clock.Sleep);

        Assert.False(outcome.Persisted);
        Assert.Equal(MaxAttempts, outcome.Attempts);
        Assert.True(outcome.FocusEverLanded); // focus landed + typed => points at a product Save failure
        Assert.Equal(MaxAttempts, saveCount); // one Save per attempt, all retried
    }

    [Fact]
    public void Persists_OnLatePoll_WithinBudget()
    {
        var clock = new FakeClock();
        int checks = 0;

        var outcome = CredentialSlotFill.FillSlotWithRetry(
            attemptFocus: () => true,
            typePassword: () => { },
            save: () => { },
            hasPersisted: () => ++checks >= 3, // false, false, true (2 sleeps in between)
            maxAttempts: MaxAttempts,
            persistBudget: Budget,
            pollInterval: Interval,
            utcNow: clock.Now,
            sleep: clock.Sleep);

        Assert.True(outcome.Persisted);
        Assert.Equal(1, outcome.Attempts);
        Assert.Equal(2, clock.SleepCount); // polled twice before it took, never a fixed sleep
    }

    [Fact]
    public void Retries_NextAttempt_WhenFirstAttemptBudgetExpires()
    {
        var clock = new FakeClock();
        int saveCount = 0;

        var outcome = CredentialSlotFill.FillSlotWithRetry(
            attemptFocus: () => true,
            typePassword: () => { },
            save: () => saveCount++,
            hasPersisted: () => saveCount >= 2, // attempt 1 never persists; attempt 2 persists at once
            maxAttempts: MaxAttempts,
            persistBudget: Budget,
            pollInterval: Interval,
            utcNow: clock.Now,
            sleep: clock.Sleep);

        Assert.True(outcome.Persisted);
        Assert.Equal(2, outcome.Attempts);
        Assert.Equal(2, saveCount);
    }
}
