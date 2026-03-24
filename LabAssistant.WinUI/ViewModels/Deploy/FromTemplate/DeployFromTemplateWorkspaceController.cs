using LabAssistant.Business.Deployment;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Models.Deploy;

namespace LabAssistant.WinUI.ViewModels.Deploy;

internal sealed class DeployFromTemplateWorkspaceController
{
    private readonly DeployFromTemplateWorkspaceViewModel _workspace;
    private readonly IDeployFromTemplateWorkspaceControllerHost _host;

    public DeployFromTemplateWorkspaceController(
        DeployFromTemplateWorkspaceViewModel workspace,
        IDeployFromTemplateWorkspaceControllerHost host)
    {
        _workspace = workspace;
        _host = host;
    }

    public async Task EvaluateReadinessAsync(DeploymentPreflightMode mode)
    {
        if (!_workspace.IsStarting)
        {
            _workspace.SetShowAllVmRows(false);
        }

        var activeTemplateDocument = _host.ActiveTemplateDocument;
        if (activeTemplateDocument is null)
        {
            _workspace.SetActionStatus("Select a template first.");
            _host.ApplyWorkspaceState();
            return;
        }

        _workspace.SetWorkflowState(
            isEvaluatingReadiness: true,
            isStarting: _workspace.IsStarting,
            lifecycleState: "Evaluating",
            progressPercent: 10,
            progressSummary: mode == DeploymentPreflightMode.Full
                ? "Running full readiness checks..."
                : "Running quick readiness checks...");
        _workspace.SetActionStatus(
            mode == DeploymentPreflightMode.Full
                ? "Running full deploy readiness evaluation..."
                : "Running quick deploy readiness evaluation...");

        try
        {
            await _host.EnsureTemplateSwitchesAsync(forceRefresh: false);
            var deployContext = DeployContextBuilder.Build(
                activeTemplateDocument.Template,
                _host.DeploymentSettings,
                _host.LoadCatalogItems(),
                _host.AvailableSwitches);
            _host.ReplaceCompatibilityIssues(deployContext.CompatibilityIssues);

            var readinessReport = await _host.RunReadinessAsync(deployContext.MultiVmContext, mode);
            _host.CurrentReadinessReport = readinessReport;

            var blockingCount = deployContext.CompatibilityIssues.Count(issue => issue.IsBlocking) +
                                readinessReport.Results.Count(result => result.Status == DeploymentReadinessStatus.Fail);
            var warningCount = deployContext.CompatibilityIssues.Count(issue => !issue.IsBlocking) +
                               readinessReport.Results.Count(result => result.Status == DeploymentReadinessStatus.Warn);

            _workspace.SetWorkflowState(
                isEvaluatingReadiness: true,
                isStarting: _workspace.IsStarting,
                lifecycleState: blockingCount > 0 ? "Blocked" : warningCount > 0 ? "Warning" : "Ready",
                progressPercent: 35,
                progressSummary: blockingCount > 0
                    ? $"Readiness blocked ({blockingCount} fail, {warningCount} warn)."
                    : warningCount > 0
                        ? $"Readiness passed with warnings ({warningCount})."
                        : "Readiness passed.");
            _workspace.SetActionStatus(
                blockingCount > 0
                    ? $"Readiness found {blockingCount} blocking issue(s) and {warningCount} warning(s)."
                    : warningCount > 0
                        ? $"Readiness passed with {warningCount} warning(s)."
                        : "Readiness passed with no issues.");
        }
        catch (Exception ex)
        {
            _host.CurrentReadinessReport = null;
            _host.ReplaceCompatibilityIssues([]);
            _workspace.SetWorkflowState(
                isEvaluatingReadiness: true,
                isStarting: _workspace.IsStarting,
                lifecycleState: "Error",
                progressPercent: 0,
                progressSummary: "Readiness evaluation failed.");
            _workspace.SetActionStatus($"Readiness evaluation failed. {ex.Message}");
        }
        finally
        {
            _workspace.SetWorkflowState(
                isEvaluatingReadiness: false,
                isStarting: _workspace.IsStarting,
                lifecycleState: _workspace.LifecycleState,
                progressPercent: _workspace.ProgressPercent,
                progressSummary: _workspace.ProgressSummary);
            _host.ApplyWorkspaceState();
        }
    }

    public async Task StartDeployAsync()
    {
        var activeTemplateDocument = _host.ActiveTemplateDocument;
        if (activeTemplateDocument is null)
        {
            _workspace.SetActionStatus("Select a template first.");
            _host.ApplyWorkspaceState();
            return;
        }

        _workspace.SetWorkflowState(
            isEvaluatingReadiness: false,
            isStarting: true,
            lifecycleState: "Running",
            progressPercent: 45,
            progressSummary: "Preparing deployment...");
        _workspace.SetShowAllVmRows(true);
        _workspace.ClearResultRows();
        _host.ApplyWorkspaceState();

        try
        {
            await EvaluateReadinessAsync(DeploymentPreflightMode.Full);
            var readinessReport = _host.CurrentReadinessReport;
            var hasBlockingFailures = readinessReport?.HasBlockingFailures == true ||
                                      _workspace.IssueRows.Any(issue => string.Equals(issue.Severity, "Block", StringComparison.OrdinalIgnoreCase));
            if (hasBlockingFailures)
            {
                _workspace.SetWorkflowState(
                    isEvaluatingReadiness: false,
                    isStarting: true,
                    lifecycleState: "Blocked",
                    progressPercent: 35,
                    progressSummary: "Deployment blocked by readiness failures.");
                _workspace.SetActionStatus("Deploy blocked by readiness failures. Resolve blocking items first.");
                return;
            }

            var deployContext = DeployContextBuilder.Build(
                activeTemplateDocument.Template,
                _host.DeploymentSettings,
                _host.LoadCatalogItems(),
                _host.AvailableSwitches);
            _workspace.InitializeProgressRows(deployContext.MultiVmContext);
            _host.AttachProgressCallbacks(
                deployContext.MultiVmContext,
                (vmName, message) =>
                {
                    _workspace.UpdateProgressMessage(vmName, message);
                    _host.ApplyWorkspaceState();
                },
                (vmName, update) =>
                {
                    _workspace.ApplyProgressUpdate(vmName, update);
                    _host.ApplyWorkspaceState();
                });
            _workspace.SetWorkflowState(
                isEvaluatingReadiness: false,
                isStarting: true,
                lifecycleState: "Running",
                progressPercent: 60,
                progressSummary: $"Deploying {deployContext.MultiVmContext.VmContexts.Count} VM(s)...");
            _workspace.SetActionStatus("Starting deployment...");
            _host.ApplyWorkspaceState();

            var summary = await _host.DeployAllAsync(deployContext.MultiVmContext);
            _workspace.ApplyOutcomeSummary(summary);
            _workspace.SetWorkflowState(
                isEvaluatingReadiness: false,
                isStarting: true,
                lifecycleState: summary.OperationState switch
                {
                    DeploymentOperationState.Completed => "Completed",
                    DeploymentOperationState.Cancelled or DeploymentOperationState.CancelledWithResiduals => "Cancelled",
                    DeploymentOperationState.Failed or DeploymentOperationState.FailedWithResiduals => "Failed",
                    _ => "Completed"
                },
                progressPercent: 100,
                progressSummary: $"Completed. Success={summary.SucceededVmCount}, Failed={summary.FailedVmCount}, Cancelled={summary.CancelledVmCount}.");
            _workspace.SetActionStatus(
                $"Deployment finished: {summary.OperationState}. Total={summary.TotalVmCount}, " +
                $"Succeeded={summary.SucceededVmCount}, Failed={summary.FailedVmCount}, Residuals={summary.ResidualVmCount}.");
        }
        catch (Exception ex)
        {
            _workspace.SetWorkflowState(
                isEvaluatingReadiness: false,
                isStarting: true,
                lifecycleState: "Failed",
                progressPercent: 100,
                progressSummary: "Deployment failed.");
            _workspace.SetActionStatus($"Deploy failed. {ex.Message}");
        }
        finally
        {
            _workspace.SetShowAllVmRows(true);
            _workspace.SetWorkflowState(
                isEvaluatingReadiness: false,
                isStarting: false,
                lifecycleState: _workspace.LifecycleState,
                progressPercent: _workspace.ProgressPercent,
                progressSummary: _workspace.ProgressSummary);
            _host.ApplyWorkspaceState();
        }
    }
}
