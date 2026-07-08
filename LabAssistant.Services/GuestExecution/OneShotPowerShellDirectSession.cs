using System.Collections.Concurrent;
using System.Text;
using LabAssistant.Services.PowerShell;

namespace LabAssistant.Services.GuestExecution;

/// <summary>
/// An <see cref="IPersistentPowerShellSession"/> that runs each command as a fresh, short-lived
/// <c>powershell.exe</c> process whose standard input is closed (EOF) before the command executes.
/// </summary>
/// <remarks>
/// This type exists specifically to make PowerShell Direct (<c>Invoke-Command -VMName</c> /
/// <c>New-PSSession -VMName</c>) work at all. PowerShell Direct connection negotiation BLOCKS INDEFINITELY
/// while the host <c>powershell.exe</c> process holds an open redirected standard input handle; it only
/// proceeds once stdin has reached EOF. The shared <see cref="PersistentPowerShellSession"/> host keeps stdin
/// open for its whole lifetime so it can stream successive commands - correct for host-side Hyper-V queries,
/// but fatal for guest PowerShell Direct, and the root cause of the long-standing guest-transport hang.
///
/// Each <see cref="ExecuteAsync(string, IReadOnlyDictionary{string,string}?, CancellationToken)"/> therefore
/// spawns a dedicated process, writes the entire command envelope, CLOSES stdin, and only then does PowerShell
/// run it - so the guest connection negotiates against a closed input stream and returns promptly. The process
/// exits on its own after emitting the completion marker; it is force-killed (whole tree) on timeout or
/// cancellation, so no orphan <c>powershell.exe</c> is ever left behind.
///
/// Because a fresh process is used per call there is no in-guest session to reuse across steps. That is a
/// deliberate trade-off: reusing a held-open <c>New-PSSession</c> requires a long-lived (stdin-open) host,
/// which is exactly what makes PowerShell Direct hang. A short reconnect per coarse guest step is the cost of
/// correctness, and it makes reboots self-healing because every step reconnects fresh. <see cref="Dispose"/>
/// is a no-op because nothing is retained between calls.
///
/// The stdin envelope (base64 command via <c>Invoke-Expression</c>, out-of-band secure variables, tagged error
/// lines, and the stdout completion marker) intentionally mirrors <see cref="PersistentPowerShellSession"/> so
/// both honor the same wire protocol; only the process lifecycle and the stdin-close differ.
/// </remarks>
public sealed class OneShotPowerShellDirectSession : IPersistentPowerShellSession
{
    private const string OutputMarker = "__END_OF_OUTPUT__";
    private const string ErrorLineMarker = "__PS_ERROR_LINE__";
    private const int ExitPollIntervalMs = 100;
    private const int MaxExitPolls = 30;

    private static readonly TimeSpan DefaultAttemptTimeout = TimeSpan.FromMinutes(5);

    private readonly Func<IPersistentPowerShellHost> _hostFactory;
    private readonly TimeSpan _attemptTimeout;

    /// <summary>
    /// Creates a session that spawns a real, dedicated <c>powershell.exe</c> host per command.
    /// </summary>
    public OneShotPowerShellDirectSession()
        : this(static () => new ProcessPersistentPowerShellHost(), DefaultAttemptTimeout)
    {
    }

    /// <summary>
    /// Test seam: supply a host factory and a per-command timeout so the envelope, stdin-close, marker parsing,
    /// and timeout/kill behavior can be exercised without a real process or a live guest.
    /// </summary>
    internal OneShotPowerShellDirectSession(Func<IPersistentPowerShellHost> hostFactory, TimeSpan attemptTimeout)
    {
        ArgumentNullException.ThrowIfNull(hostFactory);
        _hostFactory = hostFactory;
        _attemptTimeout = attemptTimeout;
    }

    public Task<(string Output, string Error)> ExecuteAsync(string command)
        => ExecuteAsync(command, null, CancellationToken.None);

    public Task<(string Output, string Error)> ExecuteAsync(string command, CancellationToken cancellationToken)
        => ExecuteAsync(command, null, cancellationToken);

    public async Task<(string Output, string Error)> ExecuteAsync(
        string command,
        IReadOnlyDictionary<string, string>? secureVariables,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var timeoutCts = new CancellationTokenSource(_attemptTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var linked = linkedCts.Token;

        var host = _hostFactory();
        var nativeErrorLines = new ConcurrentQueue<string>();

        // Keep native stderr drained in the background so a full error pipe can never block the child process.
        var stderrPump = PumpNativeErrorAsync(host.Error, nativeErrorLines);

        try
        {
            await WriteEnvelopeAsync(host.Input, command ?? string.Empty, secureVariables).ConfigureAwait(false);

            // Closing stdin (EOF) is the whole point of this type: PowerShell Direct will not complete its
            // connection while the host process still holds an open input stream.
            CloseInput(host.Input);

            var output = new StringBuilder();
            var error = new StringBuilder();

            string? line;
            while ((line = await ReadLineAsync(host.Output, linked).ConfigureAwait(false)) != null)
            {
                if (string.Equals(line, OutputMarker, StringComparison.Ordinal))
                {
                    break;
                }

                if (line.StartsWith(ErrorLineMarker, StringComparison.Ordinal))
                {
                    error.AppendLine(line[ErrorLineMarker.Length..]);
                    continue;
                }

                output.AppendLine(line);
            }

            await WaitForExitAsync(host).ConfigureAwait(false);
            await DrainNativeErrorAsync(stderrPump).ConfigureAwait(false);
            while (nativeErrorLines.TryDequeue(out var nativeErrorLine))
            {
                error.AppendLine(nativeErrorLine);
            }

            return (
                PowerShellOutputCleaner.Clean(output.ToString()),
                PowerShellOutputCleaner.Clean(error.ToString()));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The per-command timeout elapsed rather than an external cancellation. Surface it as a retryable
            // error (not a cancellation) so the runtime's readiness loops treat it as a soft failure and retry.
            KillProcessTree(host);
            throw new TimeoutException(
                $"Guest PowerShell Direct command did not complete within {_attemptTimeout}.");
        }
        catch (OperationCanceledException)
        {
            KillProcessTree(host);
            throw;
        }
        finally
        {
            // Defensive: guarantee no orphan powershell.exe survives, even on the success path (no-op if exited).
            KillProcessTree(host);
            host.Dispose();
        }
    }

    /// <summary>
    /// No-op: a one-shot session retains nothing between calls, so there is no host runspace or guest
    /// connection to tear down.
    /// </summary>
    public void Dispose()
    {
    }

    private static async Task WriteEnvelopeAsync(
        TextWriter input,
        string command,
        IReadOnlyDictionary<string, string>? secureVariables)
    {
        var commandBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(command));

        await input.WriteLineAsync("$__laErrStart = $Error.Count").ConfigureAwait(false);

        // Inject secret-bearing variables out-of-band. Their plaintext only ever exists base64-encoded on the
        // stdin channel (never logged and never in the caller-visible command string), and each is removed from
        // the runspace after the command runs.
        var injectedVariableNames = new List<string>();
        if (secureVariables is not null)
        {
            foreach (var pair in secureVariables)
            {
                if (string.IsNullOrEmpty(pair.Key))
                {
                    continue;
                }

                var valueBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(pair.Value ?? string.Empty));
                var nameLiteral = pair.Key.Replace("'", "''", StringComparison.Ordinal);
                await input.WriteLineAsync(
                    $"Set-Variable -Name '{nameLiteral}' -Value ([System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String('{valueBase64}')))")
                    .ConfigureAwait(false);
                injectedVariableNames.Add(pair.Key);
            }
        }

        await input.WriteLineAsync(
            $"$__laCmd = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String('{commandBase64}'))")
            .ConfigureAwait(false);
        await input.WriteLineAsync("Invoke-Expression $__laCmd").ConfigureAwait(false);

        foreach (var name in injectedVariableNames)
        {
            var nameLiteral = name.Replace("'", "''", StringComparison.Ordinal);
            await input.WriteLineAsync($"Remove-Variable -Name '{nameLiteral}' -ErrorAction SilentlyContinue")
                .ConfigureAwait(false);
        }

        await input.WriteLineAsync("$__laErrDelta = [Math]::Max(0, ($Error.Count - $__laErrStart))").ConfigureAwait(false);
        await input.WriteLineAsync(
            "if ($__laErrDelta -gt 0) { $Error | Select-Object -First $__laErrDelta | ForEach-Object { [System.Console]::Out.WriteLine('"
            + ErrorLineMarker + "' + ($_.ToString())) } }").ConfigureAwait(false);
        await input.WriteLineAsync($"[System.Console]::Out.WriteLine('{OutputMarker}'); [System.Console]::Out.Flush()")
            .ConfigureAwait(false);
        await input.FlushAsync().ConfigureAwait(false);
    }

    private static void CloseInput(TextWriter input)
    {
        try
        {
            input.Close();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (IOException)
        {
        }
    }

    private static async Task<string?> ReadLineAsync(TextReader reader, CancellationToken cancellationToken)
    {
        var readTask = reader.ReadLineAsync();
        if (!cancellationToken.CanBeCanceled)
        {
            return await readTask.ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var cancellationSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using (cancellationToken.Register(static state => ((TaskCompletionSource)state!).TrySetResult(), cancellationSignal))
        {
            var completed = await Task.WhenAny(readTask, cancellationSignal.Task).ConfigureAwait(false);
            if (completed == cancellationSignal.Task)
            {
                // The abandoned read completes once KillProcessTree closes the process pipes.
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        return await readTask.ConfigureAwait(false);
    }

    private static Task PumpNativeErrorAsync(TextReader error, ConcurrentQueue<string> sink)
    {
        return Task.Run(async () =>
        {
            try
            {
                string? line;
                while ((line = await error.ReadLineAsync().ConfigureAwait(false)) != null)
                {
                    sink.Enqueue(line);
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            catch (IOException)
            {
            }
        });
    }

    private static async Task WaitForExitAsync(IPersistentPowerShellHost host)
    {
        // The process exits on its own shortly after the marker is written (stdin is already at EOF), so a
        // short bounded poll is enough. KillProcessTree in the finally guarantees termination if it lingers.
        for (var i = 0; i < MaxExitPolls && !host.HasExited; i++)
        {
            await Task.Delay(ExitPollIntervalMs).ConfigureAwait(false);
        }
    }

    private static async Task DrainNativeErrorAsync(Task stderrPump)
    {
        try
        {
            await stderrPump.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
        }
    }

    private static void KillProcessTree(IPersistentPowerShellHost host)
    {
        try
        {
            if (!host.HasExited)
            {
                host.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}
