namespace LabAssistant.Services.HyperV;

public interface IHyperVMachineAdminService
{
    Task<IReadOnlyList<HyperVHostMachineVmInfo>> ListHostVmsAsync();

    Task<HyperVMachineEditSnapshot?> GetVmEditSnapshotAsync(string vmName);

    Task<IReadOnlyList<string>> GetVirtualSwitchNamesAsync();

    Task<HyperVMachineActionResult> StartVmAsync(string vmName);

    Task<HyperVMachineActionResult> StopVmAsync(string vmName);

    Task<HyperVMachineActionResult> RestartVmAsync(string vmName);

    Task<HyperVMachineActionResult> OpenConsoleAsync(string vmName);

    Task<IReadOnlyList<string>> GetVmIpAddressesAsync(string vmName);

    Task<HyperVMachineActionResult> OpenRdpAsync(string targetIpv4);

    Task<IReadOnlyList<HyperVMachineDiskClassificationResult>> ClassifyVmDisksAsync(
        string vmName,
        IReadOnlyCollection<string> knownBaseDiskPaths,
        string? differencingDiskBasePath);

    Task<HyperVMachineActionResult> ApplyVmEditAsync(string vmName, HyperVMachineEditRequest request);

    Task<HyperVMachineActionResult> DeleteVmAsync(string vmName, bool includeStorage);
}

public sealed class HyperVHostMachineVmInfo
{
    public string VmId { get; init; } = string.Empty;

    public string VmName { get; init; } = string.Empty;

    public string State { get; init; } = string.Empty;

    public string? VmPath { get; init; }

    public IReadOnlyList<string> DiskPaths { get; init; } = Array.Empty<string>();
}

public sealed class HyperVMachineActionResult
{
    public bool Success { get; init; }

    public string? ErrorMessage { get; init; }

    public IReadOnlyDictionary<string, object?>? FailureMetadata { get; init; }
}

public sealed class HyperVMachineEditSnapshot
{
    public int ProcessorCount { get; init; }

    public long StartupMemoryBytes { get; init; }

    public bool DynamicMemoryEnabled { get; init; }

    public long MinimumMemoryBytes { get; init; }

    public long MaximumMemoryBytes { get; init; }

    public int MemoryBufferPercent { get; init; }

    public IReadOnlyList<HyperVMachineNetworkAdapterInfo> NetworkAdapters { get; init; } = Array.Empty<HyperVMachineNetworkAdapterInfo>();
}

public sealed class HyperVMachineNetworkAdapterInfo
{
    public string AdapterName { get; init; } = string.Empty;

    public string? SwitchName { get; init; }
}

public sealed class HyperVMachineEditRequest
{
    public int ProcessorCount { get; init; }

    public long StartupMemoryBytes { get; init; }

    public bool DynamicMemoryEnabled { get; init; }

    public long MinimumMemoryBytes { get; init; }

    public long MaximumMemoryBytes { get; init; }

    public int MemoryBufferPercent { get; init; }

    public IReadOnlyList<HyperVMachineNetworkAdapterAssignment> NetworkAdapterAssignments { get; init; } = Array.Empty<HyperVMachineNetworkAdapterAssignment>();
}

public sealed class HyperVMachineNetworkAdapterAssignment
{
    public string AdapterName { get; init; } = string.Empty;

    public string SwitchName { get; init; } = string.Empty;
}

public enum HyperVMachineDiskSafetyClassification
{
    DifferencingEligible,
    KnownBaseOrFull,
    PotentialBaseOrUncertain
}

public sealed class HyperVMachineDiskClassificationResult
{
    public string DiskPath { get; init; } = string.Empty;

    public HyperVMachineDiskSafetyClassification Classification { get; init; } = HyperVMachineDiskSafetyClassification.PotentialBaseOrUncertain;

    public string Reason { get; init; } = string.Empty;
}
