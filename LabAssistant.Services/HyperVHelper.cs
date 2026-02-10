using System.Diagnostics;

namespace LabAssistant.Services;

public static class HyperVHelper
{
    public static bool IsHyperVEnabled()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "Get-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All | Select-Object -ExpandProperty State",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return false;
            }

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return output.Contains("Enabled", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
