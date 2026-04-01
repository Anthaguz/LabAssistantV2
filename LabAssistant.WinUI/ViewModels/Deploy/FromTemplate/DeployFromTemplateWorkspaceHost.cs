using LabAssistant.Business.Deployment;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Models.Deploy;

namespace LabAssistant.WinUI.ViewModels.Deploy;

internal sealed class DeployFromTemplateWorkspaceHost : IDeployFromTemplateCompositionHost
{
    private readonly DeployReferenceDataService _referenceDataService;
    private readonly DeployResolveSuggestionsService _resolveSuggestionsService;
    private readonly Func<bool> _isTemplatesLoading;
    private readonly Func<IReadOnlyList<TemplateLibraryItem>> _templateLibraryItems;
    private readonly Func<bool, Task> _ensureTemplatesLibraryAsync;
    private readonly Func<string, Task<TemplateEditorDocument>> _loadTemplateForEditorAsync;
    private readonly Func<TemplateEditorDocument, string, Task> _showTemplateEditorAsync;
    private readonly Func<MultiVmDeploymentContext, DeploymentPreflightMode, Task<DeploymentReadinessReport>> _runReadinessAsync;
    private readonly Action _refreshSharedUiState;
    private readonly Action _applyRightPanelState;
    private readonly Func<MultiVmDeploymentContext, Task<DeploymentOutcomeSummary>> _deployAllAsync;
    private readonly Action<MultiVmDeploymentContext, Action<string, string?>, Action<string, DeployStepStateUpdate>> _attachProgressCallbacks;
    private readonly Action _onOpenResultsPanelRequested;

    public DeployFromTemplateWorkspaceHost(
        DeployReferenceDataService referenceDataService,
        DeployResolveSuggestionsService resolveSuggestionsService,
        Func<bool> isTemplatesLoading,
        Func<IReadOnlyList<TemplateLibraryItem>> templateLibraryItems,
        Func<bool, Task> ensureTemplatesLibraryAsync,
        Func<string, Task<TemplateEditorDocument>> loadTemplateForEditorAsync,
        Func<TemplateEditorDocument, string, Task> showTemplateEditorAsync,
        Func<MultiVmDeploymentContext, DeploymentPreflightMode, Task<DeploymentReadinessReport>> runReadinessAsync,
        Action refreshSharedUiState,
        Action applyRightPanelState,
        Func<MultiVmDeploymentContext, Task<DeploymentOutcomeSummary>> deployAllAsync,
        Action<MultiVmDeploymentContext, Action<string, string?>, Action<string, DeployStepStateUpdate>> attachProgressCallbacks,
        Action onOpenResultsPanelRequested)
    {
        _referenceDataService = referenceDataService;
        _resolveSuggestionsService = resolveSuggestionsService;
        _isTemplatesLoading = isTemplatesLoading;
        _templateLibraryItems = templateLibraryItems;
        _ensureTemplatesLibraryAsync = ensureTemplatesLibraryAsync;
        _loadTemplateForEditorAsync = loadTemplateForEditorAsync;
        _showTemplateEditorAsync = showTemplateEditorAsync;
        _runReadinessAsync = runReadinessAsync;
        _refreshSharedUiState = refreshSharedUiState;
        _applyRightPanelState = applyRightPanelState;
        _attachProgressCallbacks = attachProgressCallbacks;
        _deployAllAsync = deployAllAsync;
        _onOpenResultsPanelRequested = onOpenResultsPanelRequested;
    }

    public AppSettings DeploymentSettings => _referenceDataService.DeploymentSettings;

    public IReadOnlyList<string> AvailableSwitches => _referenceDataService.AvailableSwitches;

    public bool IsTemplatesLoading => _isTemplatesLoading();

    public IReadOnlyList<TemplateLibraryItem> TemplateLibraryItems => _templateLibraryItems();

    public IReadOnlyList<VhdxCatalogItem> LoadCatalogItems() => _referenceDataService.CatalogItems;

    public Task EnsureReferenceDataAsync(bool forceRefresh) => _referenceDataService.EnsureAsync(forceRefresh);

    public Task EnsureTemplatesLibraryAsync(bool forceRefresh) => _ensureTemplatesLibraryAsync(forceRefresh);

    public Task<TemplateEditorDocument> LoadTemplateForEditorAsync(string filePath) => _loadTemplateForEditorAsync(filePath);

    public async Task<int> ApplyResolveSuggestionsAsync(LabTemplate template)
    {
        await _referenceDataService.EnsureAsync(forceRefresh: false);
        return _resolveSuggestionsService.Apply(
            template,
            _referenceDataService.CatalogItems,
            _referenceDataService.AvailableSwitches);
    }

    public Task ShowTemplateEditorAsync(TemplateEditorDocument document, string statusText) => _showTemplateEditorAsync(document, statusText);

    public Task<DeploymentReadinessReport> RunReadinessAsync(MultiVmDeploymentContext context, DeploymentPreflightMode mode) => _runReadinessAsync(context, mode);

    public void RefreshSharedUiState() => _refreshSharedUiState();

    public void ApplyRightPanelState() => _applyRightPanelState();

    public Task<DeploymentOutcomeSummary> DeployAllAsync(MultiVmDeploymentContext context) => _deployAllAsync(context);

    public void AttachProgressCallbacks(
        MultiVmDeploymentContext context,
        Action<string, string?> onLogMessage,
        Action<string, DeployStepStateUpdate> onStepStateUpdated) => _attachProgressCallbacks(context, onLogMessage, onStepStateUpdated);

    public void OnOpenResultsPanelRequested() => _onOpenResultsPanelRequested();
}
