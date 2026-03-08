using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;

namespace LabAssistant.Business.Assets;

public sealed class AssetsSwitchesCapabilityService : IAssetsSwitchesCapabilityService
{
    private static readonly HashSet<string> SupportedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "External",
        "Internal",
        "Private"
    };

    private readonly IHyperVMachineAdminService _machineAdminService;
    private readonly IStructuredLogger _structuredLogger;

    public AssetsSwitchesCapabilityService(
        IHyperVMachineAdminService machineAdminService,
        IStructuredLogger? structuredLogger = null)
    {
        _machineAdminService = machineAdminService;
        _structuredLogger = structuredLogger ?? NullStructuredLogger.Instance;
    }

    public async Task<AssetsSwitchesInventoryResult> LoadAsync(bool isRefresh = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var operationId = Guid.NewGuid().ToString("N");
        var eventName = isRefresh ? "SwitchRefreshCompleted" : "SwitchListLoaded";

        _structuredLogger.Log(
            StructuredLogLevel.Info,
            isRefresh ? "SwitchRefreshRequested" : "SwitchListRequested",
            operationId,
            "started");

        try
        {
            var items = await _machineAdminService.ListVirtualSwitchesAsync();
            var mapped = items.Select(MapRecord).ToList();

            _structuredLogger.Log(
                StructuredLogLevel.Info,
                eventName,
                operationId,
                "success",
                new Dictionary<string, object?>
                {
                    ["switchCount"] = mapped.Count
                });

            return new AssetsSwitchesInventoryResult
            {
                OperationId = operationId,
                Items = mapped
            };
        }
        catch (Exception ex)
        {
            _structuredLogger.Log(
                StructuredLogLevel.Warn,
                isRefresh ? "SwitchRefreshFailed" : "SwitchListFailed",
                operationId,
                "failed",
                new Dictionary<string, object?>
                {
                    ["error"] = ex.Message
                });

            return new AssetsSwitchesInventoryResult
            {
                OperationId = operationId,
                Errors = [ex.Message]
            };
        }
    }

    public async Task<AssetsSwitchValidationResult> ValidateAsync(AssetsSwitchDraft draft, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var operationId = Guid.NewGuid().ToString("N");
        var inventory = await SafeListAsync();
        var result = ValidateCore(draft, inventory);

        _structuredLogger.Log(
            string.Equals(result.Severity, "Pass", StringComparison.OrdinalIgnoreCase) ? StructuredLogLevel.Info : StructuredLogLevel.Warn,
            "SwitchValidationEvaluated",
            operationId,
            result.Severity.ToLowerInvariant(),
            new Dictionary<string, object?>
            {
                ["switchName"] = draft.Name,
                ["switchType"] = draft.SwitchType,
                ["detailCount"] = result.Details.Count
            });

        return new AssetsSwitchValidationResult
        {
            OperationId = operationId,
            Severity = result.Severity,
            Summary = result.Summary,
            Details = result.Details
        };
    }

    public async Task<IReadOnlyList<string>> GetAttachedVmNamesAsync(string switchName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var operationId = Guid.NewGuid().ToString("N");

        _structuredLogger.Log(
            StructuredLogLevel.Info,
            "SwitchAttachedVmListRequested",
            operationId,
            "started",
            new Dictionary<string, object?>
            {
                ["switchName"] = switchName
            });

        try
        {
            var attachedVmNames = await _machineAdminService.GetAttachedVmNamesForSwitchAsync(switchName);
            _structuredLogger.Log(
                StructuredLogLevel.Info,
                "SwitchAttachedVmListLoaded",
                operationId,
                "success",
                new Dictionary<string, object?>
                {
                    ["switchName"] = switchName,
                    ["attachedVmCount"] = attachedVmNames.Count
                });

            return attachedVmNames;
        }
        catch (Exception ex)
        {
            _structuredLogger.Log(
                StructuredLogLevel.Warn,
                "SwitchAttachedVmListFailed",
                operationId,
                "failed",
                new Dictionary<string, object?>
                {
                    ["switchName"] = switchName,
                    ["error"] = ex.Message
                });

            return Array.Empty<string>();
        }
    }

    public async Task<AssetsSwitchOperationResult> SaveAsync(AssetsSwitchDraft draft, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var operationId = Guid.NewGuid().ToString("N");

        _structuredLogger.Log(
            StructuredLogLevel.Info,
            draft.IsNew ? "SwitchCreateStarted" : "SwitchUpdateStarted",
            operationId,
            "started",
            new Dictionary<string, object?>
            {
                ["switchName"] = draft.Name,
                ["switchType"] = draft.SwitchType,
                ["originalName"] = draft.OriginalName
            });

        try
        {
            var inventory = await _machineAdminService.ListVirtualSwitchesAsync();
            var validation = ValidateCore(draft, inventory);
            if (!string.Equals(validation.Severity, "Pass", StringComparison.Ordinal))
            {
                return CreateFailure(
                    operationId,
                    validation.Summary,
                    validation.Details,
                    draft,
                    draft.IsNew ? "SwitchCreateFailed" : "SwitchUpdateFailed");
            }

            HyperVMachineActionResult actionResult;
            string userMessage;
            if (draft.IsNew)
            {
                actionResult = await _machineAdminService.CreateVirtualSwitchAsync(new HyperVVirtualSwitchCreateRequest
                {
                    Name = draft.Name,
                    SwitchType = draft.SwitchType,
                    AdapterName = NormalizeOptional(draft.AdapterName)
                });
                userMessage = "Virtual switch created successfully.";
            }
            else if (!string.Equals(draft.OriginalName, draft.Name, StringComparison.OrdinalIgnoreCase))
            {
                actionResult = await _machineAdminService.RenameVirtualSwitchAsync(draft.OriginalName!, draft.Name);
                userMessage = "Virtual switch updated successfully.";
            }
            else
            {
                actionResult = new HyperVMachineActionResult { Success = true };
                userMessage = "Virtual switch already matches the requested configuration.";
            }

            if (!actionResult.Success)
            {
                var summarizedError = SummarizeSwitchActionError(actionResult.ErrorMessage, "Switch operation failed.");
                return CreateFailure(
                    operationId,
                    summarizedError,
                    [summarizedError],
                    draft,
                    draft.IsNew ? "SwitchCreateFailed" : "SwitchUpdateFailed");
            }

            _structuredLogger.Log(
                StructuredLogLevel.Info,
                draft.IsNew ? "SwitchCreated" : "SwitchUpdated",
                operationId,
                "success",
                new Dictionary<string, object?>
                {
                    ["switchName"] = draft.Name,
                    ["switchType"] = draft.SwitchType
                });

            return new AssetsSwitchOperationResult
            {
                Success = true,
                OperationId = operationId,
                UserMessage = userMessage,
                Item = new AssetsSwitchRecord
                {
                    Name = draft.Name,
                    SwitchType = draft.SwitchType,
                    AdapterName = NormalizeOptional(draft.AdapterName)
                }
            };
        }
        catch (Exception ex)
        {
            return CreateFailure(
                operationId,
                ex.Message,
                [ex.Message],
                draft,
                draft.IsNew ? "SwitchCreateFailed" : "SwitchUpdateFailed");
        }
    }

    public async Task<AssetsSwitchDeleteAssessment> AssessDeleteAsync(string switchName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var operationId = Guid.NewGuid().ToString("N");
        try
        {
            var inventory = await SafeListAsync();
            var item = inventory.FirstOrDefault(entry => string.Equals(entry.Name, switchName, StringComparison.OrdinalIgnoreCase));
            if (item is null)
            {
                return new AssetsSwitchDeleteAssessment
                {
                    OperationId = operationId,
                    Exists = false,
                    CanDelete = false,
                    BlockingReasons = ["The selected virtual switch was not found."],
                    Summary = "No delete assessment available."
                };
            }

            var attachedVmNames = await _machineAdminService.GetAttachedVmNamesForSwitchAsync(item.Name);
            if (attachedVmNames.Count > 0)
            {
                var blockingReasons = attachedVmNames
                    .Select(vmName => $"VM '{vmName}' is attached to this switch.")
                    .ToList();

                _structuredLogger.Log(
                    StructuredLogLevel.Warn,
                    "SwitchDeleteBlocked",
                    operationId,
                    "blocked",
                    new Dictionary<string, object?>
                    {
                        ["switchName"] = item.Name,
                        ["switchType"] = item.SwitchType,
                        ["attachedVmCount"] = attachedVmNames.Count
                    });

                return new AssetsSwitchDeleteAssessment
                {
                    OperationId = operationId,
                    Exists = true,
                    CanDelete = false,
                    BlockingReasons = blockingReasons,
                    AttachedVmNames = attachedVmNames,
                    Summary = "Delete is blocked because at least one VM is attached to this switch."
                };
            }

            return new AssetsSwitchDeleteAssessment
            {
                OperationId = operationId,
                Exists = true,
                CanDelete = true,
                Summary = "No attached VMs found. Confirmation is still required before delete."
            };
        }
        catch (Exception ex)
        {
            return new AssetsSwitchDeleteAssessment
            {
                OperationId = operationId,
                Exists = false,
                CanDelete = false,
                BlockingReasons = [ex.Message],
                Summary = "Delete assessment failed."
            };
        }
    }

    public async Task<AssetsSwitchOperationResult> DeleteAsync(string switchName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var operationId = Guid.NewGuid().ToString("N");

        _structuredLogger.Log(
            StructuredLogLevel.Info,
            "SwitchDeleteStarted",
            operationId,
            "started",
            new Dictionary<string, object?>
            {
                ["switchName"] = switchName
            });

        try
        {
            var assessment = await AssessDeleteAsync(switchName, cancellationToken);
            if (!assessment.Exists || !assessment.CanDelete)
            {
                _structuredLogger.Log(
                    StructuredLogLevel.Warn,
                    "SwitchDeleteBlocked",
                    operationId,
                    "blocked",
                    new Dictionary<string, object?>
                    {
                        ["switchName"] = switchName,
                        ["blockingReasonCount"] = assessment.BlockingReasons.Count
                    });

                return new AssetsSwitchOperationResult
                {
                    Success = false,
                    OperationId = operationId,
                    UserMessage = assessment.BlockingReasons.FirstOrDefault() ?? "Virtual switch delete is blocked.",
                    Errors = assessment.BlockingReasons
                };
            }

            var inventory = await _machineAdminService.ListVirtualSwitchesAsync();
            var item = inventory.FirstOrDefault(entry => string.Equals(entry.Name, switchName, StringComparison.OrdinalIgnoreCase));

            var result = await _machineAdminService.DeleteVirtualSwitchAsync(switchName);
            if (!result.Success)
            {
                _structuredLogger.Log(
                    StructuredLogLevel.Warn,
                    "SwitchDeleteFailed",
                    operationId,
                    "failed",
                    new Dictionary<string, object?>
                    {
                        ["switchName"] = switchName,
                        ["error"] = result.ErrorMessage
                    });

                return new AssetsSwitchOperationResult
                {
                    Success = false,
                    OperationId = operationId,
                    UserMessage = SummarizeSwitchActionError(result.ErrorMessage, "Virtual switch delete failed."),
                    Errors = [SummarizeSwitchActionError(result.ErrorMessage, "Virtual switch delete failed.")]
                };
            }

            _structuredLogger.Log(
                StructuredLogLevel.Info,
                "SwitchDeleted",
                operationId,
                "success",
                new Dictionary<string, object?>
                {
                    ["switchName"] = switchName,
                    ["switchType"] = item?.SwitchType
                });

            return new AssetsSwitchOperationResult
            {
                Success = true,
                OperationId = operationId,
                UserMessage = "Virtual switch deleted successfully.",
                Item = item is null ? null : MapRecord(item)
            };
        }
        catch (Exception ex)
        {
            _structuredLogger.Log(
                StructuredLogLevel.Warn,
                "SwitchDeleteFailed",
                operationId,
                "failed",
                new Dictionary<string, object?>
                {
                    ["switchName"] = switchName,
                    ["error"] = ex.Message
                });

                return new AssetsSwitchOperationResult
                {
                    Success = false,
                    OperationId = operationId,
                    UserMessage = SummarizeSwitchActionError(ex.Message, "Virtual switch delete failed."),
                    Errors = [SummarizeSwitchActionError(ex.Message, "Virtual switch delete failed.")]
                };
            }
    }

    private async Task<IReadOnlyList<HyperVVirtualSwitchInfo>> SafeListAsync()
    {
        return await _machineAdminService.ListVirtualSwitchesAsync();
    }

    private AssetsSwitchValidationResult ValidateCore(AssetsSwitchDraft draft, IReadOnlyList<HyperVVirtualSwitchInfo> inventory)
    {
        var details = new List<string>();
        var name = draft.Name.Trim();
        var switchType = draft.SwitchType.Trim();
        var adapterName = NormalizeOptional(draft.AdapterName);

        if (string.IsNullOrWhiteSpace(name))
        {
            details.Add("Switch name is required.");
        }

        if (!SupportedTypes.Contains(switchType))
        {
            details.Add("Switch type must be External, Internal, or Private.");
        }

        if (string.Equals(switchType, "External", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(adapterName))
        {
            details.Add("External switches require an adapter / target value.");
        }

        var existing = inventory.FirstOrDefault(item =>
            string.Equals(item.Name, draft.OriginalName ?? draft.Name, StringComparison.OrdinalIgnoreCase));

        var duplicate = inventory.FirstOrDefault(item =>
            string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(item.Name, draft.OriginalName ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        if (duplicate is not null)
        {
            details.Add($"Switch '{name}' already exists on this host.");
        }

        if (!draft.IsNew && existing is null)
        {
            details.Add("The selected virtual switch no longer exists on this host. Refresh and try again.");
        }

        if (!draft.IsNew && existing is not null)
        {
            if (!string.Equals(existing.SwitchType, switchType, StringComparison.OrdinalIgnoreCase))
            {
                details.Add("Switch type changes are not supported. Create a new switch instead.");
            }

            var existingAdapter = NormalizeOptional(existing.AdapterName);
            if (string.Equals(existing.SwitchType, "External", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(existingAdapter, adapterName, StringComparison.OrdinalIgnoreCase))
            {
                details.Add("External adapter rebinding is not supported here. Create a new switch instead.");
            }
        }

        if (details.Count > 0)
        {
            return new AssetsSwitchValidationResult
            {
                Severity = "Block",
                Summary = details[0],
                Details = details
            };
        }

        return new AssetsSwitchValidationResult
        {
            Severity = "Pass",
            Summary = draft.IsNew
                ? "This switch can be created with the current settings."
                : "This switch can be updated with the current settings.",
            Details = Array.Empty<string>()
        };
    }

    private AssetsSwitchOperationResult CreateFailure(
        string operationId,
        string userMessage,
        IReadOnlyList<string> errors,
        AssetsSwitchDraft draft,
        string eventName)
    {
        _structuredLogger.Log(
            StructuredLogLevel.Warn,
            eventName,
            operationId,
            "failed",
            new Dictionary<string, object?>
            {
                ["switchName"] = draft.Name,
                ["switchType"] = draft.SwitchType,
                ["errorCount"] = errors.Count
            });

        return new AssetsSwitchOperationResult
        {
            Success = false,
            OperationId = operationId,
            UserMessage = userMessage,
            Errors = errors
        };
    }

    private static AssetsSwitchRecord MapRecord(HyperVVirtualSwitchInfo item)
    {
        return new AssetsSwitchRecord
        {
            Name = item.Name,
            SwitchType = item.SwitchType,
            AdapterName = NormalizeOptional(item.AdapterName)
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string SummarizeSwitchActionError(string? rawMessage, string fallback)
    {
        var firstLine = (rawMessage ?? fallback)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));

        return string.IsNullOrWhiteSpace(firstLine) ? fallback : firstLine;
    }
}
