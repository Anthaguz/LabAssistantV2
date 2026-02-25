namespace LabAssistant.Services.FileSystem;

public enum DestinationPathTargetKind
{
    Directory,
    File
}

public enum DestinationPathFeasibilityStatus
{
    Valid,
    Missing,
    Invalid,
    RootUnavailable,
    Unwritable,
    ExistingDirectoryConflict,
    ExistingFileConflict
}

public sealed class DestinationPathFeasibilityResult
{
    public DestinationPathFeasibilityStatus Status { get; init; }
    public string Path { get; init; } = string.Empty;
    public string? RootPath { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Detail { get; init; }
}

public interface IDestinationPathFeasibilityProbe
{
    DestinationPathFeasibilityResult Probe(
        string? path,
        DestinationPathTargetKind targetKind,
        bool verifyWriteAccess);
}
