using System.Runtime.InteropServices;
using System.Security.Principal;

namespace LabAssistant.Deployment.Harness;

/// <summary>
/// Cheap, dependency-free host-capability probes used to decide whether a real Hyper-V smoke test can run
/// or must be skipped. Every probe is safe to call off a Hyper-V host and never throws.
/// </summary>
public static class HarnessCapabilities
{
    /// <summary>
    /// Opt-in master switch. Real Hyper-V smoke tests stay skipped unless this variable is "1" or "true",
    /// so an ordinary <c>dotnet test</c> on a dev box or CI never provisions VMs by accident.
    /// </summary>
    public const string OptInEnvVar = "LABASSISTANT_RUN_HYPERV_SMOKE";

    /// <summary>True when running on Windows.</summary>
    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>True when the current process is elevated (member of the local Administrators role).</summary>
    public static bool IsElevated
    {
        get
        {
            if (!IsWindows)
            {
                return false;
            }

            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>True when the opt-in environment variable explicitly enables real Hyper-V smoke runs.</summary>
    public static bool OptInEnabled
    {
        get
        {
            var value = Environment.GetEnvironmentVariable(OptInEnvVar);
            return value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>True when the prepared base image file is present at the given path.</summary>
    public static bool BaseImageExists(string baseImagePath) =>
        !string.IsNullOrWhiteSpace(baseImagePath) && File.Exists(baseImagePath);

    /// <summary>
    /// Returns a human-readable reason when a real Hyper-V deploy must be skipped, or null when every
    /// prerequisite (opt-in, Windows, elevation, base image present, admin password supplied) is satisfied.
    /// </summary>
    public static string? DescribeSkip(HarnessOptions options)
    {
        if (!OptInEnabled)
        {
            return $"opt-in env {OptInEnvVar}=1 not set";
        }

        if (!IsWindows)
        {
            return "not running on Windows";
        }

        if (!IsElevated)
        {
            return "process is not elevated (Administrator required for Hyper-V)";
        }

        if (!BaseImageExists(options.BaseImagePath))
        {
            return $"base image not found at {options.BaseImagePath}";
        }

        if (!options.HasPassword)
        {
            return $"admin password env {HarnessOptions.AdminPasswordEnvVar} not set";
        }

        return null;
    }
}
