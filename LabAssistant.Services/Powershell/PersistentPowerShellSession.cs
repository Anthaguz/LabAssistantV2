using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;
using System.Diagnostics;
using System.Text;
using System.Collections.Concurrent;

internal interface IPersistentPowerShellHost : IDisposable
{
    TextWriter Input { get; }
    TextReader Output { get; }
    TextReader Error { get; }
    bool HasExited { get; }
    bool WaitForExit(int milliseconds);
    void Kill(bool entireProcessTree);
}

internal sealed class ProcessPersistentPowerShellHost : IPersistentPowerShellHost
{
    private readonly Process _process;

    public ProcessPersistentPowerShellHost()
    {
        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                // Read commands from stdin as a script instead of an interactive prompt loop so
                // repeated executions do not echo prompts or desynchronize stdout reads.
                Arguments = "-NoProfile -NonInteractive -NoLogo -Command -",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        _process.Start();
    }

    public TextWriter Input => _process.StandardInput;
    public TextReader Output => _process.StandardOutput;
    public TextReader Error => _process.StandardError;
    public bool HasExited => _process.HasExited;
    public bool WaitForExit(int milliseconds) => _process.WaitForExit(milliseconds);
    public void Kill(bool entireProcessTree) => _process.Kill(entireProcessTree);
    public void Dispose() => _process.Dispose();
}

public class PersistentPowerShellSession : IPersistentPowerShellSession
{
    private const string OutputMarker = "__END_OF_OUTPUT__";
    private const string ErrorLineMarker = "__PS_ERROR_LINE__";
    private const int DisposeExitWaitMs = 1500;
    private const int DisposePostKillWaitMs = 2000;

    private readonly IPersistentPowerShellHost _host;
    private readonly TextWriter _input;
    private readonly TextReader _output;
    private readonly TextReader _error;
    private readonly ConcurrentQueue<string> _nativeErrorLines = new();
    private readonly Task _nativeErrorPumpTask;
    private readonly SemaphoreSlim _executeLock = new(1, 1);

    public PersistentPowerShellSession() : this(new ProcessPersistentPowerShellHost())
    {
    }

    internal PersistentPowerShellSession(IPersistentPowerShellHost host)
    {
        _host = host;
        _input = _host.Input;
        _output = _host.Output;
        _error = _host.Error;

        // Keep native stderr drained so the child process cannot block on a full error pipe.
        // Command completion is driven by the PowerShell envelope stdout marker, not by native stderr.
        _nativeErrorPumpTask = Task.Run(async () =>
        {
            try
            {
                string? line;
                while ((line = await _error.ReadLineAsync().ConfigureAwait(false)) != null)
                {
                    _nativeErrorLines.Enqueue(line);
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

    public async Task<(string Output, string Error)> ExecuteAsync(string command)
    {
        await _executeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            while (_nativeErrorLines.TryDequeue(out _))
            {
            }

            var commandBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(command ?? string.Empty));

            // Serialize execution per session and run commands through a PowerShell-side envelope:
            // - command is base64-encoded to avoid interactive multiline parsing/continuation issues
            // - PowerShell error records are emitted to stdout with a tagged prefix for deterministic capture
            // - stdout marker is the authoritative completion signal
            PersistentPowerShellSessionTrace.Log("ExecuteAsync: writing command to stdin.");
            await _input.WriteLineAsync("$__laErrStart = $Error.Count").ConfigureAwait(false);
            await _input.WriteLineAsync($"$__laCmd = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String('{commandBase64}'))").ConfigureAwait(false);
            await _input.WriteLineAsync("Invoke-Expression $__laCmd").ConfigureAwait(false);
            await _input.WriteLineAsync("$__laErrDelta = [Math]::Max(0, ($Error.Count - $__laErrStart))").ConfigureAwait(false);
            await _input.WriteLineAsync("if ($__laErrDelta -gt 0) { $Error | Select-Object -First $__laErrDelta | ForEach-Object { [System.Console]::Out.WriteLine('" + ErrorLineMarker + "' + ($_.ToString())) } }").ConfigureAwait(false);
            await _input.WriteLineAsync($"[System.Console]::Out.WriteLine('{OutputMarker}'); [System.Console]::Out.Flush()").ConfigureAwait(false);
            await _input.FlushAsync().ConfigureAwait(false);
            PersistentPowerShellSessionTrace.Log("ExecuteAsync: stdin flushed, starting stdout reader (stdout marker authoritative; native stderr pumped in background).");

            var output = new StringBuilder();
            var error = new StringBuilder();

            string? line;
            while ((line = await _output.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                if (string.Equals(line, OutputMarker, StringComparison.Ordinal))
                {
                    PersistentPowerShellSessionTrace.Log("ExecuteAsync: output marker received.");
                    break;
                }

                if (line.StartsWith(ErrorLineMarker, StringComparison.Ordinal))
                {
                    error.AppendLine(line.Substring(ErrorLineMarker.Length));
                    continue;
                }

                output.AppendLine(line);
            }

            while (_nativeErrorLines.TryDequeue(out var nativeErrorLine))
            {
                error.AppendLine(nativeErrorLine);
            }

            PersistentPowerShellSessionTrace.Log("ExecuteAsync: stdout reader completed; returning collected output + error.");

            string cleanedOutput = PowerShellOutputCleaner.Clean(output.ToString());
            string cleanedError = PowerShellOutputCleaner.Clean(error.ToString());
            return (cleanedOutput, cleanedError);
        }
        finally
        {
            _executeLock.Release();
        }
    }

    public void Dispose()
    {
        try
        {
            PersistentPowerShellSessionTrace.Log("Dispose: begin.");
            if (!_host.HasExited)
            {
                try
                {
                    _input.WriteLine("exit");
                    _input.Flush();
                    PersistentPowerShellSessionTrace.Log("Dispose: sent exit command.");
                }
                catch (ObjectDisposedException)
                {
                    PersistentPowerShellSessionTrace.Log("Dispose: input already disposed while sending exit.");
                }
                catch (InvalidOperationException)
                {
                    PersistentPowerShellSessionTrace.Log("Dispose: process/input invalid while sending exit.");
                }

                PersistentPowerShellSessionTrace.Log($"Dispose: waiting for process exit ({DisposeExitWaitMs}ms).");
                if (!_host.WaitForExit(DisposeExitWaitMs))
                {
                    PersistentPowerShellSessionTrace.Log("Dispose: process did not exit in time; killing process tree.");
                    try
                    {
                        _host.Kill(entireProcessTree: true);
                    }
                    catch (InvalidOperationException)
                    {
                        PersistentPowerShellSessionTrace.Log("Dispose: process already exited before kill.");
                    }

                    _host.WaitForExit(DisposePostKillWaitMs);
                    PersistentPowerShellSessionTrace.Log("Dispose: wait after kill completed.");
                }
                else
                {
                    PersistentPowerShellSessionTrace.Log("Dispose: process exited cleanly.");
                }
            }
        }
        finally
        {
            PersistentPowerShellSessionTrace.Log("Dispose: disposing process object.");
            _host.Dispose();
        }
    }
}
