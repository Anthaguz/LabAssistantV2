namespace LabAssistant.Services.HyperV;

public enum VhdxFileAccessStatus
{
    Accessible,
    Missing,
    Unreadable
}

public sealed class VhdxFileAccessProbeResult
{
    public VhdxFileAccessStatus Status { get; init; }
    public string Path { get; init; } = string.Empty;
    public string? Detail { get; init; }
}

public interface IVhdxFileAccessProbe
{
    VhdxFileAccessProbeResult Probe(string path);
}
