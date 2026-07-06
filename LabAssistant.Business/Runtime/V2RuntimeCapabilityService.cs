using System.Security.Cryptography;
using System.Text;
using LabAssistant.Business.Deployment;
using LabAssistant.Business.Runtime.Scheduling;
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
    private readonly V2FirstDomainControllerRuntimeCoordinator _firstDomainControllerRuntimeCoordinator;
    private readonly V2DomainProgressionRuntimeCoordinator _domainProgressionRuntimeCoordinator;
    private readonly V2BaseRemoteAccessRuntimeCoordinator _baseRemoteAccessRuntimeCoordinator;
    private readonly V2RouterRuntimeCoordinator _routerRuntimeCoordinator;
    private readonly V2ForestTrustRuntimeStage _forestTrustRuntimeStage;
    private readonly V2NetworkSwitchRuntimeStage _networkSwitchRuntimeStage;
    private readonly IVmCleanupOrchestrator _cleanupOrchestrator;
    private readonly IStructuredLogger _structuredLogger;
    private readonly IV2PlanScheduler _scheduler;
    private readonly bool _useGraphScheduler;

    /// <summary>Environment variable that force-enables the graph scheduler for manual real-hardware trials.</summary>
    private const string GraphSchedulerEnvVariable = "LABASSISTANT_V2_USE_GRAPH_SCHEDULER";

    public V2RuntimeCapabilityService(
        Func<IPersistentPowerShellSession> sessionFactory,
        Func<IPersistentPowerShellSession, IHyperVService> hyperVFactory,
        IGuestCommandExecutor guestCommandExecutor,
        IVmCleanupOrchestrator cleanupOrchestrator,
        IStructuredLogger? structuredLogger = null,
        IHyperVMachineAdminService? machineAdminService = null,
        bool useGraphScheduler = false)
    {
        _sessionFactory = sessionFactory;
        _hyperVFactory = hyperVFactory;
        _guestCommandExecutor = guestCommandExecutor;
        _firstDomainControllerRuntimeCoordinator = new V2FirstDomainControllerRuntimeCoordinator(guestCommandExecutor);
        _domainProgressionRuntimeCoordinator = new V2DomainProgressionRuntimeCoordinator(guestCommandExecutor);
        _baseRemoteAccessRuntimeCoordinator = new V2BaseRemoteAccessRuntimeCoordinator(guestCommandExecutor);
        _routerRuntimeCoordinator = new V2RouterRuntimeCoordinator(guestCommandExecutor);
        _forestTrustRuntimeStage = new V2ForestTrustRuntimeStage(guestCommandExecutor, structuredLogger);
        _networkSwitchRuntimeStage = new V2NetworkSwitchRuntimeStage(machineAdminService, structuredLogger);
        _cleanupOrchestrator = cleanupOrchestrator;
        _structuredLogger = structuredLogger ?? NullStructuredLogger.Instance;
        _scheduler = new V2PlanScheduler();
        _useGraphScheduler = useGraphScheduler || ResolveGraphSchedulerEnvOverride();
    }

    private static bool ResolveGraphSchedulerEnvOverride()
    {
        var value = Environment.GetEnvironmentVariable(GraphSchedulerEnvVariable);
        return !string.IsNullOrWhiteSpace(value) &&
               (string.Equals(value, "1", StringComparison.Ordinal) ||
                string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));
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
        foreach (var state in states)
        {
            state.Context.StructuredEventEmitter = (eventName, level, result, extraContext) =>
                EmitVmScopedEvent(eventName, level, multiContext, state.Context, result, extraContext);
        }

        var executedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var trustStates = _forestTrustRuntimeStage.InitializeRuntimeState(
            request,
            BuildForestTrustAnchorStates(states),
            multiContext);
        var switchStates = _networkSwitchRuntimeStage.InitializeRuntimeState(request, multiContext);
        var deferredNodeIds = new HashSet<string>(
            request.Plan.Nodes
                .Where(node => node.Kind == V2PlanNodeKind.ApplyCapabilityRole)
                .Select(node => node.NodeId),
            StringComparer.Ordinal);

        EnsureOperationId(multiContext);

        IV2NodeExecutorRegistry? graphRegistry = null;
        if (_useGraphScheduler)
        {
            graphRegistry = BuildGraphSchedulerRegistry(request, multiContext, states, trustStates, switchStates, executedNodeIds);
            try
            {
                // Validate early, before any Hyper-V work: an unregistered kind (e.g. ApplyCapabilityRole), a cycle, or an
                // unreachable node fails the run loudly rather than silently skipping work.
                V2PlanGraph.Build(request.Plan).Validate(graphRegistry);
            }
            catch (V2SchedulerValidationException validation)
            {
                foreach (var state in states)
                {
                    state.Dispose();
                }

                return new V2RuntimeExecutionResult
                {
                    Success = false,
                    DeploymentContext = multiContext,
                    BlockingMessages = validation.Errors.ToList()
                };
            }
        }

        using var cancellationRegistration = cancellationToken.Register(multiContext.RequestUserCancellation);

        multiContext.MarkRunning();
        EmitDeployEvent("DeployLabStarted", multiContext, "started");

        try
        {
            if (_useGraphScheduler)
            {
                await _scheduler.ExecuteAsync(
                    request.Plan,
                    graphRegistry!,
                    V2SchedulerOptionsFactory.ForProfile(request.Plan.Context.ResolvedDeploymentProfile),
                    _structuredLogger,
                    cancellationToken);
            }
            else
            {
                await ExecuteLegacyStagePipelineAsync(
                    request,
                    multiContext,
                    states,
                    trustStates,
                    switchStates,
                    executedNodeIds,
                    deferredNodeIds,
                    cancellationToken);
            }
        }
        finally
        {
            foreach (var state in states)
            {
                EmitVmTerminalEvent(multiContext, state.Context);
                state.Dispose();
            }

            var hasFailures = multiContext.VmContexts.Any(vm => !vm.IsSuccess);
            var hasCleanupResiduals = multiContext.CleanupResults.Any(result => result.HasResiduals) ||
                                      multiContext.V2TrustContexts.Any(trust => trust.CleanupResidual) ||
                                      multiContext.V2NetworkSwitchContexts.Any(networkSwitch => networkSwitch.CleanupResidual);
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

    /// <summary>
    /// The original hardcoded stage pipeline: network switches, then root readiness/promotion and the profile-specific
    /// remaining stages, then trust/VM/switch cleanup. Preserved verbatim so the graph scheduler can run alongside it
    /// (default off) until parity is proven. This is the code the O3 workstream will delete.
    /// </summary>
    private async Task ExecuteLegacyStagePipelineAsync(
        V2RuntimeExecutionRequest request,
        MultiVmDeploymentContext multiContext,
        IReadOnlyList<RuntimeVmState> states,
        IReadOnlyList<V2TrustRuntimeContext> trustStates,
        IReadOnlyList<V2NetworkSwitchRuntimeContext> switchStates,
        ISet<string> executedNodeIds,
        ISet<string> deferredNodeIds,
        CancellationToken cancellationToken)
    {
        await _networkSwitchRuntimeStage.ExecuteAsync(
            request,
            multiContext,
            BuildNetworkSwitchAffectedVmStates(states),
            switchStates,
            executedNodeIds,
            cancellationToken);

        if (!multiContext.IsCancellationRequested && states.All(state => state.Context.IsSuccess))
        {
            var rootStates = states
                .Where(state => IsRootFirstDomainController(state))
                .ToList();

            if (rootStates.Count > 0)
            {
                await ExecuteCriticalRootReadinessStageAsync(rootStates, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
            }

            var shouldContinue = !multiContext.IsCancellationRequested && rootStates.All(state => state.Context.IsSuccess);
            if (shouldContinue)
            {
                var nonRootStates = states
                    .Where(state => !IsRootFirstDomainController(state))
                    .ToList();

                if (request.Plan.Context.ResolvedDeploymentProfile == V2DeploymentProfile.Conservative)
                {
                    await ExecuteRootPromotionStageAsync(rootStates, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
                    if (!multiContext.IsCancellationRequested && rootStates.All(state => state.Context.IsSuccess))
                    {
                        await ExecuteRemainingStageAsync(
                            request,
                            multiContext,
                            rootStates,
                            nonRootStates,
                            executedNodeIds,
                            deferredNodeIds,
                            cancellationToken);
                    }
                }
                else
                {
                    var rootPromotionTask = ExecuteRootPromotionStageAsync(rootStates, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
                    var nonRootPreparationTask = ExecuteNonRootPreparationStageAsync(
                        request,
                        multiContext,
                        nonRootStates,
                        executedNodeIds,
                        deferredNodeIds,
                        cancellationToken);

                    await Task.WhenAll(rootPromotionTask, nonRootPreparationTask);
                    if (!multiContext.IsCancellationRequested && rootStates.All(state => state.Context.IsSuccess))
                    {
                        await ExecutePostRootRouterAndDomainProgressionAsync(
                            request,
                            multiContext,
                            rootStates,
                            nonRootStates,
                            executedNodeIds,
                            deferredNodeIds,
                            cancellationToken);
                    }
                }
            }
        }

        await _forestTrustRuntimeStage.CleanupFailedOrCancelledAsync(request, multiContext, trustStates);
        await CleanupFailedOrCancelledVmsAsync(multiContext, states);
        await _networkSwitchRuntimeStage.CleanupCreatedAsync(multiContext, switchStates, cancellationToken);
    }

    /// <summary>Node kinds executed against a single VM via <see cref="ExecutePlanNodeAsync"/>.</summary>
    private static readonly V2PlanNodeKind[] GraphVmNodeKinds =
    {
        V2PlanNodeKind.ProvisionVm,
        V2PlanNodeKind.EnableGuestServices,
        V2PlanNodeKind.StartVm,
        V2PlanNodeKind.GuestTransportReady,
        V2PlanNodeKind.PrepareGuestNetwork,
        V2PlanNodeKind.ConfigureBaseRemoteAccess,
        V2PlanNodeKind.BaseRemoteAccessReady,
        V2PlanNodeKind.PrepareRouterNetwork,
        V2PlanNodeKind.InstallRouterRemoteAccessFeature,
        V2PlanNodeKind.EnableRouterRouting,
        V2PlanNodeKind.ConfigureRouterNat,
        V2PlanNodeKind.ValidateCrossSwitchRouting,
        V2PlanNodeKind.ValidateRouterEgress,
        V2PlanNodeKind.InstallAdDomainServicesFeature,
        V2PlanNodeKind.PromoteFirstDomainController,
        V2PlanNodeKind.PromoteReplicaDomainController,
        V2PlanNodeKind.ReplicaDomainReady,
        V2PlanNodeKind.StabilizeDomainDns,
        V2PlanNodeKind.JoinDomain,
        V2PlanNodeKind.JoinedDomainReady,
        V2PlanNodeKind.DomainReady,
        V2PlanNodeKind.RouterReady
    };

    /// <summary>Forest-trust node kinds executed via the trust stage.</summary>
    private static readonly V2PlanNodeKind[] GraphTrustNodeKinds =
    {
        V2PlanNodeKind.PrepareForestTrustDns,
        V2PlanNodeKind.CreateForestTrust,
        V2PlanNodeKind.ValidateForestTrust
    };

    /// <summary>
    /// Builds the per-run executor registry for the graph scheduler. Each executor wraps an existing coordinator, stage,
    /// or script-builder call unchanged; failure is surfaced to the scheduler by throwing after the wrapped call marks its
    /// context. Cleanup delegates map to the three existing cleanup homes (per-VM delete, switch removal, trust removal)
    /// and are guarded so each resource is cleaned at most once as the scheduler unwinds in reverse completion order.
    /// <c>ApplyCapabilityRole</c> is intentionally not registered so a plan that contains it fails validation loudly.
    /// </summary>
    private IV2NodeExecutorRegistry BuildGraphSchedulerRegistry(
        V2RuntimeExecutionRequest request,
        MultiVmDeploymentContext multiContext,
        IReadOnlyList<RuntimeVmState> states,
        IReadOnlyList<V2TrustRuntimeContext> trustStates,
        IReadOnlyList<V2NetworkSwitchRuntimeContext> switchStates,
        ISet<string> executedNodeIds)
    {
        var stateByVmId = states.ToDictionary(state => state.PlanVm.VmId, StringComparer.OrdinalIgnoreCase);
        var anchorStates = BuildForestTrustAnchorStates(states);
        var affectedVmStates = BuildNetworkSwitchAffectedVmStates(states);

        // Once-guards so cleanup runs at most once per resource across the scheduler's reverse-order unwind.
        var cleanedVmIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var trustCleanupDone = new bool[1];
        var switchCleanupDone = new bool[1];

        Func<V2NodeExecutionContext, CancellationToken, Task> vmExecute = async (context, ct) =>
        {
            var node = context.Node;
            if (!stateByVmId.TryGetValue(node.VmId, out var state))
            {
                return;
            }

            var wasSuccessBefore = state.Context.IsSuccess;
            await ExecutePlanNodeAsync(state, node, request, multiContext, executedNodeIds, ct);
            if (state.Context.WasCancelled)
            {
                throw new OperationCanceledException();
            }

            if (wasSuccessBefore && !state.Context.IsSuccess)
            {
                throw new InvalidOperationException(
                    state.Context.FailureMessage ?? $"Node '{node.NodeId}' ({node.Kind}) failed.");
            }
        };

        Func<V2NodeExecutionContext, CancellationToken, Task> vmCleanup = async (context, ct) =>
        {
            var node = context.Node;
            if (!stateByVmId.TryGetValue(node.VmId, out var state))
            {
                return;
            }

            if (!cleanedVmIds.Add(state.PlanVm.VmId))
            {
                return;
            }

            await CleanupSingleVmAsync(state, multiContext);
        };

        Func<V2NodeExecutionContext, CancellationToken, Task> trustExecute = async (context, ct) =>
        {
            var (executed, success, wasCancelled) = await _forestTrustRuntimeStage.ExecuteTrustNodeAsync(
                request, context.Node, anchorStates, multiContext, executedNodeIds, ct);
            if (wasCancelled)
            {
                throw new OperationCanceledException();
            }

            if (executed && !success)
            {
                throw new InvalidOperationException($"Forest-trust node '{context.Node.NodeId}' ({context.Node.Kind}) failed.");
            }
        };

        Func<V2NodeExecutionContext, CancellationToken, Task> trustCleanup = async (context, ct) =>
        {
            if (trustCleanupDone[0])
            {
                return;
            }

            trustCleanupDone[0] = true;
            await _forestTrustRuntimeStage.CleanupFailedOrCancelledAsync(request, multiContext, trustStates);
        };

        Func<V2NodeExecutionContext, CancellationToken, Task> switchExecute = async (context, ct) =>
        {
            var success = await _networkSwitchRuntimeStage.EnsureNetworkSwitchNodeAsync(
                request, context.Node, switchStates, multiContext, affectedVmStates, executedNodeIds);
            if (!success)
            {
                throw new InvalidOperationException($"Network-switch node '{context.Node.NodeId}' failed.");
            }
        };

        Func<V2NodeExecutionContext, CancellationToken, Task> switchCleanup = async (context, ct) =>
        {
            if (switchCleanupDone[0])
            {
                return;
            }

            switchCleanupDone[0] = true;
            await _networkSwitchRuntimeStage.CleanupCreatedAsync(multiContext, switchStates, CancellationToken.None);
        };

        var executors = new List<IV2NodeExecutor>();
        foreach (var kind in GraphVmNodeKinds)
        {
            executors.Add(new V2DelegatingNodeExecutor(kind, vmExecute, vmCleanup));
        }

        foreach (var kind in GraphTrustNodeKinds)
        {
            executors.Add(new V2DelegatingNodeExecutor(kind, trustExecute, trustCleanup));
        }

        executors.Add(new V2DelegatingNodeExecutor(V2PlanNodeKind.EnsureNetworkSwitch, switchExecute, switchCleanup));

        return new V2NodeExecutorRegistry(executors);
    }

    private async Task ExecuteCriticalRootReadinessStageAsync(
        IReadOnlyList<RuntimeVmState> rootStates,
        V2RuntimeExecutionRequest request,
        MultiVmDeploymentContext multiContext,
        ISet<string> executedNodeIds,
        ISet<string> deferredNodeIds,
        CancellationToken cancellationToken)
    {
        await ExecuteNodeSetAsync(rootStates, V2PlanNodeKind.ProvisionVm, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteNodeSetAsync(rootStates, V2PlanNodeKind.EnableGuestServices, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteNodeSetAsync(rootStates, V2PlanNodeKind.StartVm, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteNodeSetAsync(rootStates, V2PlanNodeKind.GuestTransportReady, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
    }

    private async Task ExecuteRootPromotionStageAsync(
        IReadOnlyList<RuntimeVmState> rootStates,
        V2RuntimeExecutionRequest request,
        MultiVmDeploymentContext multiContext,
        ISet<string> executedNodeIds,
        ISet<string> deferredNodeIds,
        CancellationToken cancellationToken)
    {
        await ExecuteNodeSetAsync(rootStates, V2PlanNodeKind.PrepareGuestNetwork, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteNodeSetAsync(rootStates, V2PlanNodeKind.InstallAdDomainServicesFeature, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteNodeSetAsync(rootStates, V2PlanNodeKind.PromoteFirstDomainController, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteNodeSetAsync(rootStates, V2PlanNodeKind.DomainReady, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
    }

    private async Task ExecuteRemainingStageAsync(
        V2RuntimeExecutionRequest request,
        MultiVmDeploymentContext multiContext,
        IReadOnlyList<RuntimeVmState> rootStates,
        IReadOnlyList<RuntimeVmState> nonRootStates,
        ISet<string> executedNodeIds,
        ISet<string> deferredNodeIds,
        CancellationToken cancellationToken)
    {
        if (nonRootStates.Count == 0)
        {
            await ExecuteForestTrustStageAsync(
                request,
                multiContext,
                rootStates.Concat(nonRootStates).ToList(),
                executedNodeIds,
                cancellationToken);
            return;
        }

        await ExecuteNonRootPreparationStageAsync(request, multiContext, nonRootStates, executedNodeIds, deferredNodeIds, cancellationToken);
        if (multiContext.IsCancellationRequested)
        {
            return;
        }

        await ExecutePostRootRouterAndDomainProgressionAsync(request, multiContext, rootStates, nonRootStates, executedNodeIds, deferredNodeIds, cancellationToken);
    }

    private async Task ExecuteNonRootPreparationStageAsync(
        V2RuntimeExecutionRequest request,
        MultiVmDeploymentContext multiContext,
        IReadOnlyList<RuntimeVmState> nonRootStates,
        ISet<string> executedNodeIds,
        ISet<string> deferredNodeIds,
        CancellationToken cancellationToken)
    {
        if (nonRootStates.Count == 0)
        {
            return;
        }

        switch (request.Plan.Context.ResolvedDeploymentProfile)
        {
            case V2DeploymentProfile.Conservative:
                await ExecuteNonRootPreparationConservativeAsync(request, multiContext, nonRootStates, executedNodeIds, deferredNodeIds, cancellationToken);
                break;

            case V2DeploymentProfile.Balanced:
                await ExecuteNonRootPreparationBalancedAsync(nonRootStates, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
                break;

            default:
                await ExecuteNonRootPreparationAggressiveAsync(nonRootStates, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
                break;
        }
    }

    private async Task ExecutePostRootRouterAndDomainProgressionAsync(
        V2RuntimeExecutionRequest request,
        MultiVmDeploymentContext multiContext,
        IReadOnlyList<RuntimeVmState> rootStates,
        IReadOnlyList<RuntimeVmState> nonRootStates,
        ISet<string> executedNodeIds,
        ISet<string> deferredNodeIds,
        CancellationToken cancellationToken)
    {
        var routerStates = nonRootStates.Where(state => state.PlanVm.TopologyRole == "Router").ToList();

        await ExecuteNodeSetAsync(routerStates, V2PlanNodeKind.PrepareRouterNetwork, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteNodeSetAsync(routerStates, V2PlanNodeKind.InstallRouterRemoteAccessFeature, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteNodeSetAsync(routerStates, V2PlanNodeKind.EnableRouterRouting, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteNodeSetAsync(routerStates, V2PlanNodeKind.ConfigureRouterNat, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteNodeSetAsync(routerStates, V2PlanNodeKind.ValidateCrossSwitchRouting, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteNodeSetAsync(routerStates, V2PlanNodeKind.ValidateRouterEgress, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteNodeSetAsync(routerStates, V2PlanNodeKind.RouterReady, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecutePostRootDomainProgressionAsync(rootStates, nonRootStates, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteBaseRemoteAccessStageAsync(rootStates.Concat(nonRootStates).ToList(), request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
    }

    private async Task ExecuteBaseRemoteAccessStageAsync(
        IReadOnlyList<RuntimeVmState> states,
        V2RuntimeExecutionRequest request,
        MultiVmDeploymentContext multiContext,
        ISet<string> executedNodeIds,
        ISet<string> deferredNodeIds,
        CancellationToken cancellationToken)
    {
        if (states.Count == 0)
        {
            return;
        }

        await ExecuteNodeSetAsync(states, V2PlanNodeKind.ConfigureBaseRemoteAccess, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteNodeSetAsync(states, V2PlanNodeKind.BaseRemoteAccessReady, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
    }

    private async Task ExecuteNonRootPreparationConservativeAsync(
        V2RuntimeExecutionRequest request,
        MultiVmDeploymentContext multiContext,
        IReadOnlyList<RuntimeVmState> nonRootStates,
        ISet<string> executedNodeIds,
        ISet<string> deferredNodeIds,
        CancellationToken cancellationToken)
    {
        var stateByVmId = nonRootStates.ToDictionary(state => state.PlanVm.VmId, StringComparer.OrdinalIgnoreCase);
        foreach (var wave in request.Plan.Waves.OrderBy(w => w.WaveNumber))
        {
            if (multiContext.IsCancellationRequested)
            {
                break;
            }

            var waveNodes = wave.NodeIds
                .Select(nodeId => request.Plan.Nodes.First(node => node.NodeId == nodeId))
                .Where(node => stateByVmId.ContainsKey(node.VmId))
                .ToList();

            if (waveNodes.Count == 0)
            {
                continue;
            }

            var runnableNodes = waveNodes
                .Where(node => node.Kind is V2PlanNodeKind.ProvisionVm
                    or V2PlanNodeKind.EnableGuestServices
                    or V2PlanNodeKind.StartVm
                    or V2PlanNodeKind.GuestTransportReady)
                .ToList();

            if (runnableNodes.Count > 0)
            {
                await Task.WhenAll(runnableNodes.Select(node =>
                    ExecutePlanNodeAsync(stateByVmId[node.VmId], node, request, multiContext, executedNodeIds, cancellationToken)));
            }

            var prepareNodes = waveNodes
                .Where(node =>
                {
                    if (node.Kind != V2PlanNodeKind.PrepareGuestNetwork)
                    {
                        return false;
                    }

                    var state = stateByVmId[node.VmId];
                    return !string.Equals(state.PlanVm.TopologyRole, "Router", StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            if (prepareNodes.Count > 0)
            {
                await Task.WhenAll(prepareNodes.Select(node =>
                    ExecutePlanNodeAsync(stateByVmId[node.VmId], node, request, multiContext, executedNodeIds, cancellationToken)));
            }
        }
    }

    private async Task ExecuteNonRootPreparationBalancedAsync(
        IReadOnlyList<RuntimeVmState> nonRootStates,
        V2RuntimeExecutionRequest request,
        MultiVmDeploymentContext multiContext,
        ISet<string> executedNodeIds,
        ISet<string> deferredNodeIds,
        CancellationToken cancellationToken)
    {
        await ExecuteNodeSetAsync(nonRootStates, V2PlanNodeKind.ProvisionVm, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteNodeSetAsync(nonRootStates, V2PlanNodeKind.EnableGuestServices, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteNodeSetAsync(nonRootStates, V2PlanNodeKind.StartVm, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteNodeSetAsync(nonRootStates, V2PlanNodeKind.GuestTransportReady, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteNodeSetAsync(nonRootStates.Where(state => !string.Equals(state.PlanVm.TopologyRole, "Router", StringComparison.OrdinalIgnoreCase)).ToList(), V2PlanNodeKind.PrepareGuestNetwork, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
    }

    private async Task ExecuteNonRootPreparationAggressiveAsync(
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

            foreach (var kind in new[] { V2PlanNodeKind.ProvisionVm, V2PlanNodeKind.EnableGuestServices, V2PlanNodeKind.StartVm, V2PlanNodeKind.GuestTransportReady, V2PlanNodeKind.PrepareGuestNetwork })
            {
                if (kind == V2PlanNodeKind.PrepareGuestNetwork &&
                    string.Equals(state.PlanVm.TopologyRole, "Router", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var node = state.TryGetNode(kind);
                if (node is not null)
                {
                    await ExecutePlanNodeAsync(state, node, request, multiContext, executedNodeIds, cancellationToken);
                }
            }
        }));
    }

    private async Task ExecutePostRootDomainProgressionAsync(
        IReadOnlyList<RuntimeVmState> rootStates,
        IReadOnlyList<RuntimeVmState> nonRootStates,
        V2RuntimeExecutionRequest request,
        MultiVmDeploymentContext multiContext,
        ISet<string> executedNodeIds,
        ISet<string> deferredNodeIds,
        CancellationToken cancellationToken)
    {
        var dependentFirstDomainStates = nonRootStates.Where(IsDependentFirstDomainController).ToList();
        var replicaStates = nonRootStates.Where(state => state.PlanVm.TopologyRole == "ReplicaDomainController").ToList();
        var memberStates = nonRootStates.Where(state => state.PlanVm.RequiresDomainJoin).ToList();
        var dnsStates = rootStates.Concat(dependentFirstDomainStates).ToList();

        await ExecuteNodeSetAsync(dependentFirstDomainStates, V2PlanNodeKind.InstallAdDomainServicesFeature, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteNodeSetAsync(dependentFirstDomainStates, V2PlanNodeKind.PromoteFirstDomainController, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteNodeSetAsync(dependentFirstDomainStates, V2PlanNodeKind.DomainReady, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteNodeSetAsync(replicaStates, V2PlanNodeKind.InstallAdDomainServicesFeature, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteNodeSetAsync(replicaStates, V2PlanNodeKind.PromoteReplicaDomainController, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteNodeSetAsync(replicaStates, V2PlanNodeKind.ReplicaDomainReady, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteNodeSetAsync(dnsStates, V2PlanNodeKind.StabilizeDomainDns, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteNodeSetAsync(memberStates, V2PlanNodeKind.JoinDomain, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteNodeSetAsync(memberStates, V2PlanNodeKind.JoinedDomainReady, request, multiContext, executedNodeIds, deferredNodeIds, cancellationToken);
        await ExecuteForestTrustStageAsync(
            request,
            multiContext,
            rootStates.Concat(nonRootStates).ToList(),
            executedNodeIds,
            cancellationToken);
    }

    private async Task ExecuteForestTrustStageAsync(
        V2RuntimeExecutionRequest request,
        MultiVmDeploymentContext multiContext,
        IReadOnlyList<RuntimeVmState> states,
        ISet<string> executedNodeIds,
        CancellationToken cancellationToken)
    {
        await _forestTrustRuntimeStage.ExecuteAsync(
            request,
            multiContext,
            BuildForestTrustAnchorStates(states),
            executedNodeIds,
            cancellationToken);
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

            case V2PlanNodeKind.EnableGuestServices:
                await ExecuteRuntimeStepAsync(
                    state.Context,
                    DeploymentStepKeys.V2EnableGuestServices,
                    "Enable guest services",
                    context => EnableGuestServicesAsync(state, context),
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

            case V2PlanNodeKind.PrepareGuestNetwork:
                await ExecuteRuntimeStepAsync(
                    state.Context,
                    DeploymentStepKeys.V2PrepareGuestNetwork,
                    "Prepare guest network",
                    context => PrepareGuestNetworkAsync(state, request, context, cancellationToken),
                    multiContext,
                    cancellationToken);
                executedNodeIds.Add(node.NodeId);
                break;

            case V2PlanNodeKind.ConfigureBaseRemoteAccess:
                await ExecuteRuntimeStepAsync(
                    state.Context,
                    DeploymentStepKeys.V2ConfigureBaseRemoteAccess,
                    "Configure base remote access",
                    context => ConfigureBaseRemoteAccessAsync(state, request, context, cancellationToken),
                    multiContext,
                    cancellationToken);
                executedNodeIds.Add(node.NodeId);
                break;

            case V2PlanNodeKind.BaseRemoteAccessReady:
                await ExecuteRuntimeStepAsync(
                    state.Context,
                    DeploymentStepKeys.V2BaseRemoteAccessReady,
                    "Base remote access ready",
                    _ => Task.CompletedTask,
                    multiContext,
                    cancellationToken);
                executedNodeIds.Add(node.NodeId);
                break;

            case V2PlanNodeKind.PrepareRouterNetwork:
                await ExecuteRuntimeStepAsync(
                    state.Context,
                    DeploymentStepKeys.V2PrepareRouterNetwork,
                    "Prepare router network",
                    context => PrepareRouterNetworkAsync(state, request, context, cancellationToken),
                    multiContext,
                    cancellationToken);
                executedNodeIds.Add(node.NodeId);
                break;

            case V2PlanNodeKind.InstallRouterRemoteAccessFeature:
                await ExecuteRuntimeStepAsync(
                    state.Context,
                    DeploymentStepKeys.V2InstallRouterRemoteAccessFeature,
                    "Install router remote-access feature",
                    context => InstallRouterRemoteAccessFeatureAsync(state, request, context, cancellationToken),
                    multiContext,
                    cancellationToken);
                executedNodeIds.Add(node.NodeId);
                break;

            case V2PlanNodeKind.EnableRouterRouting:
                await ExecuteRuntimeStepAsync(
                    state.Context,
                    DeploymentStepKeys.V2EnableRouterRouting,
                    "Enable router routing",
                    context => EnableRouterRoutingAsync(state, request, context, cancellationToken),
                    multiContext,
                    cancellationToken);
                executedNodeIds.Add(node.NodeId);
                break;

            case V2PlanNodeKind.ConfigureRouterNat:
                await ExecuteRuntimeStepAsync(
                    state.Context,
                    DeploymentStepKeys.V2ConfigureRouterNat,
                    "Configure router NAT",
                    context => ConfigureRouterNatAsync(state, request, context, cancellationToken),
                    multiContext,
                    cancellationToken);
                executedNodeIds.Add(node.NodeId);
                break;

            case V2PlanNodeKind.ValidateCrossSwitchRouting:
                await ExecuteRuntimeStepAsync(
                    state.Context,
                    DeploymentStepKeys.V2ValidateCrossSwitchRouting,
                    "Validate cross-switch routing",
                    context => ValidateCrossSwitchRoutingAsync(state, request, context, cancellationToken),
                    multiContext,
                    cancellationToken);
                executedNodeIds.Add(node.NodeId);
                break;

            case V2PlanNodeKind.ValidateRouterEgress:
                await ExecuteRuntimeStepAsync(
                    state.Context,
                    DeploymentStepKeys.V2ValidateRouterEgress,
                    "Validate router egress",
                    context => ValidateRouterEgressAsync(state, request, context, cancellationToken),
                    multiContext,
                    cancellationToken);
                executedNodeIds.Add(node.NodeId);
                break;

            case V2PlanNodeKind.InstallAdDomainServicesFeature:
                await ExecuteRuntimeStepAsync(
                    state.Context,
                    DeploymentStepKeys.V2InstallAdDomainServices,
                    "Install AD DS feature",
                    context => InstallAdDomainServicesFeatureAsync(state, request, context, cancellationToken),
                    multiContext,
                    cancellationToken);
                executedNodeIds.Add(node.NodeId);
                break;

            case V2PlanNodeKind.PromoteFirstDomainController:
                await ExecuteRuntimeStepAsync(
                    state.Context,
                    DeploymentStepKeys.V2PromoteFirstDomainController,
                    "Create first domain controller",
                    context => PromoteFirstDomainControllerAsync(state, request, context, cancellationToken),
                    multiContext,
                    cancellationToken);
                executedNodeIds.Add(node.NodeId);
                break;

            case V2PlanNodeKind.PromoteReplicaDomainController:
                await ExecuteRuntimeStepAsync(
                    state.Context,
                    DeploymentStepKeys.V2PromoteReplicaDomainController,
                    "Promote replica domain controller",
                    context => PromoteReplicaDomainControllerAsync(state, request, context, cancellationToken),
                    multiContext,
                    cancellationToken);
                executedNodeIds.Add(node.NodeId);
                break;

            case V2PlanNodeKind.ReplicaDomainReady:
                await ExecuteRuntimeStepAsync(
                    state.Context,
                    DeploymentStepKeys.V2ReplicaDomainReady,
                    "Wait for replica domain ready",
                    context => WaitForReplicaDomainReadyAsync(state, request, context, cancellationToken),
                    multiContext,
                    cancellationToken);
                executedNodeIds.Add(node.NodeId);
                break;

            case V2PlanNodeKind.StabilizeDomainDns:
                await ExecuteRuntimeStepAsync(
                    state.Context,
                    DeploymentStepKeys.V2StabilizeDomainDns,
                    "Stabilize domain DNS",
                    context => StabilizeDomainDnsAsync(state, request, context, cancellationToken),
                    multiContext,
                    cancellationToken);
                executedNodeIds.Add(node.NodeId);
                break;

            case V2PlanNodeKind.JoinDomain:
                await ExecuteRuntimeStepAsync(
                    state.Context,
                    DeploymentStepKeys.V2JoinDomain,
                    "Join domain",
                    context => JoinDomainAsync(state, request, context, cancellationToken),
                    multiContext,
                    cancellationToken);
                executedNodeIds.Add(node.NodeId);
                break;

            case V2PlanNodeKind.JoinedDomainReady:
                await ExecuteRuntimeStepAsync(
                    state.Context,
                    DeploymentStepKeys.V2JoinedDomainReady,
                    "Validate joined domain",
                    context => WaitForJoinedDomainReadyAsync(state, request, context, cancellationToken),
                    multiContext,
                    cancellationToken);
                executedNodeIds.Add(node.NodeId);
                break;

            case V2PlanNodeKind.DomainReady:
                await ExecuteRuntimeStepAsync(
                    state.Context,
                    DeploymentStepKeys.V2DomainReady,
                    "Wait for domain ready",
                    context => WaitForDomainReadyAsync(state, request, context, cancellationToken),
                    multiContext,
                    cancellationToken);
                executedNodeIds.Add(node.NodeId);
                break;

            case V2PlanNodeKind.RouterReady:
                await ExecuteRuntimeStepAsync(
                    state.Context,
                    DeploymentStepKeys.V2RouterReady,
                    "Router ready",
                    _ => Task.CompletedTask,
                    multiContext,
                    cancellationToken);
                executedNodeIds.Add(node.NodeId);
                break;

        }
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

        if (!await hyperV.DisableVmCheckpointsAsync(context.VmName))
        {
            context.MarkFailure(
                DeploymentStepKeys.V2ProvisionVm,
                $"Failed to disable checkpoints on '{context.VmName}'.");
        }
    }

    private async Task EnableGuestServicesAsync(RuntimeVmState state, VmDeploymentContext context)
    {
        var hyperV = state.GetOrCreateHyperV(_sessionFactory, _hyperVFactory);
        if (!await hyperV.EnableGuestServicesAsync(context.VmName))
        {
            context.MarkFailure(
                DeploymentStepKeys.V2EnableGuestServices,
                $"Failed to enable guest services on '{context.VmName}'.");
            return;
        }

        context.GuestServicesEnabled = true;
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

    private async Task PrepareGuestNetworkAsync(
        RuntimeVmState state,
        V2RuntimeExecutionRequest request,
        VmDeploymentContext context,
        CancellationToken cancellationToken)
    {
        if (state.PlanVm.Nics.Count == 0)
        {
            return;
        }

        var credential = ResolveCredential(
            request.CredentialSlotValues,
            state.PlanVm.EffectiveBootstrapCredentialSlot,
            context,
            DeploymentStepKeys.V2PrepareGuestNetwork,
            "bootstrap");
        if (credential is null)
        {
            return;
        }

        var preparedNics = BuildPreparedNics(state, request.Plan.Context.Vms);
        var result = await _domainProgressionRuntimeCoordinator.PrepareGuestNetworkAsync(
            context.VmName,
            credential,
            preparedNics,
            cancellationToken);

        if (!result.Success)
        {
            context.MarkFailure(
                DeploymentStepKeys.V2PrepareGuestNetwork,
                $"Failed to prepare guest network on '{context.VmName}'. {result.Error}".Trim());
        }
    }

    private async Task ConfigureBaseRemoteAccessAsync(
        RuntimeVmState state,
        V2RuntimeExecutionRequest request,
        VmDeploymentContext context,
        CancellationToken cancellationToken)
    {
        var credential = ResolveCredential(
            request.CredentialSlotValues,
            state.PlanVm.EffectiveBootstrapCredentialSlot,
            context,
            DeploymentStepKeys.V2ConfigureBaseRemoteAccess,
            "bootstrap");
        if (credential is null)
        {
            return;
        }

        var result = await _baseRemoteAccessRuntimeCoordinator.ConfigureBaseRemoteAccessAsync(
            context.VmName,
            credential,
            request.BaseRemoteAccessOptions,
            cancellationToken);

        if (!result.Success)
        {
            context.MarkFailure(
                DeploymentStepKeys.V2ConfigureBaseRemoteAccess,
                $"Failed to configure base remote access on '{context.VmName}'. {result.Error}".Trim());
        }
    }

    private async Task PrepareRouterNetworkAsync(
        RuntimeVmState state,
        V2RuntimeExecutionRequest request,
        VmDeploymentContext context,
        CancellationToken cancellationToken)
    {
        var bootstrapCredential = ResolveCredential(
            request.CredentialSlotValues,
            state.PlanVm.EffectiveBootstrapCredentialSlot,
            context,
            DeploymentStepKeys.V2PrepareRouterNetwork,
            "bootstrap");
        if (bootstrapCredential is null)
        {
            return;
        }

        var nicPlans = await BuildRouterNicPlansAsync(state, context, cancellationToken);
        if (!context.IsSuccess)
        {
            return;
        }

        var result = await _routerRuntimeCoordinator.PrepareRouterNetworkAsync(
            context.VmName,
            bootstrapCredential,
            nicPlans,
            cancellationToken);
        if (!result.Success)
        {
            context.MarkFailure(
                DeploymentStepKeys.V2PrepareRouterNetwork,
                $"Failed to prepare router network on '{context.VmName}'. {result.Error}".Trim());
        }
    }

    private async Task InstallRouterRemoteAccessFeatureAsync(
        RuntimeVmState state,
        V2RuntimeExecutionRequest request,
        VmDeploymentContext context,
        CancellationToken cancellationToken)
    {
        var bootstrapCredential = ResolveCredential(
            request.CredentialSlotValues,
            state.PlanVm.EffectiveBootstrapCredentialSlot,
            context,
            DeploymentStepKeys.V2InstallRouterRemoteAccessFeature,
            "bootstrap");
        if (bootstrapCredential is null)
        {
            return;
        }

        var result = await _routerRuntimeCoordinator.InstallRemoteAccessFeatureAsync(
            context.VmName,
            bootstrapCredential,
            cancellationToken);
        if (!result.Success)
        {
            context.MarkFailure(
                DeploymentStepKeys.V2InstallRouterRemoteAccessFeature,
                $"Failed to install router remote-access features on '{context.VmName}'. {result.Error}".Trim());
        }
    }

    private async Task EnableRouterRoutingAsync(
        RuntimeVmState state,
        V2RuntimeExecutionRequest request,
        VmDeploymentContext context,
        CancellationToken cancellationToken)
    {
        var bootstrapCredential = ResolveCredential(
            request.CredentialSlotValues,
            state.PlanVm.EffectiveBootstrapCredentialSlot,
            context,
            DeploymentStepKeys.V2EnableRouterRouting,
            "bootstrap");
        if (bootstrapCredential is null)
        {
            return;
        }

        var adapters = await EnsureRouterAdapterInventoryAsync(state, context);
        var externalAdapter = adapters
            .FirstOrDefault(adapter => SwitchTypeIs(adapter.SwitchType, "External"));
        if (externalAdapter is null)
        {
            context.MarkFailure(
                DeploymentStepKeys.V2EnableRouterRouting,
                $"Router VM '{context.VmName}' has no external switch attachment for routing.");
            return;
        }

        var result = await _routerRuntimeCoordinator.EnableRoutingAsync(
            context.VmName,
            bootstrapCredential,
            externalAdapter.MacAddress,
            cancellationToken);
        if (!result.Success)
        {
            context.MarkFailure(
                DeploymentStepKeys.V2EnableRouterRouting,
                $"Failed to enable routing on '{context.VmName}'. {result.Error}".Trim());
        }
    }

    private async Task ConfigureRouterNatAsync(
        RuntimeVmState state,
        V2RuntimeExecutionRequest request,
        VmDeploymentContext context,
        CancellationToken cancellationToken)
    {
        var bootstrapCredential = ResolveCredential(
            request.CredentialSlotValues,
            state.PlanVm.EffectiveBootstrapCredentialSlot,
            context,
            DeploymentStepKeys.V2ConfigureRouterNat,
            "bootstrap");
        if (bootstrapCredential is null)
        {
            return;
        }

        var adapters = await EnsureRouterAdapterInventoryAsync(state, context);
        var externalAdapter = adapters
            .FirstOrDefault(adapter => SwitchTypeIs(adapter.SwitchType, "External"));
        var internalAdapters = adapters
            .Where(adapter => !SwitchTypeIs(adapter.SwitchType, "External"))
            .ToArray();
        if (externalAdapter is null || internalAdapters.Length == 0)
        {
            context.MarkFailure(
                DeploymentStepKeys.V2ConfigureRouterNat,
                $"Router VM '{context.VmName}' must have one external and at least one internal NIC before NAT can be configured.");
            return;
        }

        var result = await _routerRuntimeCoordinator.ConfigureNatAsync(
            context.VmName,
            bootstrapCredential,
            externalAdapter.MacAddress,
            internalAdapters.Select(adapter => adapter.MacAddress).ToArray(),
            cancellationToken);
        if (!result.Success)
        {
            context.MarkFailure(
                DeploymentStepKeys.V2ConfigureRouterNat,
                $"Failed to configure router NAT on '{context.VmName}'. {result.Error}".Trim());
        }
    }

    private async Task ValidateCrossSwitchRoutingAsync(
        RuntimeVmState state,
        V2RuntimeExecutionRequest request,
        VmDeploymentContext context,
        CancellationToken cancellationToken)
    {
        var targets = GetRouterCrossSwitchTargets(state, request);
        if (targets.Count == 0)
        {
            context.SetStepTerminalOverride(
                DeploymentStepKeys.V2ValidateCrossSwitchRouting,
                DeployStepState.Skipped,
                "No cross-switch dependent guests require router validation.");
            return;
        }

        foreach (var target in targets)
        {
            var bootstrapCredential = ResolveCredential(
                request.CredentialSlotValues,
                target.PlanVm.EffectiveBootstrapCredentialSlot,
                target.Context,
                DeploymentStepKeys.V2ValidateCrossSwitchRouting,
                "bootstrap");
            if (bootstrapCredential is null)
            {
                context.MarkFailure(
                    DeploymentStepKeys.V2ValidateCrossSwitchRouting,
                    $"Cross-switch validation target '{target.Context.VmName}' is missing bootstrap credentials.");
                return;
            }

            if (target.Domain is null)
            {
                context.MarkFailure(
                    DeploymentStepKeys.V2ValidateCrossSwitchRouting,
                    $"Cross-switch validation target '{target.Context.VmName}' is missing resolved domain topology.");
                return;
            }

            var result = await _routerRuntimeCoordinator.ValidateCrossSwitchRoutingAsync(
                target.Context.VmName,
                bootstrapCredential,
                target.Domain.DnsName,
                cancellationToken);
            if (!result.Success)
            {
                context.MarkFailure(
                    DeploymentStepKeys.V2ValidateCrossSwitchRouting,
                    $"Cross-switch validation failed from '{target.Context.VmName}'. {result.Error}".Trim());
                return;
            }
        }
    }

    private async Task ValidateRouterEgressAsync(
        RuntimeVmState state,
        V2RuntimeExecutionRequest request,
        VmDeploymentContext context,
        CancellationToken cancellationToken)
    {
        var egressTargets = GetRouterEgressTargets(state, request);
        if (egressTargets.Count == 0)
        {
            context.SetStepTerminalOverride(
                DeploymentStepKeys.V2ValidateRouterEgress,
                DeployStepState.Skipped,
                "No router-dependent guests require outbound egress validation.");
            return;
        }

        var bootstrapCredential = ResolveCredential(
            request.CredentialSlotValues,
            state.PlanVm.EffectiveBootstrapCredentialSlot,
            context,
            DeploymentStepKeys.V2ValidateRouterEgress,
            "bootstrap");
        if (bootstrapCredential is null)
        {
            return;
        }

        var adapters = await EnsureRouterAdapterInventoryAsync(state, context);
        var externalAdapter = adapters.FirstOrDefault(adapter => SwitchTypeIs(adapter.SwitchType, "External"));
        if (externalAdapter is null)
        {
            context.MarkFailure(
                DeploymentStepKeys.V2ValidateRouterEgress,
                $"Router VM '{context.VmName}' has no external switch attachment for outbound validation.");
            return;
        }

        var readiness = await _routerRuntimeCoordinator.ProbeExternalReadinessAsync(
            context.VmName,
            bootstrapCredential,
            externalAdapter.MacAddress,
            cancellationToken);
        if (!readiness.Success)
        {
            context.MarkFailure(
                DeploymentStepKeys.V2ValidateRouterEgress,
                $"Failed to probe router egress readiness on '{context.VmName}'. {readiness.Error}".Trim());
            return;
        }

        if ((readiness.Output ?? string.Empty).Contains("HOST_OFFLINE", StringComparison.OrdinalIgnoreCase))
        {
            context.LogCallback?.Invoke($"Skipping outbound router validation on '{context.VmName}' because the host or external link appears offline.");
            context.SetStepTerminalOverride(
                DeploymentStepKeys.V2ValidateRouterEgress,
                DeployStepState.Skipped,
                "Host or external link appears offline; outbound validation was skipped.");
            return;
        }

        foreach (var target in egressTargets)
        {
            var targetBootstrapCredential = ResolveCredential(
                request.CredentialSlotValues,
                target.PlanVm.EffectiveBootstrapCredentialSlot,
                target.Context,
                DeploymentStepKeys.V2ValidateRouterEgress,
                "bootstrap");
            if (targetBootstrapCredential is null)
            {
                context.MarkFailure(
                    DeploymentStepKeys.V2ValidateRouterEgress,
                    $"Router egress validation target '{target.Context.VmName}' is missing bootstrap credentials.");
                return;
            }

            var result = await _routerRuntimeCoordinator.ValidateRouterEgressAsync(
                target.Context.VmName,
                targetBootstrapCredential,
                cancellationToken);
            if (!result.Success)
            {
                context.MarkFailure(
                    DeploymentStepKeys.V2ValidateRouterEgress,
                    $"Outbound router validation failed from '{target.Context.VmName}'. {result.Error}".Trim());
                return;
            }
        }
    }

    private async Task InstallAdDomainServicesFeatureAsync(
        RuntimeVmState state,
        V2RuntimeExecutionRequest request,
        VmDeploymentContext context,
        CancellationToken cancellationToken)
    {
        var credential = ResolveCredential(
            request.CredentialSlotValues,
            state.PlanVm.EffectiveBootstrapCredentialSlot,
            context,
            DeploymentStepKeys.V2InstallAdDomainServices,
            "bootstrap");
        if (credential is null)
        {
            return;
        }

        var result = await _firstDomainControllerRuntimeCoordinator.EnsureAdDomainServicesInstalledAsync(
            context.VmName,
            credential,
            cancellationToken);
        if (!result.Success)
        {
            context.MarkFailure(
                DeploymentStepKeys.V2InstallAdDomainServices,
                $"Failed to install AD DS on '{context.VmName}'. {result.Error}".Trim());
        }
    }

    private async Task PromoteFirstDomainControllerAsync(
        RuntimeVmState state,
        V2RuntimeExecutionRequest request,
        VmDeploymentContext context,
        CancellationToken cancellationToken)
    {
        var domain = state.Domain;
        if (domain is null)
        {
            context.MarkFailure(
                DeploymentStepKeys.V2PromoteFirstDomainController,
                $"VM '{context.VmName}' is missing resolved domain topology.");
            return;
        }

        var bootstrapCredential = ResolveCredential(
            request.CredentialSlotValues,
            state.PlanVm.EffectiveBootstrapCredentialSlot,
            context,
            DeploymentStepKeys.V2PromoteFirstDomainController,
            "bootstrap");
        var dsrmCredential = ResolveCredential(
            request.CredentialSlotValues,
            state.PlanVm.EffectiveDsrmCredentialSlot,
            context,
            DeploymentStepKeys.V2PromoteFirstDomainController,
            "DSRM");
        V2RuntimeCredential? parentDomainAdminCredential = null;
        V2ResolvedDomainPlanningContext? parentDomain = null;
        if (domain.RelationKind is V2DomainRelationKind.Child or V2DomainRelationKind.Tree)
        {
            if (string.IsNullOrWhiteSpace(domain.ParentDomainId))
            {
                context.MarkFailure(
                    DeploymentStepKeys.V2PromoteFirstDomainController,
                    $"{domain.RelationKind} domain '{domain.DomainId}' is missing parent-domain topology.");
                return;
            }

            parentDomain = request.Plan.Context.Domains.FirstOrDefault(candidate =>
                string.Equals(candidate.DomainId, domain.ParentDomainId, StringComparison.OrdinalIgnoreCase));
            if (parentDomain is null)
            {
                context.MarkFailure(
                    DeploymentStepKeys.V2PromoteFirstDomainController,
                    $"{domain.RelationKind} domain '{domain.DomainId}' references missing parent domain '{domain.ParentDomainId}'.");
                return;
            }

            parentDomainAdminCredential = ResolveCredential(
                request.CredentialSlotValues,
                state.PlanVm.EffectiveParentDomainAdminCredentialSlot,
                context,
                DeploymentStepKeys.V2PromoteFirstDomainController,
                "parent-domain-admin");
        }

        if (bootstrapCredential is null ||
            dsrmCredential is null ||
            (domain.RelationKind is V2DomainRelationKind.Child or V2DomainRelationKind.Tree && parentDomainAdminCredential is null))
        {
            return;
        }

        if (domain.RelationKind is V2DomainRelationKind.Child or V2DomainRelationKind.Tree)
        {
            string? lastDnsError = null;
            for (var attempt = 1; attempt <= request.GuestTransportMaxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var dnsReadyResult = await _firstDomainControllerRuntimeCoordinator.ProbeParentDomainDnsReadyAsync(
                    context.VmName,
                    bootstrapCredential,
                    parentDomain!.DnsName,
                    cancellationToken);
                if (dnsReadyResult.Success)
                {
                    lastDnsError = null;
                    break;
                }

                lastDnsError = dnsReadyResult.Error;
                if (attempt < request.GuestTransportMaxRetries)
                {
                    await Task.Delay(request.GuestTransportRetryDelay, cancellationToken);
                }
            }

            if (lastDnsError is not null)
            {
                context.MarkFailure(
                    DeploymentStepKeys.V2PromoteFirstDomainController,
                    $"{domain.RelationKind} domain '{domain.DnsName}' could not resolve parent-domain DNS from '{context.VmName}'. Last error: {lastDnsError}");
                return;
            }
        }

        var result = await _firstDomainControllerRuntimeCoordinator.PromoteFirstDomainControllerAsync(
            context.VmName,
            bootstrapCredential,
            domain,
            dsrmCredential.Password,
            parentDomainAdminCredential,
            parentDomain,
            cancellationToken);
        if (result.Success || V2FirstDomainControllerRuntimeCoordinator.IsExpectedRestartBoundaryError(result.Error))
        {
            return;
        }

        context.MarkFailure(
            DeploymentStepKeys.V2PromoteFirstDomainController,
            $"First domain-controller creation failed on '{context.VmName}'. {result.Error}".Trim());
    }

    private async Task WaitForDomainReadyAsync(
        RuntimeVmState state,
        V2RuntimeExecutionRequest request,
        VmDeploymentContext context,
        CancellationToken cancellationToken)
    {
        var domain = state.Domain;
        if (domain is null)
        {
            context.MarkFailure(
                DeploymentStepKeys.V2DomainReady,
                $"VM '{context.VmName}' is missing resolved domain topology.");
            return;
        }

        var domainAdminCredential = ResolveCredential(
            request.CredentialSlotValues,
            state.PlanVm.EffectiveDomainAdminCredentialSlot,
            context,
            DeploymentStepKeys.V2DomainReady,
            "domain-admin");
        if (domainAdminCredential is null)
        {
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

            var verifyResult = await _firstDomainControllerRuntimeCoordinator.VerifyDomainControllerAsync(
                context.VmName,
                domainAdminCredential,
                domain.DnsName,
                cancellationToken);
            if (!verifyResult.Success)
            {
                lastError = verifyResult.Error;
                if (attempt < request.GuestTransportMaxRetries)
                {
                    await Task.Delay(request.GuestTransportRetryDelay, cancellationToken);
                }
                continue;
            }

            var readyResult = await _firstDomainControllerRuntimeCoordinator.ProbeDomainReadyAsync(
                context.VmName,
                domainAdminCredential,
                domain.DnsName,
                cancellationToken);
            if (readyResult.Success)
            {
                return;
            }

            lastError = readyResult.Error;
            if (attempt < request.GuestTransportMaxRetries)
            {
                await Task.Delay(request.GuestTransportRetryDelay, cancellationToken);
            }
        }

        context.MarkFailure(
            DeploymentStepKeys.V2DomainReady,
            $"Domain '{domain.DnsName}' did not become ready on '{context.VmName}'. Last error: {lastError ?? "unknown"}");
    }

    private async Task PromoteReplicaDomainControllerAsync(
        RuntimeVmState state,
        V2RuntimeExecutionRequest request,
        VmDeploymentContext context,
        CancellationToken cancellationToken)
    {
        var domain = state.Domain;
        if (domain is null)
        {
            context.MarkFailure(
                DeploymentStepKeys.V2PromoteReplicaDomainController,
                $"VM '{context.VmName}' is missing resolved domain topology.");
            return;
        }

        var bootstrapCredential = ResolveCredential(
            request.CredentialSlotValues,
            state.PlanVm.EffectiveBootstrapCredentialSlot,
            context,
            DeploymentStepKeys.V2PromoteReplicaDomainController,
            "bootstrap");
        var domainJoinCredential = ResolveCredential(
            request.CredentialSlotValues,
            state.PlanVm.EffectiveDomainJoinCredentialSlot,
            context,
            DeploymentStepKeys.V2PromoteReplicaDomainController,
            "domain-join");
        var dsrmCredential = ResolveCredential(
            request.CredentialSlotValues,
            state.PlanVm.EffectiveDsrmCredentialSlot,
            context,
            DeploymentStepKeys.V2PromoteReplicaDomainController,
            "DSRM");
        if (bootstrapCredential is null || domainJoinCredential is null || dsrmCredential is null)
        {
            return;
        }

        var result = await _domainProgressionRuntimeCoordinator.PromoteReplicaDomainControllerAsync(
            context.VmName,
            bootstrapCredential,
            domain,
            domainJoinCredential,
            dsrmCredential.Password,
            cancellationToken);
        if (result.Success || V2DomainProgressionRuntimeCoordinator.IsExpectedRestartBoundaryError(result.Error))
        {
            return;
        }

        context.MarkFailure(
            DeploymentStepKeys.V2PromoteReplicaDomainController,
            $"Replica promotion failed on '{context.VmName}'. {result.Error}".Trim());
    }

    private async Task WaitForReplicaDomainReadyAsync(
        RuntimeVmState state,
        V2RuntimeExecutionRequest request,
        VmDeploymentContext context,
        CancellationToken cancellationToken)
    {
        var domain = state.Domain;
        if (domain is null)
        {
            context.MarkFailure(
                DeploymentStepKeys.V2ReplicaDomainReady,
                $"VM '{context.VmName}' is missing resolved domain topology.");
            return;
        }

        var domainAdminCredential = ResolveCredential(
            request.CredentialSlotValues,
            state.PlanVm.EffectiveDomainAdminCredentialSlot,
            context,
            DeploymentStepKeys.V2ReplicaDomainReady,
            "domain-admin");
        if (domainAdminCredential is null)
        {
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

            var verifyResult = await _domainProgressionRuntimeCoordinator.VerifyReplicaDomainControllerAsync(
                context.VmName,
                domainAdminCredential,
                domain.DnsName,
                cancellationToken);
            if (!verifyResult.Success)
            {
                lastError = verifyResult.Error;
                if (attempt < request.GuestTransportMaxRetries)
                {
                    await Task.Delay(request.GuestTransportRetryDelay, cancellationToken);
                }
                continue;
            }

            var readyResult = await _domainProgressionRuntimeCoordinator.ProbeReplicaDomainReadyAsync(
                context.VmName,
                domainAdminCredential,
                domain.DnsName,
                cancellationToken);
            if (readyResult.Success)
            {
                return;
            }

            lastError = readyResult.Error;
            if (attempt < request.GuestTransportMaxRetries)
            {
                await Task.Delay(request.GuestTransportRetryDelay, cancellationToken);
            }
        }

        context.MarkFailure(
            DeploymentStepKeys.V2ReplicaDomainReady,
            $"Replica domain '{domain.DnsName}' did not become ready on '{context.VmName}'. Last error: {lastError ?? "unknown"}");
    }

    private async Task StabilizeDomainDnsAsync(
        RuntimeVmState state,
        V2RuntimeExecutionRequest request,
        VmDeploymentContext context,
        CancellationToken cancellationToken)
    {
        var domain = state.Domain;
        if (domain is null)
        {
            context.MarkFailure(
                DeploymentStepKeys.V2StabilizeDomainDns,
                $"VM '{context.VmName}' is missing resolved domain topology.");
            return;
        }

        var domainAdminCredential = ResolveCredential(
            request.CredentialSlotValues,
            state.PlanVm.EffectiveDomainAdminCredentialSlot,
            context,
            DeploymentStepKeys.V2StabilizeDomainDns,
            "domain-admin");
        if (domainAdminCredential is null)
        {
            return;
        }

        var domainControllerTargets = request.Template.VmTemplates
            .Where(vm => string.Equals(vm.DomainId, domain.DomainId, StringComparison.OrdinalIgnoreCase) &&
                         (string.Equals(vm.TopologyRole, "FirstDomainController", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(vm.TopologyRole, "RootDomainController", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(vm.TopologyRole, "ReplicaDomainController", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(vm => vm.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(vm => vm.VmId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var targetVm in domainControllerTargets)
        {
            var targetPlanVm = request.Plan.Context.Vms.First(vm => string.Equals(vm.VmId, targetVm.VmId, StringComparison.OrdinalIgnoreCase));
            var dnsServers = BuildDomainControllerDnsOrder(targetPlanVm, request.Plan.Context.Vms);
            var result = await _domainProgressionRuntimeCoordinator.StabilizeDomainDnsAsync(
                targetVm.Name,
                domainAdminCredential,
                dnsServers,
                cancellationToken);

            if (!result.Success)
            {
                context.MarkFailure(
                    DeploymentStepKeys.V2StabilizeDomainDns,
                    $"Failed to stabilize domain DNS on '{targetVm.Name}'. {result.Error}".Trim());
                return;
            }
        }
    }

    private async Task JoinDomainAsync(
        RuntimeVmState state,
        V2RuntimeExecutionRequest request,
        VmDeploymentContext context,
        CancellationToken cancellationToken)
    {
        var domain = state.Domain;
        if (domain is null)
        {
            context.MarkFailure(
                DeploymentStepKeys.V2JoinDomain,
                $"VM '{context.VmName}' is missing resolved domain topology.");
            return;
        }

        var bootstrapCredential = ResolveCredential(
            request.CredentialSlotValues,
            state.PlanVm.EffectiveBootstrapCredentialSlot,
            context,
            DeploymentStepKeys.V2JoinDomain,
            "bootstrap");
        var joinCredential = ResolveCredential(
            request.CredentialSlotValues,
            state.PlanVm.EffectiveDomainJoinCredentialSlot,
            context,
            DeploymentStepKeys.V2JoinDomain,
            "domain-join");
        if (bootstrapCredential is null || joinCredential is null)
        {
            return;
        }

        var result = await _domainProgressionRuntimeCoordinator.JoinDomainAsync(
            context.VmName,
            bootstrapCredential,
            domain.DnsName,
            joinCredential,
            cancellationToken);
        if (result.Success || V2DomainProgressionRuntimeCoordinator.IsExpectedRestartBoundaryError(result.Error))
        {
            return;
        }

        context.MarkFailure(
            DeploymentStepKeys.V2JoinDomain,
            $"Domain join failed on '{context.VmName}'. {result.Error}".Trim());
    }

    private async Task WaitForJoinedDomainReadyAsync(
        RuntimeVmState state,
        V2RuntimeExecutionRequest request,
        VmDeploymentContext context,
        CancellationToken cancellationToken)
    {
        var domain = state.Domain;
        if (domain is null)
        {
            context.MarkFailure(
                DeploymentStepKeys.V2JoinedDomainReady,
                $"VM '{context.VmName}' is missing resolved domain topology.");
            return;
        }

        var bootstrapCredential = ResolveCredential(
            request.CredentialSlotValues,
            state.PlanVm.EffectiveBootstrapCredentialSlot,
            context,
            DeploymentStepKeys.V2JoinedDomainReady,
            "bootstrap");
        var domainAdminCredential = ResolveCredential(
            request.CredentialSlotValues,
            state.PlanVm.EffectiveDomainAdminCredentialSlot,
            context,
            DeploymentStepKeys.V2JoinedDomainReady,
            "domain-admin");
        if (bootstrapCredential is null || domainAdminCredential is null)
        {
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

            var localResult = await _domainProgressionRuntimeCoordinator.VerifyJoinedDomainLocallyAsync(
                context.VmName,
                bootstrapCredential,
                domain.DnsName,
                cancellationToken);
            if (!localResult.Success)
            {
                lastError = localResult.Error;
                if (attempt < request.GuestTransportMaxRetries)
                {
                    await Task.Delay(request.GuestTransportRetryDelay, cancellationToken);
                }
                continue;
            }

            var domainResult = await _domainProgressionRuntimeCoordinator.VerifyJoinedDomainWithDomainCredentialAsync(
                context.VmName,
                domainAdminCredential,
                domain.DnsName,
                cancellationToken);
            if (domainResult.Success)
            {
                return;
            }

            lastError = domainResult.Error;
            if (attempt < request.GuestTransportMaxRetries)
            {
                await Task.Delay(request.GuestTransportRetryDelay, cancellationToken);
            }
        }

        context.MarkFailure(
            DeploymentStepKeys.V2JoinedDomainReady,
            $"Joined-domain validation did not succeed on '{context.VmName}'. Last error: {lastError ?? "unknown"}");
    }

    private async Task WaitForGuestTransportAsync(
        RuntimeVmState state,
        V2RuntimeExecutionRequest request,
        VmDeploymentContext context,
        CancellationToken cancellationToken)
    {
        var credential = ResolveCredential(
            request.CredentialSlotValues,
            state.PlanVm.EffectiveBootstrapCredentialSlot,
            context,
            DeploymentStepKeys.V2GuestTransportReady,
            "bootstrap");
        if (credential is null)
        {
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

    private async Task<IReadOnlyList<RuntimeRouterAdapter>> EnsureRouterAdapterInventoryAsync(
        RuntimeVmState state,
        VmDeploymentContext context)
    {
        if (state.RouterAdapters is not null)
        {
            return state.RouterAdapters;
        }

        var hyperV = state.GetOrCreateHyperV(_sessionFactory, _hyperVFactory);
        var adapters = await hyperV.GetVmNetworkAdaptersAsync(context.VmName);
        var plans = state.PlanVm.Nics
            .OrderBy(nic => nic.EffectiveSwitchName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(nic => nic.NicId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var adapterMap = adapters
            .Where(adapter => !string.IsNullOrWhiteSpace(adapter.SwitchName))
            .GroupBy(adapter => adapter.SwitchName!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var resolved = new List<RuntimeRouterAdapter>();
        foreach (var nic in plans)
        {
            if (string.IsNullOrWhiteSpace(nic.EffectiveSwitchName) ||
                !adapterMap.TryGetValue(nic.EffectiveSwitchName, out var adapter) ||
                string.IsNullOrWhiteSpace(adapter.MacAddress))
            {
                context.MarkFailure(
                    DeploymentStepKeys.V2PrepareRouterNetwork,
                    $"Router VM '{context.VmName}' could not resolve a Hyper-V adapter for switch '{nic.EffectiveSwitchName ?? nic.NicId}'.");
                return Array.Empty<RuntimeRouterAdapter>();
            }

            resolved.Add(new RuntimeRouterAdapter(
                nic.EffectiveSwitchName,
                nic.EffectiveSwitchType,
                adapter.AdapterName,
                adapter.MacAddress,
                nic));
        }

        state.RouterAdapters = resolved;
        return resolved;
    }

    private async Task<IReadOnlyList<RouterNicPlan>> BuildRouterNicPlansAsync(
        RuntimeVmState state,
        VmDeploymentContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var adapters = await EnsureRouterAdapterInventoryAsync(state, context);
        if (!context.IsSuccess)
        {
            return Array.Empty<RouterNicPlan>();
        }

        return adapters
            .Select(adapter => new RouterNicPlan
            {
                SwitchName = adapter.SwitchName,
                MacAddress = adapter.MacAddress,
                IsExternal = SwitchTypeIs(adapter.SwitchType, "External"),
                IpAddress = SwitchTypeIs(adapter.SwitchType, "External") ? null : adapter.Nic.IpAddress,
                PrefixLength = SwitchTypeIs(adapter.SwitchType, "External") ? null : adapter.Nic.PrefixLength,
                DnsServers = SwitchTypeIs(adapter.SwitchType, "External") ? Array.Empty<string>() : adapter.Nic.DnsServers
            })
            .ToArray();
    }

    private static IReadOnlyList<RuntimeVmState> GetRouterCrossSwitchTargets(
        RuntimeVmState routerState,
        V2RuntimeExecutionRequest request)
    {
        return routerState.AllStates
            .Where(state => state.RequiresRouterDependency && !state.PlanVm.IsRouterCapable)
            .GroupBy(
                state => state.PlanVm.Nics
                    .Select(nic => nic.NetworkId ?? nic.EffectiveSwitchName)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? state.PlanVm.VmId,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(state => state.Context.VmName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(state => state.PlanVm.VmId, StringComparer.OrdinalIgnoreCase)
                .First())
            .ToArray();
    }

    private static IReadOnlyList<RuntimeVmState> GetRouterEgressTargets(
        RuntimeVmState routerState,
        V2RuntimeExecutionRequest request)
    {
        return routerState.AllStates
            .Where(state => state.ExpectsRouterEgress && !state.PlanVm.IsRouterCapable)
            .GroupBy(
                state => state.PlanVm.Nics
                    .Select(nic => nic.NetworkId ?? nic.EffectiveSwitchName)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? state.PlanVm.VmId,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(state => state.Context.VmName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(state => state.PlanVm.VmId, StringComparer.OrdinalIgnoreCase)
                .First())
            .ToArray();
    }

    private static bool SwitchTypeIs(string? switchType, string expectedType)
        => string.Equals(switchType, expectedType, StringComparison.OrdinalIgnoreCase);

    private static V2RuntimeCredential? ResolveCredential(
        IReadOnlyDictionary<string, V2RuntimeCredential> credentialSlotValues,
        string? slotKey,
        VmDeploymentContext context,
        string stepKey,
        string purpose)
    {
        if (string.IsNullOrWhiteSpace(slotKey))
        {
            context.MarkFailure(stepKey, $"VM '{context.VmName}' is missing a resolved {purpose} credential slot.");
            return null;
        }

        if (!credentialSlotValues.TryGetValue(slotKey, out var credential))
        {
            context.MarkFailure(stepKey, $"VM '{context.VmName}' is missing runtime credential material for slot '{slotKey}'.");
            return null;
        }

        return credential;
    }

    private static IReadOnlyList<V2ResolvedVmNetworkInterface> BuildPreparedNics(
        RuntimeVmState state,
        IReadOnlyList<V2ResolvedVmPlanningContext> allPlanVms)
    {
        if (state.PlanVm.Nics.Count == 0)
        {
            return Array.Empty<V2ResolvedVmNetworkInterface>();
        }

        var orderedDcIps = GetOrderedDomainControllerIps(state, allPlanVms);
        var includeInternetFallback = state.PlanVm.Nics.Any(nic => nic.DnsServers.Any(server => string.Equals(server, "8.8.8.8", StringComparison.OrdinalIgnoreCase)));

        return state.PlanVm.Nics
            .Select(nic => new V2ResolvedVmNetworkInterface
            {
                NicId = nic.NicId,
                Name = nic.Name,
                NetworkId = nic.NetworkId,
                EffectiveSwitchName = nic.EffectiveSwitchName,
                EffectiveSwitchType = nic.EffectiveSwitchType,
                IpAddress = nic.IpAddress,
                PrefixLength = nic.PrefixLength,
                DefaultGateway = nic.DefaultGateway,
                DnsServers = BuildPreparedDnsServers(state, nic, orderedDcIps, includeInternetFallback)
            })
            .ToArray();
    }

    private static IReadOnlyList<string> BuildPreparedDnsServers(
        RuntimeVmState state,
        V2ResolvedVmNetworkInterface nic,
        IReadOnlyList<string> orderedDcIps,
        bool includeInternetFallback)
    {
        var dnsServers = new List<string>();
        if (!string.IsNullOrWhiteSpace(state.PlanVm.DomainId))
        {
            dnsServers.AddRange(orderedDcIps);
        }

        dnsServers.AddRange(nic.DnsServers.Where(server => !string.Equals(server, "8.8.8.8", StringComparison.OrdinalIgnoreCase)));
        if (includeInternetFallback || nic.DnsServers.Any(server => string.Equals(server, "8.8.8.8", StringComparison.OrdinalIgnoreCase)))
        {
            dnsServers.Add("8.8.8.8");
        }

        return dnsServers
            .Where(server => !string.IsNullOrWhiteSpace(server))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildDomainControllerDnsOrder(
        V2ResolvedVmPlanningContext targetVm,
        IReadOnlyList<V2ResolvedVmPlanningContext> allPlanVms)
    {
        var orderedDcIps = GetOrderedDomainControllerIps(targetVm, allPlanVms);
        var nicHasInternetFallback = targetVm.Nics.Any(nic => nic.DnsServers.Any(server => string.Equals(server, "8.8.8.8", StringComparison.OrdinalIgnoreCase)));
        var dnsServers = new List<string>(orderedDcIps);
        if (nicHasInternetFallback)
        {
            dnsServers.Add("8.8.8.8");
        }

        return dnsServers
            .Where(server => !string.IsNullOrWhiteSpace(server))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> GetOrderedDomainControllerIps(
        RuntimeVmState state,
        IReadOnlyList<V2ResolvedVmPlanningContext> allPlanVms)
        => GetOrderedDomainControllerIps(state.PlanVm, allPlanVms);

    private static IReadOnlyList<string> GetOrderedDomainControllerIps(
        V2ResolvedVmPlanningContext targetVm,
        IReadOnlyList<V2ResolvedVmPlanningContext> allPlanVms)
    {
        if (string.IsNullOrWhiteSpace(targetVm.DomainId))
        {
            return Array.Empty<string>();
        }

        var dcVms = allPlanVms
            .Where(vm => string.Equals(vm.DomainId, targetVm.DomainId, StringComparison.OrdinalIgnoreCase) &&
                         (string.Equals(vm.TopologyRole, "FirstDomainController", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(vm.TopologyRole, "ReplicaDomainController", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var preferredSelfIp = targetVm.Nics
            .Select(nic => nic.IpAddress)
            .FirstOrDefault(ip => !string.IsNullOrWhiteSpace(ip));

        var orderedIps = new List<string>();
        if (!string.IsNullOrWhiteSpace(preferredSelfIp))
        {
            orderedIps.Add(preferredSelfIp);
        }

        orderedIps.AddRange(dcVms
            .SelectMany(vm => vm.Nics)
            .Select(nic => nic.IpAddress)
            .Where(ip => !string.IsNullOrWhiteSpace(ip))
            .Cast<string>());

        return orderedIps
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
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
        foreach (var state in states)
        {
            await CleanupSingleVmAsync(state, multiContext);
        }
    }

    private async Task CleanupSingleVmAsync(RuntimeVmState state, MultiVmDeploymentContext multiContext)
    {
        if (!NeedsCleanup(state))
        {
            return;
        }

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

    private static bool NeedsCleanup(RuntimeVmState state)
    {
        var context = state.Context;
        return (!context.IsSuccess || context.WasCancelled) &&
               (context.VmFolderCreated || context.DifferencingDiskCreated || context.VmRegistered || context.VmStarted);
    }

    private static bool IsRootFirstDomainController(RuntimeVmState state)
        => string.Equals(state.PlanVm.TopologyRole, "FirstDomainController", StringComparison.OrdinalIgnoreCase) &&
           state.Domain?.RelationKind == V2DomainRelationKind.Root;

    private static bool IsDependentFirstDomainController(RuntimeVmState state)
        => string.Equals(state.PlanVm.TopologyRole, "FirstDomainController", StringComparison.OrdinalIgnoreCase) &&
           state.Domain?.RelationKind is V2DomainRelationKind.Child or V2DomainRelationKind.Tree;

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
        multiContext.V2TrustContexts.Clear();
        multiContext.V2NetworkSwitchContexts.Clear();
        multiContext.StopAllOnAnyVmFailure = settings.StopAllOnAnyVmFailure;
    }

    private static IReadOnlyList<V2ForestTrustAnchorState> BuildForestTrustAnchorStates(
        IReadOnlyList<RuntimeVmState> states)
        => states
            .Select(state => new V2ForestTrustAnchorState(state.PlanVm.VmId, state.Context))
            .ToArray();

    private static IReadOnlyList<V2NetworkSwitchAffectedVmState> BuildNetworkSwitchAffectedVmStates(
        IReadOnlyList<RuntimeVmState> states)
        => states
            .Select(state => new V2NetworkSwitchAffectedVmState(state.PlanVm.VmId, state.Context))
            .ToArray();

    private static List<RuntimeVmState> BuildRuntimeStates(V2RuntimeExecutionRequest request, MultiVmDeploymentContext multiContext)
    {
        var planVmById = request.Plan.Context.Vms.ToDictionary(vm => vm.VmId, StringComparer.OrdinalIgnoreCase);
        var domainById = request.Plan.Context.Domains.ToDictionary(domain => domain.DomainId, StringComparer.OrdinalIgnoreCase);
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
            var runtimeDomain = !string.IsNullOrWhiteSpace(planVm.DomainId) && domainById.TryGetValue(planVm.DomainId, out var resolvedDomain)
                ? resolvedDomain
                : null;

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
                V2MembershipMode = planVm.MembershipMode,
                V2DomainId = planVm.DomainId,
                V2DomainDnsName = runtimeDomain?.DnsName,
                V2ForestId = runtimeDomain?.ForestId,
                V2BootstrapCredentialSlot = planVm.EffectiveBootstrapCredentialSlot,
                V2DomainAdminCredentialSlot = planVm.EffectiveDomainAdminCredentialSlot,
                V2DomainJoinCredentialSlot = planVm.EffectiveDomainJoinCredentialSlot,
                V2DsrmCredentialSlot = planVm.EffectiveDsrmCredentialSlot
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
            states.Add(new RuntimeVmState(
                context,
                vm,
                planVm,
                runtimeDomain,
                nodes ?? [],
                planVm.RequiresRouterDependency,
                planVm.ExpectsRouterEgress));
        }

        foreach (var state in states)
        {
            state.AllStates = states;
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
            V2ResolvedDomainPlanningContext? domain,
            IReadOnlyList<V2PlanNode> nodes,
            bool requiresRouterDependency,
            bool expectsRouterEgress)
        {
            Context = context;
            TemplateVm = templateVm;
            PlanVm = planVm;
            Domain = domain;
            RequiresRouterDependency = requiresRouterDependency;
            ExpectsRouterEgress = expectsRouterEgress;
            _nodes = nodes
                .GroupBy(node => node.Kind)
                .ToDictionary(group => group.Key, group => group.First());
        }

        public VmDeploymentContext Context { get; }

        public VmTemplate TemplateVm { get; }

        public V2ResolvedVmPlanningContext PlanVm { get; }

        public V2ResolvedDomainPlanningContext? Domain { get; }

        public bool RequiresRouterDependency { get; }

        public bool ExpectsRouterEgress { get; }

        public IReadOnlyList<RuntimeVmState> AllStates { get; set; } = Array.Empty<RuntimeVmState>();

        public IReadOnlyList<RuntimeRouterAdapter>? RouterAdapters { get; set; }

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

    private sealed record RuntimeRouterAdapter(
        string SwitchName,
        string? SwitchType,
        string AdapterName,
        string MacAddress,
        V2ResolvedVmNetworkInterface Nic);
}
