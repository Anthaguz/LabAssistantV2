namespace LabAssistant.Services.PowerShell;

/// <summary>
/// Handle to a checked-out pooled session. Disposing returns it to the pool, unless it has been
/// marked for retirement (for example after a fault), in which case it is disposed instead of reused.
/// Use with <c>await using</c> for automatic return.
/// </summary>
public sealed class PooledSessionHandle : IAsyncDisposable
{
    private readonly IPersistentPowerShellSession _session;
    private readonly Action<IPersistentPowerShellSession> _returnToPool;
    private readonly Action<IPersistentPowerShellSession>? _retireFromPool;
    private bool _disposed;
    private bool _retire;

    internal PooledSessionHandle(
        IPersistentPowerShellSession session,
        Action<IPersistentPowerShellSession> returnToPool,
        Action<IPersistentPowerShellSession>? retireFromPool = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _returnToPool = returnToPool ?? throw new ArgumentNullException(nameof(returnToPool));
        _retireFromPool = retireFromPool;
        Session = session;
    }

    /// <summary>
    /// Gets the checked-out PowerShell session.
    /// </summary>
    public IPersistentPowerShellSession Session { get; }

    /// <summary>
    /// Marks the underlying session for retirement so that, on disposal, it is disposed and removed
    /// from the pool instead of being returned for reuse. Use when the session is known to be broken
    /// (for example after a cancelled command faulted it).
    /// </summary>
    public void MarkForRetire()
    {
        _retire = true;
    }

    /// <summary>
    /// Returns the session to the pool once, or retires it when marked for retirement.
    /// </summary>
    /// <returns>A completed value task.</returns>
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;

        if (_retire && _retireFromPool is not null)
        {
            _retireFromPool(_session);
        }
        else
        {
            _returnToPool(_session);
        }

        return ValueTask.CompletedTask;
    }
}
