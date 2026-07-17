using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;

namespace LabAssistant.Business.Runtime;

internal sealed class V2NetworkSwitchRuntimeStage
{
    private readonly IHyperVMachineAdminService? _machineAdminService;
    private readonly IStructuredLogger _structuredLogger;

    public V2NetworkSwitchRuntimeStage(
        IHyperVMachineAdminService? machineAdminService,
        IStructuredLogger? structuredLogger)
    {
        _machineAdminService = machineAdminService;
        _structuredLogger = structuredLogger ?? NullStructuredLogger.Instance;
    }

    public IReadOnlyList<V2NetworkSwitchRuntimeContext> InitializeRuntimeState(
        V2RuntimeExecutionRequest request,
        MultiVmDeploymentContext multiContext)
    {
        var states = request.Plan.Context.NetworkSwitchRequirements
            .Select(requirement => new V2NetworkSwitchRuntimeContext
            {
                SwitchName = requirement.SwitchName,
                SwitchType = requirement.SwitchType
            })
            .ToList();

        multiContext.V2NetworkSwitchContexts.AddRange(states);
        return states;
    }

    public async Task ExecuteAsync(
        V2RuntimeExecutionRequest request,
        MultiVmDeploymentContext multiContext,
        IReadOnlyList<V2NetworkSwitchAffectedVmState> affectedVmStates,
        IReadOnlyList<V2NetworkSwitchRuntimeContext> switchStates,
        ISet<string> executedNodeIds,
        CancellationToken cancellationToken)
    {
        if (request.Plan.Context.NetworkSwitchRequirements.Count == 0 || multiContext.IsCancellationRequested)
        {
            return;
        }

        var switchStateByName = switchStates.ToDictionary(state => state.SwitchName, StringComparer.OrdinalIgnoreCase);
        foreach (var requirement in request.Plan.Context.NetworkSwitchRequirements)
        {
            if (multiContext.IsCancellationRequested)
            {
                break;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                multiContext.RequestUserCancellation();
                break;
            }

            if (!switchStateByName.TryGetValue(requirement.SwitchName, out var switchState))
            {
                continue;
            }

            var success = await EnsureNetworkSwitchAsync(requirement, switchState, multiContext, affectedVmStates);
            if (success)
            {
                executedNodeIds.Add(requirement.NodeId);
                continue;
            }

            multiContext.RequestCancellation();
            break;
        }
    }

    public async Task CleanupCreatedAsync(
        MultiVmDeploymentContext multiContext,
        IReadOnlyList<V2NetworkSwitchRuntimeContext> switchStates,
        CancellationToken cancellationToken)
    {
        var needsCleanup = multiContext.IsCancellationRequested ||
                           multiContext.VmContexts.Any(context => !context.IsSuccess) ||
                           multiContext.V2TrustContexts.Any(context => context.TrustObjectsCreated && !context.TrustReady);
        if (!needsCleanup || _machineAdminService is null)
        {
            return;
        }

        foreach (var switchState in switchStates.Where(state => state.CreatedByDeployment))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            multiContext.MarkCleanupInProgress();
            switchState.CleanupAttempted = true;
            EmitDeployEvent(
                LaStatus.DeployNetwork_CleaningUpSwitch,
                multiContext,
                "started",
                extra: new Dictionary<string, object?>
                {
                    ["switchName"] = switchState.SwitchName,
                    ["switchType"] = switchState.SwitchType
                });

            try
            {
                var result = await _machineAdminService.DeleteVirtualSwitchAsync(switchState.SwitchName);
                if (result.Success)
                {
                    EmitDeployEvent(
                        LaStatus.DeployNetwork_SwitchCleanedUp,
                        multiContext,
                        "success",
                        extra: new Dictionary<string, object?>
                        {
                            ["switchName"] = switchState.SwitchName,
                            ["switchType"] = switchState.SwitchType
                        });
                    continue;
                }

                switchState.CleanupResidual = true;
                EmitDeployEvent(
                    LaStatus.DeployNetwork_SwitchCleanupFailed,
                    multiContext,
                    "failed",
                    MergeDictionaries(
                        new Dictionary<string, object?>
                        {
                            ["switchName"] = switchState.SwitchName,
                            ["switchType"] = switchState.SwitchType,
                            ["error"] = result.ErrorMessage
                        },
                        result.FailureMetadata));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                switchState.CleanupResidual = true;
                EmitDeployEvent(
                    LaStatus.DeployNetwork_SwitchCleanupFailedException,
                    multiContext,
                    "exception",
                    new Dictionary<string, object?>
                    {
                        ["switchName"] = switchState.SwitchName,
                        ["switchType"] = switchState.SwitchType,
                        ["exceptionType"] = ex.GetType().Name,
                        ["error"] = ex.Message
                    });
            }
        }
    }

    /// <summary>
    /// Ensures the single network switch referenced by <paramref name="node"/> for the graph scheduler.
    /// The switch-before-VM ordering is enforced by the plan's dependency edges; this method resolves the requirement and
    /// runtime state for the node and runs the existing ensure logic unchanged. Unlike the batch path it does not request
    /// cancellation on failure - it returns <c>false</c> so the scheduler can drain and clean up.
    /// </summary>
    internal async Task<bool> EnsureNetworkSwitchNodeAsync(
        V2RuntimeExecutionRequest request,
        V2PlanNode node,
        IReadOnlyList<V2NetworkSwitchRuntimeContext> switchStates,
        MultiVmDeploymentContext multiContext,
        IReadOnlyList<V2NetworkSwitchAffectedVmState> affectedVmStates,
        ISet<string> executedNodeIds)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(switchStates);
        ArgumentNullException.ThrowIfNull(multiContext);
        ArgumentNullException.ThrowIfNull(affectedVmStates);
        ArgumentNullException.ThrowIfNull(executedNodeIds);

        var requirement = request.Plan.Context.NetworkSwitchRequirements.FirstOrDefault(item =>
            string.Equals(item.NodeId, node.NodeId, StringComparison.Ordinal));
        if (requirement is null)
        {
            return true;
        }

        var switchState = switchStates.FirstOrDefault(state =>
            string.Equals(state.SwitchName, requirement.SwitchName, StringComparison.OrdinalIgnoreCase));
        if (switchState is null)
        {
            return true;
        }

        var success = await EnsureNetworkSwitchAsync(requirement, switchState, multiContext, affectedVmStates);
        if (success)
        {
            executedNodeIds.Add(requirement.NodeId);
        }

        return success;
    }

    private async Task<bool> EnsureNetworkSwitchAsync(
        V2ResolvedNetworkSwitchRequirement requirement,
        V2NetworkSwitchRuntimeContext switchState,
        MultiVmDeploymentContext multiContext,
        IReadOnlyList<V2NetworkSwitchAffectedVmState> affectedVmStates)
    {
        var logContext = new Dictionary<string, object?>
        {
            ["switchName"] = requirement.SwitchName,
            ["switchType"] = requirement.SwitchType,
            ["networkIds"] = requirement.NetworkIds,
            ["affectedVmIds"] = requirement.AffectedVmIds
        };
        EmitDeployEvent(LaStatus.DeployNetwork_EnsuringSwitch, multiContext, "started", extra: logContext);

        if (_machineAdminService is null)
        {
            return FailNetworkSwitchEnsure(
                requirement,
                multiContext,
                affectedVmStates,
                "The V2 runtime cannot ensure network switches because the Hyper-V machine admin service is unavailable.",
                "machine_admin_unavailable");
        }

        try
        {
            var inventory = await _machineAdminService.ListVirtualSwitchesAsync();
            var existing = inventory.FirstOrDefault(item =>
                string.Equals(item.Name, requirement.SwitchName, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                if (!SwitchTypeMatches(existing.SwitchType, requirement.SwitchType))
                {
                    return FailNetworkSwitchEnsure(
                        requirement,
                        multiContext,
                        affectedVmStates,
                        $"Switch '{requirement.SwitchName}' exists as '{existing.SwitchType}', but the V2 template requires '{requirement.SwitchType}'.",
                        "type_mismatch",
                        new Dictionary<string, object?>
                        {
                            ["existingSwitchType"] = existing.SwitchType
                        });
                }

                switchState.Ready = true;
                EmitDeployEvent(LaStatus.DeployNetwork_SwitchEnsured, multiContext, "success", extra: logContext);
                return true;
            }

            if (SwitchTypeMatches(requirement.SwitchType, V2SwitchTypeCatalog.External) &&
                string.IsNullOrWhiteSpace(requirement.ExternalAdapterName))
            {
                return FailNetworkSwitchEnsure(
                    requirement,
                    multiContext,
                    affectedVmStates,
                    $"External switch '{requirement.SwitchName}' requires a deploy-review adapter mapping before creation.",
                    "external_adapter_missing");
            }

            EmitDeployEvent(LaStatus.DeployNetwork_CreatingSwitch, multiContext, "started", extra: logContext);
            var createResult = await _machineAdminService.CreateVirtualSwitchAsync(new HyperVVirtualSwitchCreateRequest
            {
                Name = requirement.SwitchName,
                SwitchType = requirement.SwitchType,
                AdapterName = requirement.ExternalAdapterName
            });

            if (!createResult.Success)
            {
                return FailNetworkSwitchEnsure(
                    requirement,
                    multiContext,
                    affectedVmStates,
                    $"Failed to create switch '{requirement.SwitchName}'. {createResult.ErrorMessage}".Trim(),
                    "create_failed",
                    createResult.FailureMetadata);
            }

            switchState.CreatedByDeployment = true;
            switchState.Ready = true;
            EmitDeployEvent(LaStatus.DeployNetwork_SwitchCreated, multiContext, "success", extra: logContext);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return FailNetworkSwitchEnsure(
                requirement,
                multiContext,
                affectedVmStates,
                $"Failed to ensure switch '{requirement.SwitchName}'. {ex.Message}".Trim(),
                "exception",
                new Dictionary<string, object?>
                {
                    ["exceptionType"] = ex.GetType().Name
                });
        }
    }

    private bool FailNetworkSwitchEnsure(
        V2ResolvedNetworkSwitchRequirement requirement,
        MultiVmDeploymentContext multiContext,
        IReadOnlyList<V2NetworkSwitchAffectedVmState> affectedVmStates,
        string message,
        string result,
        IReadOnlyDictionary<string, object?>? extra = null)
    {
        var context = new Dictionary<string, object?>
        {
            ["switchName"] = requirement.SwitchName,
            ["switchType"] = requirement.SwitchType,
            ["networkIds"] = requirement.NetworkIds,
            ["affectedVmIds"] = requirement.AffectedVmIds
        };
        if (extra != null)
        {
            foreach (var pair in extra)
            {
                context[pair.Key] = pair.Value;
            }
        }

        EmitDeployEvent(LaStatus.DeployNetwork_EnsureSwitchFailed, multiContext, result, context);

        var stateByVmId = affectedVmStates.ToDictionary(state => state.VmId, StringComparer.OrdinalIgnoreCase);
        var targetStates = requirement.AffectedVmIds
            .Where(vmId => stateByVmId.ContainsKey(vmId))
            .Select(vmId => stateByVmId[vmId])
            .ToList();
        if (targetStates.Count == 0)
        {
            targetStates = affectedVmStates.ToList();
        }

        foreach (var state in targetStates)
        {
            state.Context.MarkFailure(
                DeploymentStepKeys.V2EnsureNetworkSwitch,
                message,
                context);
        }

        return false;
    }

    private void EmitDeployEvent(
        uint code,
        MultiVmDeploymentContext multiContext,
        string? result,
        IReadOnlyDictionary<string, object?>? extra = null)
    {
        var context = new Dictionary<string, object?>
        {
            ["vmCount"] = multiContext.VmContexts.Count,
            ["stopAllOnAnyVmFailure"] = multiContext.StopAllOnAnyVmFailure,
            ["executionEngine"] = "V2"
        };

        if (extra != null)
        {
            foreach (var pair in extra)
            {
                context[pair.Key] = pair.Value;
            }
        }

        _structuredLogger.Log(code, multiContext.OperationId, result, context);
    }

    private static IReadOnlyDictionary<string, object?> MergeDictionaries(
        IReadOnlyDictionary<string, object?> first,
        IReadOnlyDictionary<string, object?>? second)
    {
        if (second is null)
        {
            return first;
        }

        var payload = new Dictionary<string, object?>(first);
        foreach (var pair in second)
        {
            payload[pair.Key] = pair.Value;
        }

        return payload;
    }

    private static bool SwitchTypeMatches(string? actual, string expected)
        => V2SwitchTypeCatalog.TryNormalize(actual, out var normalizedActual) &&
           string.Equals(normalizedActual, expected, StringComparison.OrdinalIgnoreCase);
}

internal sealed record V2NetworkSwitchAffectedVmState(string VmId, VmDeploymentContext Context);
