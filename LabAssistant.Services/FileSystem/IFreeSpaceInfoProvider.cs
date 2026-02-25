namespace LabAssistant.Services.FileSystem;

public enum FreeSpaceProbeStatus
{
    Known,
    Unknown
}

public sealed class FreeSpaceProbeResult
{
    public FreeSpaceProbeStatus Status { get; init; }
    public string Path { get; init; } = string.Empty;
    public string? RootPath { get; init; }
    public long? AvailableBytes { get; init; }
    public string Message { get; init; } = string.Empty;
}

public interface IFreeSpaceInfoProvider
{
    FreeSpaceProbeResult Probe(string? path);
}
