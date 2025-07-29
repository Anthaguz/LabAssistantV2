using LabAssistant.Services.Logging;
using System.Diagnostics;
using System.Text;

namespace LabAssistant.Services.PowerShell;

public class PowerShellExecutor : IPowerShellExecutor
{
    public async Task<string> ExecuteAsync(string script, CancellationToken cancellationToken = default)
    {
        var (output, _) = await ExecuteWithResultAsync(script, cancellationToken);
        return output;
    }

    public async Task<(string Output, string Error)> ExecuteWithResultAsync(string script, CancellationToken cancellationToken = default)
    {
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();
        DebugLogger.Log($"Executing PowerShell script: {script}");

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe", // ensures PS 5.1 is used
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{EscapeScript(script)}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };

        process.OutputDataReceived += (s, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) errorBuilder.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);
        DebugLogger.Log($"PowerShell process exited with code {process.ExitCode}");

        return (outputBuilder.ToString().Trim(), errorBuilder.ToString().Trim());
    }

    /// <summary>
    /// Escapes double quotes and newlines for embedding in the -Command argument.
    /// </summary>
    private static string EscapeScript(string script)
    {
        return script
            .Replace("\"", "`\"")
            .Replace("\r", "")
            .Replace("\n", "; ");
    }
}
