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
    private readonly TemplatesLibraryView _libraryView;
    private readonly TemplatesEditorView _editorView;
    private readonly ITemplatesWorkspaceShellBridge _shellBridge;
    private readonly Action _ensureTemplatesLibraryLoaded;

    public TemplatesWorkspaceComposition(
        FrameworkElement workspaceHost,
        TemplatesLibraryView libraryView,
        TemplatesEditorView editorView,
        Action ensureTemplatesLibraryLoaded,
        ITemplatesWorkspaceShellBridge shellBridge)
    {
        _workspaceHost = workspaceHost;
        _libraryView = libraryView;
        _editorView = editorView;
        _ensureTemplatesLibraryLoaded = ensureTemplatesLibraryLoaded;
        _shellBridge = shellBridge;
    }

    public void ApplyShellState()
    {
        _workspaceHost.Visibility = _shellBridge.IsTemplatesCapabilityActive ? Visibility.Visible : Visibility.Collapsed;
        _libraryView.Visibility = _shellBridge.IsTemplatesLibraryActive ? Visibility.Visible : Visibility.Collapsed;
        _editorView.Visibility = _shellBridge.IsTemplatesEditorActive ? Visibility.Visible : Visibility.Collapsed;

        if (_shellBridge.IsTemplatesLibraryActive)
        {
            _ensureTemplatesLibraryLoaded();
        }
    }

    public void ApplyUiState(TemplatesWorkspaceUiState state)
    {
        _libraryView.OpenTemplateInEditorButtonControl.IsEnabled = state.HasSelectedLibraryItem && !state.IsLoading;
        _libraryView.DeleteTemplateButtonControl.IsEnabled = state.HasSelectedLibraryItem && !state.IsLoading;
        _libraryView.ExportTemplateButtonControl.IsEnabled = state.HasSelectedLibraryItem && !state.IsLoading;
        _libraryView.ApplyTemplateSearchButtonControl.IsEnabled = !state.IsLoading;
        _libraryView.ClearTemplateSearchButtonControl.IsEnabled = !state.IsLoading;
        _libraryView.ReloadTemplatesButtonControl.IsEnabled = !state.IsLoading;
        _libraryView.ImportTemplateButtonControl.IsEnabled = !state.IsLoading;
        _libraryView.CreateTemplateButtonControl.IsEnabled = !state.IsLoading;
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
