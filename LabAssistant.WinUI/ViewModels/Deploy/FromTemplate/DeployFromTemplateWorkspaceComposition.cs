using LabAssistant.Business.Deployment;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Deployment;
using LabAssistant.WinUI.Models.Deploy;
using LabAssistant.WinUI.Views.Deploy;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Applies From Template workspace state to the long-lived view pair while leaving workflow orchestration and shared-helper coordination in the owner/controller seams.
/// </summary>
internal sealed class DeployFromTemplateWorkspaceComposition
{
    private readonly DeployFromTemplateView _view;
    private readonly DeployFromTemplateRightPanelView _rightPanelView;
    private readonly DeployFromTemplateWorkspaceViewModel _workspace;

    public DeployFromTemplateWorkspaceComposition(
        DeployFromTemplateView view,
        DeployFromTemplateRightPanelView rightPanelView,
        object? templateItemsSource,
        DeployFromTemplateWorkspaceViewModel workspace)
    {
        _view = view;
        _rightPanelView = rightPanelView;
        _workspace = workspace;
        _view.SetTemplateItemsSource(templateItemsSource);
        _view.SetTemplateSelectorDisplayMemberPath(nameof(TemplateLibraryItem.Name));
        _view.SetSharedIssueSummariesItemsSource(_workspace.SharedIssueSummaries);
        _rightPanelView.SetIssueRowsItemsSource(_workspace.IssueRows);
        _rightPanelView.SetResultRowsItemsSource(_workspace.ResultRows);
        _rightPanelView.ResetPanelState();
        SetVisibility(isActive: false);
        UpdateUi(
            isLoadingTemplates: false,
            isTemplatesLoading: false,
            compatibilityIssues: [],
            readinessReport: null);
    }

    public void SetVisibility(bool isActive)
    {
        _view.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
    }

    public void ResetPanelState()
    {
        _rightPanelView.ResetPanelState();
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

    public void UpdateUi(
        bool isLoadingTemplates,
        bool isTemplatesLoading,
        IReadOnlyList<DeployCompatibilityIssue> compatibilityIssues,
        DeploymentReadinessReport? readinessReport)
    {
        var activeTemplateDocument = _workspace.ActiveTemplateDocument;
        var hasBlockingFailures = compatibilityIssues.Any(issue => issue.IsBlocking) ||
                                  (readinessReport?.HasBlockingFailures ?? false);

        SetInteractionState(isLoadingTemplates || isTemplatesLoading, hasBlockingFailures);
        _workspace.RefreshReviewState(hasBlockingFailures);

        if (activeTemplateDocument is null)
        {
            _workspace.SetReadinessSummary("Select a template to evaluate readiness and run deploy.");
            _workspace.ClearGroupedIssueState();
            _workspace.RefreshResultRows(compatibilityIssues, readinessReport);
            UpdateIssueRows(compatibilityIssues, readinessReport);
            ApplyWorkspaceState();
            return;
        }

        var failCount = compatibilityIssues.Count(issue => issue.IsBlocking) +
                        (readinessReport?.Results.Count(result => result.Status == DeploymentReadinessStatus.Fail) ?? 0);
        var warnCount = compatibilityIssues.Count(issue => !issue.IsBlocking) +
                        (readinessReport?.Results.Count(result => result.Status == DeploymentReadinessStatus.Warn) ?? 0);
        var passCount = readinessReport?.Results.Count(result => result.Status == DeploymentReadinessStatus.Pass) ?? 0;
        var deployState = hasBlockingFailures ? "Blocked" : "Ready";
        _workspace.SetReadinessSummary(
            $"{deployState}. Pass={passCount}, Warn={warnCount}, Fail={failCount}. " +
            $"Template: {activeTemplateDocument.Template.Name} ({activeTemplateDocument.Template.VmTemplates.Count} VMs).");

        _workspace.RefreshResultRows(compatibilityIssues, readinessReport);
        UpdateIssueRows(compatibilityIssues, readinessReport);
        ApplyWorkspaceState();
    }

    private void SetInteractionState(bool isLoadingTemplates, bool hasBlockingFailures)
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

    private void UpdateIssueRows(
        IReadOnlyList<DeployCompatibilityIssue> compatibilityIssues,
        DeploymentReadinessReport? readinessReport)
    {
        var issueRows = new List<DeployIssueRow>();

        foreach (var issue in compatibilityIssues)
        {
            var scope = string.IsNullOrWhiteSpace(issue.VmName) ? "Global" : issue.VmName;
            issueRows.Add(new DeployIssueRow(
                Scope: scope,
                Severity: issue.IsBlocking ? "Block" : "Warn",
                Message: $"{issue.Message} {issue.Guidance}".Trim()));
        }

        if (readinessReport is not null)
        {
            foreach (var result in readinessReport.Results.Where(result => result.Status is DeploymentReadinessStatus.Fail or DeploymentReadinessStatus.Warn))
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
