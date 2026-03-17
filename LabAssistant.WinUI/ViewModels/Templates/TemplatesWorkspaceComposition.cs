using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Views.Templates;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.ViewModels.Templates;

internal interface ITemplatesWorkspaceShellBridge
{
    bool IsTemplatesCapabilityActive { get; }

    bool IsTemplatesLibraryActive { get; }

    bool IsTemplatesEditorActive { get; }
}

internal sealed class TemplatesWorkspaceShellBridge : ITemplatesWorkspaceShellBridge
{
    private readonly Func<bool> _isTemplatesCapabilityActive;
    private readonly Func<bool> _isTemplatesLibraryActive;
    private readonly Func<bool> _isTemplatesEditorActive;

    public TemplatesWorkspaceShellBridge(
        Func<bool> isTemplatesCapabilityActive,
        Func<bool> isTemplatesLibraryActive,
        Func<bool> isTemplatesEditorActive)
    {
        _isTemplatesCapabilityActive = isTemplatesCapabilityActive;
        _isTemplatesLibraryActive = isTemplatesLibraryActive;
        _isTemplatesEditorActive = isTemplatesEditorActive;
    }

    public bool IsTemplatesCapabilityActive => _isTemplatesCapabilityActive();

    public bool IsTemplatesLibraryActive => _isTemplatesLibraryActive();

    public bool IsTemplatesEditorActive => _isTemplatesEditorActive();
}

internal sealed class TemplatesWorkspaceComposition
{
    private readonly FrameworkElement _workspaceHost;
    private readonly TemplatesLibraryWorkspaceComposition _libraryComposition;
    private readonly TemplatesEditorWorkspaceComposition _editorComposition;
    private readonly ITemplatesWorkspaceShellBridge _shellBridge;

    public TemplatesWorkspaceComposition(
        FrameworkElement workspaceHost,
        TemplatesLibraryWorkspaceComposition libraryComposition,
        TemplatesEditorWorkspaceComposition editorComposition,
        ITemplatesWorkspaceShellBridge shellBridge)
    {
        _workspaceHost = workspaceHost;
        _libraryComposition = libraryComposition;
        _editorComposition = editorComposition;
        _shellBridge = shellBridge;
    }

    public IList<TemplateLibraryItem> LibraryItems => _libraryComposition.LibraryItems;

    public string LibrarySearchQuery => _libraryComposition.SearchQuery;

    public TemplateLibraryItem? SelectedLibraryItem => _libraryComposition.SelectedItem;

    public bool HasActiveEditorDocument => _editorComposition.HasActiveDocument;

    public TemplateEditorDocument? ActiveEditorDocument => _editorComposition.ActiveDocument;

    public IReadOnlyList<VmTemplate> EditorVmEntries => _editorComposition.VmEntries;

    public VmTemplate? SelectedEditorVmEntry => _editorComposition.SelectedVmEntry;

    public TemplatesEditorVmDraftSnapshot CaptureEditorVmDraftState() => _editorComposition.CaptureVmDraftState();

    public Task EnsureLibraryAsync(bool forceRefresh) => _libraryComposition.EnsureLibraryAsync(forceRefresh);

    public Task ShowEditorDocumentAsync(TemplateEditorDocument document, string statusText) => _editorComposition.ShowDocumentAsync(document, statusText);

    public void SetEditorStatus(string statusText) => _editorComposition.SetStatus(statusText);

    public TemplatesEditorDocumentHeaderInteractionState CaptureEditorDocumentHeaderState() => _editorComposition.CaptureDocumentHeaderState();

    public void ReplaceEditorVmEntries(IReadOnlyList<VmTemplate> vmEntries) => _editorComposition.ReplaceVmEntries(vmEntries);

    public bool AddEditorVmEntry() => _editorComposition.AddVmEntry();

    public Task RemoveSelectedEditorVmEntryAsync() => _editorComposition.RemoveSelectedVmEntryAsync();

    public void RefreshEditorVmEntries() => _editorComposition.RefreshVmEntries();

    public void SetEditorVmReferenceData(
        IReadOnlyList<string> availableVmSwitches,
        IReadOnlyList<TemplateVhdxCatalogOption> vmVhdxCatalogOptions) =>
        _editorComposition.SetVmReferenceData(availableVmSwitches, vmVhdxCatalogOptions);

    public void SyncEditorVmEntriesToDocument() => _editorComposition.SyncVmEntriesToDocument();

    public bool ApplySelectedEditorVmDraft(bool showSuccessStatus) => _editorComposition.ApplySelectedVmDraft(showSuccessStatus);

    public Task SaveEditorAsync() => _editorComposition.SaveAsync();

    public Task SaveEditorAsAsync() => _editorComposition.SaveAsAsync();

    public Task ValidateEditorAsync() => _editorComposition.ValidateAsync();

    public void ApplyShellState()
    {
        _workspaceHost.Visibility = _shellBridge.IsTemplatesCapabilityActive ? Visibility.Visible : Visibility.Collapsed;
        _editorComposition.ApplyShellState(_shellBridge.IsTemplatesEditorActive);
        _libraryComposition.ApplyShellState(_shellBridge.IsTemplatesLibraryActive);
    }

    public void ApplyUiState(TemplatesWorkspaceUiState state)
    {
        _libraryComposition.ApplyUiState(state.IsLoading, state.HasSelectedLibraryItem);
        _editorComposition.RefreshUiState();
    }
}

internal readonly record struct TemplatesWorkspaceUiState(
    bool IsLoading,
    bool HasSelectedLibraryItem);
