using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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
        // New-VM defaults ProcessorCount to 1, so the requested cpuCount must be applied
        // explicitly with Set-VMProcessor or every deployed VM silently gets a single vCPU.
        var effectiveCpuCount = cpuCount < 1 ? 1 : cpuCount;
        var script = $"New-VM -Name {PowerShellCommandBuilder.Quote(vmName)} -MemoryStartupBytes {memoryMb}MB -Generation 2 -BootDevice VHD -VHDPath {PowerShellCommandBuilder.Quote(vhdPath)} -Path {PowerShellCommandBuilder.Quote(vmPath)} -ErrorAction Stop; Set-VMProcessor -VMName {PowerShellCommandBuilder.Quote(vmName)} -Count {effectiveCpuCount} -ErrorAction Stop";
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
        var script = BuildGetVmNetworkAdaptersScript(vmName);
        var (output, error) = await ExecuteMeasuredAsync("get_vm_network_adapters", script, vmName);
        DebugLogger.LogPowerShellOutput(script, output, error);

        // Parse the payload FIRST, before deciding whether a non-empty error stream is fatal. The query runs with
        // -ErrorAction Stop, so a TERMINATING failure (VM not found, access denied) stops the pipeline before
        // ConvertTo-Json runs and leaves the success stream empty. That means a non-empty error stream arriving
        // ALONGSIDE a valid adapter payload can only be a NON-terminating error/warning (module-autoload noise, a
        // per-adapter CIM hiccup): the adapters it returned are real. Blanking the whole list on any stderr would
        // silently reproduce the no-adapters/no-IP failure, so we only treat a non-empty error as fatal when there
        // is no usable payload to return.
        if (TryParseVmNetworkAdapters(output, vmName, out var adapters))
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                LogStructured(
                    LaStatus.Hyperv_VMNetworkAdapterStderrTolerated,
                    Guid.NewGuid().ToString("N"),
                    "tolerated",
                    new Dictionary<string, object?>
                    {
                        ["vmName"] = vmName,
                        ["adapterCount"] = adapters.Count,
                        ["error"] = error
                    });
            }

            ClearLastFailureMetadata();
            return adapters;
        }

        // No usable adapter payload. A non-empty error is now a genuine failure worth capturing for diagnostics so
        // callers surface an actionable runtime error instead of a silently-empty adapter list.
        if (!string.IsNullOrWhiteSpace(error))
        {
            CaptureFailureMetadataAndReturnSuccess(error);
            return Array.Empty<HyperVVmNetworkAdapterInfo>();
        }

        ClearLastFailureMetadata();
        return Array.Empty<HyperVVmNetworkAdapterInfo>();
    }

    // Cleans and parses the Get-VMNetworkAdapter JSON payload into adapter records. Returns false (with an empty
    // list) when the payload is empty or unusable so the caller can decide how to treat an accompanying error
    // stream. A malformed-but-non-empty payload is recorded as a parse warning here rather than swallowed silently.
    private bool TryParseVmNetworkAdapters(
        string rawOutput,
        string vmName,
        out IReadOnlyList<HyperVVmNetworkAdapterInfo> adapters)
    {
        adapters = Array.Empty<HyperVVmNetworkAdapterInfo>();

        var output = PowerShellOutputCleaner.Clean(rawOutput);
        if (string.IsNullOrWhiteSpace(output))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(output);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                adapters = document.RootElement
                    .EnumerateArray()
                    .Select(MapVmNetworkAdapter)
                    .ToArray();
                return true;
            }

            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                adapters = [MapVmNetworkAdapter(document.RootElement)];
                return true;
            }
        }
        catch (JsonException ex)
        {
            // The adapter query succeeded but returned output we could not parse. Record why here so the malformed
            // payload is diagnosable instead of being silently swallowed; the caller returns an empty result so
            // downstream resolution fails later with explicit runtime diagnostics.
            LogStructured(
                LaStatus.Hyperv_VMNetworkAdapterParseWarning,
                Guid.NewGuid().ToString("N"),
                "failure",
                new Dictionary<string, object?>
                {
                    ["vmName"] = vmName,
                    ["exceptionType"] = ex.GetType().Name,
                    ["errorMessage"] = ex.Message
                });
        }

        return false;
    }

    /// <summary>
    /// Builds the PowerShell that queries a VM's network adapters and projects them to JSON.
    /// The pipeline must terminate in <c>ConvertTo-Json</c> writing to the success stream: a prior
    /// version captured the pipeline into a local variable instead of emitting it, so the
    /// persistent session captured empty stdout, <see cref="GetVmNetworkAdaptersAsync"/> returned no
    /// adapters, and every guest-network deploy failed to resolve a NIC. Keep the terminal emit.
    /// </summary>
    internal static string BuildGetVmNetworkAdaptersScript(string vmName)
    {
        return string.Join(
            Environment.NewLine,
            $"Get-VMNetworkAdapter -VMName {PowerShellCommandBuilder.Quote(vmName)} -ErrorAction Stop |",
            "    Select-Object @{Name='AdapterName';Expression={$_.Name}}, @{Name='SwitchName';Expression={$_.SwitchName}}, @{Name='MacAddress';Expression={$_.MacAddress}}, @{Name='Status';Expression={($_.Status -join ',')}}, @{Name='Connected';Expression={$_.Connected}}, @{Name='IsManagementOs';Expression={$_.IsManagementOs}} |",
            "    ConvertTo-Json -Depth 3");
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
            ResolveHyperVCode(commandName, succeeded),
            operationId,
            succeeded ? "success" : "failure",
            context);

        return result;
    }

    private void LogStructured(
        uint code,
        string operationId,
        string result,
        IReadOnlyDictionary<string, object?> context,
        [CallerFilePath] string? callerFilePath = null,
        [CallerLineNumber] int callerLineNumber = 0)
    {
        _structuredLogger.Log(code, operationId, result, context, callerFilePath, callerLineNumber);
    }

    // Maps the internal Hyper-V command name to its status code. Kept as an explicit switch so a new
    // command must consciously register its success/failure codes rather than silently logging uncoded.
    private static uint ResolveHyperVCode(string commandName, bool succeeded) => commandName switch
    {
        "create_vm" => succeeded ? LaStatus.Hyperv_VMCreated : LaStatus.Hyperv_VMCreateFailed,
        "start_vm" => succeeded ? LaStatus.Hyperv_VMStarted : LaStatus.Hyperv_VMStartFailed,
        "stop_vm" => succeeded ? LaStatus.Hyperv_VMStopped : LaStatus.Hyperv_VMStopFailed,
        "remove_vm" => succeeded ? LaStatus.Hyperv_VMRemoved : LaStatus.Hyperv_VMRemoveFailed,
        "vm_exists" => succeeded ? LaStatus.Hyperv_VMExistenceChecked : LaStatus.Hyperv_VMExistenceCheckFailed,
        "is_vm_running" => succeeded ? LaStatus.Hyperv_VMRunningStateChecked : LaStatus.Hyperv_VMRunningCheckFailed,
        "create_vhd_differencing" => succeeded ? LaStatus.Hyperv_DifferencingDiskCreated : LaStatus.Hyperv_DifferencingDiskCreateFailed,
        "create_vhd_fixed" => succeeded ? LaStatus.Hyperv_FixedDiskCreated : LaStatus.Hyperv_FixedDiskCreateFailed,
        "disable_vm_checkpoints" => succeeded ? LaStatus.Hyperv_CheckpointsDisabled : LaStatus.Hyperv_DisableCheckpointsFailed,
        "enable_guest_services" => succeeded ? LaStatus.Hyperv_GuestServicesEnabled : LaStatus.Hyperv_EnableGuestServicesFailed,
        "get_virtual_switch_names" => succeeded ? LaStatus.Hyperv_VirtualSwitchesListed : LaStatus.Hyperv_ListVirtualSwitchesFailed,
        "get_vm_network_adapters" => succeeded ? LaStatus.Hyperv_VMNetworkAdaptersListed : LaStatus.Hyperv_ListVMNetworkAdaptersFailed,
        "add_virtual_switch_to_vm" => succeeded ? LaStatus.Hyperv_SwitchAttachedToVM : LaStatus.Hyperv_AttachSwitchToVMFailed,
        "add_virtual_switches_to_vm" => succeeded ? LaStatus.Hyperv_SwitchesAttachedToVM : LaStatus.Hyperv_AttachSwitchesToVMFailed,
        _ => throw new ArgumentOutOfRangeException(nameof(commandName), commandName, "No status code registered for this Hyper-V command.")
    };

    private static HyperVVmNetworkAdapterInfo MapVmNetworkAdapter(JsonElement element)
    {
        return new HyperVVmNetworkAdapterInfo
        {
            AdapterName = element.TryGetProperty("AdapterName", out var adapterName) ? adapterName.GetString() ?? string.Empty : string.Empty,
            SwitchName = element.TryGetProperty("SwitchName", out var switchName) ? switchName.GetString() : null,
            MacAddress = element.TryGetProperty("MacAddress", out var macAddress)
                ? MacAddressNormalizer.NormalizeMacAddress(macAddress.GetString())
                : string.Empty,
            Status = element.TryGetProperty("Status", out var status)
                ? NullIfEmpty(status.GetString())
                : null,
            Connected = ReadNullableBool(element, "Connected"),
            IsManagementOs = ReadNullableBool(element, "IsManagementOs")
        };
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static bool? ReadNullableBool(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            }
            : null;
}
