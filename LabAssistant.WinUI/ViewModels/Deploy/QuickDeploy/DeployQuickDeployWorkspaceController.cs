using LabAssistant.Business.Deployment;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Models.Deploy;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Owns Quick Deploy workflow orchestration that belongs with the long-lived workspace rather than in the shell,
/// including readiness evaluation, deploy start sequencing, and auto-evaluate debounce.
/// </summary>
internal sealed class DeployQuickDeployWorkspaceController
{
    private readonly DeployQuickDeployViewModel _workspace;
    private readonly IDeployQuickDeployWorkspaceControllerHost _host;
    private readonly int _autoEvaluateDelayMs;
    private int _autoEvaluateNonce;

    public DeployQuickDeployWorkspaceController(
        DeployQuickDeployViewModel workspace,
        IDeployQuickDeployWorkspaceControllerHost host,
        int autoEvaluateDelayMs = 350)
    {
        _workspace = workspace;
        _host = host;
        _autoEvaluateDelayMs = autoEvaluateDelayMs;
    }

    /// <summary>
    /// Starts the Quick Deploy action workflow after reconciling the current editor draft into the workspace state.
    /// Shell chrome and panel lifecycle stay outside this boundary.
    /// </summary>
    public async Task StartDeployAsync()
    {
        if (!_host.TryApplyVmFields(showSuccessStatus: false) && _workspace.SelectedVmEntry is not null)
        {
            return;
        }

        if (_workspace.VmEntryCount == 0)
        {
            _host.SetActionStatus("Add at least one VM entry first.");
            return;
        }

        _workspace.BeginStarting();
        BeginDeployWorkflow();
        try
        {
            await EvaluateReadinessAsync(DeploymentPreflightMode.Full);
            if (_workspace.HasBlockingFailures)
            {
                SetDeployBlocked();
                return;
            }

            var template = _host.BuildV2Template();
            var plan = await _host.BuildV2PlanAsync(template);
            if (!plan.Success)
            {
                SetPlanBlocked(plan);
                return;
            }

            var deploymentContext = new MultiVmDeploymentContext();
            PrepareDeployExecution(plan, deploymentContext);
            var result = await _host.ExecuteV2DeployAsync(template, plan, deploymentContext);
            ApplyDeployResult(result, deploymentContext);
        }
        catch (Exception ex)
        {
            SetDeployFailed(ex.Message);
        }
        finally
        {
            _workspace.EndStarting();
            FinalizeDeployWorkflow();
        }
    }

    /// <summary>
    /// Runs the readiness boundary for the current workspace snapshot.
    /// This method owns workflow sequencing while delegating only narrow service and UI-thread hooks through the local owner host contract.
    /// </summary>
    public async Task EvaluateReadinessAsync(DeploymentPreflightMode mode)
    {
        if (!_workspace.IsStarting)
        {
            _workspace.SetShowAllVmRows(false);
        }

        if (!_host.TryApplyVmFields(showSuccessStatus: false) && _workspace.SelectedVmEntry is not null)
        {
            return;
        }

        if (_workspace.VmEntryCount == 0)
        {
            _host.SetActionStatus("Add at least one VM entry first.");
            return;
        }

        _workspace.BeginReadinessEvaluation();
        _workspace.SetWorkflowState(
            lifecycleState: "Evaluating",
            progressPercent: mode == DeploymentPreflightMode.Full ? 18 : 12,
            progressSummary: mode == DeploymentPreflightMode.Full
                ? "Running full readiness checks..."
                : "Running quick readiness checks...");
        _host.SetActionStatus(
            mode == DeploymentPreflightMode.Full
                ? "Running full quick deploy readiness evaluation..."
                : "Running quick deploy readiness evaluation...");

        try
        {
            await _host.EnsureReferenceDataAsync(forceRefresh: false);
            var template = _host.BuildTemplate();
            var deployContext = DeployContextBuilder.Build(
                template,
                _host.DeploymentSettings,
                _host.LoadCatalogItems(),
                _host.AvailableSwitches);
            var readinessReport = await _host.RunReadinessChecksAsync(deployContext.MultiVmContext, mode);

            // Count de-duplicated issues so the summary matches the real number of problems: a single
            // concern (for example a missing base disk) is reported by both the compatibility list and the
            // readiness report and must not be counted twice.
            var mergedIssues = DeployReadinessProjection.Merge(deployContext.CompatibilityIssues, readinessReport);
            var (blockingCount, warningCount) = DeployReadinessProjection.Count(mergedIssues);

            // F34: use human-friendly readiness copy that names the first blocking reason instead of an
            // opaque "Blocked" label, so the user learns what to fix without hunting the issue list.
            var firstBlockingReason = mergedIssues.FirstOrDefault(issue => issue.IsBlocking)?.Message;
            var moreBlockers = blockingCount > 1 ? $" (+{blockingCount - 1} more)" : string.Empty;
            var readinessSummaryText = blockingCount > 0
                ? $"Not ready to deploy: {firstBlockingReason}{moreBlockers}"
                : warningCount > 0
                    ? $"Machines are ready to deploy. {warningCount} warning(s) to review."
                    : "Machines are ready to deploy.";

            _workspace.ApplyReadinessResult(
                deployContext.CompatibilityIssues,
                readinessReport,
                readinessSummaryText);

            _workspace.SetWorkflowState(
                lifecycleState: blockingCount > 0 ? "Blocked" : warningCount > 0 ? "Warning" : "Ready",
                progressPercent: blockingCount > 0 ? 0 : 100,
                progressSummary: blockingCount > 0
                    ? $"Not ready to deploy: {firstBlockingReason}{moreBlockers}"
                    : warningCount > 0
                        ? "Ready to deploy, with warnings to review."
                        : "Machines are ready to deploy.");
            _host.SetActionStatus(
                blockingCount > 0
                    ? $"Deploy blocked: {firstBlockingReason} Resolve blocking items first."
                    : warningCount > 0
                        ? $"Ready to deploy. {warningCount} warning(s) to review."
                        : "Machines are ready to deploy.");
        }
        catch (Exception ex)
        {
            _workspace.SetReadinessEvaluationFailed("Readiness evaluation failed.");
            _workspace.SetWorkflowState("Error", 0, "Readiness evaluation failed.");
            _host.SetActionStatus($"Readiness evaluation failed. {ex.Message}");
        }
        finally
        {
            _host.UpdateUi();
        }
    }

    /// <summary>
    /// Schedules a delayed readiness pass after editor changes without routing the debounce boundary back through the shell.
    /// </summary>
    public void ScheduleAutoEvaluate()
    {
        if (_workspace.IsSynchronizingEditorDraft || _workspace.IsStarting)
        {
            return;
        }

        var nonce = Interlocked.Increment(ref _autoEvaluateNonce);
        _ = DebouncedAutoEvaluateAsync(nonce);
    }

    private async Task DebouncedAutoEvaluateAsync(int nonce)
    {
        await Task.Delay(_autoEvaluateDelayMs);

        // Only the newest scheduled pass may continue, and only while the workflow is idle enough
        // to safely reconcile the draft back into workspace state before calling the current residual readiness bridge.
        if (nonce != _autoEvaluateNonce || _workspace.IsStarting || _workspace.IsEvaluatingReadiness)
        {
            return;
        }

        if (!_host.TryApplyVmFields(showSuccessStatus: false, showValidationErrors: false))
        {
            return;
        }

        _workspace.ClearReadinessState("Readiness has not been evaluated.");
        _workspace.ResetProgressState();
        _host.UpdateUi();

        await EvaluateReadinessAsync(DeploymentPreflightMode.Full);
    }

    /// <summary>
    /// Initializes the execution-visible workflow state before readiness-corrected deployment begins.
    /// </summary>
    private void BeginDeployWorkflow()
    {
        _workspace.SetShowAllVmRows(true);
        _workspace.SetWorkflowState("Running", 0, "Preparing deployment...");
        _host.UpdateUi();
    }

    /// <summary>
    /// Prepares progress rows and callback wiring for a concrete V2 deploy execution. The timeline is seeded from
    /// the plan nodes (the V2 runtime rebuilds its per-VM contexts during execution) and callbacks are wired so
    /// contexts the runtime registers at run time still stream their progress into the workspace rows.
    /// </summary>
    private void PrepareDeployExecution(V2PlanBuildResult plan, MultiVmDeploymentContext context)
    {
        _workspace.InitializeProgressRows(plan);
        AttachProgressCallbacks(context);
        _workspace.SetWorkflowState("Running", 40, $"Deploying {_workspace.LiveProgressVmCount} VM(s)...");
        _host.SetActionStatus("Starting quick deploy...");
        _host.UpdateUi();
    }

    /// <summary>
    /// Applies the blocked state after readiness fails immediately before execution.
    /// </summary>
    private void SetDeployBlocked()
    {
        _workspace.SetWorkflowState("Blocked", 0, "Deployment blocked by readiness failures.");
        _host.SetActionStatus("Deploy blocked by readiness failures. Resolve blocking items first.");
        _host.UpdateUi();
    }

    /// <summary>
    /// Applies the blocked state when the V2 planner rejects the template before any Hyper-V work starts, surfacing
    /// the plan's blocking issues so the user can correct the input.
    /// </summary>
    private void SetPlanBlocked(V2PlanBuildResult plan)
    {
        var blockingIssues = plan.Issues
            .Where(issue => issue.Severity == V2PlanIssueSeverity.Blocking)
            .ToList();
        _workspace.ApplyPlanBlockers(blockingIssues);

        var firstBlocker = blockingIssues.FirstOrDefault()?.Message;
        _workspace.SetWorkflowState(
            "Blocked",
            0,
            firstBlocker is null ? "Deployment blocked by the deployment plan." : firstBlocker);
        _host.SetActionStatus(
            blockingIssues.Count > 0
                ? $"Deploy blocked by {blockingIssues.Count} plan issue(s). Resolve them first."
                : "Deploy blocked: the deployment plan could not be built.");
        _host.UpdateUi();
    }

    /// <summary>
    /// Applies the finished V2 execution result to workspace-owned rows and workflow state. Per-VM rows already
    /// reflect the live step states streamed during execution; this projects the terminal lifecycle and any
    /// blocking messages the runtime returned.
    /// </summary>
    private void ApplyDeployResult(V2RuntimeExecutionResult result, MultiVmDeploymentContext context)
    {
        var lifecycleState = result.Success
            ? "Completed"
            : context.IsCancellationRequested ? "Cancelled" : "Failed";
        var progressSummary = result.Success
            ? "V2 deployment completed."
            : result.BlockingMessages.Count > 0
                ? string.Join(" ", result.BlockingMessages)
                : context.IsCancellationRequested
                    ? "V2 deployment cancelled."
                    : "V2 deployment finished with failures.";

        _workspace.ApplyV2ExecutionResult(result);
        _workspace.SetWorkflowState(lifecycleState, 100, progressSummary);
        _host.SetActionStatus(
            result.Success
                ? "Deployment finished successfully."
                : $"Deployment finished: {lifecycleState}.");
        _host.UpdateUi();
    }

    /// <summary>
    /// Applies the failed execution state after an exception escapes the deploy pipeline.
    /// </summary>
    private void SetDeployFailed(string errorMessage)
    {
        _workspace.SetWorkflowState("Failed", 100, "Deployment failed.");
        _host.SetActionStatus($"Deploy failed. {errorMessage}");
        _host.UpdateUi();
    }

    /// <summary>
    /// Restores the post-execution shell-visible row mode after deployment exits.
    /// </summary>
    private void FinalizeDeployWorkflow()
    {
        _workspace.SetShowAllVmRows(true);
        _host.UpdateUi();
    }

    private void AttachProgressCallbacks(MultiVmDeploymentContext context)
    {
        void Wire(VmDeploymentContext vmContext)
        {
            var vmName = string.IsNullOrWhiteSpace(vmContext.VmName) ? "Unnamed-VM" : vmContext.VmName.Trim();
            vmContext.LogCallback = message => _host.EnqueueUiUpdate(() =>
            {
                _workspace.UpdateProgressMessage(vmName, message);
                _host.UpdateUi();
            });
            vmContext.StepStateEmitter = update => _host.EnqueueUiUpdate(() =>
            {
                _workspace.ApplyProgressUpdate(vmName, update);
                _host.UpdateUi();
            });
        }

        // Wire any contexts that already exist, plus those the V2 runtime rebuilds during execution: it clears and
        // repopulates VmContexts under the shared deployment context, so without this hook its step-state and log
        // callbacks would attach to nothing.
        foreach (var vmContext in context.VmContexts)
        {
            Wire(vmContext);
        }

        context.VmContextRegistered = Wire;
    }
}
