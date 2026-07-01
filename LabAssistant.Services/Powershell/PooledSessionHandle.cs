namespace LabAssistant.Services.PowerShell;

/// <summary>
/// Handle to a checked-out pooled session. Disposing returns it to the pool.
/// Use with <c>await using</c> for automatic return.
/// </summary>
public sealed class PooledSessionHandle : IAsyncDisposable
{
    private readonly IPersistentPowerShellSession _session;
    private readonly Action<IPersistentPowerShellSession> _returnToPool;
    private bool _disposed;

    internal PooledSessionHandle(IPersistentPowerShellSession session, Action<IPersistentPowerShellSession> returnToPool)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _returnToPool = returnToPool ?? throw new ArgumentNullException(nameof(returnToPool));
        Session = session;
    }

    /// <summary>
    /// Gets the checked-out PowerShell session.
    /// </summary>
    public IPersistentPowerShellSession Session { get; }

    /// <summary>
    /// Returns the session to the pool once.
    /// </summary>
    /// <returns>A completed value task.</returns>
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _returnToPool(_session);
        return ValueTask.CompletedTask;
    }
}
