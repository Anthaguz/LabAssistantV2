using LabAssistant.Business.Templates;
using LabAssistant.WinUI.Views.Templates;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

internal interface ITemplatesBuilderWorkspaceHost
{
    bool IsTemplatesLoading { get; }

    void SetTemplatesLoading(bool isLoading);

    void ApplyTemplatesWorkspaceUiState();

    Task EnsureTemplatesLibraryAsync(bool forceRefresh);

    Task<TemplatesBuilderReferenceData> LoadReferenceDataAsync(bool forceRefresh);

    Task<string?> PickTemplateFileForSaveAsync(string suggestedFileName);

    void NavigateToBuilder();

    void NavigateToLibrary();
}

internal sealed class TemplatesBuilderWorkspaceHost : ITemplatesBuilderWorkspaceHost
{
    private readonly Func<bool> _isTemplatesLoading;
    private readonly Action<bool> _setTemplatesLoading;
    private readonly Action _applyTemplatesWorkspaceUiState;
    private readonly Func<bool, Task> _ensureTemplatesLibraryAsync;
    private readonly Func<bool, Task<TemplatesBuilderReferenceData>> _loadReferenceDataAsync;
    private readonly Func<string, Task<string?>> _pickTemplateFileForSaveAsync;
    private readonly Action _navigateToBuilder;
    private readonly Action _navigateToLibrary;

    public TemplatesBuilderWorkspaceHost(
        Func<bool> isTemplatesLoading,
        Action<bool> setTemplatesLoading,
        Action applyTemplatesWorkspaceUiState,
        Func<bool, Task> ensureTemplatesLibraryAsync,
        Func<bool, Task<TemplatesBuilderReferenceData>> loadReferenceDataAsync,
        Func<string, Task<string?>> pickTemplateFileForSaveAsync,
        Action navigateToBuilder,
        Action navigateToLibrary)
    {
        _isTemplatesLoading = isTemplatesLoading;
        _setTemplatesLoading = setTemplatesLoading;
        _applyTemplatesWorkspaceUiState = applyTemplatesWorkspaceUiState;
        _ensureTemplatesLibraryAsync = ensureTemplatesLibraryAsync;
        _loadReferenceDataAsync = loadReferenceDataAsync;
        _pickTemplateFileForSaveAsync = pickTemplateFileForSaveAsync;
        _navigateToBuilder = navigateToBuilder;
        _navigateToLibrary = navigateToLibrary;
    }

    public bool IsTemplatesLoading => _isTemplatesLoading();

    public void SetTemplatesLoading(bool isLoading) => _setTemplatesLoading(isLoading);

    public void ApplyTemplatesWorkspaceUiState() => _applyTemplatesWorkspaceUiState();

    public Task EnsureTemplatesLibraryAsync(bool forceRefresh) => _ensureTemplatesLibraryAsync(forceRefresh);

    public Task<TemplatesBuilderReferenceData> LoadReferenceDataAsync(bool forceRefresh) => _loadReferenceDataAsync(forceRefresh);

    public Task<string?> PickTemplateFileForSaveAsync(string suggestedFileName) => _pickTemplateFileForSaveAsync(suggestedFileName);

    public void NavigateToBuilder() => _navigateToBuilder();

    public void NavigateToLibrary() => _navigateToLibrary();
}

internal sealed class TemplatesBuilderWorkspaceComposition : ITemplatesBuilderWorkspaceControllerHost
{
    private readonly TemplatesBuilderView _view;
    private readonly ITemplatesBuilderWorkspaceHost _host;
    private readonly TemplatesBuilderWorkspaceViewModel _workspace = new();
    private readonly TemplatesBuilderWorkspaceController _controller;

    public TemplatesBuilderWorkspaceComposition(
        ITemplatesCapabilityService templatesCapabilityService,
        TemplatesBuilderView view,
        ITemplatesBuilderWorkspaceHost host)
    {
        _view = view;
        _host = host;
        _controller = new TemplatesBuilderWorkspaceController(templatesCapabilityService, _workspace, this);
        WireHandlers();
        RefreshUiState();
    }

    public Task CreateDraftAsync() => _controller.CreateDraftAsync();

    public Task ShowDocumentAsync(TemplateEditorDocument document) => _controller.ShowDocumentAsync(document);

    public void ApplyShellState(bool isBuilderActive)
    {
        _view.Visibility = isBuilderActive ? Visibility.Visible : Visibility.Collapsed;
        RefreshUiState();
    }

    public void RefreshUiState()
    {
        _view.UpdateViewState(new TemplatesBuilderViewState(
            ContextText: _workspace.ContextText,
            ReferenceText: _workspace.ReferenceText,
            StatusText: _workspace.StatusText,
            IsStatusVisible: _workspace.HasStatusText,
            HasActiveDraft: _workspace.HasActiveDraft,
            Draft: _workspace.CaptureDraft()));
        _view.UpdateActionState(new TemplatesBuilderActionState(
            CanNavigate: _workspace.HasActiveDraft && !_host.IsTemplatesLoading,
            CanSave: _workspace.HasActiveDraft && !_host.IsTemplatesLoading,
            CanSaveAs: _workspace.HasActiveDraft && !_host.IsTemplatesLoading,
            CanBackToLibrary: !_host.IsTemplatesLoading));
    }

    bool ITemplatesBuilderWorkspaceControllerHost.IsTemplatesLoading => _host.IsTemplatesLoading;

    void ITemplatesBuilderWorkspaceControllerHost.SetTemplatesLoading(bool isLoading) => _host.SetTemplatesLoading(isLoading);

    void ITemplatesBuilderWorkspaceControllerHost.ApplyWorkspaceState()
    {
        RefreshUiState();
        _host.ApplyTemplatesWorkspaceUiState();
    }

    Task ITemplatesBuilderWorkspaceControllerHost.EnsureTemplatesLibraryAsync(bool forceRefresh) => _host.EnsureTemplatesLibraryAsync(forceRefresh);

    Task<TemplatesBuilderReferenceData> ITemplatesBuilderWorkspaceControllerHost.LoadReferenceDataAsync(bool forceRefresh) => _host.LoadReferenceDataAsync(forceRefresh);

    Task<string?> ITemplatesBuilderWorkspaceControllerHost.PickTemplateFileForSaveAsync(string suggestedFileName) => _host.PickTemplateFileForSaveAsync(suggestedFileName);

    void ITemplatesBuilderWorkspaceControllerHost.NavigateToBuilder() => _host.NavigateToBuilder();

    void ITemplatesBuilderWorkspaceControllerHost.NavigateToLibrary() => _host.NavigateToLibrary();

    private void WireHandlers()
    {
        _view.DraftChanged += TemplatesBuilderView_DraftChanged;
        _view.SaveRequested += TemplatesBuilderView_SaveRequested;
        _view.SaveAsRequested += TemplatesBuilderView_SaveAsRequested;
        _view.BackToLibraryRequested += TemplatesBuilderView_BackToLibraryRequested;
    }

    private void TemplatesBuilderView_DraftChanged(object? sender, EventArgs e)
    {
        _workspace.ApplyDraft(_view.CaptureDraft());
        _view.UpdateActionState(new TemplatesBuilderActionState(
            CanNavigate: _workspace.HasActiveDraft && !_host.IsTemplatesLoading,
            CanSave: _workspace.HasActiveDraft && !_host.IsTemplatesLoading,
            CanSaveAs: _workspace.HasActiveDraft && !_host.IsTemplatesLoading,
            CanBackToLibrary: !_host.IsTemplatesLoading));
    }

    private async void TemplatesBuilderView_SaveRequested(object? sender, EventArgs e)
    {
        await _controller.SaveAsync();
    }

    private async void TemplatesBuilderView_SaveAsRequested(object? sender, EventArgs e)
    {
        await _controller.SaveAsAsync();
    }

    private void TemplatesBuilderView_BackToLibraryRequested(object? sender, EventArgs e)
    {
        _host.NavigateToLibrary();
    }
}
