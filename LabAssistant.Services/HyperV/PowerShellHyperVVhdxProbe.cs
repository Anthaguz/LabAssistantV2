using LabAssistant.Services.PowerShell;
using LabAssistant.Services.Logging;

namespace LabAssistant.Services.HyperV;

public sealed class PowerShellHyperVVhdxProbe : IHyperVVhdxProbe
{
    private readonly Func<IPersistentPowerShellSession> _sessionFactory;

    public PowerShellHyperVVhdxProbe(Func<IPersistentPowerShellSession> sessionFactory)
    {
        _sessionFactory = sessionFactory;
    }

    public async Task<HyperVVhdxProbeResult> ProbeAsync(string path, CancellationToken cancellationToken = default)
    {
        var safePath = (path ?? string.Empty).Replace("'", "''", StringComparison.Ordinal);
        var script = string.Join(
            Environment.NewLine,
            "$ErrorActionPreference = 'Stop'",
            "try {",
            $"    Get-VHD -Path '{safePath}' -ErrorAction Stop | Out-Null",
            "    'VALID'",
            "}",
            "catch {",
            "    $msg = $_.Exception.Message",
            "    if ($msg -match 'access is denied|denied|unauthorized') { 'UNREADABLE' } else { 'INVALID' }",
            "    $msg",
            "}");

        using var session = _sessionFactory();
        var (output, error) = await session.ExecuteAsync(script).ConfigureAwait(false);
        DebugLogger.LogPowerShellOutput(script, output, error);

        var lines = PowerShellOutputCleaner.Clean(output)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        var markerIndex = lines.FindIndex(line =>
            string.Equals(line, "VALID", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(line, "UNREADABLE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(line, "INVALID", StringComparison.OrdinalIgnoreCase));

        var marker = markerIndex >= 0 ? lines[markerIndex] : null;
        var detail = markerIndex >= 0
            ? lines.Skip(markerIndex + 1).FirstOrDefault(line => !line.StartsWith("__PS_ERROR_LINE__", StringComparison.Ordinal))
            : lines.FirstOrDefault();

        if (string.Equals(marker, "VALID", StringComparison.OrdinalIgnoreCase))
        {
            return new HyperVVhdxProbeResult
            {
                Status = HyperVVhdxProbeStatus.Valid,
                Path = path ?? string.Empty,
                Message = "Base VHDX is valid."
            };
        }

        if (string.Equals(marker, "UNREADABLE", StringComparison.OrdinalIgnoreCase))
        {
            return new HyperVVhdxProbeResult
            {
                Status = HyperVVhdxProbeStatus.Unreadable,
                Path = path ?? string.Empty,
                Message = "Base VHDX is unreadable or inaccessible.",
                Detail = string.IsNullOrWhiteSpace(detail) ? error : detail
            };
        }

        return new HyperVVhdxProbeResult
        {
            Status = HyperVVhdxProbeStatus.Invalid,
            Path = path ?? string.Empty,
            Message = "Selected base disk is not a valid Hyper-V VHDX.",
            Detail = string.IsNullOrWhiteSpace(detail) ? (string.IsNullOrWhiteSpace(error) ? marker : error) : detail
        };
    }
}
