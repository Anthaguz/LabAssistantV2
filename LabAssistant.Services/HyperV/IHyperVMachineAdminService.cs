namespace LabAssistant.Services.HyperV;

public interface IHyperVMachineAdminService
{
    Task<IReadOnlyList<HyperVHostMachineVmInfo>> ListHostVmsAsync();

    Task<HyperVMachineActionResult> StartVmAsync(string vmName);

    Task<HyperVMachineActionResult> StopVmAsync(string vmName);

    Task<HyperVMachineActionResult> RestartVmAsync(string vmName);

    Task<HyperVMachineActionResult> OpenConsoleAsync(string vmName);

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
