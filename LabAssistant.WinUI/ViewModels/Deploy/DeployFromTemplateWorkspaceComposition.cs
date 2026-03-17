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
        _view.DeployTemplateSelectorComboBoxControl.ItemsSource = templateItemsSource;
        _view.DeployTemplateSelectorComboBoxControl.DisplayMemberPath = nameof(TemplateLibraryItem.Name);
        _view.DeploySharedIssuesListViewControl.ItemsSource = _workspace.SharedIssueSummaries;
        _rightPanelView.DeployGlobalIssuesListViewControl.ItemsSource = _workspace.IssueRows;
        _rightPanelView.DeployVmResultsListViewControl.ItemsSource = _workspace.ResultRows;
        _rightPanelView.DeployGlobalIssuesExpanderControl.IsExpanded = false;
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

    public void ApplyShellState(bool isFromTemplateActive)
    {
        _view.Visibility = isFromTemplateActive ? Visibility.Visible : Visibility.Collapsed;
        if (isFromTemplateActive)
        {
            ApplyWorkspaceState();
        }
    }

    public void ResetPanelState()
    {
        _rightPanelView.DeployGlobalIssuesExpanderControl.IsExpanded = false;
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

    AppSettings IDeployFromTemplateWorkspaceControllerHost.DeploymentSettings => _host.DeploymentSettings;

    IReadOnlyList<string> IDeployFromTemplateWorkspaceControllerHost.AvailableSwitches => _host.AvailableSwitches;

    TemplateEditorDocument? IDeployFromTemplateWorkspaceControllerHost.ActiveTemplateDocument => _host.ActiveTemplateDocument;

    IReadOnlyList<VhdxCatalogItem> IDeployFromTemplateWorkspaceControllerHost.LoadCatalogItems() => _host.LoadCatalogItems();

    Task IDeployFromTemplateWorkspaceControllerHost.EnsureTemplateSwitchesAsync(bool forceRefresh) => _host.EnsureTemplateSwitchesAsync(forceRefresh);

    Task<DeploymentReadinessReport> IDeployFromTemplateWorkspaceControllerHost.RunReadinessAsync(MultiVmDeploymentContext context, DeploymentPreflightMode mode) => _host.RunReadinessAsync(context, mode);

    void IDeployFromTemplateWorkspaceControllerHost.ReplaceCompatibilityIssues(IReadOnlyList<DeployCompatibilityIssue> issues) => _host.ReplaceCompatibilityIssues(issues);

    DeploymentReadinessReport? IDeployFromTemplateWorkspaceControllerHost.CurrentReadinessReport
    {
        get => _host.CurrentReadinessReport;
        set => _host.CurrentReadinessReport = value;
    }

    Task<DeploymentOutcomeSummary> IDeployFromTemplateWorkspaceControllerHost.DeployAllAsync(MultiVmDeploymentContext context) => _host.DeployAllAsync(context);

    void IDeployFromTemplateWorkspaceControllerHost.AttachProgressCallbacks(
        MultiVmDeploymentContext context,
        Action<string, string?> onLogMessage,
        Action<string, DeployStepStateUpdate> onStepStateUpdated) => _host.AttachProgressCallbacks(context, onLogMessage, onStepStateUpdated);

    void IDeployFromTemplateWorkspaceControllerHost.ApplyWorkspaceState() => ApplyWorkspaceState();

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
