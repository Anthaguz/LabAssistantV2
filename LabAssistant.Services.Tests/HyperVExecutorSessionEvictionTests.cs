using LabAssistant.Services.HyperV;
using LabAssistant.Services.PowerShell;
using Xunit;

namespace LabAssistant.Services.Tests;

/// <summary>
/// Verifies that the Hyper-V query/admin executors do not keep using a session that faulted (for
/// example because a command was cancelled and the session was killed). The next call must transparently
/// obtain a fresh, healthy session and the pool slot held by the dead session must be reclaimed.
/// </summary>
public class HyperVExecutorSessionEvictionTests
{
    private static SessionPoolOptions PoolOptions(int maxPoolSize) => new()
    {
        WarmupCount = 0,
        MaxPoolSize = maxPoolSize,
        CheckoutTimeout = TimeSpan.FromSeconds(5),
        HealthCheckInterval = TimeSpan.FromMinutes(30),
        IdleRecycleTimeout = TimeSpan.FromMinutes(30)
    };

    [Fact]
    public async Task QueryExecutor_WhenCachedSessionFaults_EvictsAndDisposesIt_ThenUsesFreshSession()
    {
        var factory = new ScriptedSessionFactory(throwCancel: true, thenOk: true);
        using var executor = new HyperVQueryExecutor(factory.Create);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => executor.ExecuteAsync("query", "Get-VM"));

        // The faulted session was evicted from the cache and disposed.
        Assert.True(factory.Created[0].Disposed);

        // The next call transparently checks out a new session and succeeds.
        var result = await executor.ExecuteAsync("query", "Get-VM");

        Assert.Equal("ok", result.Output);
        Assert.Equal(2, factory.CreatedCount);
    }

    [Fact]
    public async Task QueryExecutor_WhenPooledSessionFaults_ReclaimsPoolSlot()
    {
        var factory = new ScriptedSessionFactory(throwCancel: true, thenOk: true);
        await using var pool = new PowerShellSessionPool(PoolOptions(1), null, factory.Create);
        using var executor = new HyperVQueryExecutor(
            () => new PooledSessionLease(pool.CheckoutAsync().GetAwaiter().GetResult()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => executor.ExecuteAsync("query", "Get-VM"));

        // The dead session was retired, freeing the single pool slot.
        Assert.Equal(0, pool.TotalCount);

        var result = await executor.ExecuteAsync("query", "Get-VM");

        Assert.Equal("ok", result.Output);
        Assert.Equal(1, pool.TotalCount);
        Assert.Equal(2, factory.CreatedCount);
    }

    [Fact]
    public async Task AdminExecutor_WhenPooledSessionFaults_ReclaimsPoolSlot_AndNextCallSucceeds()
    {
        var factory = new ScriptedSessionFactory(throwCancel: true, thenOk: true);
        await using var pool = new PowerShellSessionPool(PoolOptions(1), null, factory.Create);
        var executor = new HyperVAdministrativeCommandExecutor(
            () => new PooledSessionLease(pool.CheckoutAsync().GetAwaiter().GetResult()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => executor.ExecuteAsync("admin", "Start-VM"));

        // The admin executor disposes its per-call session with `using`, so the slot is already reclaimed.
        Assert.Equal(0, pool.TotalCount);

        var result = await executor.ExecuteAsync("admin", "Start-VM");

        Assert.Equal("ok", result.Output);
        Assert.Equal(1, pool.TotalCount);
        Assert.Equal(2, factory.CreatedCount);
    }

    /// <summary>
    /// Produces sessions whose first instance throws <see cref="OperationCanceledException"/> from
    /// ExecuteAsync (modelling a cancelled/killed session) and whose subsequent instances return success.
    /// </summary>
    private sealed class ScriptedSessionFactory
    {
        private readonly Queue<bool> _throwFlags;

        public ScriptedSessionFactory(bool throwCancel, bool thenOk)
        {
            _throwFlags = new Queue<bool>();
            _throwFlags.Enqueue(throwCancel);
            if (thenOk)
            {
                _throwFlags.Enqueue(false);
            }
        }

        public List<BehaviorSession> Created { get; } = [];

        public int CreatedCount => Created.Count;

        public IPersistentPowerShellSession Create()
        {
            var shouldThrow = _throwFlags.Count > 0 && _throwFlags.Dequeue();
            var session = new BehaviorSession(shouldThrow);
            Created.Add(session);
            return session;
        }
    }

    private sealed class BehaviorSession : IPersistentPowerShellSession
    {
        private readonly bool _throwCancel;

        public BehaviorSession(bool throwCancel)
        {
            _throwCancel = throwCancel;
        }

        public bool Disposed { get; private set; }

        public Task<(string Output, string Error)> ExecuteAsync(string command)
        {
            if (_throwCancel)
            {
                throw new OperationCanceledException();
            }

            return Task.FromResult(("ok", string.Empty));
        }

        public void Dispose() => Disposed = true;
    }
}
