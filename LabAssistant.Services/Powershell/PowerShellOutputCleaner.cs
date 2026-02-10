using System.Text;

namespace LabAssistant.Services.PowerShell;

public static class PowerShellOutputCleaner
{
    public static string Clean(string raw)
    {
        return raw
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(line =>
                !line.StartsWith("PS ", StringComparison.OrdinalIgnoreCase) &&
                !line.StartsWith("Windows", StringComparison.OrdinalIgnoreCase) &&
                !line.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase) &&
                !line.StartsWith("Copyright", StringComparison.OrdinalIgnoreCase) &&
                !line.StartsWith("Install the latest", StringComparison.OrdinalIgnoreCase))
            .Select(line => line.Trim())
            .Aggregate(new StringBuilder(), (sb, line) => sb.AppendLine(line), sb => sb.ToString().Trim());
    }
}
