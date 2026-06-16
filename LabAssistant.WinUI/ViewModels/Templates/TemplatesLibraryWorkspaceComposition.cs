using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Views.Templates;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.ViewModels.Templates;

internal interface ITemplatesLibraryWorkspaceHost
{
    bool IsTemplatesLoading { get; }

    void SetTemplatesLoading(bool isLoading);

    void ApplyTemplatesWorkspaceUiState();

    Task ShowTemplateEditorAsync(TemplateEditorDocument document, string statusText);

    Task ShowTemplateBuilderAsync(TemplateEditorDocument document);

    Task CreateTemplateBuilderDraftAsync();

    void SetTemplateEditorStatus(string statusText);

    Task<string?> PickTemplateFileForOpenAsync();

    Task<string?> PickTemplateFileForSaveAsync(string suggestedFileName);

    Task<bool> ShowDeleteTemplateConfirmationDialogAsync(TemplateLibraryItem templateItem);

    void ReconcileDeployTemplateSelection(IReadOnlyList<TemplateLibraryItem> items);
}

internal sealed class TemplatesLibraryWorkspaceHost : ITemplatesLibraryWorkspaceHost
{
    private readonly Func<bool> _isTemplatesLoading;
    private readonly Action<bool> _setTemplatesLoading;
    private readonly Action _applyTemplatesWorkspaceUiState;
    private readonly Func<TemplateEditorDocument, string, Task> _showTemplateEditorAsync;
    private readonly Func<TemplateEditorDocument, Task> _showTemplateBuilderAsync;
    private readonly Func<Task> _createTemplateBuilderDraftAsync;
    private readonly Action<string> _setTemplateEditorStatus;
    private readonly Func<Task<string?>> _pickTemplateFileForOpenAsync;
    private readonly Func<string, Task<string?>> _pickTemplateFileForSaveAsync;
    private readonly Func<TemplateLibraryItem, Task<bool>> _showDeleteTemplateConfirmationDialogAsync;
    private readonly Action<IReadOnlyList<TemplateLibraryItem>> _reconcileDeployTemplateSelection;

    public TemplatesLibraryWorkspaceHost(
        Func<bool> isTemplatesLoading,
        Action<bool> setTemplatesLoading,
        Action applyTemplatesWorkspaceUiState,
        Func<TemplateEditorDocument, string, Task> showTemplateEditorAsync,
        Func<TemplateEditorDocument, Task> showTemplateBuilderAsync,
        Func<Task> createTemplateBuilderDraftAsync,
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
        _showTemplateBuilderAsync = showTemplateBuilderAsync;
        _createTemplateBuilderDraftAsync = createTemplateBuilderDraftAsync;
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

    public Task ShowTemplateBuilderAsync(TemplateEditorDocument document) => _showTemplateBuilderAsync(document);

    public Task CreateTemplateBuilderDraftAsync() => _createTemplateBuilderDraftAsync();

    public void SetTemplateEditorStatus(string statusText) => _setTemplateEditorStatus(statusText);

    public Task<string?> PickTemplateFileForOpenAsync() => _pickTemplateFileForOpenAsync();

    public Task<string?> PickTemplateFileForSaveAsync(string suggestedFileName) => _pickTemplateFileForSaveAsync(suggestedFileName);

    public Task<bool> ShowDeleteTemplateConfirmationDialogAsync(TemplateLibraryItem templateItem) => _showDeleteTemplateConfirmationDialogAsync(templateItem);

    public void ReconcileDeployTemplateSelection(IReadOnlyList<TemplateLibraryItem> items) => _reconcileDeployTemplateSelection(items);
}

internal sealed class TemplatesLibraryWorkspaceComposition : ITemplatesLibraryWorkspaceControllerHost
{
    private readonly TemplatesLibraryView _view;
    private readonly ITemplatesLibraryWorkspaceHost _host;
    private readonly TemplatesLibraryWorkspaceViewModel _workspace = new();
    private readonly TemplatesLibraryWorkspaceController _controller;

    public TemplatesLibraryWorkspaceComposition(
        ITemplatesCapabilityService templatesCapabilityService,
        TemplatesLibraryView view,
        ITemplatesLibraryWorkspaceHost host)
    {
        _view = view;
        _host = host;
        _controller = new TemplatesLibraryWorkspaceController(templatesCapabilityService, _workspace, this);
        _view.SetInventorySource(_workspace.Items);
        WireHandlers();
        ApplyViewState();
    }

    public IList<TemplateLibraryItem> LibraryItems => _workspace.Items;

    public string SearchQuery => _workspace.SearchQuery;

    public TemplateLibraryItem? SelectedItem => _workspace.SelectedItem;

    public void ApplyShellState(bool isLibraryActive)
    {
        _view.Visibility = isLibraryActive ? Visibility.Visible : Visibility.Collapsed;
        if (isLibraryActive)
        {
            _ = _controller.EnsureLibraryAsync(forceRefresh: false);
        }
    }

    public Task EnsureLibraryAsync(bool forceRefresh) => _controller.EnsureLibraryAsync(forceRefresh);

    public void ApplyUiState(bool isLoading, bool hasSelectedLibraryItem)
    {
        _view.UpdateViewState(BuildViewState(isLoading, hasSelectedLibraryItem));
    }

    private void ApplyViewState()
    {
        _view.UpdateViewState(BuildViewState(_host.IsTemplatesLoading, _workspace.SelectedItem is not null));
    }

    private void WireHandlers()
    {
        _view.SearchTextChanged += TemplatesLibraryView_SearchTextChanged;
        _view.SelectedTemplateChanged += TemplatesLibraryView_SelectedTemplateChanged;
        _view.ApplySearchRequested += TemplatesLibraryView_ApplySearchRequested;
        _view.ClearSearchRequested += TemplatesLibraryView_ClearSearchRequested;
        _view.ReloadRequested += TemplatesLibraryView_ReloadRequested;
        _view.OpenTemplateRequested += TemplatesLibraryView_OpenTemplateRequested;
        _view.CreateTemplateRequested += TemplatesLibraryView_CreateTemplateRequested;
        _view.OpenTemplateInBuilderRequested += TemplatesLibraryView_OpenTemplateInBuilderRequested;
        _view.CreateBuilderTemplateRequested += TemplatesLibraryView_CreateBuilderTemplateRequested;
        _view.DeleteTemplateRequested += TemplatesLibraryView_DeleteTemplateRequested;
        _view.ImportTemplateRequested += TemplatesLibraryView_ImportTemplateRequested;
        _view.ExportTemplateRequested += TemplatesLibraryView_ExportTemplateRequested;
    }

    bool ITemplatesLibraryWorkspaceControllerHost.IsTemplatesLoading => _host.IsTemplatesLoading;

    void ITemplatesLibraryWorkspaceControllerHost.SetTemplatesLoading(bool isLoading) => _host.SetTemplatesLoading(isLoading);

    void ITemplatesLibraryWorkspaceControllerHost.ApplyWorkspaceState()
    {
        ApplyViewState();
        _host.ApplyTemplatesWorkspaceUiState();
    }

    Task ITemplatesLibraryWorkspaceControllerHost.ShowTemplateEditorAsync(TemplateEditorDocument document, string statusText) => _host.ShowTemplateEditorAsync(document, statusText);

    Task ITemplatesLibraryWorkspaceControllerHost.ShowTemplateBuilderAsync(TemplateEditorDocument document) => _host.ShowTemplateBuilderAsync(document);

    Task ITemplatesLibraryWorkspaceControllerHost.CreateTemplateBuilderDraftAsync() => _host.CreateTemplateBuilderDraftAsync();

    void ITemplatesLibraryWorkspaceControllerHost.SetTemplateEditorStatus(string statusText) => _host.SetTemplateEditorStatus(statusText);

    Task<string?> ITemplatesLibraryWorkspaceControllerHost.PickTemplateFileForOpenAsync() => _host.PickTemplateFileForOpenAsync();

    Task<string?> ITemplatesLibraryWorkspaceControllerHost.PickTemplateFileForSaveAsync(string suggestedFileName) => _host.PickTemplateFileForSaveAsync(suggestedFileName);

    Task<bool> ITemplatesLibraryWorkspaceControllerHost.ShowDeleteTemplateConfirmationDialogAsync(TemplateLibraryItem templateItem) => _host.ShowDeleteTemplateConfirmationDialogAsync(templateItem);

    void ITemplatesLibraryWorkspaceControllerHost.ReconcileDeployTemplateSelection(IReadOnlyList<TemplateLibraryItem> items) => _host.ReconcileDeployTemplateSelection(items);

    private void TemplatesLibraryView_SearchTextChanged(object? sender, EventArgs e)
    {
        _controller.HandleSearchTextChanged(_view.CaptureInteractionState().SearchText);
    }

    private void TemplatesLibraryView_SelectedTemplateChanged(object? sender, EventArgs e)
    {
        _controller.HandleSelectionChanged(_view.CaptureInteractionState().SelectedTemplate);
    }

    private async void TemplatesLibraryView_ApplySearchRequested(object? sender, EventArgs e)
    {
        await _controller.ApplySearchAsync();
    }

    private async void TemplatesLibraryView_ClearSearchRequested(object? sender, EventArgs e)
    {
        await _controller.ClearSearchAsync();
    }

    private async void TemplatesLibraryView_ReloadRequested(object? sender, EventArgs e)
    {
        await _controller.ReloadAsync();
    }

    private async void TemplatesLibraryView_OpenTemplateRequested(object? sender, EventArgs e)
    {
        await _controller.OpenSelectedTemplateInEditorAsync();
    }

    private async void TemplatesLibraryView_CreateTemplateRequested(object? sender, EventArgs e)
    {
        await _controller.CreateTemplateAsync();
    }

    private async void TemplatesLibraryView_OpenTemplateInBuilderRequested(object? sender, EventArgs e)
    {
        await _controller.OpenSelectedTemplateInBuilderAsync();
    }

    private async void TemplatesLibraryView_CreateBuilderTemplateRequested(object? sender, EventArgs e)
    {
        await _controller.CreateBuilderTemplateAsync();
    }

    private async void TemplatesLibraryView_DeleteTemplateRequested(object? sender, EventArgs e)
    {
        await _controller.DeleteSelectedTemplateAsync();
    }

    private async void TemplatesLibraryView_ImportTemplateRequested(object? sender, EventArgs e)
    {
        await _controller.ImportTemplateAsync();
    }

    private async void TemplatesLibraryView_ExportTemplateRequested(object? sender, EventArgs e)
    {
        await _controller.ExportSelectedTemplateAsync();
    }

    private TemplatesLibraryViewState BuildViewState(bool isLoading, bool hasSelectedLibraryItem)
    {
        return new TemplatesLibraryViewState(
            SearchText: _workspace.SearchQuery,
            StatusText: _workspace.StatusText,
            SelectedTemplate: _workspace.SelectedItem,
            CanApplySearch: !isLoading,
            CanClearSearch: !isLoading,
            CanReload: !isLoading,
            CanOpenTemplate: hasSelectedLibraryItem && !isLoading,
            CanCreateTemplate: !isLoading,
            CanOpenTemplateInBuilder: _workspace.SelectedItem?.ExecutionEngine == TemplateExecutionEngine.V2UnifiedPlanning && !isLoading,
            CanCreateBuilderTemplate: !isLoading,
            CanDeleteTemplate: hasSelectedLibraryItem && !isLoading,
            CanImportTemplate: !isLoading,
            CanExportTemplate: hasSelectedLibraryItem && !isLoading);
    }
}
