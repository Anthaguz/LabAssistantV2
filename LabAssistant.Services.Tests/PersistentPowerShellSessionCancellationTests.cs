using System.Collections.Concurrent;
using System.Text;
using Xunit;

namespace LabAssistant.Services.Tests;

/// <summary>
/// Verifies that <see cref="PersistentPowerShellSession"/> honors cancellation: a pre-cancelled token
/// short-circuits, and cancelling a command that is blocked reading guest output faults and kills the
/// session so a desynchronized session is never reused.
/// </summary>
public class PersistentPowerShellSessionCancellationTests
{
    [Fact]
    public async Task ExecuteAsync_PreCancelledToken_ThrowsWithoutExecuting()
    {
        var host = new ControllableHost();
        using var session = new PersistentPowerShellSession(host);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => session.ExecuteAsync("Get-Date", cts.Token));

        // Nothing was written because the token was already cancelled at entry.
        Assert.Empty(host.Input.GetLines());
    }

    [Fact]
    public async Task ExecuteAsync_CancelledWhileAwaitingOutput_FaultsAndKillsSession()
    {
        var host = new ControllableHost();
        using var session = new PersistentPowerShellSession(host);
        using var cts = new CancellationTokenSource();

        // No stdout marker is ever enqueued, so the command blocks in the output read loop.
        var executeTask = session.ExecuteAsync("Start-Sleep 60", cts.Token);

        // Wait until the command has been written and the reader is blocked on stdout.
        await host.Input.WaitForLineContainingAsync("Invoke-Expression $__laCmd", 1, TimeSpan.FromSeconds(2));

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executeTask);

        Assert.True(session.IsFaulted);
        Assert.True(host.KillCalled);
        Assert.True(host.KillEntireProcessTree);
    }

    [Fact]
    public async Task ExecuteAsync_AfterFault_RefusesFurtherCommands()
    {
        var host = new ControllableHost();
        using var session = new PersistentPowerShellSession(host);
        using var cts = new CancellationTokenSource();

        var executeTask = session.ExecuteAsync("Start-Sleep 60", cts.Token);
        await host.Input.WaitForLineContainingAsync("Invoke-Expression $__laCmd", 1, TimeSpan.FromSeconds(2));
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executeTask);

        // A faulted session must not run further commands with a fresh (uncancelled) token.
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteAsync("Get-Date"));
    }

    private sealed class ControllableHost : IPersistentPowerShellHost
    {
        public RecordingTextWriter Input { get; } = new();
        public QueueTextReader Stdout { get; } = new();
        public QueueTextReader Stderr { get; } = new();
        public bool HasExitedValue { get; set; }
        public bool KillCalled { get; private set; }
        public bool KillEntireProcessTree { get; private set; }

        TextWriter IPersistentPowerShellHost.Input => Input;
        TextReader IPersistentPowerShellHost.Output => Stdout;
        TextReader IPersistentPowerShellHost.Error => Stderr;
        bool IPersistentPowerShellHost.HasExited => HasExitedValue;

        bool IPersistentPowerShellHost.WaitForExit(int milliseconds) => true;

        void IPersistentPowerShellHost.Kill(bool entireProcessTree)
        {
            KillCalled = true;
            KillEntireProcessTree = entireProcessTree;
            HasExitedValue = true;
            // Releasing the readers lets the abandoned read complete instead of hanging.
            Stdout.Complete();
            Stderr.Complete();
        }

        void IDisposable.Dispose()
        {
            Stdout.Complete();
            Stderr.Complete();
        }
    }

    private sealed class RecordingTextWriter : TextWriter
    {
        private readonly List<string> _lines = [];
        private readonly object _sync = new();
        private readonly SemaphoreSlim _lineSignal = new(0);

        public override Encoding Encoding => Encoding.UTF8;

        public override void WriteLine(string? value)
        {
            lock (_sync)
            {
                _lines.Add(value ?? string.Empty);
            }

            _lineSignal.Release();
        }

        public override Task WriteLineAsync(string? value)
        {
            WriteLine(value);
            return Task.CompletedTask;
        }

        public override Task FlushAsync() => Task.CompletedTask;
        public override void Flush() { }

        public IReadOnlyList<string> GetLines()
        {
            lock (_sync)
            {
                return _lines.ToList();
            }
        }

        public async Task WaitForLineContainingAsync(string token, int occurrence, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            while (GetLines().Count(line => line.Contains(token, StringComparison.Ordinal)) < occurrence)
            {
                await _lineSignal.WaitAsync(cts.Token);
            }
        }
    }

    private sealed class QueueTextReader : TextReader
    {
        private readonly ConcurrentQueue<string?> _queue = new();
        private readonly SemaphoreSlim _signal = new(0);
        private bool _completed;

        public void Complete()
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            _queue.Enqueue(null);
            _signal.Release();
        }

        public override async Task<string?> ReadLineAsync()
        {
            await _signal.WaitAsync();
            _queue.TryDequeue(out var line);
            return line;
        }
    }
}
