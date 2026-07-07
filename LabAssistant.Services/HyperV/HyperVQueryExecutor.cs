using System.Diagnostics;
using LabAssistant.Services.PowerShell;

namespace LabAssistant.Services.HyperV;

/// <summary>
/// Keeps a reusable Hyper-V query session alive so read-heavy paths do not pay full shell startup cost on every call.
/// </summary>
public sealed class HyperVQueryExecutor : IHyperVQueryExecutor, IDisposable
{
    private readonly Func<IPersistentPowerShellSession> _sessionFactory;
    private readonly object _sync = new();
    private IPersistentPowerShellSession? _session;
    private bool _disposed;

    public HyperVQueryExecutor(Func<IPersistentPowerShellSession> sessionFactory)
    {
        _sessionFactory = sessionFactory;
    }

    public async Task<HyperVPowerShellExecutionResult> ExecuteAsync(
        string queryName,
        string script,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var sessionCreated = false;
        var sessionCreationDurationMs = 0L;
        IPersistentPowerShellSession session;

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_session is null)
            {
                var sessionCreationStopwatch = Stopwatch.StartNew();
                _session = _sessionFactory();
                sessionCreationStopwatch.Stop();
                sessionCreated = true;
                sessionCreationDurationMs = sessionCreationStopwatch.ElapsedMilliseconds;
            }

            session = _session;
        }

        if (sessionCreated)
        {
            HyperVPowerShellTimingLogger.LogQuerySessionCreated(queryName, sessionCreationDurationMs);
        }

        var commandStopwatch = Stopwatch.StartNew();
        string output;
        string error;
        try
        {
            (output, error) = await session.ExecuteAsync(script, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // A thrown execution (cancellation faults and kills the underlying session; a faulted session
            // refuses further commands) leaves the cached session unusable. Evict and dispose it so the
            // next call checks out a fresh, healthy session and the pool slot held by the dead session is
            // reclaimed instead of leaking for the executor's lifetime.
            EvictSession(session);
            throw;
        }
        commandStopwatch.Stop();

        HyperVPowerShellTimingLogger.LogQueryExecution(
            queryName,
            sessionCreated,
            commandStopwatch.ElapsedMilliseconds,
            string.IsNullOrWhiteSpace(error));

        return new HyperVPowerShellExecutionResult
        {
            Output = output,
            Error = error,
            SessionCreated = sessionCreated,
            SessionCreationDurationMs = sessionCreationDurationMs,
            CommandDurationMs = commandStopwatch.ElapsedMilliseconds
        };
    }

    private void EvictSession(IPersistentPowerShellSession session)
    {
        lock (_sync)
        {
            if (ReferenceEquals(_session, session))
            {
                _session = null;
            }
        }

        // Dispose outside the lock: for a pooled lease this returns/retires the handle, freeing the slot.
        session.Dispose();
    }

    public void Dispose()
    {
        IPersistentPowerShellSession? session;
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            session = _session;
            _session = null;
        }

        session?.Dispose();
    }
}
