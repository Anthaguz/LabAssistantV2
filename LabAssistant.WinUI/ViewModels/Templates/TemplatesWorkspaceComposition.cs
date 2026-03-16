using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Views.Templates;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.ViewModels.Templates;

internal interface ITemplatesWorkspaceCompositionHost
{
    bool IsTemplatesLoading { get; }

    void SetTemplatesLoading(bool isLoading);

    void ApplyTemplatesWorkspaceUiState();

    Task ShowTemplateEditorAsync(TemplateEditorDocument document, string statusText);

    void SetTemplateEditorStatus(string statusText);

    Task<string?> PickTemplateFileForOpenAsync();

    Task<string?> PickTemplateFileForSaveAsync(string suggestedFileName);

    Task<bool> ShowDeleteTemplateConfirmationDialogAsync(TemplateLibraryItem templateItem);

    void ReconcileDeployTemplateSelection(IReadOnlyList<TemplateLibraryItem> items);
}

internal sealed class TemplatesWorkspaceCompositionHost : ITemplatesWorkspaceCompositionHost
{
    private readonly Func<bool> _isTemplatesLoading;
    private readonly Action<bool> _setTemplatesLoading;
    private readonly Action _applyTemplatesWorkspaceUiState;
    private readonly Func<TemplateEditorDocument, string, Task> _showTemplateEditorAsync;
    private readonly Action<string> _setTemplateEditorStatus;
    private readonly Func<Task<string?>> _pickTemplateFileForOpenAsync;
    private readonly Func<string, Task<string?>> _pickTemplateFileForSaveAsync;
    private readonly Func<TemplateLibraryItem, Task<bool>> _showDeleteTemplateConfirmationDialogAsync;
    private readonly Action<IReadOnlyList<TemplateLibraryItem>> _reconcileDeployTemplateSelection;

    public TemplatesWorkspaceCompositionHost(
        Func<bool> isTemplatesLoading,
        Action<bool> setTemplatesLoading,
        Action applyTemplatesWorkspaceUiState,
        Func<TemplateEditorDocument, string, Task> showTemplateEditorAsync,
        Action<string> setTemplateEditorStatus,
        Func<Task<string?>> pickTemplateFileForOpenAsync,
        Func<string, Task<string?>> pickTemplateFileForSaveAsync,
        Func<TemplateLibraryItem, Task<bool>> showDeleteTemplateConfirmationDialogAsync,
        Action<IReadOnlyList<TemplateLibraryItem>> reconcileDeployTemplateSelection)
    {
        _isTemplatesLoading = isTemplatesLoading;
        _setTemplatesLoading = setTemplatesLoading;
        _applyTemplatesWorkspaceUiState = applyTemplatesWorkspaceUiState;
        _showTemplateEditorAsync = showTemplateEditorAsync;
        _setTemplateEditorStatus = setTemplateEditorStatus;
        _pickTemplateFileForOpenAsync = pickTemplateFileForOpenAsync;
        _pickTemplateFileForSaveAsync = pickTemplateFileForSaveAsync;
        _showDeleteTemplateConfirmationDialogAsync = showDeleteTemplateConfirmationDialogAsync;
        _reconcileDeployTemplateSelection = reconcileDeployTemplateSelection;
    }

    public bool IsTemplatesLoading => _isTemplatesLoading();

    public void SetTemplatesLoading(bool isLoading) => _setTemplatesLoading(isLoading);

    public void ApplyTemplatesWorkspaceUiState() => _applyTemplatesWorkspaceUiState();

    public Task ShowTemplateEditorAsync(TemplateEditorDocument document, string statusText) => _showTemplateEditorAsync(document, statusText);

    public void SetTemplateEditorStatus(string statusText) => _setTemplateEditorStatus(statusText);

    public Task<string?> PickTemplateFileForOpenAsync() => _pickTemplateFileForOpenAsync();

    public Task<string?> PickTemplateFileForSaveAsync(string suggestedFileName) => _pickTemplateFileForSaveAsync(suggestedFileName);

    public Task<bool> ShowDeleteTemplateConfirmationDialogAsync(TemplateLibraryItem templateItem) => _showDeleteTemplateConfirmationDialogAsync(templateItem);

    public void ReconcileDeployTemplateSelection(IReadOnlyList<TemplateLibraryItem> items) => _reconcileDeployTemplateSelection(items);
}

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

internal sealed class TemplatesWorkspaceComposition : ITemplatesLibraryWorkspaceControllerHost
{
    private readonly FrameworkElement _workspaceHost;
    private readonly TemplatesLibraryView _libraryView;
    private readonly TemplatesEditorView _editorView;
    private readonly ITemplatesWorkspaceShellBridge _shellBridge;
    private readonly ITemplatesWorkspaceCompositionHost _host;
    private readonly TemplatesLibraryWorkspaceViewModel _libraryWorkspace = new();
    private readonly TemplatesLibraryWorkspaceController _libraryController;

    public TemplatesWorkspaceComposition(
        ITemplatesCapabilityService templatesCapabilityService,
        FrameworkElement workspaceHost,
        TemplatesLibraryView libraryView,
        TemplatesEditorView editorView,
        ITemplatesWorkspaceCompositionHost host,
        ITemplatesWorkspaceShellBridge shellBridge)
    {
        _workspaceHost = workspaceHost;
        _libraryView = libraryView;
        _editorView = editorView;
        _host = host;
        _shellBridge = shellBridge;
        _libraryController = new TemplatesLibraryWorkspaceController(templatesCapabilityService, _libraryWorkspace, this);
        _libraryView.SetInventorySource(_libraryWorkspace.Items);
        WireLibraryHandlers();
        ApplyLibraryState();
    }

    public IList<TemplateLibraryItem> LibraryItems => _libraryWorkspace.Items;

    public string LibrarySearchQuery => _libraryWorkspace.SearchQuery;

    public TemplateLibraryItem? SelectedLibraryItem => _libraryWorkspace.SelectedItem;

    public Task EnsureLibraryAsync(bool forceRefresh) => _libraryController.EnsureLibraryAsync(forceRefresh);

    public void ApplyShellState()
    {
        _workspaceHost.Visibility = _shellBridge.IsTemplatesCapabilityActive ? Visibility.Visible : Visibility.Collapsed;
        _libraryView.Visibility = _shellBridge.IsTemplatesLibraryActive ? Visibility.Visible : Visibility.Collapsed;
        _editorView.Visibility = _shellBridge.IsTemplatesEditorActive ? Visibility.Visible : Visibility.Collapsed;

        if (_shellBridge.IsTemplatesLibraryActive)
        {
            _ = _libraryController.EnsureLibraryAsync(forceRefresh: false);
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

    private void ApplyLibraryState()
    {
        _libraryView.SetSearchText(_libraryWorkspace.SearchQuery);
        _libraryView.SetSelectedTemplate(_libraryWorkspace.SelectedItem);
        _libraryView.SetStatusText(_libraryWorkspace.StatusText);
    }

    private void WireLibraryHandlers()
    {
        _libraryView.SearchTextChanged += TemplatesLibraryView_SearchTextChanged;
        _libraryView.SelectedTemplateChanged += TemplatesLibraryView_SelectedTemplateChanged;
        _libraryView.ApplySearchRequested += TemplatesLibraryView_ApplySearchRequested;
        _libraryView.ClearSearchRequested += TemplatesLibraryView_ClearSearchRequested;
        _libraryView.ReloadRequested += TemplatesLibraryView_ReloadRequested;
        _libraryView.OpenTemplateRequested += TemplatesLibraryView_OpenTemplateRequested;
        _libraryView.CreateTemplateRequested += TemplatesLibraryView_CreateTemplateRequested;
        _libraryView.DeleteTemplateRequested += TemplatesLibraryView_DeleteTemplateRequested;
        _libraryView.ImportTemplateRequested += TemplatesLibraryView_ImportTemplateRequested;
        _libraryView.ExportTemplateRequested += TemplatesLibraryView_ExportTemplateRequested;
    }

    bool ITemplatesLibraryWorkspaceControllerHost.IsTemplatesLoading => _host.IsTemplatesLoading;

    void ITemplatesLibraryWorkspaceControllerHost.SetTemplatesLoading(bool isLoading) => _host.SetTemplatesLoading(isLoading);

    void ITemplatesLibraryWorkspaceControllerHost.ApplyWorkspaceState()
    {
        ApplyLibraryState();
        _host.ApplyTemplatesWorkspaceUiState();
    }

    Task ITemplatesLibraryWorkspaceControllerHost.ShowTemplateEditorAsync(TemplateEditorDocument document, string statusText) => _host.ShowTemplateEditorAsync(document, statusText);

    void ITemplatesLibraryWorkspaceControllerHost.SetTemplateEditorStatus(string statusText) => _host.SetTemplateEditorStatus(statusText);

    Task<string?> ITemplatesLibraryWorkspaceControllerHost.PickTemplateFileForOpenAsync() => _host.PickTemplateFileForOpenAsync();

    Task<string?> ITemplatesLibraryWorkspaceControllerHost.PickTemplateFileForSaveAsync(string suggestedFileName) => _host.PickTemplateFileForSaveAsync(suggestedFileName);

    Task<bool> ITemplatesLibraryWorkspaceControllerHost.ShowDeleteTemplateConfirmationDialogAsync(TemplateLibraryItem templateItem) => _host.ShowDeleteTemplateConfirmationDialogAsync(templateItem);

    void ITemplatesLibraryWorkspaceControllerHost.ReconcileDeployTemplateSelection(IReadOnlyList<TemplateLibraryItem> items) => _host.ReconcileDeployTemplateSelection(items);

    private void TemplatesLibraryView_SearchTextChanged(object? sender, EventArgs e)
    {
        _libraryController.HandleSearchTextChanged(_libraryView.SearchText);
    }

    private void TemplatesLibraryView_SelectedTemplateChanged(object? sender, EventArgs e)
    {
        _libraryController.HandleSelectionChanged(_libraryView.SelectedTemplate);
    }

    private async void TemplatesLibraryView_ApplySearchRequested(object? sender, EventArgs e)
    {
        await _libraryController.ApplySearchAsync();
    }

    private async void TemplatesLibraryView_ClearSearchRequested(object? sender, EventArgs e)
    {
        await _libraryController.ClearSearchAsync();
    }

    private async void TemplatesLibraryView_ReloadRequested(object? sender, EventArgs e)
    {
        await _libraryController.ReloadAsync();
    }

    private async void TemplatesLibraryView_OpenTemplateRequested(object? sender, EventArgs e)
    {
        await _libraryController.OpenSelectedTemplateInEditorAsync();
    }

    private async void TemplatesLibraryView_CreateTemplateRequested(object? sender, EventArgs e)
    {
        await _libraryController.CreateTemplateAsync();
    }

    private async void TemplatesLibraryView_DeleteTemplateRequested(object? sender, EventArgs e)
    {
        await _libraryController.DeleteSelectedTemplateAsync();
    }

    private async void TemplatesLibraryView_ImportTemplateRequested(object? sender, EventArgs e)
    {
        await _libraryController.ImportTemplateAsync();
    }

    private async void TemplatesLibraryView_ExportTemplateRequested(object? sender, EventArgs e)
    {
        await _libraryController.ExportSelectedTemplateAsync();
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
