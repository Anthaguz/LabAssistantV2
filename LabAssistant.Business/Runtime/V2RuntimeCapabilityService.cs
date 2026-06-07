using System.Security.Cryptography;
using System.Text;
using LabAssistant.Business.Deployment;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.Services.GuestExecution;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;

namespace LabAssistant.Business.Runtime;

public sealed class V2RuntimeCapabilityService : IV2RuntimeCapabilityService
{
    private readonly Func<IPersistentPowerShellSession> _sessionFactory;
    private readonly Func<IPersistentPowerShellSession, IHyperVService> _hyperVFactory;
    private readonly IGuestCommandExecutor _guestCommandExecutor;
    private readonly IVmCleanupOrchestrator _cleanupOrchestrator;
    private readonly IStructuredLogger _structuredLogger;

    public V2RuntimeCapabilityService(
        Func<IPersistentPowerShellSession> sessionFactory,
        Func<IPersistentPowerShellSession, IHyperVService> hyperVFactory,
        IGuestCommandExecutor guestCommandExecutor,
        IVmCleanupOrchestrator cleanupOrchestrator,
        IStructuredLogger? structuredLogger = null)
    {
        _sessionFactory = sessionFactory;
        _hyperVFactory = hyperVFactory;
        _guestCommandExecutor = guestCommandExecutor;
        _cleanupOrchestrator = cleanupOrchestrator;
        _structuredLogger = structuredLogger ?? NullStructuredLogger.Instance;
    }

    public async Task<V2RuntimeExecutionResult> ExecuteAsync(
        V2RuntimeExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Template);
        ArgumentNullException.ThrowIfNull(request.Plan);
        ArgumentNullException.ThrowIfNull(request.Settings);

        var multiContext = request.DeploymentContext ?? new MultiVmDeploymentContext();
        var blockingMessages = ValidateRequest(request);
        if (blockingMessages.Count > 0)
        {
            return new V2RuntimeExecutionResult
            {
                Success = false,
                DeploymentContext = multiContext,
                BlockingMessages = blockingMessages
            };
        }

        InitializeDeploymentContext(multiContext, request.Settings);
        var states = BuildRuntimeStates(request, multiContext);
        var stateByVmId = states.ToDictionary(state => state.PlanVm.VmId, StringComparer.OrdinalIgnoreCase);
        foreach (var state in states)
        {
            state.Context.StructuredEventEmitter = (eventName, level, result, extraContext) =>
                EmitVmScopedEvent(eventName, level, multiContext, state.Context, result, extraContext);
        }

        var executedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var deferredNodeIds = new HashSet<string>(
            request.Plan.Nodes
                .Where(node => node.Kind is not V2PlanNodeKind.ProvisionVm
                    and not V2PlanNodeKind.StartVm
                    and not V2PlanNodeKind.GuestTransportReady)
                .Select(node => node.NodeId),
            StringComparer.Ordinal);

        EnsureOperationId(multiContext);
        using var cancellationRegistration = cancellationToken.Register(multiContext.RequestUserCancellation);

        multiContext.MarkRunning();
        EmitDeployEvent("DeployLabStarted", multiContext, "started");

        try
        {
            var rootStates = states
                .Where(state => string.Equals(state.PlanVm.TopologyRole, "RootDomainController", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (rootStates.Count > 0)
            {
                await ExecuteCriticalRootStageAsync(rootStates, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
            }

            var shouldContinue = !multiContext.IsCancellationRequested && rootStates.All(state => state.Context.IsSuccess);
            if (shouldContinue)
            {
                var nonRootStates = states
                    .Where(state => !string.Equals(state.PlanVm.TopologyRole, "RootDomainController", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                await ExecuteRemainingStageAsync(
                    request,
                    multiContext,
                    nonRootStates,
                    stateByVmId,
                    executedNodeIds,
                    deferredNodeIds,
                    rootStates.Select(state => state.PlanVm.VmId).ToHashSet(StringComparer.OrdinalIgnoreCase),
                    cancellationToken);
            }

            await CleanupFailedOrCancelledVmsAsync(multiContext, states);
        }
        finally
        {
            foreach (var state in states)
            {
                EmitVmTerminalEvent(multiContext, state.Context);
                state.Dispose();
            }

            var hasFailures = multiContext.VmContexts.Any(vm => !vm.IsSuccess);
            var hasCleanupResiduals = multiContext.CleanupResults.Any(result => result.HasResiduals);
            multiContext.CompleteTerminalState(hasFailures, hasCleanupResiduals);
            EmitDeployTerminalEvent(multiContext);
        }

        return new V2RuntimeExecutionResult
        {
            Success = multiContext.OperationState == DeploymentOperationState.Completed,
            DeploymentContext = multiContext,
            ExecutedNodeIds = executedNodeIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            DeferredNodeIds = deferredNodeIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            BlockingMessages = blockingMessages
        };
    }

    private async Task ExecuteCriticalRootStageAsync(
        IReadOnlyList<RuntimeVmState> rootStates,
        V2RuntimeExecutionRequest request,
        MultiVmDeploymentContext multiContext,
        ISet<string> executedNodeIds,
        ISet<string> deferredNodeIds,
        CancellationToken cancellationToken)
    {
        await ExecuteNodeSetAsync(rootStates, V2PlanNodeKind.ProvisionVm, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteNodeSetAsync(rootStates, V2PlanNodeKind.StartVm, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteNodeSetAsync(rootStates, V2PlanNodeKind.GuestTransportReady, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await EmitDeferredBootstrapStepsAsync(rootStates, multiContext, deferredNodeIds);
    }

    private async Task ExecuteRemainingStageAsync(
        V2RuntimeExecutionRequest request,
        MultiVmDeploymentContext multiContext,
        IReadOnlyList<RuntimeVmState> nonRootStates,
        IReadOnlyDictionary<string, RuntimeVmState> stateByVmId,
        ISet<string> executedNodeIds,
        ISet<string> deferredNodeIds,
        ISet<string> rootVmIds,
        CancellationToken cancellationToken)
    {
        if (nonRootStates.Count == 0)
        {
            return;
        }

        switch (request.Plan.Context.ResolvedDeploymentProfile)
        {
            case V2DeploymentProfile.Conservative:
                await ExecuteRemainingConservativeAsync(request, multiContext, stateByVmId, executedNodeIds, deferredNodeIds, rootVmIds, cancellationToken);
                break;

            case V2DeploymentProfile.Balanced:
                await ExecuteRemainingBalancedAsync(nonRootStates, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
                break;

            default:
                await ExecuteRemainingAggressiveAsync(nonRootStates, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
                break;
        }
    }

    private async Task ExecuteRemainingConservativeAsync(
        V2RuntimeExecutionRequest request,
        MultiVmDeploymentContext multiContext,
        IReadOnlyDictionary<string, RuntimeVmState> stateByVmId,
        ISet<string> executedNodeIds,
        ISet<string> deferredNodeIds,
        ISet<string> rootVmIds,
        CancellationToken cancellationToken)
    {
        foreach (var wave in request.Plan.Waves.OrderBy(w => w.WaveNumber))
        {
            if (multiContext.IsCancellationRequested)
            {
                break;
            }

            var waveNodes = wave.NodeIds
                .Select(nodeId => request.Plan.Nodes.First(node => node.NodeId == nodeId))
                .Where(node => !rootVmIds.Contains(node.VmId))
                .ToList();

            if (waveNodes.Count == 0)
            {
                continue;
            }

            var runnableNodes = waveNodes
                .Where(node => node.Kind is V2PlanNodeKind.ProvisionVm
                    or V2PlanNodeKind.StartVm
                    or V2PlanNodeKind.GuestTransportReady)
                .ToList();

            if (runnableNodes.Count > 0)
            {
                await Task.WhenAll(runnableNodes.Select(node =>
                    ExecutePlanNodeAsync(stateByVmId[node.VmId], node, request, multiContext, executedNodeIds, cancellationToken)));
            }

            var bootstrapNodes = waveNodes.Where(node => node.Kind == V2PlanNodeKind.BootstrapGuestNetwork).ToList();
            if (bootstrapNodes.Count > 0)
            {
                await Task.WhenAll(bootstrapNodes.Select(node =>
                    EmitDeferredBootstrapStepAsync(stateByVmId[node.VmId], node, multiContext, deferredNodeIds)));
            }
        }
    }

    private async Task ExecuteRemainingBalancedAsync(
        IReadOnlyList<RuntimeVmState> nonRootStates,
        V2RuntimeExecutionRequest request,
        MultiVmDeploymentContext multiContext,
        ISet<string> executedNodeIds,
        ISet<string> deferredNodeIds,
        CancellationToken cancellationToken)
    {
        await ExecuteNodeSetAsync(nonRootStates, V2PlanNodeKind.ProvisionVm, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteNodeSetAsync(nonRootStates, V2PlanNodeKind.StartVm, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteNodeSetAsync(nonRootStates, V2PlanNodeKind.GuestTransportReady, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await EmitDeferredBootstrapStepsAsync(nonRootStates, multiContext, deferredNodeIds);
    }

    private async Task ExecuteRemainingAggressiveAsync(
        IReadOnlyList<RuntimeVmState> nonRootStates,
        V2RuntimeExecutionRequest request,
        MultiVmDeploymentContext multiContext,
        ISet<string> executedNodeIds,
        ISet<string> deferredNodeIds,
        CancellationToken cancellationToken)
    {
        await Task.WhenAll(nonRootStates.Select(async state =>
        {
            if (multiContext.IsCancellationRequested)
            {
                return;
            }

            foreach (var kind in new[] { V2PlanNodeKind.ProvisionVm, V2PlanNodeKind.StartVm, V2PlanNodeKind.GuestTransportReady })
            {
                var node = state.TryGetNode(kind);
                if (node is not null)
                {
                    await ExecutePlanNodeAsync(state, node, request, multiContext, executedNodeIds, cancellationToken);
                }
            }

            var bootstrapNode = state.TryGetNode(V2PlanNodeKind.BootstrapGuestNetwork);
            if (bootstrapNode is not null)
            {
                await EmitDeferredBootstrapStepAsync(state, bootstrapNode, multiContext, deferredNodeIds);
            }
        }));
    }

    private async Task ExecuteNodeSetAsync(
        IReadOnlyList<RuntimeVmState> states,
        V2PlanNodeKind kind,
        V2RuntimeExecutionRequest request,
        MultiVmDeploymentContext multiContext,
        ISet<string> executedNodeIds,
        ISet<string> deferredNodeIds,
        CancellationToken cancellationToken)
    {
        if (multiContext.IsCancellationRequested)
        {
            return;
        }

        var nodes = states
            .Select(state => state.TryGetNode(kind))
            .Where(node => node is not null)
            .Cast<V2PlanNode>()
            .ToList();

        if (nodes.Count == 0)
        {
            return;
        }

        var stateByVmId = states.ToDictionary(state => state.PlanVm.VmId, StringComparer.OrdinalIgnoreCase);
        await Task.WhenAll(nodes.Select(node =>
            ExecutePlanNodeAsync(stateByVmId[node.VmId], node, request, multiContext, executedNodeIds, cancellationToken)));
    }

    private async Task ExecutePlanNodeAsync(
        RuntimeVmState state,
        V2PlanNode node,
        V2RuntimeExecutionRequest request,
        MultiVmDeploymentContext multiContext,
        ISet<string> executedNodeIds,
        CancellationToken cancellationToken)
    {
        switch (node.Kind)
        {
            case V2PlanNodeKind.ProvisionVm:
                await ExecuteRuntimeStepAsync(
                    state.Context,
                    DeploymentStepKeys.V2ProvisionVm,
                    "V2 provision VM",
                    context => ProvisionVmAsync(state, context),
                    multiContext,
                    cancellationToken);
                executedNodeIds.Add(node.NodeId);
                break;

            case V2PlanNodeKind.StartVm:
                await ExecuteRuntimeStepAsync(
                    state.Context,
                    DeploymentStepKeys.V2StartVm,
                    "V2 start VM",
                    context => StartVmAsync(state, context),
                    multiContext,
                    cancellationToken);
                executedNodeIds.Add(node.NodeId);
                break;

            case V2PlanNodeKind.GuestTransportReady:
                await ExecuteRuntimeStepAsync(
                    state.Context,
                    DeploymentStepKeys.V2GuestTransportReady,
                    "Wait for guest transport",
                    context => WaitForGuestTransportAsync(state, request, context, cancellationToken),
                    multiContext,
                    cancellationToken);
                executedNodeIds.Add(node.NodeId);
                break;
        }
    }

    private async Task EmitDeferredBootstrapStepsAsync(
        IReadOnlyList<RuntimeVmState> states,
        MultiVmDeploymentContext multiContext,
        ISet<string> deferredNodeIds)
    {
        await Task.WhenAll(states.Select(state =>
        {
            var node = state.TryGetNode(V2PlanNodeKind.BootstrapGuestNetwork);
            return node is null
                ? Task.CompletedTask
                : EmitDeferredBootstrapStepAsync(state, node, multiContext, deferredNodeIds);
        }));
    }

    private async Task EmitDeferredBootstrapStepAsync(
        RuntimeVmState state,
        V2PlanNode node,
        MultiVmDeploymentContext multiContext,
        ISet<string> deferredNodeIds)
    {
        deferredNodeIds.Add(node.NodeId);
        await ExecuteRuntimeStepAsync(
            state.Context,
            DeploymentStepKeys.V2BootstrapGuestNetwork,
            "Bootstrap guest network",
            context =>
            {
                context.SetStepTerminalOverride(
                    DeploymentStepKeys.V2BootstrapGuestNetwork,
                    DeployStepState.Skipped,
                    "Deferred in V2 runtime slice #728.");
                return Task.CompletedTask;
            },
            multiContext,
            CancellationToken.None);
    }

    private async Task ProvisionVmAsync(RuntimeVmState state, VmDeploymentContext context)
    {
        var hyperV = state.GetOrCreateHyperV(_sessionFactory, _hyperVFactory);
        if (!Directory.Exists(context.VmPath))
        {
            Directory.CreateDirectory(context.VmPath);
        }

        context.VmFolderCreated = true;

        if (!await hyperV.CreateVhdDifferencingAsync(context.BaseVhdPath, context.VhdPath))
        {
            context.MarkFailure(
                DeploymentStepKeys.V2ProvisionVm,
                $"Failed to create differencing disk for '{context.VmName}'.",
                MergeFailureMetadata(hyperV, new Dictionary<string, object?>
                {
                    ["parentVhdPath"] = context.BaseVhdPath,
                    ["targetVhdPath"] = context.VhdPath
                }));
            return;
        }

        context.DifferencingDiskCreated = true;

        if (!await hyperV.CreateVmAsync(context.VmName, context.VmPath, context.VhdPath, context.MemoryMb, context.CpuCount))
        {
            context.MarkFailure(
                DeploymentStepKeys.V2ProvisionVm,
                $"Failed to create VM '{context.VmName}'.",
                MergeFailureMetadata(hyperV, new Dictionary<string, object?>
                {
                    ["vmPath"] = context.VmPath,
                    ["targetVhdPath"] = context.VhdPath
                }));
            return;
        }

        context.VmRegistered = true;

        if (!await hyperV.AddVirtualSwitchesToVmAsync(context.VmName, context.VirtualSwitchNames))
        {
            context.MarkFailure(
                DeploymentStepKeys.V2ProvisionVm,
                $"Failed to attach switches to '{context.VmName}'.");
            return;
        }

        if (!await hyperV.EnableGuestServicesAsync(context.VmName))
        {
            context.MarkFailure(
                DeploymentStepKeys.V2ProvisionVm,
                $"Failed to enable guest services on '{context.VmName}'.");
            return;
        }

        context.GuestServicesEnabled = true;

        if (!await hyperV.DisableVmCheckpointsAsync(context.VmName))
        {
            context.MarkFailure(
                DeploymentStepKeys.V2ProvisionVm,
                $"Failed to disable checkpoints on '{context.VmName}'.");
        }
    }

    private async Task StartVmAsync(RuntimeVmState state, VmDeploymentContext context)
    {
        var hyperV = state.GetOrCreateHyperV(_sessionFactory, _hyperVFactory);
        if (!await hyperV.StartVmAsync(context.VmName))
        {
            context.MarkFailure(DeploymentStepKeys.V2StartVm, $"Failed to start VM '{context.VmName}'.");
            return;
        }

        context.VmStarted = true;
    }

    private async Task WaitForGuestTransportAsync(
        RuntimeVmState state,
        V2RuntimeExecutionRequest request,
        VmDeploymentContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(state.PlanVm.EffectiveBootstrapCredentialSlot))
        {
            context.MarkFailure(
                DeploymentStepKeys.V2GuestTransportReady,
                $"VM '{context.VmName}' is missing a resolved bootstrap credential slot for PowerShell Direct access.");
            return;
        }

        if (!request.CredentialSlotValues.TryGetValue(state.PlanVm.EffectiveBootstrapCredentialSlot, out var credential))
        {
            context.MarkFailure(
                DeploymentStepKeys.V2GuestTransportReady,
                $"VM '{context.VmName}' is missing runtime credential material for slot '{state.PlanVm.EffectiveBootstrapCredentialSlot}'.");
            return;
        }

        string? lastError = null;
        for (var attempt = 1; attempt <= request.GuestTransportMaxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (context.ShouldAbort?.Invoke() == true)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            var result = await _guestCommandExecutor.ExecutePowerShellDirectAsync(
                context.VmName,
                credential,
                "$env:COMPUTERNAME",
                cancellationToken);

            if (result.Success)
            {
                context.V2GuestTransportReady = true;
                context.LogCallback?.Invoke($"PowerShell Direct is ready on '{context.VmName}'.");
                return;
            }

            lastError = result.Error;
            if (attempt < request.GuestTransportMaxRetries)
            {
                await Task.Delay(request.GuestTransportRetryDelay, cancellationToken);
            }
        }

        context.MarkFailure(
            DeploymentStepKeys.V2GuestTransportReady,
            $"PowerShell Direct did not become ready on '{context.VmName}'. Last error: {lastError ?? "unknown"}");
    }

    private async Task ExecuteRuntimeStepAsync(
        VmDeploymentContext context,
        string stepKey,
        string stepLabel,
        Func<VmDeploymentContext, Task> action,
        MultiVmDeploymentContext multiContext,
        CancellationToken cancellationToken)
    {
        if (context.ShouldAbort?.Invoke() == true || multiContext.IsCancellationRequested)
        {
            context.MarkCancelled();
            return;
        }

        if (!context.IsSuccess && context.PerVmFailFast)
        {
            return;
        }

        context.EmitStepState(stepKey, stepLabel, DeployStepState.Pending);
        context.EmitStepState(stepKey, stepLabel, DeployStepState.Running);
        EmitStepEvent(context, multiContext, "StepStarted", "info", null, stepKey);

        try
        {
            await action(context);
        }
        catch (OperationCanceledException)
        {
            context.MarkCancelled();
            context.SetStepTerminalOverride(stepKey, DeployStepState.Skipped, "Cancelled before completion.");
        }
        catch (Exception ex)
        {
            context.MarkFailure(
                stepKey,
                ex.Message,
                new Dictionary<string, object?>
                {
                    ["exceptionType"] = ex.GetType().Name
                });
        }

        var terminalState = context.TryConsumeStepTerminalOverride(stepKey, out var overrideState, out var overrideMessage)
            ? overrideState
            : context.IsSuccess
                ? DeployStepState.Succeeded
                : context.WasCancelled
                    ? DeployStepState.Skipped
                    : DeployStepState.Failed;

        var result = terminalState switch
        {
            DeployStepState.Succeeded => "success",
            DeployStepState.Skipped => "skipped",
            _ => "failed"
        };

        EmitStepEvent(context, multiContext, "StepCompleted", "info", result, stepKey);
        context.EmitStepState(stepKey, stepLabel, terminalState, overrideMessage);
    }

    private async Task CleanupFailedOrCancelledVmsAsync(
        MultiVmDeploymentContext multiContext,
        IReadOnlyList<RuntimeVmState> states)
    {
        foreach (var state in states.Where(NeedsCleanup))
        {
            multiContext.MarkCleanupInProgress();
            EmitVmScopedEvent("CleanupStarted", "info", multiContext, state.Context, "started");
            var cleanupResult = await _cleanupOrchestrator.CleanupAsync(state.Context, state.GetOrCreateHyperV(_sessionFactory, _hyperVFactory));
            state.Context.CleanupResult = cleanupResult;
            multiContext.CleanupResults.Add(cleanupResult);
            EmitVmScopedEvent(
                "CleanupCompleted",
                cleanupResult.HasResiduals ? "warn" : "info",
                multiContext,
                state.Context,
                cleanupResult.HasResiduals ? "completed_with_residuals" : "completed");
        }
    }

    private static bool NeedsCleanup(RuntimeVmState state)
    {
        var context = state.Context;
        return (!context.IsSuccess || context.WasCancelled) &&
               (context.VmFolderCreated || context.DifferencingDiskCreated || context.VmRegistered || context.VmStarted);
    }

    private static List<string> ValidateRequest(V2RuntimeExecutionRequest request)
    {
        var messages = new List<string>();
        if (request.Plan.Context.ExecutionEngine != TemplateExecutionEngine.V2UnifiedPlanning)
        {
            messages.Add("V2 runtime requires a V2 plan built from a V2 template.");
        }

        if (!request.Plan.Success)
        {
            messages.Add("V2 runtime cannot execute a plan that still has blocking planning issues.");
        }

        if (request.Plan.Issues.Any(issue => issue.Severity == V2PlanIssueSeverity.Blocking))
        {
            messages.AddRange(request.Plan.Issues
                .Where(issue => issue.Severity == V2PlanIssueSeverity.Blocking)
                .Select(issue => issue.Message));
        }

        if (request.Plan.UnresolvedRequirements.Count > 0)
        {
            messages.AddRange(request.Plan.UnresolvedRequirements.Select(requirement => requirement.Description));
        }

        if (request.Template.ExecutionEngine == TemplateExecutionEngine.V1Deployment ||
            TemplateSchemaVersionCatalog.Classify(request.Template.SchemaVersion) == TemplateExecutionEngine.V1Deployment)
        {
            messages.Add("V2 runtime rejects V1 templates.");
        }

        return messages.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void InitializeDeploymentContext(MultiVmDeploymentContext multiContext, AppSettings settings)
    {
        multiContext.VmContexts.Clear();
        multiContext.StopAllOnAnyVmFailure = settings.StopAllOnAnyVmFailure;
    }

    private static List<RuntimeVmState> BuildRuntimeStates(V2RuntimeExecutionRequest request, MultiVmDeploymentContext multiContext)
    {
        var planVmById = request.Plan.Context.Vms.ToDictionary(vm => vm.VmId, StringComparer.OrdinalIgnoreCase);
        var nodesByVmId = request.Plan.Nodes
            .GroupBy(node => node.VmId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var states = new List<RuntimeVmState>();
        foreach (var vm in request.Template.VmTemplates
            .OrderBy(templateVm => string.IsNullOrWhiteSpace(templateVm.Name) ? "~" : templateVm.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(templateVm => templateVm.VmId, StringComparer.OrdinalIgnoreCase))
        {
            if (!planVmById.TryGetValue(vm.VmId, out var planVm))
            {
                continue;
            }

            var vmName = string.IsNullOrWhiteSpace(vm.Name) ? "Unnamed-VM" : vm.Name.Trim();
            var vmPath = Path.Combine(request.Settings.VmBasePath, vmName);
            var vhdPath = Path.Combine(vmPath, $"{vmName}.vhdx");
            var switches = planVm.Nics
                .Select(nic => nic.EffectiveSwitchName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var context = new VmDeploymentContext
            {
                VmId = CreateStableGuid(vm.VmId),
                VmName = vmName,
                MemoryMb = vm.MemoryMb > 0 ? vm.MemoryMb : request.Settings.DefaultVmMemoryMb,
                CpuCount = vm.CpuCount > 0 ? vm.CpuCount : request.Settings.DefaultCpuCount,
                VmPath = vmPath,
                VhdPath = vhdPath,
                BaseVhdPath = planVm.ResolvedCatalogPath ?? string.Empty,
                VhdxId = planVm.ResolvedCatalogItemId,
                VhdxSignature = vm.VhdxSignature,
                VirtualSwitchName = switches.FirstOrDefault() ?? string.Empty,
                VirtualSwitchNames = switches,
                PerVmFailFast = request.Settings.PerVmFailFast,
                NonBlockingOptionalSteps = new List<string>(request.Settings.NonBlockingOptionalSteps ?? []),
                V2TopologyRole = planVm.TopologyRole,
                V2BootstrapCredentialSlot = planVm.EffectiveBootstrapCredentialSlot
            };

            context.OperationId = multiContext.OperationId;
            context.ShouldAbort = () => multiContext.IsCancellationRequested;
            context.OnBlockingFailure = () =>
            {
                if (multiContext.StopAllOnAnyVmFailure)
                {
                    multiContext.RequestCancellation();
                }
            };

            multiContext.VmContexts.Add(context);
            nodesByVmId.TryGetValue(vm.VmId, out var nodes);
            states.Add(new RuntimeVmState(context, vm, planVm, nodes ?? []));
        }

        return states;
    }

    private static Guid CreateStableGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static void EnsureOperationId(MultiVmDeploymentContext multiContext)
    {
        if (string.IsNullOrWhiteSpace(multiContext.OperationId))
        {
            multiContext.OperationId = Guid.NewGuid().ToString("N");
        }
    }

    private static IReadOnlyDictionary<string, object?> MergeFailureMetadata(
        IHyperVService hyperV,
        IReadOnlyDictionary<string, object?> metadata)
    {
        if (hyperV is not IHyperVFailureDiagnosticsProvider diagnosticsProvider ||
            diagnosticsProvider.LastFailureMetadata == null)
        {
            return metadata;
        }

        var payload = new Dictionary<string, object?>(metadata);
        foreach (var pair in diagnosticsProvider.LastFailureMetadata)
        {
            payload[pair.Key] = pair.Value;
        }

        return payload;
    }

    private void EmitDeployEvent(
        string eventName,
        MultiVmDeploymentContext multiContext,
        string? result,
        string level = "info",
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

        _structuredLogger.Log(ParseLevel(level), eventName, multiContext.OperationId, result, context);
    }

    private void EmitDeployTerminalEvent(MultiVmDeploymentContext multiContext)
    {
        var eventName = multiContext.OperationState switch
        {
            DeploymentOperationState.Completed => "DeployLabCompleted",
            DeploymentOperationState.Cancelled or DeploymentOperationState.CancelledWithResiduals => "DeployLabCancelled",
            _ => "DeployLabFailed"
        };

        var result = multiContext.OperationState switch
        {
            DeploymentOperationState.Completed => "success",
            DeploymentOperationState.Cancelled => "cancelled",
            DeploymentOperationState.CancelledWithResiduals => "cancelled_with_residuals",
            DeploymentOperationState.FailedWithResiduals => "failed_with_residuals",
            _ => "failed"
        };

        EmitDeployEvent(
            eventName,
            multiContext,
            result,
            multiContext.OperationState is DeploymentOperationState.Failed
                or DeploymentOperationState.FailedWithResiduals
                or DeploymentOperationState.CancelledWithResiduals ? "error" : "info");
    }

    private void EmitVmScopedEvent(
        string eventName,
        string level,
        MultiVmDeploymentContext multiContext,
        VmDeploymentContext vmContext,
        string? result,
        IReadOnlyDictionary<string, object?>? extraContext = null)
    {
        var context = new Dictionary<string, object?>
        {
            ["vmId"] = vmContext.VmId,
            ["vmName"] = vmContext.VmName,
            ["executionEngine"] = "V2",
            ["topologyRole"] = vmContext.V2TopologyRole
        };

        if (extraContext != null)
        {
            foreach (var pair in extraContext)
            {
                context[pair.Key] = pair.Value;
            }
        }

        _structuredLogger.Log(ParseLevel(level), eventName, multiContext.OperationId, result, context);
    }

    private void EmitVmTerminalEvent(MultiVmDeploymentContext multiContext, VmDeploymentContext vmContext)
    {
        var (eventName, result, level) = vmContext.WasCancelled
            ? ("VmDeployFailed", "cancelled", "warn")
            : vmContext.IsSuccess
                ? ("VmDeployCompleted", "success", "info")
                : ("VmDeployFailed", vmContext.CleanupResult?.HasResiduals == true ? "failed_with_residuals" : "failed", "error");

        EmitVmScopedEvent(eventName, level, multiContext, vmContext, result);
    }

    private void EmitStepEvent(
        VmDeploymentContext context,
        MultiVmDeploymentContext multiContext,
        string eventName,
        string level,
        string? result,
        string stepKey)
    {
        EmitVmScopedEvent(
            eventName,
            level,
            multiContext,
            context,
            result,
            new Dictionary<string, object?>
            {
                ["stepKey"] = stepKey
            });
    }

    private static StructuredLogLevel ParseLevel(string level) => level switch
    {
        "error" => StructuredLogLevel.Error,
        "warn" => StructuredLogLevel.Warn,
        "debug" => StructuredLogLevel.Debug,
        _ => StructuredLogLevel.Info
    };

    private sealed class RuntimeVmState : IDisposable
    {
        private readonly Dictionary<V2PlanNodeKind, V2PlanNode> _nodes;
        private IPersistentPowerShellSession? _session;
        private IHyperVService? _hyperV;

        public RuntimeVmState(
            VmDeploymentContext context,
            VmTemplate templateVm,
            V2ResolvedVmPlanningContext planVm,
            IReadOnlyList<V2PlanNode> nodes)
        {
            Context = context;
            TemplateVm = templateVm;
            PlanVm = planVm;
            _nodes = nodes
                .GroupBy(node => node.Kind)
                .ToDictionary(group => group.Key, group => group.First());
        }

        public VmDeploymentContext Context { get; }

        public VmTemplate TemplateVm { get; }

        public V2ResolvedVmPlanningContext PlanVm { get; }

        public V2PlanNode? TryGetNode(V2PlanNodeKind kind) =>
            _nodes.TryGetValue(kind, out var node) ? node : null;

        public IHyperVService GetOrCreateHyperV(
            Func<IPersistentPowerShellSession> sessionFactory,
            Func<IPersistentPowerShellSession, IHyperVService> hyperVFactory)
        {
            if (_hyperV != null)
            {
                return _hyperV;
            }

            _session = sessionFactory();
            _hyperV = hyperVFactory(_session);
            return _hyperV;
        }

        public void Dispose()
        {
            _session?.Dispose();
        }
    }
}
