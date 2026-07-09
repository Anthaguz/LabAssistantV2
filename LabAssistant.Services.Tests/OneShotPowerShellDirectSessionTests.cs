using System.Text;
using LabAssistant.Services.GuestExecution;
using LabAssistant.Services.PowerShell;
using Xunit;

namespace LabAssistant.Services.Tests;

/// <summary>
/// Behavior tests for <see cref="OneShotPowerShellDirectSession"/>: the stdin envelope, the mandatory
/// stdin-close (EOF) that lets PowerShell Direct connect without hanging, marker/error parsing, and the
/// timeout/cancellation kill guarantee. All but the final test run against a fake host, so no real process or
/// live guest is required.
/// </summary>
public class OneShotPowerShellDirectSessionTests
{
    [Fact]
    public async Task ExecuteAsync_DeliversCommandAndPasswordOutOfBand_AndClosesStdin()
    {
        var host = new FakeOneShotHost(
            stdout: new StringReader("payload-line\n__END_OF_OUTPUT__\n"),
            stderr: new StringReader(string.Empty),
            hasExited: true);
        var session = new OneShotPowerShellDirectSession(() => host, TimeSpan.FromSeconds(30));
        var secure = new Dictionary<string, string> { ["__laGuestPassword"] = "S3cr3t-P@ss!" };

        var result = await session.ExecuteAsync(
            "Invoke-Command -VMName 'x' -ScriptBlock { Get-Date }", secure, CancellationToken.None);

        Assert.Contains("payload-line", result.Output);

        var written = host.Input.Joined;
        // The password and the command body must never appear as plaintext in the emitted stdin stream.
        Assert.DoesNotContain("S3cr3t-P@ss!", written);
        Assert.DoesNotContain("Get-Date", written);
        // They are carried as base64: the command via Invoke-Expression, the password via a secure Set-Variable.
        Assert.Contains("FromBase64String", written);
        Assert.Contains("Invoke-Expression $__laCmd", written);
        Assert.Contains("Set-Variable -Name '__laGuestPassword'", written);
        // The core fix: stdin is closed (EOF) so PowerShell Direct does not hang on an open input stream.
        Assert.True(host.Input.CloseCalled);
    }

    [Fact]
    public async Task ExecuteAsync_SeparatesTaggedErrorLinesFromOutput()
    {
        var host = new FakeOneShotHost(
            stdout: new StringReader("out1\n__PS_ERROR_LINE__boom\nout2\n__END_OF_OUTPUT__\n"),
            stderr: new StringReader(string.Empty),
            hasExited: true);
        var session = new OneShotPowerShellDirectSession(() => host, TimeSpan.FromSeconds(30));

        var result = await session.ExecuteAsync("x", null, CancellationToken.None);

        Assert.Contains("out1", result.Output);
        Assert.Contains("out2", result.Output);
        Assert.DoesNotContain("boom", result.Output);
        Assert.Contains("boom", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_FoldsNativeStderrIntoErrorChannel()
    {
        var host = new FakeOneShotHost(
            stdout: new StringReader("__END_OF_OUTPUT__\n"),
            stderr: new StringReader("native-error-line\n"),
            hasExited: true);
        var session = new OneShotPowerShellDirectSession(() => host, TimeSpan.FromSeconds(30));

        var result = await session.ExecuteAsync("x", null, CancellationToken.None);

        Assert.Contains("native-error-line", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_OnTimeout_KillsProcessTree_AndThrowsTimeout_NotCancellation()
    {
        var host = new FakeOneShotHost(
            stdout: new BlockingTextReader(),
            stderr: new StringReader(string.Empty),
            hasExited: false);
        var session = new OneShotPowerShellDirectSession(() => host, TimeSpan.FromMilliseconds(150));

        await Assert.ThrowsAsync<TimeoutException>(
            () => session.ExecuteAsync("x", null, CancellationToken.None));

        Assert.True(host.KillCalled);
        Assert.True(host.KillEntireProcessTree);
        Assert.True(host.DisposeCalled);
    }

    [Fact]
    public async Task ExecuteAsync_OnExternalCancellation_ThrowsOperationCanceled_AndKills()
    {
        var host = new FakeOneShotHost(
            stdout: new BlockingTextReader(),
            stderr: new StringReader(string.Empty),
            hasExited: false);
        var session = new OneShotPowerShellDirectSession(() => host, TimeSpan.FromSeconds(30));
        using var cts = new CancellationTokenSource();

        var task = session.ExecuteAsync("x", null, cts.Token);
        cts.CancelAfter(100);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.True(host.KillCalled);
    }

    [Fact]
    public async Task ExecuteAsync_WithRealPowerShellProcess_RunsHostCommandAndClosesStdin()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var session = new OneShotPowerShellDirectSession();

        var result = await session.ExecuteAsync(
            "Write-Output 'oneshot-roundtrip-ok'", null, CancellationToken.None);

        Assert.Equal("oneshot-roundtrip-ok", result.Output.Trim());
        Assert.True(string.IsNullOrWhiteSpace(result.Error));
    }

    private sealed class FakeOneShotHost : IPersistentPowerShellHost
    {
        private readonly TextReader _stdout;
        private readonly TextReader _stderr;

        public FakeOneShotHost(TextReader stdout, TextReader stderr, bool hasExited)
        {
            _stdout = stdout;
            _stderr = stderr;
            HasExitedValue = hasExited;
        }

        public RecordingWriter Input { get; } = new();
        public bool HasExitedValue { get; set; }
        public bool KillCalled { get; private set; }
        public bool KillEntireProcessTree { get; private set; }
        public bool DisposeCalled { get; private set; }

        TextWriter IPersistentPowerShellHost.Input => Input;
        TextReader IPersistentPowerShellHost.Output => _stdout;
        TextReader IPersistentPowerShellHost.Error => _stderr;
        bool IPersistentPowerShellHost.HasExited => HasExitedValue;
        bool IPersistentPowerShellHost.WaitForExit(int milliseconds) => HasExitedValue;

        void IPersistentPowerShellHost.Kill(bool entireProcessTree)
        {
            KillCalled = true;
            KillEntireProcessTree = entireProcessTree;
            HasExitedValue = true;
        }

        void IDisposable.Dispose() => DisposeCalled = true;
    }

    private sealed class RecordingWriter : TextWriter
    {
        private readonly List<string> _lines = [];
        private readonly object _sync = new();

        public bool CloseCalled { get; private set; }

        public override Encoding Encoding => Encoding.UTF8;

        public override void WriteLine(string? value)
        {
            lock (_sync)
            {
                _lines.Add(value ?? string.Empty);
            }
        }

        public override Task WriteLineAsync(string? value)
        {
            WriteLine(value);
            return Task.CompletedTask;
        }

        public override Task FlushAsync() => Task.CompletedTask;
        public override void Flush() { }

        // Record the close without disposing the underlying buffer, so the test can still read what was written.
        public override void Close() => CloseCalled = true;

        public string Joined
        {
            get
            {
                lock (_sync)
                {
                    return string.Join("\n", _lines);
                }
            }
        }
    }

    private sealed class BlockingTextReader : TextReader
    {
        // Never completes, simulating a guest read that would hang until the process is killed.
        public override Task<string?> ReadLineAsync() => new TaskCompletionSource<string?>().Task;
    }
}
