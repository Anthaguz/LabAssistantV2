using LabAssistant.Services.Logging;

namespace LabAssistant.Business.Machines;

public interface IMachinesCapabilityService
{
    Task<IReadOnlyList<MachineInventoryItem>> LoadInventoryAsync();

    Task<MachineOperationResult> StartVmAsync(MachineInventoryItem vm);

    Task<MachineOperationResult> StopVmAsync(MachineInventoryItem vm);

    Task<MachineOperationResult> RestartVmAsync(MachineInventoryItem vm);

    Task<MachineOperationResult> OpenConsoleAsync(MachineInventoryItem vm);

    Task<MachineRdpReadinessResult> EvaluateRdpReadinessAsync(MachineInventoryItem vm, CancellationToken cancellationToken = default);

    Task<MachineOperationResult> OpenRdpAsync(MachineInventoryItem vm, string targetIpv4);

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

public enum MachineDeleteScope
{
    VmRegistrationOnly,
    VmAndStorage
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
