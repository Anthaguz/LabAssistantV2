namespace LabAssistant.Services.HyperV;

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
    Task<bool> AddVirtualSwitchToVmAsync(string vmName, string switchName);
}
