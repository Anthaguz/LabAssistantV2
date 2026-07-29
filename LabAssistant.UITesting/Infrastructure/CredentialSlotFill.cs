namespace LabAssistant.UITesting.Infrastructure;

/// <summary>
/// Result of driving one credential slot through fill + Save with bounded retries.
/// <see cref="Persisted"/> is the authoritative "the app accepted this credential" signal (the
/// slot left the unresolved list). <see cref="FocusEverLanded"/> records whether keyboard focus
/// ever landed on the password box across the attempts - the discriminator between a harness
/// fill-miss (focus never landed, so an empty password was never typed into the box) and a
/// product persist failure (focus landed, a non-empty password was typed, Save still did not take).
/// </summary>
public sealed record CredentialFillOutcome(bool Persisted, int Attempts, bool FocusEverLanded);

/// <summary>
/// Pure retry/verify orchestration for the credential-slot fill automation, factored out of the
/// FlaUI page object so it can be unit-tested without a live app.
///
/// The WinUI PasswordBox exposes no Value pattern, so the password is entered as real keystrokes
/// after focusing the box. If focus misses, the keystrokes land nowhere, the password stays empty,
/// the Save no-ops, and nothing persists - the exact latent flake that wedged live deploys. This
/// helper closes that hole: each attempt only types once focus is confirmed, and a Save is only
/// counted after persistence is verified against a caller-supplied predicate (the slot leaving the
/// unresolved list / the store gaining the record) polled on a bounded budget - never a fixed sleep.
/// It retries focus+type+Save up to <paramref name="maxAttempts"/> and reports the outcome so the
/// caller can fail loudly instead of masking a genuine product bug.
/// </summary>
public static class CredentialSlotFill
{
    /// <summary>Attempts to focus, type, Save and verify persistence for one slot, retrying on miss.</summary>
    /// <param name="attemptFocus">Select the row and focus the password box; returns true when keyboard focus landed.</param>
    /// <param name="typePassword">Type the password into the (now focused) password box.</param>
    /// <param name="save">Activate the Save button (a no-op if it is disabled).</param>
    /// <param name="hasPersisted">Instantaneous check that the Save took (the slot left the unresolved list).</param>
    /// <param name="maxAttempts">Maximum focus+type+Save attempts before giving up.</param>
    /// <param name="persistBudget">How long to poll for persistence after each Save.</param>
    /// <param name="pollInterval">Gap between persistence polls.</param>
    /// <param name="utcNow">Clock accessor (injected for tests).</param>
    /// <param name="sleep">Sleep primitive (injected for tests).</param>
    public static CredentialFillOutcome FillSlotWithRetry(
        Func<bool> attemptFocus,
        Action typePassword,
        Action save,
        Func<bool> hasPersisted,
        int maxAttempts,
        TimeSpan persistBudget,
        TimeSpan pollInterval,
        Func<DateTime> utcNow,
        Action<TimeSpan> sleep)
    {
        bool focusEverLanded = false;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (!attemptFocus())
            {
                // Focus never landed: typing now would type into the void. Retry the whole attempt
                // rather than Save an empty password.
                continue;
            }

            focusEverLanded = true;
            typePassword();
            save();

            // Poll for persistence on a bounded budget. Read first so a Save that already took is
            // recognized immediately; only genuine budget-expiry-with-no-persist falls through to a retry.
            var deadline = utcNow() + persistBudget;
            while (true)
            {
                if (hasPersisted())
                {
                    return new CredentialFillOutcome(Persisted: true, Attempts: attempt, FocusEverLanded: true);
                }

                if (utcNow() >= deadline)
                {
                    break;
                }

                sleep(pollInterval);
            }
        }

        return new CredentialFillOutcome(Persisted: false, Attempts: maxAttempts, FocusEverLanded: focusEverLanded);
    }
}
