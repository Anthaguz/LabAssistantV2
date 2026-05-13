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
    private readonly DeployTemplatesShellAdapter _templatesShellAdapter;
    private readonly Action _refreshSharedUiState;
    private readonly Action _refreshResultsPanelState;
    private readonly Func<MultiVmDeploymentContext, DeploymentPreflightMode, Task<DeploymentReadinessReport>> _runReadinessAsync;
    private readonly Func<MultiVmDeploymentContext, Task<DeploymentOutcomeSummary>> _deployAllAsync;
    private readonly Action<MultiVmDeploymentContext, Action<string, string?>, Action<string, DeployStepStateUpdate>> _attachProgressCallbacks;
    private readonly Action _onOpenResultsPanelRequested;

    public DeployFromTemplateWorkspaceHost(
        DeployReferenceDataService referenceDataService,
        DeployResolveSuggestionsService resolveSuggestionsService,
        DeployTemplatesShellAdapter templatesShellAdapter,
        Action refreshSharedUiState,
        Action refreshResultsPanelState,
        Func<MultiVmDeploymentContext, DeploymentPreflightMode, Task<DeploymentReadinessReport>> runReadinessAsync,
        Func<MultiVmDeploymentContext, Task<DeploymentOutcomeSummary>> deployAllAsync,
        Action<MultiVmDeploymentContext, Action<string, string?>, Action<string, DeployStepStateUpdate>> attachProgressCallbacks,
        Action onOpenResultsPanelRequested)
    {
        _referenceDataService = referenceDataService;
        _resolveSuggestionsService = resolveSuggestionsService;
        _templatesShellAdapter = templatesShellAdapter;
        _refreshSharedUiState = refreshSharedUiState;
        _refreshResultsPanelState = refreshResultsPanelState;
        _runReadinessAsync = runReadinessAsync;
        _attachProgressCallbacks = attachProgressCallbacks;
        _deployAllAsync = deployAllAsync;
        _onOpenResultsPanelRequested = onOpenResultsPanelRequested;
    }

    public AppSettings DeploymentSettings => _referenceDataService.DeploymentSettings;

    public IReadOnlyList<string> AvailableSwitches => _referenceDataService.AvailableSwitches;

    public bool IsTemplatesLoading => _templatesShellAdapter.IsTemplatesLoading;

    public IReadOnlyList<TemplateLibraryItem> TemplateLibraryItems => _templatesShellAdapter.GetLibraryItems();

    public IReadOnlyList<VhdxCatalogItem> LoadCatalogItems() => _referenceDataService.CatalogItems;

    public Task EnsureReferenceDataAsync(bool forceRefresh) => _referenceDataService.EnsureAsync(forceRefresh);

    public Task EnsureTemplatesLibraryAsync(bool forceRefresh) => _templatesShellAdapter.EnsureLibraryAsync(forceRefresh);

    public Task<TemplateEditorDocument> LoadTemplateForEditorAsync(string filePath) => _templatesShellAdapter.LoadTemplateForEditorAsync(filePath);

    public async Task<int> ApplyResolveSuggestionsAsync(LabTemplate template)
    {
        await _referenceDataService.EnsureAsync(forceRefresh: false);
        return _resolveSuggestionsService.Apply(
            template,
            _referenceDataService.CatalogItems,
            _referenceDataService.AvailableSwitches);
    }

    public Task ShowTemplateEditorAsync(TemplateEditorDocument document, string statusText) =>
        _templatesShellAdapter.ShowTemplateEditorAsync(document, statusText);

    public Task<DeploymentReadinessReport> RunReadinessAsync(MultiVmDeploymentContext context, DeploymentPreflightMode mode) => _runReadinessAsync(context, mode);

    public void RefreshSharedUiState() => _refreshSharedUiState();

    public void RefreshResultsPanelState() => _refreshResultsPanelState();

    public Task<DeploymentOutcomeSummary> DeployAllAsync(MultiVmDeploymentContext context) => _deployAllAsync(context);

    public void AttachProgressCallbacks(
        MultiVmDeploymentContext context,
        Action<string, string?> onLogMessage,
        Action<string, DeployStepStateUpdate> onStepStateUpdated) => _attachProgressCallbacks(context, onLogMessage, onStepStateUpdated);

    public void OnOpenResultsPanelRequested() => _onOpenResultsPanelRequested();
}
