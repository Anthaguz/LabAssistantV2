using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;
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
        _view.SelectedVmChanged += TemplatesEditorView_SelectedVmChanged;
        _view.SetVmEntriesSource(_workspace.VmEntries);
        ApplyViewState();
        ApplyActionState(isLoading: false);
    }

    public bool HasActiveDocument => _workspace.HasActiveDocument;

    public IReadOnlyList<VmTemplate> VmEntries => _workspace.VmEntries;

    public VmTemplate? SelectedVmEntry => _workspace.SelectedVmEntry;

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

    public void ReplaceVmEntries(IReadOnlyList<VmTemplate> vmEntries)
    {
        _workspace.ReplaceVmEntries(vmEntries);
        ApplyVmListState();
    }

    public void AddVmEntry(VmTemplate vmEntry)
    {
        _workspace.AddVmEntry(vmEntry);
        ApplyVmListState();
    }

    public VmTemplate? RemoveSelectedVmEntry()
    {
        var removedEntry = _workspace.RemoveSelectedVmEntry();
        ApplyVmListState();
        return removedEntry;
    }

    public void RefreshVmEntries()
    {
        _view.RefreshVmEntries();
        ApplyVmListState();
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

    public void ApplyActionState(bool isLoading)
    {
        _view.UpdateActionState(new TemplatesEditorActionState(
            CanSave: _workspace.HasActiveDocument && !isLoading,
            CanSaveAs: _workspace.HasActiveDocument && !isLoading,
            CanValidate: _workspace.HasActiveDocument && !isLoading,
            CanBackToLibrary: !isLoading,
            CanAddTemplateVm: _workspace.HasActiveDocument && !isLoading,
            CanRemoveTemplateVm: _workspace.SelectedVmEntry is not null && !isLoading,
            CanAddTemplateVmSwitchRow: _workspace.SelectedVmEntry is not null && !isLoading,
            CanSelectTemplateVmVhdx: _workspace.SelectedVmEntry is not null && !isLoading,
            CanApplyTemplateVmChanges: _workspace.SelectedVmEntry is not null && !isLoading));
    }

    private void TemplatesEditorView_DocumentHeaderChanged(object? sender, EventArgs e)
    {
        var interactionState = _view.CaptureDocumentHeaderInteractionState();
        _workspace.SetDocumentHeaderDraft(interactionState.TemplateName, interactionState.TemplateDescription);
    }

    private void TemplatesEditorView_SelectedVmChanged(object? sender, EventArgs e)
    {
        var interactionState = _view.CaptureVmListInteractionState();
        _workspace.SetSelectedVmEntry(interactionState.SelectedVmEntry);
        ApplyVmListState();
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

    private void ApplyVmListState()
    {
        _view.UpdateVmSelection(_workspace.SelectedVmEntry);
    }
}
