using LabAssistant.Business.Templates;
using LabAssistant.WinUI.Views.Templates;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.ViewModels.Templates;

internal sealed class TemplatesEditorWorkspaceComposition
{
    private readonly TemplatesEditorView _view;
    private readonly TemplatesEditorWorkspaceViewModel _workspace = new();

    public TemplatesEditorWorkspaceComposition(TemplatesEditorView view)
    {
        _view = view;
        _view.DocumentHeaderChanged += TemplatesEditorView_DocumentHeaderChanged;
        ApplyViewState();
        ApplyActionState(isLoading: false, hasSelectedTemplateVmEntry: false);
    }

    public bool HasActiveDocument => _workspace.HasActiveDocument;

    public void ApplyShellState(bool isEditorActive)
    {
        _view.Visibility = isEditorActive ? Visibility.Visible : Visibility.Collapsed;
    }

    public void SetDocument(TemplateEditorDocument? document)
    {
        if (document is null)
        {
            _workspace.ClearDocument();
        }
        else
        {
            _workspace.SetDocument(document);
        }

        ApplyViewState();
    }

    public void SetStatus(string statusText)
    {
        _workspace.SetStatusText(statusText);
        ApplyViewState();
    }

    public void SetVmCount(int vmCount)
    {
        _workspace.SetVmCount(vmCount);
        ApplyViewState();
    }

    public TemplatesEditorDocumentHeaderInteractionState CaptureDocumentHeaderState()
    {
        return new TemplatesEditorDocumentHeaderInteractionState(
            _workspace.TemplateName,
            _workspace.TemplateDescription);
    }

    public void ApplyActionState(bool isLoading, bool hasSelectedTemplateVmEntry)
    {
        _view.UpdateActionState(new TemplatesEditorActionState(
            CanSave: _workspace.HasActiveDocument && !isLoading,
            CanSaveAs: _workspace.HasActiveDocument && !isLoading,
            CanValidate: _workspace.HasActiveDocument && !isLoading,
            CanBackToLibrary: !isLoading,
            CanAddTemplateVm: _workspace.HasActiveDocument && !isLoading,
            CanRemoveTemplateVm: hasSelectedTemplateVmEntry && !isLoading,
            CanAddTemplateVmSwitchRow: hasSelectedTemplateVmEntry && !isLoading,
            CanSelectTemplateVmVhdx: hasSelectedTemplateVmEntry && !isLoading,
            CanApplyTemplateVmChanges: hasSelectedTemplateVmEntry && !isLoading));
    }

    private void TemplatesEditorView_DocumentHeaderChanged(object? sender, EventArgs e)
    {
        var interactionState = _view.CaptureDocumentHeaderInteractionState();
        _workspace.SetDocumentHeaderDraft(interactionState.TemplateName, interactionState.TemplateDescription);
    }

    private void ApplyViewState()
    {
        _view.UpdateDocumentHeaderState(new TemplatesEditorDocumentHeaderViewState(
            TemplateEditorContextText: _workspace.TemplateEditorContextText,
            TemplateIdText: _workspace.TemplateIdText,
            TemplateFilePathText: _workspace.TemplateFilePathText,
            TemplateVmCountText: _workspace.TemplateVmCountText,
            TemplateName: _workspace.TemplateName,
            TemplateDescription: _workspace.TemplateDescription,
            StatusText: _workspace.StatusText,
            IsStatusVisible: _workspace.HasStatusText));
    }
}
