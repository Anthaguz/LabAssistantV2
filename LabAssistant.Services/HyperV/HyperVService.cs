using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;
using System.Diagnostics;

namespace LabAssistant.Services.HyperV;

public class HyperVService : IHyperVService, IHyperVFailureDiagnosticsProvider
{
    private readonly IPersistentPowerShellSession _session; // Persistent session per VM

    public HyperVService(IPersistentPowerShellSession session)
    {
        _session = session;
    }

    public IReadOnlyDictionary<string, object?>? LastFailureMetadata { get; private set; }

    public async Task<bool> CreateVmAsync(string vmName, string vmPath, string vhdPath, int memoryMb, int cpuCount)
    {
        var script = $"New-VM -Name '{vmName}' -MemoryStartupBytes {memoryMb}MB -Generation 2 -BootDevice VHD -VHDPath '{vhdPath}' -Path '{vmPath}'";
        var (output, error) = await ExecuteMeasuredAsync("create_vm", script);
        DebugLogger.LogPowerShellOutput(script, output, error);
        return CaptureFailureMetadataAndReturnSuccess(error);
    }

    public async Task<bool> EnableGuestServicesAsync(string vmName)
    {
        var script = $"Enable-VMIntegrationService -VMName '{vmName}' -Name 'Guest Service Interface'";
        var (output, error) = await ExecuteMeasuredAsync("enable_guest_services", script);
        DebugLogger.LogPowerShellOutput(script, output, error);
        return CaptureFailureMetadataAndReturnSuccess(error);
    }

    public async Task<bool> StartVmAsync(string vmName)
    {
        var script = $"Start-VM -Name '{vmName}'";
        var (output, error) = await ExecuteMeasuredAsync("start_vm", script);
        DebugLogger.LogPowerShellOutput(script, output, error);
        return CaptureFailureMetadataAndReturnSuccess(error);
    }

    public async Task<bool> StopVmAsync(string vmName)
    {
        var script = $"Stop-VM -Name '{vmName}' -Force";
        var (output, error) = await ExecuteMeasuredAsync("stop_vm", script);
        DebugLogger.LogPowerShellOutput(script, output, error);
        return CaptureFailureMetadataAndReturnSuccess(error);
    }

    public async Task<bool> VmExistsAsync(string vmName)
    {
        var script = $"if (Get-VM -Name '{vmName}' -ErrorAction SilentlyContinue) {{ 'True' }} else {{ 'False' }}";
        var (output, error) = await ExecuteMeasuredAsync("vm_exists", script);
        DebugLogger.LogPowerShellOutput(script, output, error);
        if (!string.IsNullOrWhiteSpace(error))
        {
            CaptureFailureMetadataAndReturnSuccess(error);
            return false;
        }

        ClearLastFailureMetadata();
        return PowerShellOutputCleaner.Clean(output)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Any(line => string.Equals(line.Trim(), "True", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> IsVmRunningAsync(string vmName)
    {
        var script = $"$vm = Get-VM -Name '{vmName}' -ErrorAction SilentlyContinue; if ($null -eq $vm) {{ 'Missing' }} else {{ $vm.State.ToString() }}";
        var (output, error) = await ExecuteMeasuredAsync("is_vm_running", script);
        DebugLogger.LogPowerShellOutput(script, output, error);
        if (!string.IsNullOrWhiteSpace(error))
        {
            CaptureFailureMetadataAndReturnSuccess(error);
            return false;
        }

        ClearLastFailureMetadata();
        var cleaned = PowerShellOutputCleaner.Clean(output);
        return cleaned.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Any(line => string.Equals(line.Trim(), "Running", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> RemoveVmAsync(string vmName)
    {
        var script = $"Remove-VM -Name '{vmName}' -Force";
        var (output, error) = await ExecuteMeasuredAsync("remove_vm", script);
        DebugLogger.LogPowerShellOutput(script, output, error);
        return CaptureFailureMetadataAndReturnSuccess(error);
    }

    public async Task<bool> CreateVhdDifferencingAsync(string parentDiskPath, string vhdPath)
    {
        var script = $"New-VHD -ParentPath '{parentDiskPath}' -Path '{vhdPath}' -Differencing";
        var (output, error) = await ExecuteMeasuredAsync("create_vhd_differencing", script);
        DebugLogger.LogPowerShellOutput(script, output, error);
        return CaptureFailureMetadataAndReturnSuccess(error);
    }

    public async Task<bool> CreateVhdFixedSizeAsync(string vhdPath, long sizeBytes)
    {
        var script = $"New-VHD -Path '{vhdPath}' -SizeBytes {sizeBytes} -Fixed";
        var (output, error) = await ExecuteMeasuredAsync("create_vhd_fixed", script);
        DebugLogger.LogPowerShellOutput(script, output, error);
        return CaptureFailureMetadataAndReturnSuccess(error);
    }

    public async Task<bool> DisableVmCheckpointsAsync(string vmName)
    {
        var script = $"Set-VM -Name '{vmName}' -CheckpointType Disabled";
        var (output, error) = await ExecuteMeasuredAsync("disable_vm_checkpoints", script);
        DebugLogger.LogPowerShellOutput(script, output, error);
        return CaptureFailureMetadataAndReturnSuccess(error);
    }

    public async Task<List<string>> GetVirtualSwitchNamesAsync()
    {
        var script = "Get-VMSwitch | Select-Object -ExpandProperty Name";
        var (output, error) = await ExecuteMeasuredAsync("get_virtual_switch_names", script);
        DebugLogger.LogPowerShellOutput(script, output, error);

        output = PowerShellOutputCleaner.Clean(output);

        var switches = output
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line =>
                !string.IsNullOrWhiteSpace(line) &&
                !line.StartsWith("Get-VMSwitch", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains(":\\")) // eliminate file paths like 'C:\...'
            .ToList();

        return switches;
    }

    public async Task<IReadOnlyList<HyperVVmNetworkAdapterInfo>> GetVmNetworkAdaptersAsync(string vmName)
    {
        var script = string.Join(
            Environment.NewLine,
            $"$items = Get-VMNetworkAdapter -VMName '{vmName}' -ErrorAction Stop |",
            "    Select-Object @{Name='AdapterName';Expression={$_.Name}}, @{Name='SwitchName';Expression={$_.SwitchName}}, @{Name='MacAddress';Expression={$_.MacAddress}} |",
            "    ConvertTo-Json -Depth 3");
        var (output, error) = await ExecuteMeasuredAsync("get_vm_network_adapters", script);
        DebugLogger.LogPowerShellOutput(script, output, error);

        if (!string.IsNullOrWhiteSpace(error))
        {
            CaptureFailureMetadataAndReturnSuccess(error);
            return Array.Empty<HyperVVmNetworkAdapterInfo>();
        }

        ClearLastFailureMetadata();
        output = PowerShellOutputCleaner.Clean(output);
        if (string.IsNullOrWhiteSpace(output))
        {
            return Array.Empty<HyperVVmNetworkAdapterInfo>();
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(output);
            if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                return document.RootElement
                    .EnumerateArray()
                    .Select(MapVmNetworkAdapter)
                    .ToArray();
            }

            if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                return [MapVmNetworkAdapter(document.RootElement)];
            }
        }
        catch
        {
            // Let the empty result surface and fail later with explicit runtime diagnostics.
        }

        return Array.Empty<HyperVVmNetworkAdapterInfo>();
    }

    public async Task<bool> AddVirtualSwitchToVmAsync(string vmName, string switchName)
    {
        var script = $"Connect-VMNetworkAdapter -VMName '{vmName}' -SwitchName '{switchName}'";
        var (output, error) = await ExecuteMeasuredAsync("add_virtual_switch_to_vm", script);
        DebugLogger.LogPowerShellOutput(script, output, error);
        return CaptureFailureMetadataAndReturnSuccess(error);
    }

    public async Task<bool> AddVirtualSwitchesToVmAsync(string vmName, IReadOnlyList<string> switchNames)
    {
        if (switchNames is null || switchNames.Count == 0)
        {
            return true;
        }

        var commands = new List<string>
        {
            $"Connect-VMNetworkAdapter -VMName '{vmName}' -SwitchName '{switchNames[0]}'"
        };

        for (var index = 1; index < switchNames.Count; index++)
        {
            var adapterName = $"Network Adapter {index + 1}";
            commands.Add($"Add-VMNetworkAdapter -VMName '{vmName}' -Name '{adapterName}' -SwitchName '{switchNames[index]}'");
        }

        var script = string.Join(Environment.NewLine, commands);
        var (output, error) = await ExecuteMeasuredAsync("add_virtual_switches_to_vm", script);
        DebugLogger.LogPowerShellOutput(script, output, error);
        return CaptureFailureMetadataAndReturnSuccess(error);
    }

    public void ClearLastFailureMetadata()
    {
        LastFailureMetadata = null;
    }

    private bool CaptureFailureMetadataAndReturnSuccess(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            ClearLastFailureMetadata();
            return true;
        }

        LastFailureMetadata = RuntimeErrorMetadataNormalizer.FromPowerShellErrorText(error);
        return false;
    }

    private async Task<(string Output, string Error)> ExecuteMeasuredAsync(string commandName, string script)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await _session.ExecuteAsync(script);
        stopwatch.Stop();

        HyperVPowerShellTimingLogger.LogWorkflowCommand(
            commandName,
            stopwatch.ElapsedMilliseconds,
            string.IsNullOrWhiteSpace(result.Error));

        return result;
    }

    private static HyperVVmNetworkAdapterInfo MapVmNetworkAdapter(System.Text.Json.JsonElement element)
    {
        return new HyperVVmNetworkAdapterInfo
        {
            AdapterName = element.TryGetProperty("AdapterName", out var adapterName) ? adapterName.GetString() ?? string.Empty : string.Empty,
            SwitchName = element.TryGetProperty("SwitchName", out var switchName) ? switchName.GetString() : null,
            MacAddress = element.TryGetProperty("MacAddress", out var macAddress) ? macAddress.GetString() ?? string.Empty : string.Empty
        };
    }
}
