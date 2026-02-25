using System.Collections.Concurrent;
using System.Text;
using Xunit;

namespace LabAssistant.Services.Tests;

public class PersistentPowerShellSessionTests
{
    [Fact]
    public async Task ExecuteAsync_CompletesWithStdoutMarker_WithoutNativeStderrSentinel()
    {
        var host = new FakeHost();
        using var session = new PersistentPowerShellSession(host);

        host.Stdout.Enqueue("line-one");
        host.Stdout.Enqueue("__END_OF_OUTPUT__");

        var result = await session.ExecuteAsync("Write-Output 'hello'");

        Assert.Contains("line-one", result.Output);
        Assert.True(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public async Task ExecuteAsync_CapturesNativeStderrAsSupplementalError()
    {
        var host = new FakeHost();
        using var session = new PersistentPowerShellSession(host);

        host.Stderr.Enqueue("native-error-line");
        host.Stdout.Enqueue("__END_OF_OUTPUT__");

        var result = await session.ExecuteAsync("Write-Output 'ok'");

        Assert.Contains("native-error-line", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_UsesBase64Envelope_ForMultilineCommandPayload()
    {
        var host = new FakeHost();
        using var session = new PersistentPowerShellSession(host);
        var command = "$a = 1" + Environment.NewLine + "Write-Output $a";

        host.Stdout.Enqueue("__END_OF_OUTPUT__");

        await session.ExecuteAsync(command);

        var written = host.Input.GetLines();
        Assert.Contains(written, line => line.Contains("FromBase64String", StringComparison.Ordinal));
        Assert.DoesNotContain(written, line => line.Contains("Write-Output $a", StringComparison.Ordinal));
        Assert.Contains(written, line => line.Contains("Invoke-Expression $__laCmd", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_SerializesConcurrentCommands_PerSession()
    {
        var host = new FakeHost();
        using var session = new PersistentPowerShellSession(host);

        var firstTask = session.ExecuteAsync("Write-Output 'first'");
        await host.Input.WaitForLineContainingAsync("$__laErrStart = $Error.Count", 1, TimeSpan.FromSeconds(2));

        var secondTask = session.ExecuteAsync("Write-Output 'second'");

        // First command is still blocked waiting on stdout marker, so second command should not write yet.
        await Task.Delay(100);
        Assert.Equal(1, host.Input.GetLines().Count(line => line.Contains("$__laErrStart = $Error.Count", StringComparison.Ordinal)));

        host.Stdout.Enqueue("__END_OF_OUTPUT__");
        await firstTask;

        await host.Input.WaitForLineContainingAsync("$__laErrStart = $Error.Count", 2, TimeSpan.FromSeconds(2));
        host.Stdout.Enqueue("__END_OF_OUTPUT__");
        await secondTask;
    }

    [Fact]
    public void Dispose_WhenProcessDoesNotExit_KillsEntireProcessTreeAfterBoundedWait()
    {
        var host = new FakeHost
        {
            HasExitedValue = false,
            WaitForExitResults = new Queue<bool>(new[] { false, true })
        };

        using (var session = new PersistentPowerShellSession(host))
        {
        }

        Assert.Contains("exit", host.Input.GetLines());
        Assert.Equal([1500, 2000], host.WaitForExitCalls);
        Assert.True(host.KillCalled);
        Assert.True(host.KillEntireProcessTree);
        Assert.True(host.DisposeCalled);
    }

    private sealed class FakeHost : IPersistentPowerShellHost
    {
        public RecordingTextWriter Input { get; } = new();
        public QueueTextReader Stdout { get; } = new();
        public QueueTextReader Stderr { get; } = new();
        public Queue<bool> WaitForExitResults { get; set; } = new(new[] { true });
        public List<int> WaitForExitCalls { get; } = [];
        public bool HasExitedValue { get; set; } = false;
        public bool KillCalled { get; private set; }
        public bool KillEntireProcessTree { get; private set; }
        public bool DisposeCalled { get; private set; }

        TextWriter IPersistentPowerShellHost.Input => Input;
        TextReader IPersistentPowerShellHost.Output => Stdout;
        TextReader IPersistentPowerShellHost.Error => Stderr;
        bool IPersistentPowerShellHost.HasExited => HasExitedValue;

        bool IPersistentPowerShellHost.WaitForExit(int milliseconds)
        {
            WaitForExitCalls.Add(milliseconds);
            if (WaitForExitResults.Count == 0)
            {
                return true;
            }

            var result = WaitForExitResults.Dequeue();
            if (result)
            {
                HasExitedValue = true;
            }

            return result;
        }

        void IPersistentPowerShellHost.Kill(bool entireProcessTree)
        {
            KillCalled = true;
            KillEntireProcessTree = entireProcessTree;
            HasExitedValue = true;
        }

        void IDisposable.Dispose()
        {
            DisposeCalled = true;
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

        public void Enqueue(string line)
        {
            _queue.Enqueue(line);
            _signal.Release();
        }

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
