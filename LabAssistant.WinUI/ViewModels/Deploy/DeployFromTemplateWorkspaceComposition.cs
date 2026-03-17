using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Models.Deploy;
using LabAssistant.WinUI.Views.Deploy;

namespace LabAssistant.WinUI.ViewModels.Deploy;

internal sealed class DeployFromTemplateWorkspaceComposition
{
    private readonly DeployFromTemplateView _view;
    private readonly DeployFromTemplateRightPanelView _rightPanelView;
    private readonly DeployFromTemplateWorkspaceViewModel _workspace = new();

    public DeployFromTemplateWorkspaceComposition(
        DeployFromTemplateView view,
        DeployFromTemplateRightPanelView rightPanelView,
        object? templateItemsSource)
    {
        _view = view;
        _rightPanelView = rightPanelView;
        _view.DeployTemplateSelectorComboBoxControl.ItemsSource = templateItemsSource;
        _view.DeploySharedIssuesListViewControl.ItemsSource = _workspace.SharedIssueSummaries;
        _rightPanelView.DeployGlobalIssuesListViewControl.ItemsSource = _workspace.IssueRows;
        ApplyWorkspaceState();
    }

    public TemplateLibraryItem? SelectedTemplateLibraryItem => _workspace.SelectedTemplateLibraryItem;

    public TemplateEditorDocument? ActiveTemplateDocument => _workspace.ActiveTemplateDocument;

    public int IssueRowCount => _workspace.IssueRows.Count;

    public void ApplyShellState()
    {
        ApplyWorkspaceState();
    }

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
    }
}
