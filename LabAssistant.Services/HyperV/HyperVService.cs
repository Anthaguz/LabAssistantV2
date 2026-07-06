using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;
using System.Diagnostics;
using System.Text.Json;

namespace LabAssistant.Services.HyperV;

public class HyperVService : IHyperVService, IHyperVFailureDiagnosticsProvider
{
    private readonly IPersistentPowerShellSession _session; // Persistent session per VM
    private readonly IStructuredLogger _structuredLogger;

    public HyperVService(IPersistentPowerShellSession session)
        : this(session, null)
    {
    }

    public HyperVService(IPersistentPowerShellSession session, IStructuredLogger? structuredLogger)
    {
        _session = session;
        _structuredLogger = structuredLogger ?? NullStructuredLogger.Instance;
    }

    public IReadOnlyDictionary<string, object?>? LastFailureMetadata { get; private set; }

    public async Task<bool> CreateVmAsync(string vmName, string vmPath, string vhdPath, int memoryMb, int cpuCount)
    {
        var script = $"New-VM -Name {PowerShellCommandBuilder.Quote(vmName)} -MemoryStartupBytes {memoryMb}MB -Generation 2 -BootDevice VHD -VHDPath {PowerShellCommandBuilder.Quote(vhdPath)} -Path {PowerShellCommandBuilder.Quote(vmPath)}";
        var (output, error) = await ExecuteMeasuredAsync("create_vm", script, vmName);
        DebugLogger.LogPowerShellOutput(script, output, error);
        return CaptureFailureMetadataAndReturnSuccess(error);
    }

    public async Task<bool> EnableGuestServicesAsync(string vmName)
    {
        var script = $"Enable-VMIntegrationService -VMName {PowerShellCommandBuilder.Quote(vmName)} -Name 'Guest Service Interface'";
        var (output, error) = await ExecuteMeasuredAsync("enable_guest_services", script, vmName);
        DebugLogger.LogPowerShellOutput(script, output, error);
        return CaptureFailureMetadataAndReturnSuccess(error);
    }

    public async Task<bool> StartVmAsync(string vmName)
    {
        var script = $"Start-VM -Name {PowerShellCommandBuilder.Quote(vmName)}";
        var (output, error) = await ExecuteMeasuredAsync("start_vm", script, vmName);
        DebugLogger.LogPowerShellOutput(script, output, error);
        return CaptureFailureMetadataAndReturnSuccess(error);
    }

    public async Task<bool> StopVmAsync(string vmName)
    {
        var script = $"Stop-VM -Name {PowerShellCommandBuilder.Quote(vmName)} -Force";
        var (output, error) = await ExecuteMeasuredAsync("stop_vm", script, vmName);
        DebugLogger.LogPowerShellOutput(script, output, error);
        return CaptureFailureMetadataAndReturnSuccess(error);
    }

    public async Task<bool> VmExistsAsync(string vmName)
    {
        var script = $"if (Get-VM -Name {PowerShellCommandBuilder.Quote(vmName)} -ErrorAction SilentlyContinue) {{ 'True' }} else {{ 'False' }}";
        var (output, error) = await ExecuteMeasuredAsync("vm_exists", script, vmName);
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
        var script = $"$vm = Get-VM -Name {PowerShellCommandBuilder.Quote(vmName)} -ErrorAction SilentlyContinue; if ($null -eq $vm) {{ 'Missing' }} else {{ $vm.State.ToString() }}";
        var (output, error) = await ExecuteMeasuredAsync("is_vm_running", script, vmName);
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
        var script = $"Remove-VM -Name {PowerShellCommandBuilder.Quote(vmName)} -Force";
        var (output, error) = await ExecuteMeasuredAsync("remove_vm", script, vmName);
        DebugLogger.LogPowerShellOutput(script, output, error);
        return CaptureFailureMetadataAndReturnSuccess(error);
    }

    public async Task<bool> CreateVhdDifferencingAsync(string parentDiskPath, string vhdPath)
    {
        var script = $"New-VHD -ParentPath {PowerShellCommandBuilder.Quote(parentDiskPath)} -Path {PowerShellCommandBuilder.Quote(vhdPath)} -Differencing";
        var (output, error) = await ExecuteMeasuredAsync("create_vhd_differencing", script, resourcePath: vhdPath);
        DebugLogger.LogPowerShellOutput(script, output, error);
        return CaptureFailureMetadataAndReturnSuccess(error);
    }

    public async Task<bool> CreateVhdFixedSizeAsync(string vhdPath, long sizeBytes)
    {
        var script = $"New-VHD -Path {PowerShellCommandBuilder.Quote(vhdPath)} -SizeBytes {sizeBytes} -Fixed";
        var (output, error) = await ExecuteMeasuredAsync("create_vhd_fixed", script, resourcePath: vhdPath);
        DebugLogger.LogPowerShellOutput(script, output, error);
        return CaptureFailureMetadataAndReturnSuccess(error);
    }

    public async Task<bool> DisableVmCheckpointsAsync(string vmName)
    {
        var script = $"Set-VM -Name {PowerShellCommandBuilder.Quote(vmName)} -CheckpointType Disabled";
        var (output, error) = await ExecuteMeasuredAsync("disable_vm_checkpoints", script, vmName);
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
            $"$items = Get-VMNetworkAdapter -VMName {PowerShellCommandBuilder.Quote(vmName)} -ErrorAction Stop |",
            "    Select-Object @{Name='AdapterName';Expression={$_.Name}}, @{Name='SwitchName';Expression={$_.SwitchName}}, @{Name='MacAddress';Expression={$_.MacAddress}} |",
            "    ConvertTo-Json -Depth 3");
        var (output, error) = await ExecuteMeasuredAsync("get_vm_network_adapters", script, vmName);
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
            using var document = JsonDocument.Parse(output);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                return document.RootElement
                    .EnumerateArray()
                    .Select(MapVmNetworkAdapter)
                    .ToArray();
            }

            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                return [MapVmNetworkAdapter(document.RootElement)];
            }
        }
        catch (JsonException ex)
        {
            // The adapter query succeeded but returned output we could not parse. Surface an empty
            // result so callers fail later with explicit runtime diagnostics, but record why here so
            // the malformed payload is diagnosable instead of being silently swallowed.
            LogStructured(
                StructuredLogLevel.Warn,
                "get_vm_network_adapters_parse",
                Guid.NewGuid().ToString("N"),
                "failure",
                new Dictionary<string, object?>
                {
                    ["vmName"] = vmName,
                    ["exceptionType"] = ex.GetType().Name,
                    ["errorMessage"] = ex.Message
                });
        }

        return Array.Empty<HyperVVmNetworkAdapterInfo>();
    }

    public async Task<bool> AddVirtualSwitchToVmAsync(string vmName, string switchName)
    {
        var script = $"Connect-VMNetworkAdapter -VMName {PowerShellCommandBuilder.Quote(vmName)} -SwitchName {PowerShellCommandBuilder.Quote(switchName)}";
        var (output, error) = await ExecuteMeasuredAsync("add_virtual_switch_to_vm", script, vmName);
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
            $"Connect-VMNetworkAdapter -VMName {PowerShellCommandBuilder.Quote(vmName)} -SwitchName {PowerShellCommandBuilder.Quote(switchNames[0])}"
        };

        for (var index = 1; index < switchNames.Count; index++)
        {
            var adapterName = $"Network Adapter {index + 1}";
            commands.Add($"Add-VMNetworkAdapter -VMName {PowerShellCommandBuilder.Quote(vmName)} -Name {PowerShellCommandBuilder.Quote(adapterName)} -SwitchName {PowerShellCommandBuilder.Quote(switchNames[index])}");
        }

        var script = string.Join(Environment.NewLine, commands);
        var (output, error) = await ExecuteMeasuredAsync("add_virtual_switches_to_vm", script, vmName);
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

    private async Task<(string Output, string Error)> ExecuteMeasuredAsync(
        string commandName,
        string script,
        string? vmName = null,
        string? resourcePath = null,
        CancellationToken cancellationToken = default)
    {
        var operationId = Guid.NewGuid().ToString("N");
        var stopwatch = Stopwatch.StartNew();
        var result = await _session.ExecuteAsync(script, cancellationToken);
        stopwatch.Stop();

        var succeeded = string.IsNullOrWhiteSpace(result.Error);

        HyperVPowerShellTimingLogger.LogWorkflowCommand(
            commandName,
            stopwatch.ElapsedMilliseconds,
            succeeded);

        var context = new Dictionary<string, object?>
        {
            ["durationMs"] = stopwatch.ElapsedMilliseconds
        };

        if (!string.IsNullOrWhiteSpace(vmName))
        {
            context["vmName"] = vmName;
        }

        if (!string.IsNullOrWhiteSpace(resourcePath))
        {
            context["resourcePath"] = resourcePath;
        }

        if (!succeeded)
        {
            context["errorMessage"] = result.Error;
        }

        LogStructured(
            succeeded ? StructuredLogLevel.Info : StructuredLogLevel.Error,
            commandName,
            operationId,
            succeeded ? "success" : "failure",
            context);

        return result;
    }

    private void LogStructured(
        StructuredLogLevel level,
        string commandName,
        string operationId,
        string result,
        IReadOnlyDictionary<string, object?> context)
    {
        _structuredLogger.Log(level, $"hyperv.{commandName}", operationId, result, context);
    }

    private static HyperVVmNetworkAdapterInfo MapVmNetworkAdapter(JsonElement element)
    {
        return new HyperVVmNetworkAdapterInfo
        {
            AdapterName = element.TryGetProperty("AdapterName", out var adapterName) ? adapterName.GetString() ?? string.Empty : string.Empty,
            SwitchName = element.TryGetProperty("SwitchName", out var switchName) ? switchName.GetString() : null,
            MacAddress = element.TryGetProperty("MacAddress", out var macAddress)
                ? MacAddressNormalizer.Normalize(macAddress.GetString())
                : string.Empty
        };
    }
}
