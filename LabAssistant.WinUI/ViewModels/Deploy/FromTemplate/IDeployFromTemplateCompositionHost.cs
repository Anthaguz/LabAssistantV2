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

    void OnOpenResultsPanelRequested();
}
