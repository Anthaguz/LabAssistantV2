namespace LabAssistant.Services.HyperV;

public sealed class HyperVVmNetworkAdapterInfo
{
    public string AdapterName { get; init; } = string.Empty;

    public string? SwitchName { get; init; }

    public string MacAddress { get; init; } = string.Empty;

    /// <summary>
    /// The adapter's Hyper-V operational status (for example <c>Ok</c> or <c>Degraded</c>), captured purely for
    /// diagnostics so a NIC that is attached but reporting an empty switch name can be told apart from one that
    /// never attached. Null when the host did not report a status.
    /// </summary>
    public string? Status { get; init; }
}

public interface IHyperVService
{
    Task<bool> CreateVmAsync(string vmName, string vmPath, string vhdPath, int memoryMb, int cpuCount);
    Task<bool> EnableGuestServicesAsync(string vmName);
    Task<bool> StartVmAsync(string vmName);
    Task<bool> StopVmAsync(string vmName);
    Task<bool> VmExistsAsync(string vmName);
    Task<bool> IsVmRunningAsync(string vmName);
    Task<bool> RemoveVmAsync(string vmName);
    Task<bool> CreateVhdDifferencingAsync(string parentDiskPath, string vhdPath);
    Task<bool> CreateVhdFixedSizeAsync(string vhdPath, long sizeBytes);
    Task<bool> DisableVmCheckpointsAsync(string vmName);
    Task<List<string>> GetVirtualSwitchNamesAsync();

    Task<IReadOnlyList<HyperVVmNetworkAdapterInfo>> GetVmNetworkAdaptersAsync(string vmName)
        => Task.FromResult<IReadOnlyList<HyperVVmNetworkAdapterInfo>>(Array.Empty<HyperVVmNetworkAdapterInfo>());
    Task<bool> AddVirtualSwitchToVmAsync(string vmName, string switchName);

    async Task<bool> AddVirtualSwitchesToVmAsync(string vmName, IReadOnlyList<string> switchNames)
    {
        foreach (var switchName in switchNames)
        {
            if (!await AddVirtualSwitchToVmAsync(vmName, switchName))
            {
                return false;
            }
        }

        return true;
    }
}
