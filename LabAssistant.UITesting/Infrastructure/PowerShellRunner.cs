using System.Diagnostics;
using System.Text;

namespace LabAssistant.UITesting.Infrastructure;

/// <summary>Result of a single PowerShell invocation.</summary>
public sealed record PowerShellResult(int ExitCode, string StdOut, string StdErr)
{
    public bool Success => ExitCode == 0;
}

/// <summary>
/// Thin wrapper over Windows PowerShell (5.1) used to query and manipulate
/// Hyper-V from the harness. Kept out-of-process on purpose: it needs the
/// Hyper-V module and the same elevation the app runs under, and staying at
/// arm's length means a wedged cmdlet can never take the harness down with it.
/// </summary>
public static class PowerShellRunner
{
    /// <summary>Runs a script block and returns raw stdout/stderr. Never throws on a non-zero exit.</summary>
    public static PowerShellResult Run(string script, TimeSpan? timeout = null)
    {
        var psi = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add("$ErrorActionPreference='Stop'; " + script);

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        // No interactive prompts: feed EOF so a cmdlet that asks for input fails fast instead of blocking.
        process.StandardInput.Close();

        if (!process.WaitForExit((int)(timeout ?? TimeSpan.FromMinutes(3)).TotalMilliseconds))
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            return new PowerShellResult(-1, stdout.ToString(), stderr.ToString() + "\n[timed out]");
        }

        process.WaitForExit();
        return new PowerShellResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    /// <summary>Runs a script and throws with the captured stderr when it fails.</summary>
    public static string RunOrThrow(string script, TimeSpan? timeout = null)
    {
        var result = Run(script, timeout);
        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"PowerShell failed (exit {result.ExitCode}): {result.StdErr.Trim()}");
        }

        return result.StdOut;
    }
}
