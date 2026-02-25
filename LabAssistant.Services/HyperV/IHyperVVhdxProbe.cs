namespace LabAssistant.Services.HyperV;

public enum HyperVVhdxProbeStatus
{
    Valid,
    Unreadable,
    Invalid
}

public sealed class HyperVVhdxProbeResult
{
    public HyperVVhdxProbeStatus Status { get; init; }
    public string Path { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? Detail { get; init; }
}

public interface IHyperVVhdxProbe
{
    Task<HyperVVhdxProbeResult> ProbeAsync(string path, CancellationToken cancellationToken = default);
}
