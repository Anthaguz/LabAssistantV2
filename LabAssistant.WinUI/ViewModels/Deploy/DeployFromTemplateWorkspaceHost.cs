using LabAssistant.Business.Deployment;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.WinUI.Models.Deploy;

namespace LabAssistant.WinUI.ViewModels.Deploy;

internal interface IDeployFromTemplateCompositionHost
{
    AppSettings DeploymentSettings { get; }

    IReadOnlyList<string> AvailableSwitches { get; }

    TemplateEditorDocument? ActiveTemplateDocument { get; }

    IReadOnlyList<VhdxCatalogItem> LoadCatalogItems();

    Task EnsureTemplateSwitchesAsync(bool forceRefresh);

    Task<DeploymentReadinessReport> RunReadinessAsync(MultiVmDeploymentContext context, DeploymentPreflightMode mode);

    void ReplaceCompatibilityIssues(IReadOnlyList<DeployCompatibilityIssue> issues);

    DeploymentReadinessReport? CurrentReadinessReport { get; set; }

    Task<DeploymentOutcomeSummary> DeployAllAsync(MultiVmDeploymentContext context);

    void AttachProgressCallbacks(
        MultiVmDeploymentContext context,
        Action<string, string?> onLogMessage,
        Action<string, DeployStepStateUpdate> onStepStateUpdated);
}

internal sealed class DeployFromTemplateWorkspaceHost : IDeployFromTemplateCompositionHost
{
    private readonly Func<AppSettings> _deploymentSettings;
    private readonly Func<IReadOnlyList<string>> _availableSwitches;
    private readonly Func<TemplateEditorDocument?> _activeTemplateDocument;
    private readonly Func<IReadOnlyList<VhdxCatalogItem>> _loadCatalogItems;
    private readonly Func<bool, Task> _ensureTemplateSwitchesAsync;
    private readonly Func<MultiVmDeploymentContext, DeploymentPreflightMode, Task<DeploymentReadinessReport>> _runReadinessAsync;
    private readonly Action<IReadOnlyList<DeployCompatibilityIssue>> _replaceCompatibilityIssues;
    private readonly Func<DeploymentReadinessReport?> _getCurrentReadinessReport;
    private readonly Action<DeploymentReadinessReport?> _setCurrentReadinessReport;
    private readonly Func<MultiVmDeploymentContext, Task<DeploymentOutcomeSummary>> _deployAllAsync;
    private readonly Action<MultiVmDeploymentContext, Action<string, string?>, Action<string, DeployStepStateUpdate>> _attachProgressCallbacks;

    public DeployFromTemplateWorkspaceHost(
        Func<AppSettings> deploymentSettings,
        Func<IReadOnlyList<string>> availableSwitches,
        Func<TemplateEditorDocument?> activeTemplateDocument,
        Func<IReadOnlyList<VhdxCatalogItem>> loadCatalogItems,
        Func<bool, Task> ensureTemplateSwitchesAsync,
        Func<MultiVmDeploymentContext, DeploymentPreflightMode, Task<DeploymentReadinessReport>> runReadinessAsync,
        Action<IReadOnlyList<DeployCompatibilityIssue>> replaceCompatibilityIssues,
        Func<DeploymentReadinessReport?> getCurrentReadinessReport,
        Action<DeploymentReadinessReport?> setCurrentReadinessReport,
        Func<MultiVmDeploymentContext, Task<DeploymentOutcomeSummary>> deployAllAsync,
        Action<MultiVmDeploymentContext, Action<string, string?>, Action<string, DeployStepStateUpdate>> attachProgressCallbacks)
    {
        _deploymentSettings = deploymentSettings;
        _availableSwitches = availableSwitches;
        _activeTemplateDocument = activeTemplateDocument;
        _loadCatalogItems = loadCatalogItems;
        _ensureTemplateSwitchesAsync = ensureTemplateSwitchesAsync;
        _runReadinessAsync = runReadinessAsync;
        _replaceCompatibilityIssues = replaceCompatibilityIssues;
        _getCurrentReadinessReport = getCurrentReadinessReport;
        _setCurrentReadinessReport = setCurrentReadinessReport;
        _attachProgressCallbacks = attachProgressCallbacks;
        _deployAllAsync = deployAllAsync;
    }

    public AppSettings DeploymentSettings => _deploymentSettings();

    public IReadOnlyList<string> AvailableSwitches => _availableSwitches();

    public TemplateEditorDocument? ActiveTemplateDocument => _activeTemplateDocument();

    public IReadOnlyList<VhdxCatalogItem> LoadCatalogItems() => _loadCatalogItems();

    public Task EnsureTemplateSwitchesAsync(bool forceRefresh) => _ensureTemplateSwitchesAsync(forceRefresh);

    public Task<DeploymentReadinessReport> RunReadinessAsync(MultiVmDeploymentContext context, DeploymentPreflightMode mode) => _runReadinessAsync(context, mode);

    public void ReplaceCompatibilityIssues(IReadOnlyList<DeployCompatibilityIssue> issues) => _replaceCompatibilityIssues(issues);

    public DeploymentReadinessReport? CurrentReadinessReport
    {
        get => _getCurrentReadinessReport();
        set => _setCurrentReadinessReport(value);
    }

    public Task<DeploymentOutcomeSummary> DeployAllAsync(MultiVmDeploymentContext context) => _deployAllAsync(context);

    public void AttachProgressCallbacks(
        MultiVmDeploymentContext context,
        Action<string, string?> onLogMessage,
        Action<string, DeployStepStateUpdate> onStepStateUpdated) => _attachProgressCallbacks(context, onLogMessage, onStepStateUpdated);
}
