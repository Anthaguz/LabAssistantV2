using LabAssistant.Business.Deployment;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Provides the From Template owner with non-shell access to shared Templates inventory/editor seams and deployment execution services.
/// </summary>
internal sealed class DeployFromTemplateWorkspaceHost : IDeployFromTemplateWorkspaceHost
{
    private readonly Func<bool> _isTemplatesLoading;
    private readonly Func<IReadOnlyList<TemplateLibraryItem>> _templateLibraryItems;
    private readonly Func<bool, Task> _ensureTemplatesLibraryAsync;
    private readonly Func<string, Task<TemplateEditorDocument>> _loadTemplateForEditorAsync;
    private readonly Func<MultiVmDeploymentContext, DeploymentPreflightMode, Task<DeploymentReadinessReport>> _runReadinessAsync;
    private readonly Action _refreshSharedUiState;
    private readonly Func<MultiVmDeploymentContext, Task<DeploymentOutcomeSummary>> _deployAllAsync;
    private readonly Action<MultiVmDeploymentContext, Action<string, string?>, Action<string, DeployStepStateUpdate>> _attachProgressCallbacks;

    public DeployFromTemplateWorkspaceHost(
        Func<bool> isTemplatesLoading,
        Func<IReadOnlyList<TemplateLibraryItem>> templateLibraryItems,
        Func<bool, Task> ensureTemplatesLibraryAsync,
        Func<string, Task<TemplateEditorDocument>> loadTemplateForEditorAsync,
        Func<MultiVmDeploymentContext, DeploymentPreflightMode, Task<DeploymentReadinessReport>> runReadinessAsync,
        Action refreshSharedUiState,
        Func<MultiVmDeploymentContext, Task<DeploymentOutcomeSummary>> deployAllAsync,
        Action<MultiVmDeploymentContext, Action<string, string?>, Action<string, DeployStepStateUpdate>> attachProgressCallbacks)
    {
        _isTemplatesLoading = isTemplatesLoading;
        _templateLibraryItems = templateLibraryItems;
        _ensureTemplatesLibraryAsync = ensureTemplatesLibraryAsync;
        _loadTemplateForEditorAsync = loadTemplateForEditorAsync;
        _runReadinessAsync = runReadinessAsync;
        _refreshSharedUiState = refreshSharedUiState;
        _attachProgressCallbacks = attachProgressCallbacks;
        _deployAllAsync = deployAllAsync;
    }

    public bool IsTemplatesLoading => _isTemplatesLoading();

    public IReadOnlyList<TemplateLibraryItem> TemplateLibraryItems => _templateLibraryItems();

    public Task EnsureTemplatesLibraryAsync(bool forceRefresh) => _ensureTemplatesLibraryAsync(forceRefresh);

    public Task<TemplateEditorDocument> LoadTemplateForEditorAsync(string filePath) => _loadTemplateForEditorAsync(filePath);

    public Task<DeploymentReadinessReport> RunReadinessAsync(MultiVmDeploymentContext context, DeploymentPreflightMode mode) => _runReadinessAsync(context, mode);

    public void RefreshSharedUiState() => _refreshSharedUiState();

    public Task<DeploymentOutcomeSummary> DeployAllAsync(MultiVmDeploymentContext context) => _deployAllAsync(context);

    public void AttachProgressCallbacks(
        MultiVmDeploymentContext context,
        Action<string, string?> onLogMessage,
        Action<string, DeployStepStateUpdate> onStepStateUpdated) => _attachProgressCallbacks(context, onLogMessage, onStepStateUpdated);
}
