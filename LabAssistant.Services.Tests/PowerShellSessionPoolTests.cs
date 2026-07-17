using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;
using Xunit;

namespace LabAssistant.Services.Tests;

/// <summary>
/// Verifies the session pool checkout/return lifecycle using an injected fake session factory, so the
/// pool's semantics are exercised without spawning real PowerShell processes.
/// </summary>
public class PowerShellSessionPoolTests
{
    private static SessionPoolOptions Options(int maxPoolSize) => new()
    {
        WarmupCount = 0,
        MaxPoolSize = maxPoolSize,
        CheckoutTimeout = TimeSpan.FromSeconds(5),
        // Keep health checks from firing during the short test window.
        HealthCheckInterval = TimeSpan.FromMinutes(30),
        IdleRecycleTimeout = TimeSpan.FromMinutes(30)
    };

    [Fact]
    public async Task CheckoutAsync_CreatesSession_AndTracksIt()
    {
        var factory = new CountingSessionFactory();
        await using var pool = new PowerShellSessionPool(Options(4), null, factory.Create);

        var handle = await pool.CheckoutAsync();

        Assert.Equal(1, pool.TotalCount);
        Assert.Equal(0, pool.AvailableCount);
        Assert.Equal(1, factory.CreatedCount);

        await handle.DisposeAsync();
    }

    [Fact]
    public async Task Dispose_ReturnsSessionToPool_AndItIsReusedNotRecreated()
    {
        var factory = new CountingSessionFactory();
        await using var pool = new PowerShellSessionPool(Options(4), null, factory.Create);

        var firstHandle = await pool.CheckoutAsync();
        var firstSession = firstHandle.Session;
        await firstHandle.DisposeAsync();

        Assert.Equal(1, pool.AvailableCount);
        Assert.Equal(1, pool.TotalCount);

        var secondHandle = await pool.CheckoutAsync();

        // Reused, not recreated: same instance and no additional session was constructed.
        Assert.Same(firstSession, secondHandle.Session);
        Assert.Equal(1, factory.CreatedCount);
        Assert.Equal(0, pool.AvailableCount);

        await secondHandle.DisposeAsync();
    }

    [Fact]
    public async Task CheckoutAsync_AtCapacity_HandsOutOverflowThatIsDisposedOnReturn()
    {
        var factory = new CountingSessionFactory();
        await using var pool = new PowerShellSessionPool(Options(1), null, factory.Create);

        var held = await pool.CheckoutAsync();
        Assert.Equal(1, pool.TotalCount);

        // Pool is at capacity with the only session held, so this must not block or fail; it hands out an
        // overflow session instead.
        var overflow = await pool.CheckoutAsync();
        var overflowSession = (FakeSession)overflow.Session;

        Assert.Equal(2, factory.CreatedCount);
        // The overflow session is not tracked by the pool.
        Assert.Equal(1, pool.TotalCount);

        // Returning the overflow disposes it rather than adding it to the pool.
        await overflow.DisposeAsync();
        Assert.True(overflowSession.Disposed);
        Assert.Equal(0, pool.AvailableCount);
        Assert.Equal(1, pool.TotalCount);

        await held.DisposeAsync();
    }

    [Fact]
    public async Task Dispose_DeadSession_IsRetiredNotReturnedToPool()
    {
        var factory = new CountingSessionFactory();
        await using var pool = new PowerShellSessionPool(Options(4), null, factory.Create);

        var handle = await pool.CheckoutAsync();
        var session = (FakeSession)handle.Session;

        // The session died while checked out (faulted by a cancelled command, or its process exited).
        session.IsAlive = false;
        await handle.DisposeAsync();

        // It must be retired and disposed, never queued for reuse.
        Assert.True(session.Disposed);
        Assert.Equal(0, pool.AvailableCount);
        Assert.Equal(0, pool.TotalCount);
    }

    [Fact]
    public async Task CheckoutAsync_SkipsAndRetiresDeadAvailableSession_HandsOutFreshOne()
    {
        var factory = new CountingSessionFactory();
        await using var pool = new PowerShellSessionPool(Options(4), null, factory.Create);

        // Return a healthy session so it sits in the available queue.
        var firstHandle = await pool.CheckoutAsync();
        var firstSession = (FakeSession)firstHandle.Session;
        await firstHandle.DisposeAsync();
        Assert.Equal(1, pool.AvailableCount);

        // It dies while idle in the queue (for example the backing process exited).
        firstSession.IsAlive = false;

        // The next checkout must not hand the dead one back out; it retires it and creates a fresh session.
        var secondHandle = await pool.CheckoutAsync();

        Assert.NotSame(firstSession, secondHandle.Session);
        Assert.True(firstSession.Disposed);
        Assert.Equal(2, factory.CreatedCount);
        Assert.Equal(0, pool.AvailableCount);
        Assert.Equal(1, pool.TotalCount);

        await secondHandle.DisposeAsync();
    }

    [Fact]
    public void IsAlive_DefaultInterfaceMember_ReportsAliveForMinimalImplementers()
    {
        // A lightweight implementer that only supplies ExecuteAsync must keep satisfying the contract and
        // report itself alive via the default member, so the liveness gate never retires such sessions.
        IPersistentPowerShellSession minimal = new MinimalSession();
        Assert.True(minimal.IsAlive);
    }

    [Fact]
    public async Task CheckoutAsync_RetiringDeadAvailableSession_LogsSymmetricWarnEvent()
    {
        // Finding 2: the checkout skip branch used to retire a dead available session with no log, unlike
        // ReturnSession which logs a "retired-dead" Warn. This asserts the symmetric diagnostic now fires so a
        // session dying while idle in the queue is not silently discarded.
        var logger = new RecordingStructuredLogger();
        var factory = new CountingSessionFactory();
        await using var pool = new PowerShellSessionPool(Options(4), logger, factory.Create);

        var firstHandle = await pool.CheckoutAsync();
        var firstSession = (FakeSession)firstHandle.Session;
        await firstHandle.DisposeAsync();

        firstSession.IsAlive = false;

        var secondHandle = await pool.CheckoutAsync();

        var retired = Assert.Single(logger.Events, e =>
            e.EventName == "powershell.session-pool.checkout"
            && e.Result == "retired-dead");
        Assert.Equal(StructuredLogLevel.Warn, retired.Level);
        Assert.NotNull(retired.Context);
        Assert.True(retired.Context!.TryGetValue("reason", out var reason));
        Assert.Equal("not-alive", reason as string);

        await secondHandle.DisposeAsync();
    }

    [Fact]
    public async Task CheckoutAsync_CreatingSession_EmitsCanonicalStatusCode()
    {
        // Pilot for the status-code emit path: the session-created event is now emitted via a LaStatus
        // code, so the recorded event carries the canonical code, its dotted name, and the projected level.
        var logger = new RecordingStructuredLogger();
        var factory = new CountingSessionFactory();
        await using var pool = new PowerShellSessionPool(Options(4), logger, factory.Create);

        var handle = await pool.CheckoutAsync();

        var created = Assert.Single(
            logger.Events,
            e => e.Code == $"0x{LaStatus.InfraPowershell_SessionCreated:X8}");
        Assert.Equal("infra.powershell.session.end", created.EventName);
        Assert.Equal("created", created.Result);
        Assert.Equal(StructuredLogLevel.Debug, created.Level);

        await handle.DisposeAsync();
    }

    private sealed record RecordedEvent(
        StructuredLogLevel Level,
        string EventName,
        string? Result,
        IReadOnlyDictionary<string, object?>? Context,
        string? Code = null);

    private sealed class RecordingStructuredLogger : IStructuredLogger
    {
        private readonly List<RecordedEvent> _events = [];
        private readonly object _sync = new();

        public IReadOnlyList<RecordedEvent> Events
        {
            get
            {
                lock (_sync)
                {
                    return _events.ToList();
                }
            }
        }

        public void Log(StructuredLogEvent logEvent)
        {
            // The pool logs via the structured overload below; this best-effort mapping exists only to satisfy
            // the interface for completeness.
            var level = logEvent.Level switch
            {
                "debug" => StructuredLogLevel.Debug,
                "warn" => StructuredLogLevel.Warn,
                "error" => StructuredLogLevel.Error,
                _ => StructuredLogLevel.Info
            };

            lock (_sync)
            {
                _events.Add(new RecordedEvent(level, logEvent.Event, logEvent.Result, logEvent.Context, logEvent.Code));
            }
        }

        public void Log(
            StructuredLogLevel level,
            string eventName,
            string operationId,
            string? result = null,
            IReadOnlyDictionary<string, object?>? context = null)
        {
            lock (_sync)
            {
                _events.Add(new RecordedEvent(level, eventName, result, context));
            }
        }
    }

    private sealed class CountingSessionFactory
    {
        private int _created;

        public int CreatedCount => Volatile.Read(ref _created);

        public IPersistentPowerShellSession Create()
        {
            Interlocked.Increment(ref _created);
            return new FakeSession();
        }
    }

    private sealed class FakeSession : IPersistentPowerShellSession
    {
        public bool Disposed { get; private set; }

        // Controls the liveness the pool consults on return and checkout; defaults to alive.
        public bool IsAlive { get; set; } = true;

        // Health-check probes call ExecuteAsync("echo test"); returning "test" keeps the session healthy.
        public Task<(string Output, string Error)> ExecuteAsync(string command)
            => Task.FromResult(("test", string.Empty));

        public void Dispose() => Disposed = true;
    }

    private sealed class MinimalSession : IPersistentPowerShellSession
    {
        public Task<(string Output, string Error)> ExecuteAsync(string command)
            => Task.FromResult((string.Empty, string.Empty));

        public void Dispose()
        {
        }
    }
}
