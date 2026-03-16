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
    private readonly TemplatesEditorView _editorView;
    private readonly ITemplatesWorkspaceShellBridge _shellBridge;

    public TemplatesWorkspaceComposition(
        FrameworkElement workspaceHost,
        TemplatesLibraryWorkspaceComposition libraryComposition,
        TemplatesEditorView editorView,
        ITemplatesWorkspaceShellBridge shellBridge)
    {
        _workspaceHost = workspaceHost;
        _libraryComposition = libraryComposition;
        _editorView = editorView;
        _shellBridge = shellBridge;
    }

    public IList<TemplateLibraryItem> LibraryItems => _libraryComposition.LibraryItems;

    public string LibrarySearchQuery => _libraryComposition.SearchQuery;

    public TemplateLibraryItem? SelectedLibraryItem => _libraryComposition.SelectedItem;

    public Task EnsureLibraryAsync(bool forceRefresh) => _libraryComposition.EnsureLibraryAsync(forceRefresh);

    public void ApplyShellState()
    {
        _workspaceHost.Visibility = _shellBridge.IsTemplatesCapabilityActive ? Visibility.Visible : Visibility.Collapsed;
        _editorView.Visibility = _shellBridge.IsTemplatesEditorActive ? Visibility.Visible : Visibility.Collapsed;
        _libraryComposition.ApplyShellState(_shellBridge.IsTemplatesLibraryActive);
    }

    public void ApplyUiState(TemplatesWorkspaceUiState state)
    {
        _libraryComposition.ApplyUiState(state.IsLoading, state.HasSelectedLibraryItem);
        _editorView.SaveTemplateButtonControl.IsEnabled = state.HasActiveTemplateEditorDocument && !state.IsLoading;
        _editorView.SaveTemplateAsButtonControl.IsEnabled = state.HasActiveTemplateEditorDocument && !state.IsLoading;
        _editorView.ValidateTemplateButtonControl.IsEnabled = state.HasActiveTemplateEditorDocument && !state.IsLoading;
        _editorView.BackToLibraryButtonControl.IsEnabled = !state.IsLoading;
        _editorView.AddTemplateVmButtonControl.IsEnabled = state.HasActiveTemplateEditorDocument && !state.IsLoading;
        _editorView.RemoveTemplateVmButtonControl.IsEnabled = state.HasSelectedTemplateVmEntry && !state.IsLoading;
        _editorView.AddTemplateVmSwitchRowButtonControl.IsEnabled = state.HasSelectedTemplateVmEntry && !state.IsLoading;
        _editorView.TemplateVmVhdxCatalogComboBoxControl.IsEnabled = state.HasSelectedTemplateVmEntry && !state.IsLoading;
        _editorView.ApplyTemplateVmChangesButtonControl.IsEnabled = state.HasSelectedTemplateVmEntry && !state.IsLoading;
        _editorView.TemplateEditorContextTextBlockControl.Text = state.TemplateEditorContextText;
        _editorView.TemplateIdTextBlockControl.Text = state.TemplateIdText;
        _editorView.TemplateFilePathTextBlockControl.Text = state.TemplateFilePathText;
        _editorView.TemplateVmCountTextBlockControl.Text = state.TemplateVmCountText;
        _editorView.TemplateNameTextBoxControl.Text = state.TemplateName;
        _editorView.TemplateDescriptionTextBoxControl.Text = state.TemplateDescription;
    }
}

internal readonly record struct TemplatesWorkspaceUiState(
    bool IsLoading,
    bool HasSelectedLibraryItem,
    bool HasActiveTemplateEditorDocument,
    bool HasSelectedTemplateVmEntry,
    string TemplateEditorContextText,
    string TemplateIdText,
    string TemplateFilePathText,
    string TemplateVmCountText,
    string TemplateName,
    string TemplateDescription);
