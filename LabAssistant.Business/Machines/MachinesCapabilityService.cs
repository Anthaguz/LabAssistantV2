using LabAssistant.Models.Configuration;
using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;

namespace LabAssistant.Business.Machines;

public sealed class MachinesCapabilityService : IMachinesCapabilityService
{
    private readonly IHyperVMachineAdminService _machineAdminService;
    private readonly IAppSettingsStore _settingsStore;
    private readonly IStructuredLogger _structuredLogger;

    public MachinesCapabilityService(
        IHyperVMachineAdminService machineAdminService,
        IAppSettingsStore settingsStore,
        IStructuredLogger? structuredLogger = null)
    {
        _machineAdminService = machineAdminService;
        _settingsStore = settingsStore;
        _structuredLogger = structuredLogger ?? NullStructuredLogger.Instance;
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
            "delete" => $"Failed to delete '{vmName}'.",
            _ => $"Failed to run '{actionName}' for '{vmName}'."
        };

        if (string.IsNullOrWhiteSpace(details))
        {
            return fallback;
        }

        return $"{fallback} {details}";
    }
}
