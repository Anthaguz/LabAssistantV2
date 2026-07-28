using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.PowerShell;
using LabAssistant.Models.Templates;
using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.GuestExecution;
using LabAssistant.Services.Logging;

namespace LabAssistant.Business.Runtime;

/// <summary>
/// Executes the managed bidirectional forest-trust runtime slice after both anchor forests are ready.
/// </summary>
public sealed class V2ForestTrustRuntimeStage
{
    private readonly V2ForestTrustRuntimeCoordinator _forestTrustRuntimeCoordinator;
    private readonly IStructuredLogger _structuredLogger;

    public V2ForestTrustRuntimeStage(
        IGuestCommandExecutor guestCommandExecutor,
        IStructuredLogger? structuredLogger = null)
        : this(new V2ForestTrustRuntimeCoordinator(guestCommandExecutor), structuredLogger)
    {
    }

    internal V2ForestTrustRuntimeStage(
        V2ForestTrustRuntimeCoordinator forestTrustRuntimeCoordinator,
        IStructuredLogger? structuredLogger = null)
    {
        _forestTrustRuntimeCoordinator = forestTrustRuntimeCoordinator;
        _structuredLogger = structuredLogger ?? NullStructuredLogger.Instance;
    }

    public IReadOnlyList<V2TrustRuntimeContext> InitializeRuntimeState(
        V2RuntimeExecutionRequest request,
        IReadOnlyList<V2ForestTrustAnchorState> anchorStates,
        MultiVmDeploymentContext multiContext)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(anchorStates);
        ArgumentNullException.ThrowIfNull(multiContext);

        var anchorVmIds = anchorStates
            .Select(state => state.VmId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var contexts = new List<V2TrustRuntimeContext>();

        foreach (var trust in request.Plan.Context.Trusts)
        {
            if (!anchorVmIds.Contains(trust.SourceAnchorVmId) ||
                !anchorVmIds.Contains(trust.TargetAnchorVmId))
            {
                continue;
            }

            var context = new V2TrustRuntimeContext
            {
                TrustId = trust.TrustId,
                SourceDomainId = trust.SourceDomainId,
                TargetDomainId = trust.TargetDomainId
            };
            multiContext.V2TrustContexts.Add(context);
            contexts.Add(context);
        }

        return contexts;
    }

    public async Task ExecuteAsync(
        V2RuntimeExecutionRequest request,
        MultiVmDeploymentContext multiContext,
        IReadOnlyList<V2ForestTrustAnchorState> anchorStates,
        ISet<string> executedNodeIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(multiContext);
        ArgumentNullException.ThrowIfNull(anchorStates);
        ArgumentNullException.ThrowIfNull(executedNodeIds);

        if (multiContext.IsCancellationRequested || request.Plan.Context.Trusts.Count == 0)
        {
            return;
        }

        var stateByVmId = anchorStates.ToDictionary(state => state.VmId, StringComparer.OrdinalIgnoreCase);
        foreach (var trust in request.Plan.Context.Trusts.OrderBy(item => item.TrustId, StringComparer.OrdinalIgnoreCase))
        {
            if (multiContext.IsCancellationRequested)
            {
                return;
            }

            if (!stateByVmId.TryGetValue(trust.SourceAnchorVmId, out var sourceState) ||
                !stateByVmId.TryGetValue(trust.TargetAnchorVmId, out var targetState) ||
                !sourceState.IsReady ||
                !targetState.IsReady)
            {
                continue;
            }

            foreach (var kind in new[] { V2PlanNodeKind.PrepareForestTrustDns, V2PlanNodeKind.CreateForestTrust, V2PlanNodeKind.ValidateForestTrust })
            {
                var node = request.Plan.Nodes.FirstOrDefault(candidate =>
                    candidate.Kind == kind &&
                    string.Equals(candidate.TrustId, trust.TrustId, StringComparison.OrdinalIgnoreCase));
                if (node is null)
                {
                    continue;
                }

                await ExecuteTrustPlanNodeAsync(sourceState.Context, node, request, multiContext, executedNodeIds, cancellationToken);
                if (!sourceState.Context.IsSuccess || multiContext.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }

    public async Task CleanupFailedOrCancelledAsync(
        V2RuntimeExecutionRequest request,
        MultiVmDeploymentContext multiContext,
        IReadOnlyList<V2TrustRuntimeContext> trustStates)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(multiContext);
        ArgumentNullException.ThrowIfNull(trustStates);

        if (!multiContext.IsCancellationRequested && multiContext.VmContexts.All(vm => vm.IsSuccess))
        {
            return;
        }

        foreach (var trustState in trustStates.Where(state => state.TrustObjectsCreated))
        {
            var trust = request.Plan.Context.Trusts.FirstOrDefault(candidate =>
                string.Equals(candidate.TrustId, trustState.TrustId, StringComparison.OrdinalIgnoreCase));
            if (trust is null)
            {
                continue;
            }

            multiContext.MarkCleanupInProgress();
            trustState.CleanupAttempted = true;
            EmitTrustEvent(LaStatus.DeployForestTrust_CleaningUpForestTrust, multiContext, trust, "cleanup", "started");

            var sourceCredentialAvailable = request.CredentialSlotValues.TryGetValue(trust.SourceDomainAdminCredentialSlot ?? string.Empty, out var sourceCredential);
            var targetCredentialAvailable = request.CredentialSlotValues.TryGetValue(trust.TargetDomainAdminCredentialSlot ?? string.Empty, out var targetCredential);
            if (!sourceCredentialAvailable || !targetCredentialAvailable)
            {
                trustState.CleanupResidual = true;
                EmitTrustEvent(LaStatus.DeployForestTrust_ForestTrustCleanupFailed, multiContext, trust, "cleanup", "failed", "Missing source or target domain-admin credential material for trust cleanup.");
                continue;
            }

            // Cleanup runs during deploy failure/cancellation to honor the no-orphans mandate, so it must not consult
            // the already-cancelled deploy abort signal: pass a null context (skips the abort check and per-attempt
            // logging) and CancellationToken.None, while still retrying a transient PowerShell Direct drop so a blip
            // does not leave a dangling trust. The GetADTrust-guarded delete script is idempotent, so re-running is safe.
            var sourceResult = await GuestStepTransportRetry.RunAsync(
                null,
                request,
                DeploymentStepKeys.V2CreateForestTrust,
                attemptCancellation => _forestTrustRuntimeCoordinator.CleanupForestTrustAsync(
                    trust.SourceAnchorVmName,
                    sourceCredential!,
                    trust.TargetDomainDnsName,
                    attemptCancellation),
                CancellationToken.None);
            var targetResult = await GuestStepTransportRetry.RunAsync(
                null,
                request,
                DeploymentStepKeys.V2CreateForestTrust,
                attemptCancellation => _forestTrustRuntimeCoordinator.CleanupForestTrustAsync(
                    trust.TargetAnchorVmName,
                    targetCredential!,
                    trust.SourceDomainDnsName,
                    attemptCancellation),
                CancellationToken.None);

            trustState.CleanupResidual = !sourceResult.Success || !targetResult.Success;
            var error = string.Join(
                " ",
                new[] { sourceResult.Error, targetResult.Error }.Where(value => !string.IsNullOrWhiteSpace(value)));
            EmitTrustEvent(
                trustState.CleanupResidual ? LaStatus.DeployForestTrust_ForestTrustCleanupFailed : LaStatus.DeployForestTrust_ForestTrustCleanedUp,
                multiContext,
                trust,
                "cleanup",
                trustState.CleanupResidual ? "failed" : "success",
                string.IsNullOrWhiteSpace(error) ? null : error);
        }
    }

    /// <summary>
    /// Executes a single forest-trust plan node (prepare DNS, create, or validate) for the graph scheduler.
    /// Ordering (both anchors ready, prepare -&gt; create -&gt; validate) is enforced by the plan's dependency edges, so this
    /// method only resolves the trust and its source anchor context and runs the existing per-node step unchanged.
    /// Returns whether the node executed, whether it succeeded, and whether it was cancelled, so the caller can signal
    /// failure to the scheduler without this stage taking a dependency on scheduler types.
    /// </summary>
    internal async Task<(bool Executed, bool Success, bool WasCancelled)> ExecuteTrustNodeAsync(
        V2RuntimeExecutionRequest request,
        V2PlanNode node,
        IReadOnlyList<V2ForestTrustAnchorState> anchorStates,
        MultiVmDeploymentContext multiContext,
        ISet<string> executedNodeIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(anchorStates);
        ArgumentNullException.ThrowIfNull(multiContext);
        ArgumentNullException.ThrowIfNull(executedNodeIds);

        var trust = request.Plan.Context.Trusts.FirstOrDefault(candidate =>
            string.Equals(candidate.TrustId, node.TrustId, StringComparison.OrdinalIgnoreCase));
        if (trust is null)
        {
            return (false, true, false);
        }

        var contextByVmId = anchorStates.ToDictionary(state => state.VmId, StringComparer.OrdinalIgnoreCase);
        if (!contextByVmId.TryGetValue(trust.SourceAnchorVmId, out var sourceState))
        {
            return (false, true, false);
        }

        var wasSuccessBefore = sourceState.Context.IsSuccess;
        await ExecuteTrustPlanNodeAsync(sourceState.Context, node, request, multiContext, executedNodeIds, cancellationToken);
        var success = !(wasSuccessBefore && !sourceState.Context.IsSuccess);
        return (true, success, sourceState.Context.WasCancelled);
    }

    private async Task ExecuteTrustPlanNodeAsync(
        VmDeploymentContext context,
        V2PlanNode node,
        V2RuntimeExecutionRequest request,
        MultiVmDeploymentContext multiContext,
        ISet<string> executedNodeIds,
        CancellationToken cancellationToken)
    {
        switch (node.Kind)
        {
            case V2PlanNodeKind.PrepareForestTrustDns:
                await ExecuteTrustStepAsync(
                    context,
                    DeploymentStepKeys.V2PrepareForestTrustDns,
                    "Prepare forest trust DNS",
                    vmContext => PrepareForestTrustDnsAsync(node, request, multiContext, vmContext, cancellationToken),
                    multiContext,
                    cancellationToken);
                executedNodeIds.Add(node.NodeId);
                break;

            case V2PlanNodeKind.CreateForestTrust:
                await ExecuteTrustStepAsync(
                    context,
                    DeploymentStepKeys.V2CreateForestTrust,
                    "Create forest trust",
                    vmContext => CreateForestTrustAsync(node, request, multiContext, vmContext, cancellationToken),
                    multiContext,
                    cancellationToken);
                executedNodeIds.Add(node.NodeId);
                break;

            case V2PlanNodeKind.ValidateForestTrust:
                await ExecuteTrustStepAsync(
                    context,
                    DeploymentStepKeys.V2ValidateForestTrust,
                    "Validate forest trust",
                    vmContext => ValidateForestTrustAsync(node, request, multiContext, vmContext, cancellationToken),
                    multiContext,
                    cancellationToken);
                executedNodeIds.Add(node.NodeId);
                break;
        }
    }

    private async Task PrepareForestTrustDnsAsync(
        V2PlanNode node,
        V2RuntimeExecutionRequest request,
        MultiVmDeploymentContext multiContext,
        VmDeploymentContext context,
        CancellationToken cancellationToken)
    {
        var trust = ResolveTrust(node, request, context, DeploymentStepKeys.V2PrepareForestTrustDns);
        if (trust is null)
        {
            return;
        }

        EmitTrustEvent(LaStatus.DeployForestTrust_PreparingDNSForForestTrust, multiContext, trust, "dns-prep", "started");
        var sourceCredential = ResolveCredential(
            request.CredentialSlotValues,
            trust.SourceDomainAdminCredentialSlot,
            context,
            DeploymentStepKeys.V2PrepareForestTrustDns,
            "source domain-admin");
        var targetCredential = ResolveCredential(
            request.CredentialSlotValues,
            trust.TargetDomainAdminCredentialSlot,
            context,
            DeploymentStepKeys.V2PrepareForestTrustDns,
            "target domain-admin");
        if (sourceCredential is null || targetCredential is null)
        {
            EmitTrustEvent(LaStatus.DeployForestTrust_DNSPreparationFailed, multiContext, trust, "dns-prep", "failed", "Missing source or target domain-admin credential material.");
            return;
        }

        // On a promoted DC the local SAM is gone, so PowerShell Direct must authenticate DOMAIN\User; the target
        // credential is also used cross-forest to authenticate to the target forest when creating the trust and
        // must name the target forest, not the source.
        sourceCredential = QualifyDomainCredential(sourceCredential, trust.SourceDomainNetBiosName);
        targetCredential = QualifyDomainCredential(targetCredential, trust.TargetDomainNetBiosName);

        var sourceDnsServers = GetDomainControllerDnsServers(trust.SourceDomainId, request.Plan.Context.Vms);
        var targetDnsServers = GetDomainControllerDnsServers(trust.TargetDomainId, request.Plan.Context.Vms);
        if (sourceDnsServers.Count == 0 || targetDnsServers.Count == 0)
        {
            context.MarkFailure(
                DeploymentStepKeys.V2PrepareForestTrustDns,
                $"Trust '{trust.TrustId}' could not resolve source and target domain-controller DNS server IPs.");
            EmitTrustEvent(LaStatus.DeployForestTrust_DNSPreparationFailed, multiContext, trust, "dns-prep", "failed", "Domain-controller DNS server IPs could not be resolved.");
            return;
        }

        var sourceResult = await GuestStepTransportRetry.RunAsync(
            context,
            request,
            DeploymentStepKeys.V2PrepareForestTrustDns,
            attemptCancellation => _forestTrustRuntimeCoordinator.PrepareDnsForwarderAsync(
                trust.SourceAnchorVmName,
                sourceCredential,
                trust.TargetDomainDnsName,
                targetDnsServers,
                attemptCancellation),
            cancellationToken);
        if (!sourceResult.Success)
        {
            context.MarkFailure(
                DeploymentStepKeys.V2PrepareForestTrustDns,
                $"Failed to prepare source-side DNS forwarding for trust '{trust.TrustId}'. {sourceResult.Error}".Trim());
            EmitTrustEvent(LaStatus.DeployForestTrust_DNSPreparationFailed, multiContext, trust, "dns-prep", "failed", sourceResult.Error);
            return;
        }

        var targetResult = await GuestStepTransportRetry.RunAsync(
            context,
            request,
            DeploymentStepKeys.V2PrepareForestTrustDns,
            attemptCancellation => _forestTrustRuntimeCoordinator.PrepareDnsForwarderAsync(
                trust.TargetAnchorVmName,
                targetCredential,
                trust.SourceDomainDnsName,
                sourceDnsServers,
                attemptCancellation),
            cancellationToken);
        if (!targetResult.Success)
        {
            context.MarkFailure(
                DeploymentStepKeys.V2PrepareForestTrustDns,
                $"Failed to prepare target-side DNS forwarding for trust '{trust.TrustId}'. {targetResult.Error}".Trim());
            EmitTrustEvent(LaStatus.DeployForestTrust_DNSPreparationFailed, multiContext, trust, "dns-prep", "failed", targetResult.Error);
            return;
        }

        EmitTrustEvent(LaStatus.DeployForestTrust_DNSPreparedForForestTrust, multiContext, trust, "dns-prep", "success");
    }

    private async Task CreateForestTrustAsync(
        V2PlanNode node,
        V2RuntimeExecutionRequest request,
        MultiVmDeploymentContext multiContext,
        VmDeploymentContext context,
        CancellationToken cancellationToken)
    {
        var trust = ResolveTrust(node, request, context, DeploymentStepKeys.V2CreateForestTrust);
        if (trust is null)
        {
            return;
        }

        EmitTrustEvent(LaStatus.DeployForestTrust_CreatingForestTrust, multiContext, trust, "create", "started");
        var sourceCredential = ResolveCredential(
            request.CredentialSlotValues,
            trust.SourceDomainAdminCredentialSlot,
            context,
            DeploymentStepKeys.V2CreateForestTrust,
            "source domain-admin");
        var targetCredential = ResolveCredential(
            request.CredentialSlotValues,
            trust.TargetDomainAdminCredentialSlot,
            context,
            DeploymentStepKeys.V2CreateForestTrust,
            "target domain-admin");
        if (sourceCredential is null || targetCredential is null)
        {
            EmitTrustEvent(LaStatus.DeployForestTrust_ForestTrustCreationFailed, multiContext, trust, "create", "failed", "Missing source or target domain-admin credential material.");
            return;
        }

        sourceCredential = QualifyDomainCredential(sourceCredential, trust.SourceDomainNetBiosName);
        targetCredential = QualifyDomainCredential(targetCredential, trust.TargetDomainNetBiosName);

        MarkTrustObjectsCreated(multiContext, trust.TrustId);
        var result = await GuestStepTransportRetry.RunAsync(
            context,
            request,
            DeploymentStepKeys.V2CreateForestTrust,
            attemptCancellation => _forestTrustRuntimeCoordinator.CreateBidirectionalForestTrustAsync(
                trust.SourceAnchorVmName,
                sourceCredential,
                trust,
                targetCredential,
                attemptCancellation),
            cancellationToken);
        if (!result.Success)
        {
            context.MarkFailure(
                DeploymentStepKeys.V2CreateForestTrust,
                $"Failed to create forest trust '{trust.TrustId}'. {result.Error}".Trim());
            EmitTrustEvent(LaStatus.DeployForestTrust_ForestTrustCreationFailed, multiContext, trust, "create", "failed", result.Error);
            return;
        }

        EmitTrustEvent(LaStatus.DeployForestTrust_ForestTrustCreated, multiContext, trust, "create", "success");
    }

    private async Task ValidateForestTrustAsync(
        V2PlanNode node,
        V2RuntimeExecutionRequest request,
        MultiVmDeploymentContext multiContext,
        VmDeploymentContext context,
        CancellationToken cancellationToken)
    {
        var trust = ResolveTrust(node, request, context, DeploymentStepKeys.V2ValidateForestTrust);
        if (trust is null)
        {
            return;
        }

        EmitTrustEvent(LaStatus.DeployForestTrust_ValidatingForestTrust, multiContext, trust, "validate", "started");
        var sourceCredential = ResolveCredential(
            request.CredentialSlotValues,
            trust.SourceDomainAdminCredentialSlot,
            context,
            DeploymentStepKeys.V2ValidateForestTrust,
            "source domain-admin");
        var targetCredential = ResolveCredential(
            request.CredentialSlotValues,
            trust.TargetDomainAdminCredentialSlot,
            context,
            DeploymentStepKeys.V2ValidateForestTrust,
            "target domain-admin");
        if (sourceCredential is null || targetCredential is null)
        {
            EmitTrustEvent(LaStatus.DeployForestTrust_ForestTrustValidationFailed, multiContext, trust, "validate", "failed", "Missing source or target domain-admin credential material.");
            return;
        }

        sourceCredential = QualifyDomainCredential(sourceCredential, trust.SourceDomainNetBiosName);
        targetCredential = QualifyDomainCredential(targetCredential, trust.TargetDomainNetBiosName);

        var sourceValidated = await ValidateTrustSideWithRetryAsync(
            request,
            context,
            trust.SourceAnchorVmName,
            sourceCredential,
            trust.TargetDomainDnsName,
            cancellationToken);
        if (!sourceValidated.Success)
        {
            context.MarkFailure(
                DeploymentStepKeys.V2ValidateForestTrust,
                $"Source-side validation failed for forest trust '{trust.TrustId}'. {sourceValidated.Error}".Trim());
            EmitTrustEvent(LaStatus.DeployForestTrust_ForestTrustValidationFailed, multiContext, trust, "validate", "failed", sourceValidated.Error);
            return;
        }

        var targetValidated = await ValidateTrustSideWithRetryAsync(
            request,
            context,
            trust.TargetAnchorVmName,
            targetCredential,
            trust.SourceDomainDnsName,
            cancellationToken);
        if (!targetValidated.Success)
        {
            context.MarkFailure(
                DeploymentStepKeys.V2ValidateForestTrust,
                $"Target-side validation failed for forest trust '{trust.TrustId}'. {targetValidated.Error}".Trim());
            EmitTrustEvent(LaStatus.DeployForestTrust_ForestTrustValidationFailed, multiContext, trust, "validate", "failed", targetValidated.Error);
            return;
        }

        MarkTrustReady(multiContext, trust.TrustId);
        EmitTrustEvent(LaStatus.DeployForestTrust_ForestTrustValidated, multiContext, trust, "validate", "success");
    }

    // AD trust objects replicate asynchronously, so validation is retried up to the shared
    // guest-transport retry budget before the side is treated as failed. Mirrors the AD readiness gates.
    private async Task<GuestCommandResult> ValidateTrustSideWithRetryAsync(
        V2RuntimeExecutionRequest request,
        VmDeploymentContext context,
        string anchorVmName,
        V2RuntimeCredential domainAdminCredential,
        string trustedDomainName,
        CancellationToken cancellationToken)
    {
        GuestCommandResult result = new() { Success = false, Error = "Forest trust validation was not attempted." };
        var startTick = Environment.TickCount64;
        for (var attempt = 1; attempt <= request.GuestTransportMaxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (context.ShouldAbort?.Invoke() == true)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            result = await _forestTrustRuntimeCoordinator.ValidateForestTrustAsync(
                anchorVmName,
                domainAdminCredential,
                trustedDomainName,
                cancellationToken);
            if (result.Success)
            {
                return result;
            }

            GuestReadinessLog.Attempt(
                context,
                DeploymentStepKeys.V2ValidateForestTrust,
                attempt,
                request.GuestTransportMaxRetries,
                Environment.TickCount64 - startTick,
                result.Error);
            if (attempt < request.GuestTransportMaxRetries)
            {
                await Task.Delay(request.GuestTransportRetryDelay, cancellationToken);
            }
        }

        return result;
    }

    private async Task ExecuteTrustStepAsync(
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
        EmitStepEvent(context, multiContext, LaStatus.DeployStep_StepStarted, null, stepKey);

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

        EmitStepEvent(context, multiContext, LaStatus.DeployStep_StepCompleted, result, stepKey);
        context.EmitStepState(stepKey, stepLabel, terminalState, overrideMessage);
    }

    private static V2ResolvedTrustPlanningContext? ResolveTrust(
        V2PlanNode node,
        V2RuntimeExecutionRequest request,
        VmDeploymentContext context,
        string stepKey)
    {
        if (string.IsNullOrWhiteSpace(node.TrustId))
        {
            context.MarkFailure(stepKey, $"Trust runtime node '{node.NodeId}' is missing a resolved trust id.");
            return null;
        }

        var trust = request.Plan.Context.Trusts.FirstOrDefault(candidate =>
            string.Equals(candidate.TrustId, node.TrustId, StringComparison.OrdinalIgnoreCase));
        if (trust is null)
        {
            context.MarkFailure(stepKey, $"Trust runtime node '{node.NodeId}' references unknown trust '{node.TrustId}'.");
            return null;
        }

        return trust;
    }

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

    /// <summary>
    /// Prefixes a bare admin username with the domain NetBIOS name (DOMAIN\User) so PowerShell Direct authenticates
    /// against the directory rather than a (now-absent) local SAM account on a promoted DC, and so the cross-forest
    /// target credential used to create the cross-forest trust unambiguously names the target forest. An already-qualified username
    /// (containing '\' or '@') or a missing NetBIOS name is returned unchanged.
    /// </summary>
    private static V2RuntimeCredential QualifyDomainCredential(V2RuntimeCredential credential, string? netBiosName)
    {
        var username = credential.Username ?? string.Empty;
        if (string.IsNullOrWhiteSpace(netBiosName) ||
            username.Contains('\\', StringComparison.Ordinal) ||
            username.Contains('@', StringComparison.Ordinal))
        {
            return credential;
        }

        return new V2RuntimeCredential
        {
            Username = $"{netBiosName}\\{username}",
            Password = credential.Password
        };
    }

    private static void MarkTrustObjectsCreated(MultiVmDeploymentContext multiContext, string trustId)
    {
        var context = multiContext.V2TrustContexts.FirstOrDefault(candidate =>
            string.Equals(candidate.TrustId, trustId, StringComparison.OrdinalIgnoreCase));
        if (context is not null)
        {
            context.TrustObjectsCreated = true;
        }
    }

    private static void MarkTrustReady(MultiVmDeploymentContext multiContext, string trustId)
    {
        var context = multiContext.V2TrustContexts.FirstOrDefault(candidate =>
            string.Equals(candidate.TrustId, trustId, StringComparison.OrdinalIgnoreCase));
        if (context is not null)
        {
            context.TrustReady = true;
        }
    }

    private static IReadOnlyList<string> GetDomainControllerDnsServers(
        string domainId,
        IReadOnlyList<V2ResolvedVmPlanningContext> planVms)
    {
        return planVms
            .Where(vm => string.Equals(vm.DomainId, domainId, StringComparison.OrdinalIgnoreCase) &&
                         (string.Equals(vm.TopologyRole, "FirstDomainController", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(vm.TopologyRole, "ReplicaDomainController", StringComparison.OrdinalIgnoreCase)))
            .SelectMany(vm => vm.Nics)
            .Select(nic => nic.IpAddress)
            .Where(ip => !string.IsNullOrWhiteSpace(ip))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private void EmitTrustEvent(
        uint code,
        MultiVmDeploymentContext multiContext,
        V2ResolvedTrustPlanningContext trust,
        string phase,
        string result,
        string? error = null)
    {
        var context = new Dictionary<string, object?>
        {
            ["executionEngine"] = "V2",
            ["trustId"] = trust.TrustId,
            ["sourceDomainId"] = trust.SourceDomainId,
            ["targetDomainId"] = trust.TargetDomainId,
            ["sourceDomainName"] = trust.SourceDomainDnsName,
            ["targetDomainName"] = trust.TargetDomainDnsName,
            ["phase"] = phase,
            ["stepKey"] = GetTrustStepKey(phase)
        };

        if (!string.IsNullOrWhiteSpace(error))
        {
            context["error"] = error;
        }

        _structuredLogger.Log(code, multiContext.OperationId, result, context);
    }

    private void EmitStepEvent(
        VmDeploymentContext context,
        MultiVmDeploymentContext multiContext,
        uint code,
        string? result,
        string stepKey)
    {
        var payload = new Dictionary<string, object?>
        {
            ["vmId"] = context.VmId,
            ["vmName"] = context.VmName,
            ["executionEngine"] = "V2",
            ["topologyRole"] = context.V2TopologyRole,
            ["stepKey"] = stepKey
        };

        _structuredLogger.Log(code, multiContext.OperationId, result, payload);
    }

    private static string GetTrustStepKey(string phase) => phase switch
    {
        "dns-prep" => DeploymentStepKeys.V2PrepareForestTrustDns,
        "create" => DeploymentStepKeys.V2CreateForestTrust,
        "validate" => DeploymentStepKeys.V2ValidateForestTrust,
        "cleanup" => DeploymentStepKeys.V2CleanupForestTrust,
        _ => phase
    };
}

/// <summary>
/// Minimal VM state the forest-trust stage needs from the central runtime dispatcher.
/// </summary>
public sealed record V2ForestTrustAnchorState(string VmId, VmDeploymentContext Context)
{
    public bool IsReady => Context.IsSuccess && !Context.WasCancelled;
}
