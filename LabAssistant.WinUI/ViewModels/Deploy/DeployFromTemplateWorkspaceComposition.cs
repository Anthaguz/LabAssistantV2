using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Views.Deploy;

namespace LabAssistant.WinUI.ViewModels.Deploy;

internal sealed class DeployFromTemplateWorkspaceComposition
{
    private readonly DeployFromTemplateView _view;
    private readonly DeployFromTemplateWorkspaceViewModel _workspace = new();

    public DeployFromTemplateWorkspaceComposition(
        DeployFromTemplateView view,
        object? templateItemsSource)
    {
        _view = view;
        _view.DeployTemplateSelectorComboBoxControl.ItemsSource = templateItemsSource;
        ApplyWorkspaceState();
    }

    public TemplateLibraryItem? SelectedTemplateLibraryItem => _workspace.SelectedTemplateLibraryItem;

    public TemplateEditorDocument? ActiveTemplateDocument => _workspace.ActiveTemplateDocument;

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
    }
}
