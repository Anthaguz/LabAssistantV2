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

        // Health-check probes call ExecuteAsync("echo test"); returning "test" keeps the session healthy.
        public Task<(string Output, string Error)> ExecuteAsync(string command)
            => Task.FromResult(("test", string.Empty));

        public void Dispose() => Disposed = true;
    }
}
