using LabAssistant.Business.Deployment;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;

namespace LabAssistant.WinUI.ViewModels.Deploy;

internal interface IDeployOnTheFlyWorkspaceControllerHost
{
    AppSettings DeploymentSettings { get; }

    IReadOnlyList<string> AvailableSwitches { get; }

    IReadOnlyList<VhdxCatalogItem> LoadCatalogItems();

    bool TryApplyVmFields(bool showSuccessStatus, bool showValidationErrors = true);

    void SetActionStatus(string statusText);

    LabTemplate BuildTemplate();

    Task EnsureReferenceDataAsync(bool forceRefresh);

    Task<DeploymentReadinessReport> RunReadinessChecksAsync(MultiVmDeploymentContext context, DeploymentPreflightMode mode);

    void UpdateUi();

    void BeginDeployWorkflow();

    void PrepareDeployExecution(MultiVmDeploymentContext context);

    Task<DeploymentOutcomeSummary> DeployAllAsync(MultiVmDeploymentContext context);

    void SetDeployBlocked();

    void ApplyDeploySummary(DeploymentOutcomeSummary summary);

    void SetDeployFailed(string errorMessage);

    void FinalizeDeployWorkflow();
}

internal sealed class DeployOnTheFlyWorkspaceController
{
    private readonly DeployOnTheFlyWorkspaceViewModel _workspace;
    private readonly IDeployOnTheFlyWorkspaceControllerHost _host;
    private int _autoEvaluateNonce;

    public DeployOnTheFlyWorkspaceController(
        DeployOnTheFlyWorkspaceViewModel workspace,
        IDeployOnTheFlyWorkspaceControllerHost host)
    {
        _workspace = workspace;
        _host = host;
    }

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
        _host.BeginDeployWorkflow();
        try
        {
            await EvaluateReadinessAsync(DeploymentPreflightMode.Full);
            if (_workspace.HasBlockingFailures)
            {
                _host.SetDeployBlocked();
                return;
            }

            var template = _host.BuildTemplate();
            var deployContext = DeployContextBuilder.Build(
                template,
                _host.DeploymentSettings,
                _host.LoadCatalogItems(),
                _host.AvailableSwitches);
            _host.PrepareDeployExecution(deployContext.MultiVmContext);
            var summary = await _host.DeployAllAsync(deployContext.MultiVmContext);
            _host.ApplyDeploySummary(summary);
        }
        catch (Exception ex)
        {
            _host.SetDeployFailed(ex.Message);
        }
        finally
        {
            _workspace.EndStarting();
            _host.FinalizeDeployWorkflow();
        }
    }

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

            var blockingCount = deployContext.CompatibilityIssues.Count(issue => issue.IsBlocking) +
                                readinessReport.Results.Count(result => result.Status == DeploymentReadinessStatus.Fail);
            var warningCount = deployContext.CompatibilityIssues.Count(issue => !issue.IsBlocking) +
                               readinessReport.Results.Count(result => result.Status == DeploymentReadinessStatus.Warn);
            var readinessSummaryText = blockingCount > 0
                ? $"Readiness blocked ({blockingCount} fail, {warningCount} warn)."
                : warningCount > 0
                    ? $"Readiness passed with warnings ({warningCount})."
                    : "Readiness passed.";

            _workspace.ApplyReadinessResult(
                deployContext.CompatibilityIssues,
                readinessReport,
                readinessSummaryText);

            _workspace.SetWorkflowState(
                lifecycleState: blockingCount > 0 ? "Blocked" : warningCount > 0 ? "Warning" : "Ready",
                progressPercent: blockingCount > 0 ? 35 : warningCount > 0 ? 45 : 55,
                progressSummary: blockingCount > 0
                    ? "Readiness blocked."
                    : warningCount > 0
                        ? "Readiness passed with warnings."
                        : "Readiness passed.");
            _host.SetActionStatus(
                blockingCount > 0
                    ? "Deploy blocked by readiness failures. Resolve blocking items first."
                    : warningCount > 0
                        ? $"Readiness passed with {warningCount} warning(s)."
                        : "Readiness passed with no issues.");
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
        await Task.Delay(350);
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
}
