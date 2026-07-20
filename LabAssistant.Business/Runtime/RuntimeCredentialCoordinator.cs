using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using LabAssistant.Models.Deployment;

namespace LabAssistant.Business.Runtime;

/// <summary>
/// Coordinates interactive credential re-prompts across the VMs of a single deployment run.
/// </summary>
/// <remarks>
/// The V2 runtime resolves guest credentials from a shared slot dictionary. When a running guest deterministically
/// rejects its bootstrap credential, this coordinator lets the user correct it once and retry in place, then makes the
/// correction visible to every other VM that resolves the same slot. Two guarantees matter here:
/// <list type="bullet">
/// <item>the shared slot dictionary is mutated in place (it is the same instance every resolve site reads), so a
/// correction propagates to later VMs sharing the slot without re-plumbing; and</item>
/// <item>prompting is single-flight per slot - VMs deploy in parallel, so without serialization several VMs could each
/// pop a dialog for the same wrong password. A per-slot gate ensures one prompt; VMs that were waiting adopt the value
/// the first prompt produced instead of asking again.</item>
/// </list>
/// One instance is created per deployment run, so its per-slot gates never leak across runs.
/// </remarks>
internal sealed class RuntimeCredentialCoordinator
{
    private readonly ConcurrentDictionary<string, V2RuntimeCredential> _slotValues;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _slotPromptGates =
        new(StringComparer.OrdinalIgnoreCase);

    public RuntimeCredentialCoordinator(ConcurrentDictionary<string, V2RuntimeCredential> slotValues)
    {
        _slotValues = slotValues ?? throw new ArgumentNullException(nameof(slotValues));
    }

    /// <summary>
    /// Asks the caller to supply a corrected credential for <paramref name="slotKey"/> after a deterministic rejection,
    /// serialized so only one prompt is shown per slot. Returns the corrected credential (also stored on the shared
    /// slot dictionary so later VMs pick it up), or null when there is no prompt wired (headless) or the user cancels.
    /// </summary>
    public async Task<V2RuntimeCredential?> RepromptAsync(
        VmDeploymentContext context,
        string slotKey,
        GuestCredentialPromptRequest promptRequest,
        V2RuntimeCredential rejected,
        CancellationToken cancellationToken)
    {
        var prompt = context.RequestGuestCredential;
        if (prompt is null)
        {
            // Headless run (or tests): no interactive seam, so the caller falls back to fail-fast.
            return null;
        }

        var gate = _slotPromptGates.GetOrAdd(slotKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Another VM may have corrected this slot while we waited on the gate. If the stored value no longer
            // matches the credential this VM just had rejected, adopt it instead of prompting the user again.
            if (_slotValues.TryGetValue(slotKey, out var current) && !CredentialsEqual(current, rejected))
            {
                return current;
            }

            var response = await prompt(promptRequest, cancellationToken).ConfigureAwait(false);
            if (response is null || response.Cancelled)
            {
                return null;
            }

            var corrected = new V2RuntimeCredential
            {
                Username = response.Username ?? string.Empty,
                Password = response.Password ?? string.Empty
            };
            _slotValues[slotKey] = corrected;
            return corrected;
        }
        finally
        {
            gate.Release();
        }
    }

    private static bool CredentialsEqual(V2RuntimeCredential left, V2RuntimeCredential right) =>
        string.Equals(left.Username, right.Username, StringComparison.Ordinal) &&
        string.Equals(left.Password, right.Password, StringComparison.Ordinal);
}
