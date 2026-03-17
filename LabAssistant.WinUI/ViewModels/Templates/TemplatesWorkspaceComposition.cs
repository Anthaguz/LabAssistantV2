using LabAssistant.Business.Templates;
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

    public Task EnsureLibraryAsync(bool forceRefresh) => _libraryComposition.EnsureLibraryAsync(forceRefresh);

    public void SetEditorDocument(TemplateEditorDocument? document) => _editorComposition.SetDocument(document);

    public void SetEditorStatus(string statusText) => _editorComposition.SetStatus(statusText);

    public void SetEditorVmCount(int vmCount) => _editorComposition.SetVmCount(vmCount);

    public TemplatesEditorDocumentHeaderInteractionState CaptureEditorDocumentHeaderState() => _editorComposition.CaptureDocumentHeaderState();

    public void ApplyShellState()
    {
        _workspaceHost.Visibility = _shellBridge.IsTemplatesCapabilityActive ? Visibility.Visible : Visibility.Collapsed;
        _editorComposition.ApplyShellState(_shellBridge.IsTemplatesEditorActive);
        _libraryComposition.ApplyShellState(_shellBridge.IsTemplatesLibraryActive);
    }

    public void ApplyUiState(TemplatesWorkspaceUiState state)
    {
        _libraryComposition.ApplyUiState(state.IsLoading, state.HasSelectedLibraryItem);
        _editorComposition.ApplyActionState(
            isLoading: state.IsLoading,
            hasSelectedTemplateVmEntry: state.HasSelectedTemplateVmEntry);
    }
}

internal readonly record struct TemplatesWorkspaceUiState(
    bool IsLoading,
    bool HasSelectedLibraryItem,
    bool HasSelectedTemplateVmEntry);
