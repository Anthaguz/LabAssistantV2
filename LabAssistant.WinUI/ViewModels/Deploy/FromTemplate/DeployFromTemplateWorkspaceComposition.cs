using LabAssistant.Business.Deployment;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Models.Deploy;
using LabAssistant.WinUI.Views.Deploy;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.ViewModels.Deploy;

internal sealed class DeployFromTemplateWorkspaceComposition : IDeployFromTemplateWorkspaceControllerHost
{
    private readonly DeployFromTemplateView _view;
    private readonly DeployFromTemplateRightPanelView _rightPanelView;
    private readonly IDeployFromTemplateCompositionHost _host;
    private readonly DeployFromTemplateWorkspaceViewModel _workspace = new();
    private readonly DeployFromTemplateWorkspaceController _controller;
    private readonly List<DeployCompatibilityIssue> _compatibilityIssues = [];
    private DeploymentReadinessReport? _readinessReport;
    private bool _isLoadingTemplates;

    public DeployFromTemplateWorkspaceComposition(
        DeployFromTemplateView view,
        DeployFromTemplateRightPanelView rightPanelView,
        object? templateItemsSource,
        IDeployFromTemplateCompositionHost host)
    {
        _view = view;
        _rightPanelView = rightPanelView;
        _host = host;
        _controller = new DeployFromTemplateWorkspaceController(_workspace, this);
        _view.SetTemplateItemsSource(templateItemsSource);
        _view.SetTemplateSelectorDisplayMemberPath(nameof(TemplateLibraryItem.Name));
        _view.SetSharedIssueSummariesItemsSource(_workspace.SharedIssueSummaries);
        _rightPanelView.SetIssueRowsItemsSource(_workspace.IssueRows);
        _rightPanelView.SetResultRowsItemsSource(_workspace.ResultRows);
        _rightPanelView.ResetPanelState();
        WireHandlers();
        UpdateUi();
    }

    public TemplateLibraryItem? SelectedTemplateLibraryItem => _workspace.SelectedTemplateLibraryItem;

    public TemplateEditorDocument? ActiveTemplateDocument => _workspace.ActiveTemplateDocument;

    public int IssueRowCount => _workspace.IssueRows.Count;

    public int ResultRowCount => _workspace.ResultRows.Count;

    public bool IsLoadingTemplates => _isLoadingTemplates;

    public bool IsEvaluatingReadiness => _workspace.IsEvaluatingReadiness;

    public bool IsStarting => _workspace.IsStarting;

    public string LifecycleState => _workspace.LifecycleState;

    public int ProgressPercent => _workspace.ProgressPercent;

    public string ProgressSummary => _workspace.ProgressSummary;

    public void ApplyShellState(bool isFromTemplateActive)
    {
        _view.Visibility = isFromTemplateActive ? Visibility.Visible : Visibility.Collapsed;
        if (isFromTemplateActive)
        {
            UpdateUi();
        }
    }

    public void ResetPanelState()
    {
        _rightPanelView.ResetPanelState();
    }

    public Task EvaluateReadinessAsync(DeploymentPreflightMode mode) => _controller.EvaluateReadinessAsync(mode);

    public Task EnsureTemplatesLoadedAsync(bool forceRefresh) => LoadTemplatesAsync(forceRefresh);

    public void RefreshUi() => UpdateUi();

    public Task StartDeployAsync() => _controller.StartDeployAsync();

    public void SetSelectedTemplateLibraryItem(TemplateLibraryItem? selectedTemplateLibraryItem)
    {
        _workspace.SetSelectedTemplateLibraryItem(selectedTemplateLibraryItem);
        UpdateUi();
    }

    public void ClearSelection(string actionStatusText)
    {
        _workspace.ClearSelection(actionStatusText);
        UpdateUi();
    }

    public void SetLoadedTemplateDocument(TemplateEditorDocument document, string actionStatusText)
    {
        _workspace.SetLoadedTemplateDocument(document, actionStatusText);
        UpdateUi();
    }

    public void SetSelectionLoadFailed(string actionStatusText)
    {
        _workspace.SetSelectionLoadFailed(actionStatusText);
        UpdateUi();
    }

    public void SetActionStatus(string actionStatusText)
    {
        _workspace.SetActionStatus(actionStatusText);
        UpdateUi();
    }

    public void SetReadinessSummary(string readinessSummaryText)
    {
        _workspace.SetReadinessSummary(readinessSummaryText);
        UpdateUi();
    }

    public void SetWorkflowState(
        bool isEvaluatingReadiness,
        bool isStarting,
        string lifecycleState,
        int progressPercent,
        string progressSummary)
    {
        _workspace.SetWorkflowState(isEvaluatingReadiness, isStarting, lifecycleState, progressPercent, progressSummary);
        UpdateUi();
    }

    public void ClearGroupedIssueState()
    {
        _workspace.ClearGroupedIssueState();
        UpdateUi();
    }

    public void ReplaceIssueRows(IReadOnlyList<DeployIssueRow> issueRows)
    {
        _workspace.ReplaceIssueRows(issueRows);
        UpdateUi();
    }

    public void RefreshResultRows(
        IReadOnlyList<DeployCompatibilityIssue> compatibilityIssues,
        DeploymentReadinessReport? readinessReport)
    {
        _workspace.RefreshResultRows(compatibilityIssues, readinessReport);
        UpdateUi();
    }

    public void RefreshReviewState(bool hasBlockingFailures)
    {
        _workspace.RefreshReviewState(hasBlockingFailures);
        UpdateUi();
    }

    public void ReconcileSelection(IReadOnlyList<TemplateLibraryItem> items)
    {
        _workspace.ReconcileSelection(items);
        UpdateUi();
    }

    public void SetInteractionState(bool isLoadingTemplates, bool hasBlockingFailures)
    {
        var hasTemplate = _workspace.ActiveTemplateDocument is not null;
        _view.SetInteractionState(
            isTemplateSelectorEnabled: !isLoadingTemplates && !_workspace.IsStarting,
            isReloadEnabled: !isLoadingTemplates && !_workspace.IsStarting,
            isEvaluateReadinessEnabled: hasTemplate && !_workspace.IsEvaluatingReadiness && !_workspace.IsStarting,
            isResolveSuggestionsEnabled: hasTemplate && !_workspace.IsEvaluatingReadiness && !_workspace.IsStarting,
            isOpenTemplateEditorEnabled: _workspace.SelectedTemplateLibraryItem is not null && !_workspace.IsStarting,
            isStartDeployEnabled: hasTemplate && !hasBlockingFailures && !_workspace.IsEvaluatingReadiness && !_workspace.IsStarting);
    }

    /// <summary>
    /// Applies the From Template lane-specific right-panel state while the shell retains the shared panel container and sizing mechanics.
    /// </summary>
    public void ApplyResultsPanelState(bool isActive, bool showPanel, bool panelUnavailable)
    {
        _rightPanelView.Visibility = isActive && showPanel ? Visibility.Visible : Visibility.Collapsed;
        var isRunning = _workspace.IsStarting || string.Equals(_workspace.LifecycleState, "Running", StringComparison.OrdinalIgnoreCase);
        _view.SetResultsPanelLauncherState(
            showPanel && isActive ? "Hide Progress / Results" : "Open Progress / Results",
            isActive && !panelUnavailable,
            panelUnavailable
                ? "Expand the window to review the progress and results panel."
                : isRunning
                    ? "The panel auto-opens while deployment runs and stays available for result review."
                    : _workspace.ResultRows.Count > 0
                        ? $"{_workspace.ResultRows.Count} VM result row(s) are available for review."
                        : "Use the side panel during or after deploy for progress, timeline, and results.");
    }

    AppSettings IDeployFromTemplateWorkspaceControllerHost.DeploymentSettings => _host.DeploymentSettings;

    IReadOnlyList<string> IDeployFromTemplateWorkspaceControllerHost.AvailableSwitches => _host.AvailableSwitches;

    TemplateEditorDocument? IDeployFromTemplateWorkspaceControllerHost.ActiveTemplateDocument => _workspace.ActiveTemplateDocument;

    IReadOnlyList<VhdxCatalogItem> IDeployFromTemplateWorkspaceControllerHost.LoadCatalogItems() => _host.LoadCatalogItems();

    Task IDeployFromTemplateWorkspaceControllerHost.EnsureTemplateSwitchesAsync(bool forceRefresh) => _host.EnsureTemplateSwitchesAsync(forceRefresh);

    Task<DeploymentReadinessReport> IDeployFromTemplateWorkspaceControllerHost.RunReadinessAsync(
        MultiVmDeploymentContext context,
        DeploymentPreflightMode mode) => _host.RunReadinessAsync(context, mode);

    void IDeployFromTemplateWorkspaceControllerHost.ReplaceCompatibilityIssues(IReadOnlyList<DeployCompatibilityIssue> issues)
    {
        _compatibilityIssues.Clear();
        _compatibilityIssues.AddRange(issues);
        UpdateUi();
    }

    DeploymentReadinessReport? IDeployFromTemplateWorkspaceControllerHost.CurrentReadinessReport
    {
        get => _readinessReport;
        set
        {
            _readinessReport = value;
            UpdateUi();
        }
    }

    Task<DeploymentOutcomeSummary> IDeployFromTemplateWorkspaceControllerHost.DeployAllAsync(MultiVmDeploymentContext context) => _host.DeployAllAsync(context);

    void IDeployFromTemplateWorkspaceControllerHost.AttachProgressCallbacks(
        MultiVmDeploymentContext context,
        Action<string, string?> onLogMessage,
        Action<string, DeployStepStateUpdate> onStepStateUpdated) => _host.AttachProgressCallbacks(context, onLogMessage, onStepStateUpdated);

    void IDeployFromTemplateWorkspaceControllerHost.ApplyWorkspaceState() => UpdateUi();

    private void WireHandlers()
    {
        _view.ReloadTemplatesRequested += async (_, _) => await LoadTemplatesAsync(forceRefresh: true);
        _view.TemplateSelectionChanged += async (_, _) => await HandleTemplateSelectionChangedAsync();
        _view.OpenResultsPanelRequested += (_, _) => _host.OnOpenResultsPanelRequested();
    }

    private async Task HandleTemplateSelectionChangedAsync()
    {
        if (_isLoadingTemplates || _host.IsTemplatesLoading)
        {
            return;
        }

        _workspace.SetSelectedTemplateLibraryItem(_view.SelectedTemplateLibraryItem);
        if (_workspace.SelectedTemplateLibraryItem is null)
        {
            _workspace.ClearSelection("No template selected.");
            _readinessReport = null;
            _compatibilityIssues.Clear();
            _workspace.SetWorkflowState(
                isEvaluatingReadiness: false,
                isStarting: false,
                lifecycleState: "Idle",
                progressPercent: 0,
                progressSummary: "No template selected.");
            UpdateUi();
            return;
        }

        try
        {
            var selectedTemplate = _workspace.SelectedTemplateLibraryItem;
            var document = await _host.LoadTemplateForEditorAsync(selectedTemplate.FilePath);
            _workspace.SetLoadedTemplateDocument(
                document,
                $"Loaded '{selectedTemplate.Name}' for deploy readiness.");
            _workspace.SetWorkflowState(
                isEvaluatingReadiness: false,
                isStarting: false,
                lifecycleState: "Ready",
                progressPercent: 0,
                progressSummary: $"Template '{selectedTemplate.Name}' loaded.");
            UpdateUi();
            await _controller.EvaluateReadinessAsync(DeploymentPreflightMode.Quick);
        }
        catch (Exception ex)
        {
            _workspace.SetSelectionLoadFailed($"Failed to load selected template. {ex.Message}");
            _readinessReport = null;
            _compatibilityIssues.Clear();
            _workspace.SetWorkflowState(
                isEvaluatingReadiness: false,
                isStarting: false,
                lifecycleState: "Error",
                progressPercent: 0,
                progressSummary: "Template load failed.");
            UpdateUi();
        }
    }

    private async Task LoadTemplatesAsync(bool forceRefresh)
    {
        if (!forceRefresh && _host.TemplateLibraryItems.Count > 0)
        {
            _host.RefreshSharedUiState();
            UpdateUi();
            return;
        }

        _isLoadingTemplates = true;
        _workspace.SetWorkflowState(
            isEvaluatingReadiness: false,
            isStarting: false,
            lifecycleState: "Loading",
            progressPercent: 0,
            progressSummary: "Loading templates...");
        _host.RefreshSharedUiState();
        UpdateUi();
        _workspace.SetActionStatus("Loading templates for deploy...");

        try
        {
            await _host.EnsureTemplatesLibraryAsync(forceRefresh);

            if (_host.TemplateLibraryItems.Count == 0)
            {
                _workspace.ClearSelection("No templates available for deploy.");
                _readinessReport = null;
                _compatibilityIssues.Clear();
                _workspace.SetWorkflowState(
                    isEvaluatingReadiness: false,
                    isStarting: false,
                    lifecycleState: "Idle",
                    progressPercent: 0,
                    progressSummary: "No templates available.");
            }
            else
            {
                if (_workspace.SelectedTemplateLibraryItem is null)
                {
                    _workspace.SetSelectedTemplateLibraryItem(_host.TemplateLibraryItems[0]);
                }

                _workspace.SetWorkflowState(
                    isEvaluatingReadiness: false,
                    isStarting: false,
                    lifecycleState: "Idle",
                    progressPercent: 0,
                    progressSummary: "Template list loaded.");
                _workspace.SetActionStatus($"Loaded {_host.TemplateLibraryItems.Count} template(s) for deploy.");
            }
        }
        catch (Exception ex)
        {
            _workspace.SetWorkflowState(
                isEvaluatingReadiness: false,
                isStarting: false,
                lifecycleState: "Error",
                progressPercent: 0,
                progressSummary: "Template load failed.");
            _workspace.SetActionStatus($"Failed to load deploy templates. {ex.Message}");
        }
        finally
        {
            _isLoadingTemplates = false;
            _host.RefreshSharedUiState();
            UpdateUi();
        }
    }

    private void UpdateUi()
    {
        var activeTemplateDocument = _workspace.ActiveTemplateDocument;
        var hasBlockingFailures = _compatibilityIssues.Any(issue => issue.IsBlocking) ||
                                  (_readinessReport?.HasBlockingFailures ?? false);

        SetInteractionState(_isLoadingTemplates || _host.IsTemplatesLoading, hasBlockingFailures);
        _workspace.RefreshReviewState(hasBlockingFailures);

        if (activeTemplateDocument is null)
        {
            _workspace.SetReadinessSummary("Select a template to evaluate readiness and run deploy.");
            _workspace.ClearGroupedIssueState();
            _workspace.RefreshResultRows(_compatibilityIssues, _readinessReport);
            UpdateIssueRows();
            ApplyWorkspaceState();
            _host.ApplyRightPanelState();
            return;
        }

        var failCount = _compatibilityIssues.Count(issue => issue.IsBlocking) +
                        (_readinessReport?.Results.Count(result => result.Status == DeploymentReadinessStatus.Fail) ?? 0);
        var warnCount = _compatibilityIssues.Count(issue => !issue.IsBlocking) +
                        (_readinessReport?.Results.Count(result => result.Status == DeploymentReadinessStatus.Warn) ?? 0);
        var passCount = _readinessReport?.Results.Count(result => result.Status == DeploymentReadinessStatus.Pass) ?? 0;
        var deployState = hasBlockingFailures ? "Blocked" : "Ready";
        _workspace.SetReadinessSummary(
            $"{deployState}. Pass={passCount}, Warn={warnCount}, Fail={failCount}. " +
            $"Template: {activeTemplateDocument.Template.Name} ({activeTemplateDocument.Template.VmTemplates.Count} VMs).");

        _workspace.RefreshResultRows(_compatibilityIssues, _readinessReport);
        UpdateIssueRows();
        ApplyWorkspaceState();
        _host.ApplyRightPanelState();
    }

    private void UpdateIssueRows()
    {
        var issueRows = new List<DeployIssueRow>();

        foreach (var issue in _compatibilityIssues)
        {
            var scope = string.IsNullOrWhiteSpace(issue.VmName) ? "Global" : issue.VmName;
            issueRows.Add(new DeployIssueRow(
                Scope: scope,
                Severity: issue.IsBlocking ? "Block" : "Warn",
                Message: $"{issue.Message} {issue.Guidance}".Trim()));
        }

        if (_readinessReport is not null)
        {
            foreach (var result in _readinessReport.Results.Where(result => result.Status is DeploymentReadinessStatus.Fail or DeploymentReadinessStatus.Warn))
            {
                var scope = result.AffectedVmNames.Count == 0 ? "Global" : string.Join(", ", result.AffectedVmNames);
                issueRows.Add(new DeployIssueRow(
                    Scope: scope,
                    Severity: result.Status == DeploymentReadinessStatus.Fail ? "Block" : "Warn",
                    Message: $"{result.Message} {result.ActionableGuidance}".Trim()));
            }
        }

        _workspace.ReplaceIssueRows(issueRows);
    }

    private void ApplyWorkspaceState()
    {
        if (!ReferenceEquals(_view.SelectedTemplateLibraryItem, _workspace.SelectedTemplateLibraryItem))
        {
            _view.SelectedTemplateLibraryItem = _workspace.SelectedTemplateLibraryItem;
        }

        _view.ApplyWorkspaceState(new DeployFromTemplateViewState(
            _workspace.TemplateSummaryText,
            _workspace.TemplateRemediationText,
            _workspace.ActionStatusText,
            _workspace.ReadinessSummaryText,
            _workspace.SharedIssuesSummaryText,
            _workspace.GlobalIssuesBadgeText,
            _workspace.LifecycleState,
            _workspace.ProgressPercent,
            _workspace.ProgressSummary));
    }
}
