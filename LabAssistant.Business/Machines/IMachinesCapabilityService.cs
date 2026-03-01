using LabAssistant.Services.Logging;

namespace LabAssistant.Business.Machines;

public interface IMachinesCapabilityService
{
    Task<IReadOnlyList<MachineInventoryItem>> LoadInventoryAsync();

    Task<MachineEditSnapshot?> LoadEditSnapshotAsync(MachineInventoryItem vm);

    Task<IReadOnlyList<string>> LoadVirtualSwitchesAsync();

    Task<MachineDeletionPolicyMode> GetDeletionPolicyAsync();

    Task SetDeletionPolicyAsync(MachineDeletionPolicyMode mode);

    Task<MachineDeletePreview> GetDeletePreviewAsync(MachineInventoryItem vm);

    Task<MachineOperationResult> StartVmAsync(MachineInventoryItem vm);

    Task<MachineOperationResult> StopVmAsync(MachineInventoryItem vm);

    Task<MachineOperationResult> RestartVmAsync(MachineInventoryItem vm);

    Task<MachineOperationResult> OpenConsoleAsync(MachineInventoryItem vm);

    Task<MachineRdpReadinessResult> EvaluateRdpReadinessAsync(MachineInventoryItem vm, CancellationToken cancellationToken = default);

    Task<MachineOperationResult> OpenRdpAsync(MachineInventoryItem vm, string targetIpv4);

    Task<MachineOperationResult> ApplyEditsAsync(MachineInventoryItem vm, MachineEditDraft draft);

    Task<MachineOperationResult> DeleteVmAsync(MachineInventoryItem vm, MachineDeleteScope scope);
}

public sealed class MachineInventoryItem
{
    public string VmId { get; init; } = string.Empty;

    public string VmName { get; init; } = string.Empty;

    public string State { get; init; } = string.Empty;

    public string OriginLabel { get; init; } = "Unknown";

    public string? VmPath { get; init; }

    public IReadOnlyList<string> DiskPaths { get; init; } = Array.Empty<string>();
}

public sealed class MachineOperationResult
{
    public bool Success { get; init; }

    public string OperationId { get; init; } = string.Empty;

    public string UserMessage { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, object?>? ErrorContext { get; init; }
}

public sealed class MachineEditSnapshot
{
    public int CpuCount { get; init; }

    public long StartupMemoryMb { get; init; }

    public bool DynamicMemoryEnabled { get; init; }

    public long MinimumMemoryMb { get; init; }

    public long MaximumMemoryMb { get; init; }

    public int MemoryBufferPercent { get; init; }

    public IReadOnlyList<MachineNetworkAdapterConfig> NetworkAdapters { get; init; } = Array.Empty<MachineNetworkAdapterConfig>();
}

public sealed class MachineNetworkAdapterConfig
{
    public string AdapterName { get; init; } = string.Empty;

    public string? SwitchName { get; init; }
}

public sealed class MachineEditDraft
{
    public int CpuCount { get; init; }

    public long StartupMemoryMb { get; init; }

    public bool DynamicMemoryEnabled { get; init; }

    public long MinimumMemoryMb { get; init; }

    public long MaximumMemoryMb { get; init; }

    public int MemoryBufferPercent { get; init; }

    public IReadOnlyList<MachineNetworkAdapterConfig> NetworkAdapters { get; init; } = Array.Empty<MachineNetworkAdapterConfig>();

    public IReadOnlyList<string> ChangedFieldKeys { get; init; } = Array.Empty<string>();
}

public enum MachineDeleteScope
{
    VmRegistrationOnly,
    VmAndStorage
}

public enum MachineDeletionPolicyMode
{
    AskEveryTime,
    AlwaysDeleteDisks,
    AlwaysDeleteDisksForLabAssistantProvisioned,
    AlwaysDeleteDisksForDifferencingOnly
}

public enum MachineDiskSafetyClassification
{
    DifferencingEligible,
    KnownBaseOrFull,
    PotentialBaseOrUncertain
}

public sealed class MachineDiskClassificationResult
{
    public string DiskPath { get; init; } = string.Empty;

    public MachineDiskSafetyClassification Classification { get; init; } = MachineDiskSafetyClassification.PotentialBaseOrUncertain;

    public string Reason { get; init; } = string.Empty;
}

public sealed class MachineDeletePreview
{
    public MachineDeletionPolicyMode PolicyMode { get; init; } = MachineDeletionPolicyMode.AskEveryTime;

    public MachineDeleteScope DefaultScope { get; init; } = MachineDeleteScope.VmRegistrationOnly;

    public bool SafeForAutomaticStorageDeletion { get; init; }

    public string PolicyMessage { get; init; } = string.Empty;

    public IReadOnlyList<MachineDiskClassificationResult> DiskClassifications { get; init; } = Array.Empty<MachineDiskClassificationResult>();
}

public enum MachineRdpReadinessState
{
    Unknown,
    Checking,
    Ready,
    NotReady
}

public static class MachineRdpReadinessReasonCodes
{
    public const string Ready = "ready";
    public const string VmNotRunning = "vm_not_running";
    public const string NoIpv4 = "no_ipv4";
    public const string Port3389Unreachable = "port_3389_unreachable";
    public const string CheckFailed = "check_failed";
}

public sealed class MachineRdpReadinessResult
{
    public string OperationId { get; init; } = string.Empty;

    public MachineRdpReadinessState State { get; init; } = MachineRdpReadinessState.Unknown;

    public string ReasonCode { get; init; } = MachineRdpReadinessReasonCodes.CheckFailed;

    public string Message { get; init; } = "RDP readiness not available.";

    public string? TargetIpv4 { get; init; }
}
