using LabAssistant.Models.Deployment;
using LabAssistant.Services.GuestExecution;
using LabAssistant.Services.PowerShell;
using Xunit;

namespace LabAssistant.Services.Tests;

/// <summary>
/// Behavior tests for the per-VM guest session model: one dedicated, reused connection per VM, guest steps
/// serialized within a VM but parallel across VMs, self-heal/invalidate, and mandatory teardown. All run
/// against a fake session, so no Hyper-V or real PowerShell process is required.
/// </summary>
public class PerVmGuestSessionTests
{
    private static V2RuntimeCredential Credential(string user = "Administrator", string password = "pw")
        => new() { Username = user, Password = password };

    [Fact]
    public async Task SameVm_ReusesASingleSessionAcrossSteps()
    {
        var factory = new RecordingSessionFactory();
        using var executor = new HyperVPowerShellDirectGuestCommandExecutor(factory.Create);

        await executor.ExecutePowerShellDirectAsync("vm-a", Credential(), "Step-One");
        await executor.ExecutePowerShellDirectAsync("vm-a", Credential(), "Step-Two");
        await executor.ExecutePowerShellDirectAsync("vm-a", Credential(), "Step-Three");

        Assert.Equal(1, factory.CreatedCount);
        Assert.Equal(3, factory.Sessions[0].Commands.Count);
    }

    [Fact]
    public async Task DifferentVms_GetIndependentSessions()
    {
        var factory = new RecordingSessionFactory();
        using var executor = new HyperVPowerShellDirectGuestCommandExecutor(factory.Create);

        await executor.ExecutePowerShellDirectAsync("vm-a", Credential(), "A1");
        await executor.ExecutePowerShellDirectAsync("vm-b", Credential(), "B1");
        await executor.ExecutePowerShellDirectAsync("vm-a", Credential(), "A2");

        Assert.Equal(2, factory.CreatedCount);
        Assert.Equal(2, factory.Sessions[0].Commands.Count); // vm-a
        Assert.Single(factory.Sessions[1].Commands);          // vm-b
    }

    [Fact]
    public async Task SameVm_ConcurrentSteps_AreSerialized()
    {
        var tracker = new ConcurrencyTracker();
        var factory = new RecordingSessionFactory(tracker, stepDelay: TimeSpan.FromMilliseconds(100));
        using var executor = new HyperVPowerShellDirectGuestCommandExecutor(factory.Create);

        var tasks = Enumerable.Range(0, 4)
            .Select(i => executor.ExecutePowerShellDirectAsync("vm-a", Credential(), $"Step-{i}"))
            .ToArray();
        await Task.WhenAll(tasks);

        Assert.Equal(1, factory.CreatedCount);
        Assert.Equal(1, tracker.MaxObserved); // never two guest steps in the same VM at once
    }

    [Fact]
    public async Task DifferentVms_ConcurrentSteps_RunInParallel()
    {
        var tracker = new ConcurrencyTracker();
        var factory = new RecordingSessionFactory(tracker, stepDelay: TimeSpan.FromMilliseconds(150));
        using var executor = new HyperVPowerShellDirectGuestCommandExecutor(factory.Create);

        var a = executor.ExecutePowerShellDirectAsync("vm-a", Credential(), "A");
        var b = executor.ExecutePowerShellDirectAsync("vm-b", Credential(), "B");
        await Task.WhenAll(a, b);

        Assert.Equal(2, factory.CreatedCount);
        Assert.Equal(2, tracker.MaxObserved); // cross-VM parallelism preserved
    }

    [Fact]
    public async Task ConcurrentFirstCalls_SameVm_CreateOnlyOneSession()
    {
        var factory = new RecordingSessionFactory(stepDelay: TimeSpan.FromMilliseconds(20));
        using var executor = new HyperVPowerShellDirectGuestCommandExecutor(factory.Create);

        var tasks = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => executor.ExecutePowerShellDirectAsync("vm-a", Credential(), "Go")))
            .ToArray();
        await Task.WhenAll(tasks);

        // The creation lock guarantees a burst of concurrent first-calls spins up exactly one host runspace.
        Assert.Equal(1, factory.CreatedCount);
    }

    [Fact]
    public async Task DisposeAllVmSessions_DisposesSessions_AndNextCallReopens()
    {
        var factory = new RecordingSessionFactory();
        using var executor = new HyperVPowerShellDirectGuestCommandExecutor(factory.Create);

        await executor.ExecutePowerShellDirectAsync("vm-a", Credential(), "First");
        var first = factory.Sessions[0];

        executor.DisposeAllVmSessions();
        Assert.Equal(1, first.DisposeCount);

        await executor.ExecutePowerShellDirectAsync("vm-a", Credential(), "AfterTeardown");

        Assert.Equal(2, factory.CreatedCount); // a fresh dedicated session, not the disposed one
    }

    [Fact]
    public async Task InvalidateVmSession_DropsGuestConnection_ButKeepsHostSession()
    {
        var factory = new RecordingSessionFactory();
        using var executor = new HyperVPowerShellDirectGuestCommandExecutor(factory.Create);

        await executor.ExecutePowerShellDirectAsync("vm-a", Credential(), "Configure");
        executor.InvalidateVmSession("vm-a");
        await executor.ExecutePowerShellDirectAsync("vm-a", Credential(), "AfterReboot");

        var session = factory.Sessions[0];
        Assert.Equal(1, factory.CreatedCount);       // same dedicated host runspace reused
        Assert.Equal(0, session.DisposeCount);       // not torn down, just the guest connection dropped
        Assert.Contains(session.Commands, c => c.Contains("$__laGuestSession = $null", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DisposedExecutor_RejectsFurtherCalls()
    {
        var factory = new RecordingSessionFactory();
        var executor = new HyperVPowerShellDirectGuestCommandExecutor(factory.Create);

        await executor.ExecutePowerShellDirectAsync("vm-a", Credential(), "Before");
        executor.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => executor.ExecutePowerShellDirectAsync("vm-a", Credential(), "After"));
    }

    private sealed class ConcurrencyTracker
    {
        private readonly object _sync = new();
        private int _current;

        public int MaxObserved { get; private set; }

        public IDisposable Enter()
        {
            lock (_sync)
            {
                _current++;
                if (_current > MaxObserved)
                {
                    MaxObserved = _current;
                }
            }

            return new Scope(this);
        }

        private void Leave()
        {
            lock (_sync)
            {
                _current--;
            }
        }

        private sealed class Scope : IDisposable
        {
            private readonly ConcurrencyTracker _owner;
            private bool _left;

            public Scope(ConcurrencyTracker owner) => _owner = owner;

            public void Dispose()
            {
                if (_left)
                {
                    return;
                }

                _left = true;
                _owner.Leave();
            }
        }
    }

    private sealed class RecordingSessionFactory
    {
        private readonly ConcurrencyTracker? _tracker;
        private readonly TimeSpan _stepDelay;

        public RecordingSessionFactory(ConcurrencyTracker? tracker = null, TimeSpan stepDelay = default)
        {
            _tracker = tracker;
            _stepDelay = stepDelay;
        }

        public IReadOnlyList<RecordingSession> Sessions => _ordered;

        private readonly List<RecordingSession> _ordered = new();
        private readonly object _sync = new();

        public int CreatedCount
        {
            get
            {
                lock (_sync)
                {
                    return _ordered.Count;
                }
            }
        }

        public IPersistentPowerShellSession Create()
        {
            var session = new RecordingSession(_tracker, _stepDelay);
            lock (_sync)
            {
                _ordered.Add(session);
            }

            return session;
        }
    }

    private sealed class RecordingSession : IPersistentPowerShellSession
    {
        private readonly ConcurrencyTracker? _tracker;
        private readonly TimeSpan _stepDelay;
        private readonly object _sync = new();

        public RecordingSession(ConcurrencyTracker? tracker, TimeSpan stepDelay)
        {
            _tracker = tracker;
            _stepDelay = stepDelay;
        }

        public List<string> Commands { get; } = new();

        public int DisposeCount { get; private set; }

        public Task<(string Output, string Error)> ExecuteAsync(string command)
            => ExecuteAsync(command, null, CancellationToken.None);

        public Task<(string Output, string Error)> ExecuteAsync(string command, CancellationToken cancellationToken)
            => ExecuteAsync(command, null, cancellationToken);

        public async Task<(string Output, string Error)> ExecuteAsync(
            string command,
            IReadOnlyDictionary<string, string>? secureVariables,
            CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                Commands.Add(command);
            }

            using (_tracker?.Enter())
            {
                if (_stepDelay > TimeSpan.Zero)
                {
                    await Task.Delay(_stepDelay, cancellationToken).ConfigureAwait(false);
                }
            }

            return (string.Empty, string.Empty);
        }

        public void Dispose()
        {
            lock (_sync)
            {
                DisposeCount++;
            }
        }
    }
}
