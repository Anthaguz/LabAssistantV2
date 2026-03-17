using LabAssistant.Business.Deployment;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Models.Deploy;
using LabAssistant.WinUI.Views.Deploy;

namespace LabAssistant.WinUI.ViewModels.Deploy;

internal interface IDeployFromTemplateWorkspaceHost
{
    AppSettings DeploymentSettings { get; }

    IReadOnlyList<string> AvailableSwitches { get; }

    TemplateEditorDocument? ActiveTemplateDocument { get; }

    IReadOnlyList<VhdxCatalogItem> LoadCatalogItems();

    Task EnsureTemplateSwitchesAsync(bool forceRefresh);

    Task<DeploymentReadinessReport> RunReadinessAsync(MultiVmDeploymentContext context, DeploymentPreflightMode mode);

    void ReplaceCompatibilityIssues(IReadOnlyList<DeployCompatibilityIssue> issues);

    DeploymentReadinessReport? CurrentReadinessReport { get; set; }

    Task<DeploymentOutcomeSummary> DeployAllAsync(MultiVmDeploymentContext context);

    void AttachProgressCallbacks(
        MultiVmDeploymentContext context,
        Action<string, string?> onLogMessage,
        Action<string, DeployStepStateUpdate> onStepStateUpdated);

    void UpdateUi();
}

internal sealed class DeployFromTemplateWorkspaceHost : IDeployFromTemplateWorkspaceHost
{
    private readonly Func<AppSettings> _deploymentSettings;
    private readonly Func<IReadOnlyList<string>> _availableSwitches;
    private readonly Func<TemplateEditorDocument?> _activeTemplateDocument;
    private readonly Func<IReadOnlyList<VhdxCatalogItem>> _loadCatalogItems;
    private readonly Func<bool, Task> _ensureTemplateSwitchesAsync;
    private readonly Func<MultiVmDeploymentContext, DeploymentPreflightMode, Task<DeploymentReadinessReport>> _runReadinessAsync;
    private readonly Action<IReadOnlyList<DeployCompatibilityIssue>> _replaceCompatibilityIssues;
    private readonly Func<DeploymentReadinessReport?> _getCurrentReadinessReport;
    private readonly Action<DeploymentReadinessReport?> _setCurrentReadinessReport;
    private readonly Func<MultiVmDeploymentContext, Task<DeploymentOutcomeSummary>> _deployAllAsync;
    private readonly Action<MultiVmDeploymentContext, Action<string, string?>, Action<string, DeployStepStateUpdate>> _attachProgressCallbacks;
    private readonly Action _updateUi;

    public DeployFromTemplateWorkspaceHost(
        Func<AppSettings> deploymentSettings,
        Func<IReadOnlyList<string>> availableSwitches,
        Func<TemplateEditorDocument?> activeTemplateDocument,
        Func<IReadOnlyList<VhdxCatalogItem>> loadCatalogItems,
        Func<bool, Task> ensureTemplateSwitchesAsync,
        Func<MultiVmDeploymentContext, DeploymentPreflightMode, Task<DeploymentReadinessReport>> runReadinessAsync,
        Action<IReadOnlyList<DeployCompatibilityIssue>> replaceCompatibilityIssues,
        Func<DeploymentReadinessReport?> getCurrentReadinessReport,
        Action<DeploymentReadinessReport?> setCurrentReadinessReport,
        Func<MultiVmDeploymentContext, Task<DeploymentOutcomeSummary>> deployAllAsync,
        Action<MultiVmDeploymentContext, Action<string, string?>, Action<string, DeployStepStateUpdate>> attachProgressCallbacks,
        Action updateUi)
    {
        _deploymentSettings = deploymentSettings;
        _availableSwitches = availableSwitches;
        _activeTemplateDocument = activeTemplateDocument;
        _loadCatalogItems = loadCatalogItems;
        _ensureTemplateSwitchesAsync = ensureTemplateSwitchesAsync;
        _runReadinessAsync = runReadinessAsync;
        _replaceCompatibilityIssues = replaceCompatibilityIssues;
        _getCurrentReadinessReport = getCurrentReadinessReport;
        _setCurrentReadinessReport = setCurrentReadinessReport;
        _attachProgressCallbacks = attachProgressCallbacks;
        _deployAllAsync = deployAllAsync;
        _updateUi = updateUi;
    }

    public AppSettings DeploymentSettings => _deploymentSettings();

    public IReadOnlyList<string> AvailableSwitches => _availableSwitches();

    public TemplateEditorDocument? ActiveTemplateDocument => _activeTemplateDocument();

    public IReadOnlyList<VhdxCatalogItem> LoadCatalogItems() => _loadCatalogItems();

    public Task EnsureTemplateSwitchesAsync(bool forceRefresh) => _ensureTemplateSwitchesAsync(forceRefresh);

    public Task<DeploymentReadinessReport> RunReadinessAsync(MultiVmDeploymentContext context, DeploymentPreflightMode mode) => _runReadinessAsync(context, mode);

    public void ReplaceCompatibilityIssues(IReadOnlyList<DeployCompatibilityIssue> issues) => _replaceCompatibilityIssues(issues);

    public DeploymentReadinessReport? CurrentReadinessReport
    {
        get => _getCurrentReadinessReport();
        set => _setCurrentReadinessReport(value);
    }

    public Task<DeploymentOutcomeSummary> DeployAllAsync(MultiVmDeploymentContext context) => _deployAllAsync(context);

    public void AttachProgressCallbacks(
        MultiVmDeploymentContext context,
        Action<string, string?> onLogMessage,
        Action<string, DeployStepStateUpdate> onStepStateUpdated) => _attachProgressCallbacks(context, onLogMessage, onStepStateUpdated);

    public void UpdateUi() => _updateUi();
}

internal sealed class DeployFromTemplateWorkspaceController
{
    private readonly DeployFromTemplateWorkspaceViewModel _workspace;
    private readonly IDeployFromTemplateWorkspaceHost _host;

    public DeployFromTemplateWorkspaceController(
        DeployFromTemplateWorkspaceViewModel workspace,
        IDeployFromTemplateWorkspaceHost host)
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
            _host.UpdateUi();
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
            _host.UpdateUi();
        }
    }

    public async Task StartDeployAsync()
    {
        var activeTemplateDocument = _host.ActiveTemplateDocument;
        if (activeTemplateDocument is null)
        {
            _workspace.SetActionStatus("Select a template first.");
            _host.UpdateUi();
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
        _host.UpdateUi();

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
                    _host.UpdateUi();
                },
                (vmName, update) =>
                {
                    _workspace.ApplyProgressUpdate(vmName, update);
                    _host.UpdateUi();
                });
            _workspace.SetWorkflowState(
                isEvaluatingReadiness: false,
                isStarting: true,
                lifecycleState: "Running",
                progressPercent: 60,
                progressSummary: $"Deploying {deployContext.MultiVmContext.VmContexts.Count} VM(s)...");
            _workspace.SetActionStatus("Starting deployment...");
            _host.UpdateUi();

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
            _host.UpdateUi();
        }
    }
}

internal sealed class DeployFromTemplateWorkspaceComposition
{
    private readonly DeployFromTemplateView _view;
    private readonly DeployFromTemplateRightPanelView _rightPanelView;
    private readonly DeployFromTemplateWorkspaceViewModel _workspace = new();
    private readonly DeployFromTemplateWorkspaceController _controller;

    public DeployFromTemplateWorkspaceComposition(
        DeployFromTemplateView view,
        DeployFromTemplateRightPanelView rightPanelView,
        object? templateItemsSource,
        IDeployFromTemplateWorkspaceHost host)
    {
        _view = view;
        _rightPanelView = rightPanelView;
        _controller = new DeployFromTemplateWorkspaceController(_workspace, host);
        _view.DeployTemplateSelectorComboBoxControl.ItemsSource = templateItemsSource;
        _view.DeploySharedIssuesListViewControl.ItemsSource = _workspace.SharedIssueSummaries;
        _rightPanelView.DeployGlobalIssuesListViewControl.ItemsSource = _workspace.IssueRows;
        _rightPanelView.DeployVmResultsListViewControl.ItemsSource = _workspace.ResultRows;
        ApplyWorkspaceState();
    }

    public TemplateLibraryItem? SelectedTemplateLibraryItem => _workspace.SelectedTemplateLibraryItem;

    public TemplateEditorDocument? ActiveTemplateDocument => _workspace.ActiveTemplateDocument;

    public int IssueRowCount => _workspace.IssueRows.Count;

    public int ResultRowCount => _workspace.ResultRows.Count;

    public bool IsEvaluatingReadiness => _workspace.IsEvaluatingReadiness;

    public bool IsStarting => _workspace.IsStarting;

    public string LifecycleState => _workspace.LifecycleState;

    public int ProgressPercent => _workspace.ProgressPercent;

    public string ProgressSummary => _workspace.ProgressSummary;

    public void ApplyShellState()
    {
        ApplyWorkspaceState();
    }

    public Task EvaluateReadinessAsync(DeploymentPreflightMode mode) => _controller.EvaluateReadinessAsync(mode);

    public Task StartDeployAsync() => _controller.StartDeployAsync();

    public void SetSelectedTemplateLibraryItem(TemplateLibraryItem? selectedTemplateLibraryItem)
    {
        _workspace.SetSelectedTemplateLibraryItem(selectedTemplateLibraryItem);
        ApplyWorkspaceState();
    }

    public void ClearSelection(string actionStatusText)
    {
        _workspace.ClearSelection(actionStatusText);
        ApplyWorkspaceState();
    }

    public void SetLoadedTemplateDocument(TemplateEditorDocument document, string actionStatusText)
    {
        _workspace.SetLoadedTemplateDocument(document, actionStatusText);
        ApplyWorkspaceState();
    }

    public void SetSelectionLoadFailed(string actionStatusText)
    {
        _workspace.SetSelectionLoadFailed(actionStatusText);
        ApplyWorkspaceState();
    }

    public void SetActionStatus(string actionStatusText)
    {
        _workspace.SetActionStatus(actionStatusText);
        ApplyWorkspaceState();
    }

    public void SetReadinessSummary(string readinessSummaryText)
    {
        _workspace.SetReadinessSummary(readinessSummaryText);
        ApplyWorkspaceState();
    }

    public void SetWorkflowState(
        bool isEvaluatingReadiness,
        bool isStarting,
        string lifecycleState,
        int progressPercent,
        string progressSummary)
    {
        _workspace.SetWorkflowState(isEvaluatingReadiness, isStarting, lifecycleState, progressPercent, progressSummary);
        ApplyWorkspaceState();
    }

    public void ClearGroupedIssueState()
    {
        _workspace.ClearGroupedIssueState();
        ApplyWorkspaceState();
    }

    public void ReplaceIssueRows(IReadOnlyList<DeployIssueRow> issueRows)
    {
        _workspace.ReplaceIssueRows(issueRows);
        ApplyWorkspaceState();
    }

    public void RefreshResultRows(
        IReadOnlyList<DeployCompatibilityIssue> compatibilityIssues,
        DeploymentReadinessReport? readinessReport)
    {
        _workspace.RefreshResultRows(compatibilityIssues, readinessReport);
        ApplyWorkspaceState();
    }

    public void RefreshReviewState(bool hasBlockingFailures)
    {
        _workspace.RefreshReviewState(hasBlockingFailures);
        ApplyWorkspaceState();
    }

    public void ReconcileSelection(IReadOnlyList<TemplateLibraryItem> items)
    {
        _workspace.ReconcileSelection(items);
        ApplyWorkspaceState();
    }

    private void ApplyWorkspaceState()
    {
        if (!ReferenceEquals(_view.DeployTemplateSelectorComboBoxControl.SelectedItem, _workspace.SelectedTemplateLibraryItem))
        {
            _view.DeployTemplateSelectorComboBoxControl.SelectedItem = _workspace.SelectedTemplateLibraryItem;
        }

        _view.DeployTemplateSummaryTextBlockControl.Text = _workspace.TemplateSummaryText;
        _view.DeployTemplateRemediationTextBlockControl.Text = _workspace.TemplateRemediationText;
        _view.DeployActionStatusTextBlockControl.Text = _workspace.ActionStatusText;
        _view.DeployReadinessSummaryTextBlockControl.Text = _workspace.ReadinessSummaryText;
        _view.DeploySharedIssuesSummaryTextBlockControl.Text = _workspace.SharedIssuesSummaryText;
        _view.DeployGlobalIssuesBadgeTextBlockControl.Text = _workspace.GlobalIssuesBadgeText;
        _view.DeployOverallStateTextBlockControl.Text = _workspace.LifecycleState;
        _view.DeployProgressBarControl.Value = _workspace.ProgressPercent;
        _view.DeployProgressSummaryTextBlockControl.Text = _workspace.ProgressSummary;
    }
}
