using LabAssistant.Business.Deployment;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Models.Deploy;

namespace LabAssistant.WinUI.ViewModels.Deploy;

internal interface IDeployFromTemplateWorkspaceControllerHost
{
    AppSettings DeploymentSettings { get; }

    IReadOnlyList<string> AvailableSwitches { get; }

    TemplateEditorDocument? ActiveTemplateDocument { get; }

    IReadOnlyList<VhdxCatalogItem> LoadCatalogItems();

    Task EnsureReferenceDataAsync(bool forceRefresh);

    Task<DeploymentReadinessReport> RunReadinessAsync(MultiVmDeploymentContext context, DeploymentPreflightMode mode);

    void ReplaceCompatibilityIssues(IReadOnlyList<DeployCompatibilityIssue> issues);

    DeploymentReadinessReport? CurrentReadinessReport { get; set; }

    Task<DeploymentOutcomeSummary> DeployAllAsync(MultiVmDeploymentContext context);

    void AttachProgressCallbacks(
        MultiVmDeploymentContext context,
        Action<string, string?> onLogMessage,
        Action<string, DeployStepStateUpdate> onStepStateUpdated);

    void ApplyWorkspaceState();
}
