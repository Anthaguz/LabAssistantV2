using LabAssistant.Models.Catalog;

namespace LabAssistant.Business.Assets;

public interface IAssetsBaseDisksCapabilityService
{
    Task<AssetsBaseDisksCatalogResult> LoadAsync(bool isRefresh = false, CancellationToken cancellationToken = default);

    Task<AssetsBaseDiskOperationResult> SaveAsync(AssetsBaseDiskDraft draft, CancellationToken cancellationToken = default);

    Task<AssetsBaseDiskValidationResult> ValidateAsync(AssetsBaseDiskDraft draft, CancellationToken cancellationToken = default);

    Task<AssetsBaseDiskRemovalAssessment> AssessRemoveAsync(string baseDiskId, CancellationToken cancellationToken = default);

    Task<AssetsBaseDiskOperationResult> RemoveAsync(string baseDiskId, CancellationToken cancellationToken = default);
}

public sealed class AssetsBaseDisksCatalogResult
{
    public string OperationId { get; init; } = string.Empty;

    public IReadOnlyList<AssetsBaseDiskRecord> Items { get; init; } = Array.Empty<AssetsBaseDiskRecord>();

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

public sealed class AssetsBaseDiskRecord
{
    public string Id { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public string OsName { get; init; } = string.Empty;

    public string OsVersion { get; init; } = string.Empty;

    public int Generation { get; init; }

    public long? SizeBytes { get; init; }

    public string? Signature { get; init; }

    public string? Notes { get; init; }
}

public sealed class AssetsBaseDiskDraft
{
    public string? Id { get; init; }

    public string Path { get; init; } = string.Empty;

    public string OsName { get; init; } = string.Empty;

    public string OsVersion { get; init; } = string.Empty;

    public int Generation { get; init; }

    public string? Notes { get; init; }

    public bool IsNew { get; init; }
}

public sealed class AssetsBaseDiskOperationResult
{
    public bool Success { get; init; }

    public string OperationId { get; init; } = string.Empty;

    public string UserMessage { get; init; } = string.Empty;

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public AssetsBaseDiskRecord? Item { get; init; }
}

public sealed class AssetsBaseDiskValidationResult
{
    public string OperationId { get; init; } = string.Empty;

    public string Severity { get; init; } = "Unknown";

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> Details { get; init; } = Array.Empty<string>();
}

public sealed class AssetsBaseDiskRemovalAssessment
{
    public string OperationId { get; init; } = string.Empty;

    public bool Exists { get; init; }

    public bool CanRemove { get; init; }

    public IReadOnlyList<string> BlockingReasons { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> WarningReasons { get; init; } = Array.Empty<string>();

    public string ReferenceSignalSummary { get; init; } = string.Empty;
}
