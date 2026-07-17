using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.Logging;

namespace LabAssistant.Services.PowerShell;

/// <summary>
/// Manages a thread-safe pool of pre-warmed PowerShell sessions backed by a bounded channel.
/// </summary>
public sealed class PowerShellSessionPool : IPowerShellSessionPool
{
    private readonly SessionPoolOptions _options;
    private readonly IStructuredLogger _structuredLogger;
    private readonly Func<IPersistentPowerShellSession> _sessionFactory;
    private readonly Channel<IPersistentPowerShellSession> _availableSessions;
    private readonly ConcurrentDictionary<IPersistentPowerShellSession, SessionEntry> _sessions = new();
    private readonly ConcurrentDictionary<IPersistentPowerShellSession, byte> _disposedSessions = new();
    private readonly SemaphoreSlim _stateGate = new(1, 1);
    private readonly SemaphoreSlim _healthCheckGate = new(1, 1);
    private readonly string _operationId = Guid.NewGuid().ToString("N");

    private PowerShellSessionPoolHealthMonitor? _healthMonitor;
    private int _availableCount;
    private int _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PowerShellSessionPool"/> class.
    /// </summary>
    /// <param name="options">The pool configuration.</param>
    public PowerShellSessionPool(SessionPoolOptions options)
        : this(options, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PowerShellSessionPool"/> class.
    /// </summary>
    /// <param name="options">The pool configuration.</param>
    /// <param name="structuredLogger">An optional structured logger for pool events.</param>
    public PowerShellSessionPool(SessionPoolOptions options, IStructuredLogger? structuredLogger)
        : this(options, structuredLogger, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PowerShellSessionPool"/> class with an injectable
    /// session factory. Exposed to tests so pool checkout/return semantics can be verified without
    /// spawning real PowerShell processes.
    /// </summary>
    /// <param name="options">The pool configuration.</param>
    /// <param name="structuredLogger">An optional structured logger for pool events.</param>
    /// <param name="sessionFactory">Factory used to create backing sessions. Defaults to real sessions.</param>
    internal PowerShellSessionPool(
        SessionPoolOptions options,
        IStructuredLogger? structuredLogger,
        Func<IPersistentPowerShellSession>? sessionFactory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);

        _options = options;
        _structuredLogger = structuredLogger ?? NullStructuredLogger.Instance;
        _sessionFactory = sessionFactory ?? (() => new PersistentPowerShellSession());
        _availableSessions = Channel.CreateBounded<IPersistentPowerShellSession>(
            new BoundedChannelOptions(options.MaxPoolSize)
            {
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait
            });

        _healthMonitor = new PowerShellSessionPoolHealthMonitor(this, options, _structuredLogger);
        _healthMonitor.Start();
        _ = WarmupOnStartupAsync();
    }

    /// <summary>
    /// Gets the number of currently available sessions.
    /// </summary>
    public int AvailableCount => Math.Max(0, Volatile.Read(ref _availableCount));

    /// <summary>
    /// Gets the total number of sessions tracked by the pool.
    /// </summary>
    public int TotalCount => _sessions.Count;

    /// <summary>
    /// Pre-warms the pool with additional available sessions up to the configured maximum.
    /// </summary>
    /// <param name="count">The number of sessions to create.</param>
    /// <param name="cancellationToken">Cancels the warmup operation.</param>
    public async Task WarmupAsync(int count, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (count <= 0)
        {
            return;
        }

        var created = 0;
        while (created < count)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var session = await TryCreateSessionAsync(checkOutImmediately: false, cancellationToken).ConfigureAwait(false);
            if (session is null)
            {
                break;
            }

            if (!TryEnqueueAvailable(session))
            {
                RetireSession(session);
                break;
            }

            created++;
        }

        if (created > 0)
        {
            Log(
                StructuredLogLevel.Info,
                "powershell.session-pool.warmup",
                "created",
                new Dictionary<string, object?>
                {
                    ["created"] = created,
                    ["availableCount"] = AvailableCount,
                    ["totalCount"] = TotalCount
                });
        }
    }

    /// <summary>
    /// Checks out a session from the pool, creating or waiting for one when needed.
    /// </summary>
    /// <param name="cancellationToken">Cancels the checkout.</param>
    /// <returns>A handle that returns the session to the pool when disposed.</returns>
    public async Task<PooledSessionHandle> CheckoutAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (TryTakeAvailable(out var availableSession))
            {
                // A session may have died (disposed after being faulted by a cancelled command, or its
                // backing process exited while it sat idle in the queue) after it was returned but before
                // it was re-checked-out; never hand such a session back out - retire it and keep looking.
                if (_disposedSessions.ContainsKey(availableSession)
                    || !availableSession.IsAlive
                    || !TryMarkCheckedOut(availableSession))
                {
                    // Mirror ReturnSession's "retired-dead" diagnostics on the checkout path so a session that
                    // died while idle in the queue is not silently discarded. The reason distinguishes the three
                    // ways a queued session can be unusable: already disposed, its backing process exited, or it
                    // could not be transitioned to checked-out.
                    var retireReason = _disposedSessions.ContainsKey(availableSession)
                        ? "disposed"
                        : !availableSession.IsAlive
                            ? "not-alive"
                            : "mark-failed";

                    Log(
                        StructuredLogLevel.Warn,
                        "powershell.session-pool.checkout",
                        "retired-dead",
                        new Dictionary<string, object?>
                        {
                            ["reason"] = retireReason,
                            ["availableCount"] = AvailableCount,
                            ["totalCount"] = TotalCount
                        });

                    RetireSession(availableSession);
                    continue;
                }

                Log(
                    StructuredLogLevel.Debug,
                    "powershell.session-pool.checkout",
                    "reused",
                    new Dictionary<string, object?>
                    {
                        ["availableCount"] = AvailableCount,
                        ["totalCount"] = TotalCount
                    });

                return new PooledSessionHandle(availableSession, ReturnSession, RetireSession);
            }

            var createdSession = await TryCreateSessionAsync(checkOutImmediately: true, cancellationToken).ConfigureAwait(false);
            if (createdSession is not null)
            {
                Log(
                    StructuredLogLevel.Info,
                    "powershell.session-pool.checkout",
                    "created",
                    new Dictionary<string, object?>
                    {
                        ["availableCount"] = AvailableCount,
                        ["totalCount"] = TotalCount
                    });

                return new PooledSessionHandle(createdSession, ReturnSession, RetireSession);
            }

            // The pool is at capacity with nothing available. Callers here (per-VM deploy sessions) can
            // legitimately hold a session for the full lifetime of a long operation, so blocking for a
            // returned session would cap overall concurrency and serialize otherwise-parallel work.
            // Hand out an overflow session instead; it is disposed on return rather than retained, so the
            // warm pool stays bounded while concurrency is never reduced below the on-demand baseline.
            var overflowSession = _sessionFactory();
            _disposedSessions.TryRemove(overflowSession, out _);

            Log(
                StructuredLogLevel.Warn,
                "powershell.session-pool.checkout",
                "overflow",
                new Dictionary<string, object?>
                {
                    ["availableCount"] = AvailableCount,
                    ["totalCount"] = TotalCount
                });

            // Both outcomes dispose the untracked overflow session: RetireSession removes any (absent)
            // tracking entry and disposes it exactly once.
            return new PooledSessionHandle(overflowSession, RetireSession, RetireSession);
        }
    }

    /// <summary>
    /// Health-checks available sessions and replaces unhealthy or stale ones.
    /// </summary>
    /// <param name="cancellationToken">Cancels the health check.</param>
    public async Task HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _healthCheckGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var sessionsToCheck = DrainAvailableSessions();
            if (sessionsToCheck.Count == 0)
            {
                return;
            }

            var recycled = 0;
            var replaced = 0;
            foreach (var session in sessionsToCheck)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!_sessions.TryGetValue(session, out var entry))
                {
                    DisposeSessionOnce(session);
                    continue;
                }

                if (entry.IsIdleBeyond(_options.IdleRecycleTimeout, DateTimeOffset.UtcNow))
                {
                    recycled++;
                    RetireSession(session);
                    if (await TryCreateReplacementAsync(cancellationToken).ConfigureAwait(false))
                    {
                        replaced++;
                    }

                    continue;
                }

                var isHealthy = await IsHealthyAsync(session).ConfigureAwait(false);
                if (isHealthy)
                {
                    if (!TryEnqueueAvailable(session))
                    {
                        RetireSession(session);
                    }

                    continue;
                }

                Log(
                    StructuredLogLevel.Warn,
                    "powershell.session-pool.health-failed",
                    "replacing",
                    new Dictionary<string, object?>
                    {
                        ["availableCount"] = AvailableCount,
                        ["totalCount"] = TotalCount
                    });

                RetireSession(session);
                if (await TryCreateReplacementAsync(cancellationToken).ConfigureAwait(false))
                {
                    replaced++;
                }
            }

            if (recycled > 0 || replaced > 0)
            {
                // Idle-recycle churn is routine background maintenance, not a deploy event. Emitting it at Info
                // floods the structured log (roughly one line per health-check interval) and drowns the actual
                // deployment trace, so it is logged at Debug. A genuine problem still surfaces at Warn via the
                // "powershell.session-pool.health-failed" event above.
                Log(
                    LaStatus.InfraPowershell_HealthCheck,
                    "completed",
                    new Dictionary<string, object?>
                    {
                        ["recycled"] = recycled,
                        ["replaced"] = replaced,
                        ["availableCount"] = AvailableCount,
                        ["totalCount"] = TotalCount
                    });
            }
        }
        finally
        {
            _healthCheckGate.Release();
        }
    }

    /// <summary>
    /// Stops the monitor and disposes all tracked sessions.
    /// </summary>
    /// <returns>A value task that completes when disposal finishes.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        if (_healthMonitor is not null)
        {
            await _healthMonitor.DisposeAsync().ConfigureAwait(false);
            _healthMonitor = null;
        }

        _availableSessions.Writer.TryComplete();

        foreach (var session in DrainAvailableSessions())
        {
            RetireSession(session);
        }

        foreach (var session in _sessions.Keys.ToArray())
        {
            RetireSession(session);
        }

        _stateGate.Dispose();
        _healthCheckGate.Dispose();
    }

    private static void ValidateOptions(SessionPoolOptions options)
    {
        if (options.WarmupCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "WarmupCount cannot be negative.");
        }

        if (options.MaxPoolSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxPoolSize must be greater than zero.");
        }

        if (options.CheckoutTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "CheckoutTimeout must be greater than zero.");
        }

        if (options.HealthCheckInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "HealthCheckInterval must be greater than zero.");
        }

        if (options.IdleRecycleTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "IdleRecycleTimeout must be greater than zero.");
        }
    }

    private async Task<IPersistentPowerShellSession?> TryCreateSessionAsync(bool checkOutImmediately, CancellationToken cancellationToken)
    {
        await _stateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_disposed == 1 || _sessions.Count >= _options.MaxPoolSize)
            {
                return null;
            }

            var session = _sessionFactory();
            var entry = SessionEntry.Create(checkOutImmediately);
            if (!_sessions.TryAdd(session, entry))
            {
                DisposeSessionOnce(session);
                throw new InvalidOperationException("Failed to register a PowerShell session in the pool.");
            }

            // Session creation is deliberately Debug (per the registry), not Info: a busy deploy grows the pool
            // repeatedly and one Info line per created session drowns the actual deployment trace. The retirement
            // of a dead session below is the noteworthy counterpart and is Warn. Both share the dotted event name
            // "infra.powershell.session.end"; they are told apart by their status code, severity, and result.
            Log(
                LaStatus.InfraPowershell_SessionCreated,
                "created",
                new Dictionary<string, object?>
                {
                    ["checkedOut"] = checkOutImmediately,
                    ["availableCount"] = AvailableCount,
                    ["totalCount"] = TotalCount
                });

            return session;
        }
        finally
        {
            _stateGate.Release();
        }
    }

    private bool TryMarkCheckedOut(IPersistentPowerShellSession session)
    {
        return _sessions.TryGetValue(session, out var entry) && entry.TryMarkCheckedOut();
    }

    private void ReturnSession(IPersistentPowerShellSession session)
    {
        if (!_sessions.TryGetValue(session, out var entry))
        {
            DisposeSessionOnce(session);
            return;
        }

        if (_disposed == 1)
        {
            RetireSession(session);
            return;
        }

        // Never return a dead session to the available queue - a faulted or exited session would be handed
        // straight back out and fail the next command. Retire it so the pool only ever holds live sessions.
        if (!session.IsAlive)
        {
            Log(
                LaStatus.InfraPowershell_SessionRetired,
                "retired-dead",
                new Dictionary<string, object?>
                {
                    ["availableCount"] = AvailableCount,
                    ["totalCount"] = TotalCount
                });

            RetireSession(session);
            return;
        }

        entry.MarkReturned(DateTimeOffset.UtcNow);
        if (TryEnqueueAvailable(session))
        {
            Log(
                StructuredLogLevel.Debug,
                "powershell.session-pool.return",
                "returned",
                new Dictionary<string, object?>
                {
                    ["availableCount"] = AvailableCount,
                    ["totalCount"] = TotalCount
                });

            return;
        }

        RetireSession(session);
    }

    private bool TryEnqueueAvailable(IPersistentPowerShellSession session)
    {
        if (_disposed == 1 || !_availableSessions.Writer.TryWrite(session))
        {
            return false;
        }

        Interlocked.Increment(ref _availableCount);
        return true;
    }

    private bool TryTakeAvailable(out IPersistentPowerShellSession session)
    {
        if (_availableSessions.Reader.TryRead(out session!))
        {
            Interlocked.Decrement(ref _availableCount);
            return true;
        }

        session = default!;
        return false;
    }

    private List<IPersistentPowerShellSession> DrainAvailableSessions()
    {
        var drainedSessions = new List<IPersistentPowerShellSession>();
        while (_availableSessions.Reader.TryRead(out var session))
        {
            Interlocked.Decrement(ref _availableCount);
            drainedSessions.Add(session);
        }

        return drainedSessions;
    }

    private async Task<bool> TryCreateReplacementAsync(CancellationToken cancellationToken)
    {
        var replacement = await TryCreateSessionAsync(checkOutImmediately: false, cancellationToken).ConfigureAwait(false);
        if (replacement is null)
        {
            return false;
        }

        if (TryEnqueueAvailable(replacement))
        {
            return true;
        }

        RetireSession(replacement);
        return false;
    }

    private async Task<bool> IsHealthyAsync(IPersistentPowerShellSession session)
    {
        // A faulted or exited session can never pass and probing it would just throw; short-circuit so the
        // caller retires it without running a doomed echo command.
        if (!session.IsAlive)
        {
            return false;
        }

        try
        {
            var (output, error) = await session.ExecuteAsync("echo test").ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(error)
                && string.Equals(output.Trim(), "test", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private void RetireSession(IPersistentPowerShellSession session)
    {
        _sessions.TryRemove(session, out _);
        DisposeSessionOnce(session);
    }

    private void DisposeSessionOnce(IPersistentPowerShellSession session)
    {
        if (_disposedSessions.TryAdd(session, 0))
        {
            try
            {
                session.Dispose();
            }
            catch
            {
            }
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed == 1, this);
    }

    private void Log(
        StructuredLogLevel level,
        string eventName,
        string result,
        IReadOnlyDictionary<string, object?>? context = null)
    {
        _structuredLogger.Log(level, eventName, _operationId, result, context);
    }

    // Code-based emit. Forwards the caller attributes so the recorded call site is the real emit
    // site in this file, not this wrapper.
    private void Log(
        uint code,
        string result,
        IReadOnlyDictionary<string, object?>? context = null,
        [CallerFilePath] string? callerFilePath = null,
        [CallerLineNumber] int callerLineNumber = 0)
    {
        _structuredLogger.Log(code, _operationId, result, context, callerFilePath, callerLineNumber);
    }

    private async Task WarmupOnStartupAsync()
    {
        if (_options.WarmupCount <= 0)
        {
            return;
        }

        try
        {
            await WarmupAsync(_options.WarmupCount).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception ex)
        {
            Log(
                StructuredLogLevel.Error,
                "powershell.session-pool.warmup-failed",
                "failed",
                new Dictionary<string, object?>
                {
                    ["requestedWarmupCount"] = _options.WarmupCount,
                    ["exceptionType"] = ex.GetType().FullName,
                    ["message"] = ex.Message
                });
        }
    }

    private sealed class SessionEntry
    {
        private int _checkedOut;
        private long _lastReturnedUtcTicks;

        private SessionEntry(bool checkedOut, DateTimeOffset lastReturnedUtc)
        {
            _checkedOut = checkedOut ? 1 : 0;
            _lastReturnedUtcTicks = lastReturnedUtc.UtcTicks;
        }

        public static SessionEntry Create(bool checkedOut)
        {
            var timestamp = DateTimeOffset.UtcNow;
            return new SessionEntry(checkedOut, timestamp);
        }

        public bool TryMarkCheckedOut()
        {
            return Interlocked.CompareExchange(ref _checkedOut, 1, 0) == 0;
        }

        public void MarkReturned(DateTimeOffset utcNow)
        {
            Volatile.Write(ref _lastReturnedUtcTicks, utcNow.UtcTicks);
            Volatile.Write(ref _checkedOut, 0);
        }

        public bool IsIdleBeyond(TimeSpan idleTimeout, DateTimeOffset utcNow)
        {
            if (Volatile.Read(ref _checkedOut) != 0)
            {
                return false;
            }

            var lastReturnedUtc = new DateTimeOffset(Volatile.Read(ref _lastReturnedUtcTicks), TimeSpan.Zero);
            return utcNow - lastReturnedUtc >= idleTimeout;
        }
    }
}
