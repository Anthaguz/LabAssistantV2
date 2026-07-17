using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;
using Xunit;

namespace LabAssistant.Services.Tests;

[Collection("DebugLogger tests")]
public class MilestoneVScenarioMatrixTests
{
    [Fact]
    public async Task WrapperProtocol_StdoutMarkerCompletion_StderrSupplemental_AndDefensiveDisposeRemainFunctional()
    {
        var host = new FakeHost
        {
            HasExitedValue = false,
            WaitForExitResults = new Queue<bool>(new[] { false, true })
        };

        using (var session = new PersistentPowerShellSession(host))
        {
            var executeTask = session.ExecuteAsync("Write-Output 'hello'");
            await host.Input.WaitForLineContainingAsync("$__laErrStart = $Error.Count", 1, TimeSpan.FromSeconds(2));

            host.Stderr.Enqueue("native-wrapper-error");
            await host.Stderr.WaitForDequeuedLineCountAsync(1, TimeSpan.FromSeconds(2));
            host.Stdout.Enqueue("ok-line");
            host.Stdout.Enqueue("__END_OF_OUTPUT__");

            var (output, error) = await executeTask;
            Assert.Contains("ok-line", output, StringComparison.Ordinal);
            Assert.Contains("native-wrapper-error", error, StringComparison.Ordinal);
        }

        Assert.Equal([1500, 2000], host.WaitForExitCalls);
        Assert.True(host.KillCalled);
        Assert.True(host.KillEntireProcessTree);
    }

    [Fact]
    public void StructuredLogRotation_PreservesActiveFile_AndBoundsHistory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"labassistant-v-matrix-logs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var structuredPath = Path.Combine(directory, StructuredLoggingDefaults.StructuredEventsFileName);

        try
        {
            var sink = new JsonLinesLogEventSink(structuredPath, maxActiveFileBytes: 220, retainedHistoryFiles: 2);
            for (var i = 0; i < 6; i++)
            {
                sink.Write(StructuredLogEvent.Create(
                    StructuredLogLevel.Info,
                    $"Event{i}",
                    "op-v",
                    context: new Dictionary<string, object?> { ["payload"] = new string('x', 70) }));
            }

            Assert.True(File.Exists(structuredPath));
            Assert.True(File.Exists(Rotated(structuredPath, 1)));
            Assert.True(File.Exists(Rotated(structuredPath, 2)));
            Assert.False(File.Exists(Rotated(structuredPath, 3)));
            foreach (var line in File.ReadAllLines(Rotated(structuredPath, 1)))
            {
                using var _ = JsonDocument.Parse(line);
            }
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ErrorMetadataNormalization_AndPropagation_AreConsistent_ForHyperVFailurePath()
    {
        var fakeSession = new FakePowerShellSession
        {
            Result = (string.Empty, "System.Management.Automation.RuntimeException: boom 0x80070005\r\nFullyQualifiedErrorId : OperationFailed,Microsoft.Vhd.PowerShell.Cmdlets.NewVHD")
        };
        var hyperV = new HyperVService(fakeSession);

        var ok = await hyperV.CreateVhdDifferencingAsync(@"D:\base.vhdx", @"D:\vm\disk.vhdx");

        Assert.False(ok);
        Assert.NotNull(hyperV.LastFailureMetadata);
        Assert.Equal("RuntimeException", hyperV.LastFailureMetadata!["exceptionType"]);
        Assert.Equal("0x80070005", hyperV.LastFailureMetadata!["hresult"]);
        Assert.Equal("OperationFailed", hyperV.LastFailureMetadata!["errorCode"]);

        var failSoft = RuntimeErrorMetadataNormalizer.FromPowerShellErrorText("unknown text");
        Assert.Empty(failSoft);
    }

    [Fact]
    public void WrapperTraceVerbosity_DefaultLowNoise_AndEnabledTraceLogging()
    {
        var previous = Environment.GetEnvironmentVariable(PersistentPowerShellSessionTrace.EnvironmentVariableName);
        var logger = new CollectingStructuredLogger();

        try
        {
            DebugLogger.ConfigureStructuredSink(logger);
            PersistentPowerShellSessionTrace.SetEnabledForTests(null);
            Environment.SetEnvironmentVariable(PersistentPowerShellSessionTrace.EnvironmentVariableName, null);
            PersistentPowerShellSessionTrace.Log("should-not-appear");
            Assert.Empty(logger.Events);

            Environment.SetEnvironmentVariable(PersistentPowerShellSessionTrace.EnvironmentVariableName, "1");
            PersistentPowerShellSessionTrace.Log("trace-on");
            var e = Assert.Single(logger.Events);
            Assert.Equal($"0x{LaStatus.DiagDebug_DebugTrace:X8}", e.Code);
            Assert.Equal("[PowerShellWrapperTrace] trace-on", e.Context!["message"]);
            Assert.DoesNotContain(logger.Events, x => ((string?)x.Context!["message"])!.Contains("should-not-appear"));
        }
        finally
        {
            PersistentPowerShellSessionTrace.SetEnabledForTests(null);
            Environment.SetEnvironmentVariable(PersistentPowerShellSessionTrace.EnvironmentVariableName, previous);
            ResetDebugLoggerSink();
        }
    }

    private static string Rotated(string activeFilePath, int index)
    {
        var directory = Path.GetDirectoryName(activeFilePath) ?? string.Empty;
        var extension = Path.GetExtension(activeFilePath);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(activeFilePath);
        return Path.Combine(directory, $"{fileNameWithoutExtension}.{index}{extension}");
    }

    private static void ResetDebugLoggerSink()
    {
        var method = typeof(DebugLogger).GetMethod("ResetForTests", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(null, Array.Empty<object>());
    }

    private sealed class FakePowerShellSession : IPersistentPowerShellSession
    {
        public (string Output, string Error) Result { get; set; }
        public Task<(string Output, string Error)> ExecuteAsync(string command) => Task.FromResult(Result);
        public void Dispose() { }
    }

    private sealed class FakeHost : IPersistentPowerShellHost
    {
        public RecordingTextWriter Input { get; } = new();
        public QueueTextReader Stdout { get; } = new();
        public QueueTextReader Stderr { get; } = new();
        public Queue<bool> WaitForExitResults { get; set; } = new(new[] { true });
        public List<int> WaitForExitCalls { get; } = [];
        public bool HasExitedValue { get; set; }
        public bool KillCalled { get; private set; }
        public bool KillEntireProcessTree { get; private set; }

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
            Stdout.Complete();
            Stderr.Complete();
        }
    }

    private sealed class RecordingTextWriter : TextWriter
    {
        private readonly List<string> _lines = [];
        private readonly object _sync = new();
        private readonly SemaphoreSlim _signal = new(0);

        public override Encoding Encoding => Encoding.UTF8;

        public override void WriteLine(string? value)
        {
            lock (_sync)
            {
                _lines.Add(value ?? string.Empty);
            }
            _signal.Release();
        }

        public override Task WriteLineAsync(string? value)
        {
            WriteLine(value);
            return Task.CompletedTask;
        }

        public override Task FlushAsync() => Task.CompletedTask;
        public override void Flush() { }

        public async Task WaitForLineContainingAsync(string token, int occurrence, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            while (true)
            {
                int count;
                lock (_sync)
                {
                    count = _lines.Count(l => l.Contains(token, StringComparison.Ordinal));
                }

                if (count >= occurrence)
                {
                    return;
                }

                await _signal.WaitAsync(cts.Token);
            }
        }
    }

    private sealed class QueueTextReader : TextReader
    {
        private readonly ConcurrentQueue<string?> _queue = new();
        private readonly SemaphoreSlim _signal = new(0);
        private readonly SemaphoreSlim _dequeueSignal = new(0);
        private bool _completed;
        private int _dequeued;

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
            Interlocked.Increment(ref _dequeued);
            _dequeueSignal.Release();
            return line;
        }

        public async Task WaitForDequeuedLineCountAsync(int count, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);
            while (Volatile.Read(ref _dequeued) < count)
            {
                await _dequeueSignal.WaitAsync(cts.Token);
            }
        }
    }
}
