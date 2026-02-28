using LabAssistant.Services.Logging;

namespace LabAssistant.Business.Machines;

public interface IMachinesCapabilityService
{
    Task<IReadOnlyList<MachineInventoryItem>> LoadInventoryAsync();

    Task<MachineOperationResult> StartVmAsync(MachineInventoryItem vm);

    Task<MachineOperationResult> StopVmAsync(MachineInventoryItem vm);

    Task<MachineOperationResult> RestartVmAsync(MachineInventoryItem vm);

    Task<MachineOperationResult> OpenConsoleAsync(MachineInventoryItem vm);

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
