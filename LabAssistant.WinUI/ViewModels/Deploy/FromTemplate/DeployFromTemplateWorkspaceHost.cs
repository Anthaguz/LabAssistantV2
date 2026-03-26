using LabAssistant.Business.Deployment;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.WinUI.Models.Deploy;

namespace LabAssistant.WinUI.ViewModels.Deploy;

internal sealed class DeployFromTemplateWorkspaceHost : IDeployFromTemplateCompositionHost
{
    private readonly Func<AppSettings> _deploymentSettings;
    private readonly Func<IReadOnlyList<string>> _availableSwitches;
    private readonly Func<bool> _isTemplatesLoading;
    private readonly Func<IReadOnlyList<TemplateLibraryItem>> _templateLibraryItems;
    private readonly Func<IReadOnlyList<VhdxCatalogItem>> _loadCatalogItems;
    private readonly Func<bool, Task> _ensureTemplateSwitchesAsync;
    private readonly Func<bool, Task> _ensureTemplatesLibraryAsync;
    private readonly Func<string, Task<TemplateEditorDocument>> _loadTemplateForEditorAsync;
    private readonly Func<MultiVmDeploymentContext, DeploymentPreflightMode, Task<DeploymentReadinessReport>> _runReadinessAsync;
    private readonly Action _refreshSharedUiState;
    private readonly Action _applyRightPanelState;
    private readonly Func<MultiVmDeploymentContext, Task<DeploymentOutcomeSummary>> _deployAllAsync;
    private readonly Action<MultiVmDeploymentContext, Action<string, string?>, Action<string, DeployStepStateUpdate>> _attachProgressCallbacks;
    private readonly Action _onOpenResultsPanelRequested;

    public DeployFromTemplateWorkspaceHost(
        Func<AppSettings> deploymentSettings,
        Func<IReadOnlyList<string>> availableSwitches,
        Func<bool> isTemplatesLoading,
        Func<IReadOnlyList<TemplateLibraryItem>> templateLibraryItems,
        Func<IReadOnlyList<VhdxCatalogItem>> loadCatalogItems,
        Func<bool, Task> ensureTemplateSwitchesAsync,
        Func<bool, Task> ensureTemplatesLibraryAsync,
        Func<string, Task<TemplateEditorDocument>> loadTemplateForEditorAsync,
        Func<MultiVmDeploymentContext, DeploymentPreflightMode, Task<DeploymentReadinessReport>> runReadinessAsync,
        Action refreshSharedUiState,
        Action applyRightPanelState,
        Func<MultiVmDeploymentContext, Task<DeploymentOutcomeSummary>> deployAllAsync,
        Action<MultiVmDeploymentContext, Action<string, string?>, Action<string, DeployStepStateUpdate>> attachProgressCallbacks,
        Action onOpenResultsPanelRequested)
    {
        _deploymentSettings = deploymentSettings;
        _availableSwitches = availableSwitches;
        _isTemplatesLoading = isTemplatesLoading;
        _templateLibraryItems = templateLibraryItems;
        _loadCatalogItems = loadCatalogItems;
        _ensureTemplateSwitchesAsync = ensureTemplateSwitchesAsync;
        _ensureTemplatesLibraryAsync = ensureTemplatesLibraryAsync;
        _loadTemplateForEditorAsync = loadTemplateForEditorAsync;
        _runReadinessAsync = runReadinessAsync;
        _refreshSharedUiState = refreshSharedUiState;
        _applyRightPanelState = applyRightPanelState;
        _attachProgressCallbacks = attachProgressCallbacks;
        _deployAllAsync = deployAllAsync;
        _onOpenResultsPanelRequested = onOpenResultsPanelRequested;
    }

    public AppSettings DeploymentSettings => _deploymentSettings();

    public IReadOnlyList<string> AvailableSwitches => _availableSwitches();

    public bool IsTemplatesLoading => _isTemplatesLoading();

    public IReadOnlyList<TemplateLibraryItem> TemplateLibraryItems => _templateLibraryItems();

    public IReadOnlyList<VhdxCatalogItem> LoadCatalogItems() => _loadCatalogItems();

    public Task EnsureTemplateSwitchesAsync(bool forceRefresh) => _ensureTemplateSwitchesAsync(forceRefresh);

    public Task EnsureTemplatesLibraryAsync(bool forceRefresh) => _ensureTemplatesLibraryAsync(forceRefresh);

    public Task<TemplateEditorDocument> LoadTemplateForEditorAsync(string filePath) => _loadTemplateForEditorAsync(filePath);

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
