namespace LabAssistant.Services.PowerShell;

/// <summary>
/// Adapts a checked-out <see cref="PooledSessionHandle"/> to <see cref="IPersistentPowerShellSession"/>
/// so existing consumers that create and dispose a session transparently check out from and return to
/// the pool. Disposing the lease returns the session to the pool; a session that faulted mid-execution
/// (for example via cancellation) is retired instead of reused.
/// </summary>
internal sealed class PooledSessionLease : IPersistentPowerShellSession
{
    private readonly PooledSessionHandle _handle;
    private int _disposed;

    public PooledSessionLease(PooledSessionHandle handle)
    {
        _handle = handle ?? throw new ArgumentNullException(nameof(handle));
    }

    // Report the liveness of the underlying pooled session so a consumer holding a lease sees the truth
    // (a session faulted mid-operation, or whose backing process exited, is no longer safe to reuse).
    public bool IsAlive => _handle.Session.IsAlive;

    public Task<(string Output, string Error)> ExecuteAsync(string command)
        => ExecuteAsync(command, null, CancellationToken.None);

    public Task<(string Output, string Error)> ExecuteAsync(string command, CancellationToken cancellationToken)
        => ExecuteAsync(command, null, cancellationToken);

    public async Task<(string Output, string Error)> ExecuteAsync(
        string command,
        IReadOnlyDictionary<string, string>? secureVariables,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _handle.Session.ExecuteAsync(command, secureVariables, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // ExecuteAsync only throws on cancellation (which faults/kills the session) or a catastrophic
            // failure; in either case the underlying session is unsafe to reuse, so retire it on return.
            _handle.MarkForRetire();
            throw;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        // The handle's disposal only invokes a synchronous return/retire callback, so blocking here does
        // no real async work and cannot deadlock.
        _handle.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
