using LabAssistant.Models.Configuration;
using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;
using System.Net;
using System.Net.Sockets;

namespace LabAssistant.Business.Machines;

public sealed class MachinesCapabilityService : IMachinesCapabilityService
{
    private const int RdpProbeTimeoutMs = 4000;

    private readonly IHyperVMachineAdminService _machineAdminService;
    private readonly IAppSettingsStore _settingsStore;
    private readonly IStructuredLogger _structuredLogger;
    private readonly Func<string, int, int, CancellationToken, Task<bool>> _tcpProbe;

    public MachinesCapabilityService(
        IHyperVMachineAdminService machineAdminService,
        IAppSettingsStore settingsStore,
        IStructuredLogger? structuredLogger = null,
        Func<string, int, int, CancellationToken, Task<bool>>? tcpProbe = null)
    {
        _machineAdminService = machineAdminService;
        _settingsStore = settingsStore;
        _structuredLogger = structuredLogger ?? NullStructuredLogger.Instance;
        _tcpProbe = tcpProbe ?? ProbeTcpAsync;
    }

    public async Task<IReadOnlyList<MachineInventoryItem>> LoadInventoryAsync()
    {
        var operationId = Guid.NewGuid().ToString("N");
        _structuredLogger.Log(
            StructuredLogLevel.Info,
            "MachineInventoryLoadStarted",
            operationId,
            "started");

        try
        {
            var vms = await _machineAdminService.ListHostVmsAsync();
            var vmBasePath = _settingsStore.Settings.VmBasePath;
            var mapped = vms.Select(vm => MapToInventoryItem(vm, vmBasePath)).ToList();

            _structuredLogger.Log(
                StructuredLogLevel.Info,
                "MachineInventoryLoadCompleted",
                operationId,
                "success",
                new Dictionary<string, object?>
                {
                    ["vmCount"] = mapped.Count
                });

            return mapped;
        }
        catch (Exception ex)
        {
            _structuredLogger.Log(
                StructuredLogLevel.Error,
                "MachineInventoryLoadFailed",
                operationId,
                "failed",
                RuntimeErrorMetadataNormalizer.FromException(ex));
            throw;
        }
    }

    public async Task<MachineEditSnapshot?> LoadEditSnapshotAsync(MachineInventoryItem vm)
    {
        var operationId = Guid.NewGuid().ToString("N");
        var context = BuildVmContext(vm, "load_edit_snapshot");
        _structuredLogger.Log(
            StructuredLogLevel.Info,
            "MachineEditLoadStarted",
            operationId,
            "started",
            context);

        try
        {
            var snapshot = await _machineAdminService.GetVmEditSnapshotAsync(vm.VmName);
            if (snapshot is null)
            {
                _structuredLogger.Log(
                    StructuredLogLevel.Warn,
                    "MachineEditLoadCompleted",
                    operationId,
                    "not_found",
                    context);
                return null;
            }

            var mapped = MapEditSnapshot(snapshot);
            _structuredLogger.Log(
                StructuredLogLevel.Info,
                "MachineEditLoadCompleted",
                operationId,
                "success",
                context);
            return mapped;
        }
        catch (Exception ex)
        {
            _structuredLogger.Log(
                StructuredLogLevel.Error,
                "MachineEditLoadFailed",
                operationId,
                "failed",
                RuntimeErrorMetadataNormalizer.FromException(ex));
            throw;
        }
    }

    public async Task<IReadOnlyList<string>> LoadVirtualSwitchesAsync()
    {
        return await _machineAdminService.GetVirtualSwitchNamesAsync();
    }

    public Task<MachineOperationResult> StartVmAsync(MachineInventoryItem vm)
    {
        return ExecuteVmActionAsync(vm, "start", "MachineAction", _machineAdminService.StartVmAsync);
    }

    public Task<MachineOperationResult> StopVmAsync(MachineInventoryItem vm)
    {
        return ExecuteVmActionAsync(vm, "stop", "MachineAction", _machineAdminService.StopVmAsync);
    }

    public Task<MachineOperationResult> RestartVmAsync(MachineInventoryItem vm)
    {
        return ExecuteVmActionAsync(vm, "restart", "MachineAction", _machineAdminService.RestartVmAsync);
    }

    public Task<MachineOperationResult> OpenConsoleAsync(MachineInventoryItem vm)
    {
        return ExecuteVmActionAsync(vm, "open_console", "MachineConnectionAction", _machineAdminService.OpenConsoleAsync);
    }

    public async Task<MachineRdpReadinessResult> EvaluateRdpReadinessAsync(
        MachineInventoryItem vm,
        CancellationToken cancellationToken = default)
    {
        var operationId = Guid.NewGuid().ToString("N");
        var context = BuildVmContext(vm, "rdp_readiness");

        if (!string.Equals(vm.State, "Running", StringComparison.OrdinalIgnoreCase))
        {
            return LogReadinessResult(
                operationId,
                context,
                MachineRdpReadinessState.NotReady,
                MachineRdpReadinessReasonCodes.VmNotRunning,
                "VM must be running to use RDP.");
        }

        try
        {
            var ipAddresses = await _machineAdminService.GetVmIpAddressesAsync(vm.VmName);
            var candidateIpv4s = SelectIpv4Candidates(ipAddresses);
            if (candidateIpv4s.Count == 0)
            {
                return LogReadinessResult(
                    operationId,
                    context,
                    MachineRdpReadinessState.NotReady,
                    MachineRdpReadinessReasonCodes.NoIpv4,
                    "No IPv4 address is currently available for this VM.");
            }

            string? reachableIpv4 = null;
            foreach (var candidateIpv4 in candidateIpv4s)
            {
                if (await _tcpProbe(candidateIpv4, 3389, RdpProbeTimeoutMs, cancellationToken))
                {
                    reachableIpv4 = candidateIpv4;
                    break;
                }
            }

            if (reachableIpv4 is null)
            {
                return LogReadinessResult(
                    operationId,
                    context,
                    MachineRdpReadinessState.NotReady,
                    MachineRdpReadinessReasonCodes.Port3389Unreachable,
                    "RDP port 3389 is not reachable from the host.",
                    candidateIpv4s[0]);
            }

            return LogReadinessResult(
                operationId,
                context,
                MachineRdpReadinessState.Ready,
                MachineRdpReadinessReasonCodes.Ready,
                "RDP is ready.",
                reachableIpv4);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var detail = ex.Message;
            var firstLine = detail
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            var userMessage = string.IsNullOrWhiteSpace(firstLine)
                ? "RDP readiness check failed."
                : $"RDP readiness check failed: {firstLine}";

            var result = LogReadinessResult(
                operationId,
                context,
                MachineRdpReadinessState.Unknown,
                MachineRdpReadinessReasonCodes.CheckFailed,
                userMessage);

            _structuredLogger.Log(
                StructuredLogLevel.Warn,
                "MachineRdpReadinessCheckFailed",
                operationId,
                "failed",
                RuntimeErrorMetadataNormalizer.FromException(ex));

            return result;
        }
    }

    public async Task<MachineOperationResult> OpenRdpAsync(MachineInventoryItem vm, string targetIpv4)
    {
        var operationId = Guid.NewGuid().ToString("N");
        var actionName = "open_rdp";
        var context = BuildVmContext(vm, actionName);
        context["targetIpv4"] = targetIpv4;

        _structuredLogger.Log(
            StructuredLogLevel.Info,
            "MachineConnectionActionStarted",
            operationId,
            "started",
            context);

        var actionResult = await _machineAdminService.OpenRdpAsync(targetIpv4);
        if (actionResult.Success)
        {
            _structuredLogger.Log(
                StructuredLogLevel.Info,
                "MachineConnectionActionCompleted",
                operationId,
                "success",
                context);

            return new MachineOperationResult
            {
                Success = true,
                OperationId = operationId,
                UserMessage = $"Opened RDP for '{vm.VmName}' using {targetIpv4}."
            };
        }

        var failureContext = MergeFailureContext(context, actionResult);
        _structuredLogger.Log(
            StructuredLogLevel.Error,
            "MachineConnectionActionFailed",
            operationId,
            "failed",
            failureContext);

        return new MachineOperationResult
        {
            Success = false,
            OperationId = operationId,
            UserMessage = BuildActionFailureMessage(actionName, vm.VmName, actionResult.ErrorMessage),
            ErrorContext = failureContext
        };
    }

    public async Task<MachineOperationResult> ApplyEditsAsync(MachineInventoryItem vm, MachineEditDraft draft)
    {
        var operationId = Guid.NewGuid().ToString("N");
        var context = BuildVmContext(vm, "apply_edit");
        context["changedFields"] = draft.ChangedFieldKeys.ToArray();
        context["networkAdapterCount"] = draft.NetworkAdapters.Count;

        _structuredLogger.Log(
            StructuredLogLevel.Info,
            "MachineEditApplyStarted",
            operationId,
            "started",
            context);

        var request = new HyperVMachineEditRequest
        {
            ProcessorCount = draft.CpuCount,
            StartupMemoryBytes = MbToBytes(draft.StartupMemoryMb),
            DynamicMemoryEnabled = draft.DynamicMemoryEnabled,
            MinimumMemoryBytes = MbToBytes(draft.MinimumMemoryMb),
            MaximumMemoryBytes = MbToBytes(draft.MaximumMemoryMb),
            MemoryBufferPercent = draft.MemoryBufferPercent,
            NetworkAdapterAssignments = draft.NetworkAdapters
                .Where(adapter => !string.IsNullOrWhiteSpace(adapter.AdapterName) && !string.IsNullOrWhiteSpace(adapter.SwitchName))
                .Select(adapter => new HyperVMachineNetworkAdapterAssignment
                {
                    AdapterName = adapter.AdapterName,
                    SwitchName = adapter.SwitchName!
                })
                .ToList()
        };

        var result = await _machineAdminService.ApplyVmEditAsync(vm.VmName, request);
        if (result.Success)
        {
            _structuredLogger.Log(
                StructuredLogLevel.Info,
                "MachineEditApplyCompleted",
                operationId,
                "success",
                context);

            return new MachineOperationResult
            {
                Success = true,
                OperationId = operationId,
                UserMessage = $"Applied edits to '{vm.VmName}'."
            };
        }

        var failureContext = MergeFailureContext(context, result);
        _structuredLogger.Log(
            StructuredLogLevel.Error,
            "MachineEditApplyFailed",
            operationId,
            "failed",
            failureContext);

        return new MachineOperationResult
        {
            Success = false,
            OperationId = operationId,
            UserMessage = BuildActionFailureMessage("apply_edit", vm.VmName, result.ErrorMessage),
            ErrorContext = failureContext
        };
    }

    public async Task<MachineOperationResult> DeleteVmAsync(MachineInventoryItem vm, MachineDeleteScope scope)
    {
        var operationId = Guid.NewGuid().ToString("N");
        var actionName = "delete";
        var deleteScope = scope == MachineDeleteScope.VmAndStorage ? "vm_and_storage" : "vm_registration_only";
        var context = BuildVmContext(vm, actionName);
        context["deleteScope"] = deleteScope;

        _structuredLogger.Log(
            StructuredLogLevel.Info,
            "MachineDeleteStarted",
            operationId,
            "started",
            context);

        var includeStorage = scope == MachineDeleteScope.VmAndStorage;
        var actionResult = await _machineAdminService.DeleteVmAsync(vm.VmName, includeStorage);

        if (actionResult.Success)
        {
            _structuredLogger.Log(
                StructuredLogLevel.Info,
                "MachineDeleteCompleted",
                operationId,
                "success",
                context);

            return new MachineOperationResult
            {
                Success = true,
                OperationId = operationId,
                UserMessage = includeStorage
                    ? $"Deleted '{vm.VmName}' and associated storage."
                    : $"Deleted VM registration for '{vm.VmName}'."
            };
        }

        var failureContext = MergeFailureContext(context, actionResult);
        _structuredLogger.Log(
            StructuredLogLevel.Error,
            "MachineDeleteFailed",
            operationId,
            "failed",
            failureContext);

        return new MachineOperationResult
        {
            Success = false,
            OperationId = operationId,
            UserMessage = BuildActionFailureMessage("delete", vm.VmName, actionResult.ErrorMessage),
            ErrorContext = failureContext
        };
    }

    private async Task<MachineOperationResult> ExecuteVmActionAsync(
        MachineInventoryItem vm,
        string actionName,
        string eventPrefix,
        Func<string, Task<HyperVMachineActionResult>> action)
    {
        var operationId = Guid.NewGuid().ToString("N");
        var context = BuildVmContext(vm, actionName);

        _structuredLogger.Log(
            StructuredLogLevel.Info,
            $"{eventPrefix}Started",
            operationId,
            "started",
            context);

        var actionResult = await action(vm.VmName);
        if (actionResult.Success)
        {
            _structuredLogger.Log(
                StructuredLogLevel.Info,
                $"{eventPrefix}Completed",
                operationId,
                "success",
                context);

            return new MachineOperationResult
            {
                Success = true,
                OperationId = operationId,
                UserMessage = BuildActionSuccessMessage(actionName, vm.VmName)
            };
        }

        var failureContext = MergeFailureContext(context, actionResult);
        _structuredLogger.Log(
            StructuredLogLevel.Error,
            $"{eventPrefix}Failed",
            operationId,
            "failed",
            failureContext);

        return new MachineOperationResult
        {
            Success = false,
            OperationId = operationId,
            UserMessage = BuildActionFailureMessage(actionName, vm.VmName, actionResult.ErrorMessage),
            ErrorContext = failureContext
        };
    }

    private static Dictionary<string, object?> BuildVmContext(MachineInventoryItem vm, string action)
    {
        return new Dictionary<string, object?>
        {
            ["action"] = action,
            ["vmName"] = vm.VmName,
            ["vmId"] = string.IsNullOrWhiteSpace(vm.VmId) ? null : vm.VmId,
            ["vmState"] = vm.State,
            ["vmOrigin"] = vm.OriginLabel
        };
    }

    private static IReadOnlyDictionary<string, object?> MergeFailureContext(
        IReadOnlyDictionary<string, object?> context,
        HyperVMachineActionResult result)
    {
        var merged = new Dictionary<string, object?>(context);
        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            merged["errorMessage"] = result.ErrorMessage;
        }

        if (result.FailureMetadata is null)
        {
            return merged;
        }

        foreach (var pair in result.FailureMetadata)
        {
            if (!merged.ContainsKey(pair.Key))
            {
                merged[pair.Key] = pair.Value;
            }
        }

        return merged;
    }

    private static MachineInventoryItem MapToInventoryItem(HyperVHostMachineVmInfo vm, string vmBasePath)
    {
        return new MachineInventoryItem
        {
            VmId = vm.VmId,
            VmName = vm.VmName,
            State = vm.State,
            VmPath = vm.VmPath,
            DiskPaths = vm.DiskPaths,
            OriginLabel = DetectOrigin(vm, vmBasePath)
        };
    }

    private static string DetectOrigin(HyperVHostMachineVmInfo vm, string vmBasePath)
    {
        if (!string.IsNullOrWhiteSpace(vmBasePath) &&
            !string.IsNullOrWhiteSpace(vm.VmPath) &&
            vm.VmPath.StartsWith(vmBasePath, StringComparison.OrdinalIgnoreCase))
        {
            return "LabAssistant";
        }

        return "External/Unknown";
    }

    private static string BuildActionSuccessMessage(string actionName, string vmName)
    {
        return actionName switch
        {
            "start" => $"Started '{vmName}'.",
            "stop" => $"Stopped '{vmName}'.",
            "restart" => $"Restarted '{vmName}'.",
            "open_console" => $"Opened Hyper-V Console for '{vmName}'.",
            "apply_edit" => $"Applied edits for '{vmName}'.",
            _ => $"Completed '{actionName}' for '{vmName}'."
        };
    }

    private static string BuildActionFailureMessage(string actionName, string vmName, string? details)
    {
        var fallback = actionName switch
        {
            "start" => $"Failed to start '{vmName}'.",
            "stop" => $"Failed to stop '{vmName}'.",
            "restart" => $"Failed to restart '{vmName}'.",
            "open_console" => $"Failed to open Hyper-V Console for '{vmName}'.",
            "open_rdp" => $"Failed to open RDP for '{vmName}'.",
            "apply_edit" => $"Failed to apply edits for '{vmName}'.",
            "delete" => $"Failed to delete '{vmName}'.",
            _ => $"Failed to run '{actionName}' for '{vmName}'."
        };

        if (string.IsNullOrWhiteSpace(details))
        {
            return fallback;
        }

        return $"{fallback} {details}";
    }

    private MachineRdpReadinessResult LogReadinessResult(
        string operationId,
        Dictionary<string, object?> context,
        MachineRdpReadinessState state,
        string reasonCode,
        string message,
        string? targetIpv4 = null)
    {
        context["readinessState"] = state.ToString();
        context["readinessReasonCode"] = reasonCode;
        context["targetIpv4"] = targetIpv4;

        _structuredLogger.Log(
            StructuredLogLevel.Info,
            "MachineRdpReadinessEvaluated",
            operationId,
            state == MachineRdpReadinessState.Ready ? "ready" : "not_ready",
            context);

        return new MachineRdpReadinessResult
        {
            OperationId = operationId,
            State = state,
            ReasonCode = reasonCode,
            Message = message,
            TargetIpv4 = targetIpv4
        };
    }

    private static async Task<bool> ProbeTcpAsync(
        string host,
        int port,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeoutMs);

        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(host, port, timeoutCts.Token);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout reached: treat as port unreachable, not as an explicit user/system cancellation.
            return false;
        }
    }

    private static IReadOnlyList<string> SelectIpv4Candidates(IReadOnlyList<string> ipAddresses)
    {
        static int Score(IPAddress address)
        {
            var bytes = address.GetAddressBytes();

            // Prefer RFC1918 addresses over APIPA/link-local ranges.
            if (bytes[0] == 10 ||
                (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                (bytes[0] == 192 && bytes[1] == 168))
            {
                return 0;
            }

            if (bytes[0] == 169 && bytes[1] == 254)
            {
                return 2;
            }

            return 1;
        }

        var candidates = new List<(string Text, int Score)>();
        foreach (var candidate in ipAddresses)
        {
            if (!IPAddress.TryParse(candidate, out var address))
            {
                continue;
            }

            if (address.AddressFamily != AddressFamily.InterNetwork)
            {
                continue;
            }

            candidates.Add((candidate, Score(address)));
        }

        return candidates
            .OrderBy(entry => entry.Score)
            .Select(entry => entry.Text)
            .ToList();
    }

    private static MachineEditSnapshot MapEditSnapshot(HyperVMachineEditSnapshot snapshot)
    {
        return new MachineEditSnapshot
        {
            CpuCount = snapshot.ProcessorCount,
            StartupMemoryMb = BytesToMb(snapshot.StartupMemoryBytes),
            DynamicMemoryEnabled = snapshot.DynamicMemoryEnabled,
            MinimumMemoryMb = BytesToMb(snapshot.MinimumMemoryBytes),
            MaximumMemoryMb = BytesToMb(snapshot.MaximumMemoryBytes),
            MemoryBufferPercent = snapshot.MemoryBufferPercent,
            NetworkAdapters = snapshot.NetworkAdapters
                .Select(adapter => new MachineNetworkAdapterConfig
                {
                    AdapterName = adapter.AdapterName,
                    SwitchName = adapter.SwitchName
                })
                .ToList()
        };
    }

    private static long MbToBytes(long mb)
    {
        return mb * 1024L * 1024L;
    }

    private static long BytesToMb(long bytes)
    {
        return bytes / (1024L * 1024L);
    }
}
