using System.Collections.Concurrent;
using LabAssistant.Models.Deployment;
using LabAssistant.Services.PowerShell;

namespace LabAssistant.Services.GuestExecution;

/// <summary>
/// Dispatches guest scripts over PowerShell Direct, keeping one dedicated dispatcher per VM.
/// </summary>
/// <remarks>
/// Each VM gets its own <see cref="VmGuestSession"/> (a dedicated one-shot guest-command session plus a per-VM
/// serialization lock). Every guest step is a fresh PowerShell Direct hop: reuse of a held-open in-guest
/// connection is impossible over a stdin-driven host, because the host must close stdin (EOF) for the
/// connection to negotiate at all. Routing every step for a VM through its <see cref="VmGuestSession"/> still
/// gives one useful property for free: guest steps for the same VM are serialized (its lock admits one at a
/// time), while different VMs keep independent dispatchers so cross-VM parallelism is preserved.
///
/// The dedicated sessions are intentionally minted from a factory that does NOT draw on the shared
/// catalog/admin pool, so a lab with many VMs cannot exhaust that pool and stall read queries.
///
/// This executor can outlive a single deployment (it is shared through a singleton runtime service), so the
/// deploy orchestrator must call <see cref="DisposeAllVmSessions"/> at the end of every run to guarantee no
/// per-VM dispatcher leaks between deployments.
/// </remarks>
public sealed class HyperVPowerShellDirectGuestCommandExecutor : IGuestCommandExecutor, IDisposable
{
    private readonly Func<IPersistentPowerShellSession> _sessionFactory;
    private readonly ConcurrentDictionary<string, VmGuestSession> _vmSessions =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sessionCreationLock = new();
    private bool _disposed;

    public HyperVPowerShellDirectGuestCommandExecutor(Func<IPersistentPowerShellSession> sessionFactory)
    {
        _sessionFactory = sessionFactory;
    }

    public async Task<GuestCommandResult> ExecutePowerShellDirectAsync(
        string vmName,
        V2RuntimeCredential credential,
        string script,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(vmName);
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentException.ThrowIfNullOrWhiteSpace(credential.Username);
        ArgumentException.ThrowIfNullOrWhiteSpace(script);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var session = GetOrCreateSession(vmName);

        var result = await session.ExecuteAsync(credential.Username, credential.Password, script, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var success = string.IsNullOrWhiteSpace(result.Error);
        return new GuestCommandResult
        {
            Success = success,
            Output = result.Output,
            Error = result.Error,
            ErrorCategory = success
                ? GuestCommandErrorCategory.None
                : GuestErrorClassifier.Classify(result.Error)
        };
    }

    public void InvalidateVmSession(string vmName)
    {
        if (string.IsNullOrWhiteSpace(vmName))
        {
            return;
        }

        if (_vmSessions.TryGetValue(vmName, out var session))
        {
            // Best-effort: dropping the in-guest connection is an optimization; the next dispatch self-heals
            // anyway, so a cancelled/failed invalidation must not surface as a deploy error.
            session.InvalidateAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
    }

    public void DisposeVmSession(string vmName)
    {
        if (string.IsNullOrWhiteSpace(vmName))
        {
            return;
        }

        if (_vmSessions.TryRemove(vmName, out var session))
        {
            session.Dispose();
        }
    }

    public void DisposeAllVmSessions()
    {
        foreach (var key in _vmSessions.Keys.ToArray())
        {
            if (_vmSessions.TryRemove(key, out var session))
            {
                session.Dispose();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DisposeAllVmSessions();
    }

    /// <summary>
    /// Returns the VM's session, creating it under a lock so a burst of concurrent first-calls for the same VM
    /// cannot spin up more than one dedicated host runspace. <see cref="ConcurrentDictionary{TKey,TValue}"/>'s
    /// value factory can run more than once under contention, which for a process-backed session would leak a
    /// runspace, so creation is serialized explicitly.
    /// </summary>
    private VmGuestSession GetOrCreateSession(string vmName)
    {
        if (_vmSessions.TryGetValue(vmName, out var existing))
        {
            return existing;
        }

        lock (_sessionCreationLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_vmSessions.TryGetValue(vmName, out existing))
            {
                return existing;
            }

            var created = new VmGuestSession(vmName, _sessionFactory());
            _vmSessions[vmName] = created;
            return created;
        }
    }

    /// <summary>
    /// Builds the guest dispatch command for a VM. Retained for the secret-handling tests that assert the
    /// emitted command never inlines the plaintext password; delegates to <see cref="VmGuestSession"/>.
    /// </summary>
    internal static string BuildPowerShellDirectCommand(string vmName, string username, string script)
        => VmGuestSession.BuildDispatchCommand(vmName, username, script);
}
