using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;
using System.Diagnostics;
using System.Text;

namespace LabAssistant.Services.HyperV;

public class HyperVService : IHyperVService
{
    private readonly IPersistentPowerShellSession _session; // Persistent session per VM

    public HyperVService(IPersistentPowerShellSession session)
    {
        _session = session;
    }

    public async Task<bool> CreateVmAsync(string vmName, string vmPath, string vhdPath, int memoryMb, int cpuCount)
    {
        var script = $"New-VM -Name '{vmName}' -MemoryStartupBytes {memoryMb}MB -Generation 2 -BootDevice VHD -VHDPath '{vhdPath}' -Path {vmPath}";
        var (output, error) = await _session.ExecuteAsync(script);
        DebugLogger.LogPowerShellOutput(script, output, error);
        return string.IsNullOrWhiteSpace(error);
    }

    public async Task<bool> EnableGuestServicesAsync(string vmName)
    {
        var script = $"Enable-VMIntegrationService -VMName '{vmName}' -Name 'Guest Service Interface'";
        var (output, error) = await _session.ExecuteAsync(script);
        DebugLogger.LogPowerShellOutput(script, output, error);
        return string.IsNullOrWhiteSpace(error);
    }

    public async Task<bool> StartVmAsync(string vmName)
    {
        var script = $"Start-VM -Name '{vmName}'";
        var (output, error) = await _session.ExecuteAsync(script);
        DebugLogger.LogPowerShellOutput(script, output, error);
        return string.IsNullOrWhiteSpace(error);
    }

    public async Task<bool> StopVmAsync(string vmName)
    {
        var script = $"Stop-VM -Name '{vmName}' -Force";
        var (output, error) = await _session.ExecuteAsync(script);
        DebugLogger.LogPowerShellOutput(script, output, error);
        return string.IsNullOrWhiteSpace(error);
    }

    public async Task<bool> CreateVhdDifferencingAsync(string parentDiskPath, string vhdPath)
    {
        var script = $"New-VHD -ParentPath '{parentDiskPath}' -Path '{vhdPath}' -Differencing";
        var (output, error) = await _session.ExecuteAsync(script);
        DebugLogger.LogPowerShellOutput(script, output, error);
        return string.IsNullOrWhiteSpace(error);
    }

    public async Task<bool> CreateVhdFixedSizeAsync(string vhdPath, long sizeBytes)
    {
        var script = $"New-VHD -Path '{vhdPath}' -SizeBytes {sizeBytes} -Fixed";
        var (output, error) = await _session.ExecuteAsync(script);
        DebugLogger.LogPowerShellOutput(script, output, error);
        return string.IsNullOrWhiteSpace(error);
    }

    public async Task<bool> DisableVmCheckpointsAsync(string vmName)
    {
        var script = $"Set-VM -Name '{vmName}' -CheckpointType Disabled";
        var (output, error) = await _session.ExecuteAsync(script);
        DebugLogger.LogPowerShellOutput(script, output, error);
        return string.IsNullOrWhiteSpace(error);
    }

    public async Task<List<string>> GetVirtualSwitchNamesAsync()
    {
        var script = "Get-VMSwitch | Select-Object -ExpandProperty Name";
        var (output, error) = await _session.ExecuteAsync(script);
        DebugLogger.LogPowerShellOutput(script, output, error);

        output = PowerShellOutputCleaner.Clean(output);

        var switches = output
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line =>
                !string.IsNullOrWhiteSpace(line) &&
                !line.StartsWith("Get-VMSwitch", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains(":\\") // eliminate file paths like 'C:\...'
            )
            .ToList();


        return switches;
    }
    //add virtual switch to vm
    public async Task<bool> AddVirtualSwitchToVmAsync(string vmName, string switchName)
    {
        var script = $"Connect-VMNetworkAdapter -VMName '{vmName}' -SwitchName '{switchName}'";
        var (output, error) = await _session.ExecuteAsync(script);
        DebugLogger.LogPowerShellOutput(script, output, error);
        return string.IsNullOrWhiteSpace(error);
    }

}
