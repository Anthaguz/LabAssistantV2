using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;
using System.Diagnostics;
using System.Text;
using System.Collections.Concurrent;

public class PersistentPowerShellSession : IPersistentPowerShellSession
{
    private const string OutputMarker = "__END_OF_OUTPUT__";
    private const string ErrorLineMarker = "__PS_ERROR_LINE__";

    private readonly Process _process;
    private readonly StreamWriter _input;
    private readonly StreamReader _output;
    private readonly StreamReader _error;
    private readonly ConcurrentQueue<string> _nativeErrorLines = new();
    private readonly Task _nativeErrorPumpTask;
    private readonly SemaphoreSlim _executeLock = new(1, 1);

    public PersistentPowerShellSession()
    {
        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -NoLogo",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        _process.Start();
        _input = _process.StandardInput;
        _output = _process.StandardOutput;
        _error = _process.StandardError;

        // Keep native stderr drained so the child process cannot block on a full error pipe.
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

            DebugLogger.Log("Persistent PowerShell ExecuteAsync: writing command to stdin.");
            await _input.WriteLineAsync("$__laErrStart = $Error.Count").ConfigureAwait(false);
            await _input.WriteLineAsync($"$__laCmd = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String('{commandBase64}'))").ConfigureAwait(false);
            await _input.WriteLineAsync("Invoke-Expression $__laCmd").ConfigureAwait(false);
            await _input.WriteLineAsync("$__laErrDelta = [Math]::Max(0, ($Error.Count - $__laErrStart))").ConfigureAwait(false);
            await _input.WriteLineAsync("if ($__laErrDelta -gt 0) { $Error | Select-Object -First $__laErrDelta | ForEach-Object { [System.Console]::Out.WriteLine('" + ErrorLineMarker + "' + ($_.ToString())) } }").ConfigureAwait(false);
            await _input.WriteLineAsync($"[System.Console]::Out.WriteLine('{OutputMarker}'); [System.Console]::Out.Flush()").ConfigureAwait(false);
            await _input.FlushAsync().ConfigureAwait(false);
            DebugLogger.Log("Persistent PowerShell ExecuteAsync: stdin flushed, starting stdout reader (stdout marker authoritative; native stderr pumped in background).");

            var output = new StringBuilder();
            var error = new StringBuilder();

            string? line;
            while ((line = await _output.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                if (line.Contains(OutputMarker))
                {
                    DebugLogger.Log("Persistent PowerShell ExecuteAsync: output marker received.");
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

            DebugLogger.Log("Persistent PowerShell ExecuteAsync: stdout reader completed; returning collected output + error.");

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
            DebugLogger.Log("Persistent PowerShell Dispose: begin.");
            if (!_process.HasExited)
            {
                try
                {
                    _input.WriteLine("exit");
                    _input.Flush();
                    DebugLogger.Log("Persistent PowerShell Dispose: sent exit command.");
                }
                catch (ObjectDisposedException)
                {
                    DebugLogger.Log("Persistent PowerShell Dispose: input already disposed while sending exit.");
                }
                catch (InvalidOperationException)
                {
                    DebugLogger.Log("Persistent PowerShell Dispose: process/input invalid while sending exit.");
                }

                DebugLogger.Log("Persistent PowerShell Dispose: waiting for process exit (1500ms).");
                if (!_process.WaitForExit(1500))
                {
                    DebugLogger.Log("Persistent PowerShell Dispose: process did not exit in time; killing process tree.");
                    try
                    {
                        _process.Kill(entireProcessTree: true);
                    }
                    catch (InvalidOperationException)
                    {
                        DebugLogger.Log("Persistent PowerShell Dispose: process already exited before kill.");
                    }

                    _process.WaitForExit(2000);
                    DebugLogger.Log("Persistent PowerShell Dispose: wait after kill completed.");
                }
                else
                {
                    DebugLogger.Log("Persistent PowerShell Dispose: process exited cleanly.");
                }
            }
        }
        finally
        {
            DebugLogger.Log("Persistent PowerShell Dispose: disposing process object.");
            _process.Dispose();
        }
    }
}
