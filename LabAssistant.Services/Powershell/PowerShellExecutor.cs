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
            // -EncodedCommand takes a Base64 UTF-16LE payload, so the script is passed verbatim without any
            // quoting/escaping surface. This preserves newlines and prevents argument-boundary or subexpression
            // injection that string escaping cannot reliably contain.
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {EncodeScript(script)}",
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
    /// Encodes the script as a Base64 UTF-16LE payload for PowerShell's -EncodedCommand switch. This is the
    /// injection-safe transport (no quoting concerns, newlines preserved) also used by the persistent session.
    /// </summary>
    private static string EncodeScript(string script)
    {
        var bytes = Encoding.Unicode.GetBytes(script);
        return Convert.ToBase64String(bytes);
    }
}
