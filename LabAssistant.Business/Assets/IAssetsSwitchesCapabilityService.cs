using LabAssistant.Services.HyperV;

namespace LabAssistant.Business.Assets;

public interface IAssetsSwitchesCapabilityService
{
    Task<AssetsSwitchesInventoryResult> LoadAsync(bool isRefresh = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetAttachedVmNamesAsync(string switchName, CancellationToken cancellationToken = default);

    Task<AssetsSwitchValidationResult> ValidateAsync(AssetsSwitchDraft draft, CancellationToken cancellationToken = default);

    Task<AssetsSwitchOperationResult> SaveAsync(AssetsSwitchDraft draft, CancellationToken cancellationToken = default);

    Task<AssetsSwitchDeleteAssessment> AssessDeleteAsync(string switchName, CancellationToken cancellationToken = default);

    Task<AssetsSwitchOperationResult> DeleteAsync(string switchName, CancellationToken cancellationToken = default);
}

public sealed class AssetsSwitchRecord
{
    public string Name { get; init; } = string.Empty;

    public string SwitchType { get; init; } = string.Empty;

    public string? AdapterName { get; init; }
}

public sealed class AssetsSwitchDraft
{
    public bool IsNew { get; init; }

    public string? OriginalName { get; init; }

    public string Name { get; init; } = string.Empty;

    public string SwitchType { get; init; } = string.Empty;

    public string? AdapterName { get; init; }
}

public sealed class AssetsSwitchesInventoryResult
{
    public string OperationId { get; init; } = string.Empty;

    public IReadOnlyList<AssetsSwitchRecord> Items { get; init; } = Array.Empty<AssetsSwitchRecord>();

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

public sealed class AssetsSwitchValidationResult
{
    public string OperationId { get; init; } = string.Empty;

    public string Severity { get; init; } = "Pass";

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> Details { get; init; } = Array.Empty<string>();
}

public sealed class AssetsSwitchOperationResult
{
    public bool Success { get; init; }

    public string OperationId { get; init; } = string.Empty;

    public string UserMessage { get; init; } = string.Empty;

    public AssetsSwitchRecord? Item { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

public sealed class AssetsSwitchDeleteAssessment
{
    public string OperationId { get; init; } = string.Empty;

    public bool Exists { get; init; }

    public bool CanDelete { get; init; }

    public IReadOnlyList<string> BlockingReasons { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> WarningReasons { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AttachedVmNames { get; init; } = Array.Empty<string>();

    public string Summary { get; init; } = string.Empty;
}
