using LabAssistant.Business.Deployment;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Shell-facing bridge for the Quick Deploy controller.
/// Exposes only the shell-owned state, UI refresh, and deployment services that the controller
/// needs while keeping the workflow boundary out of <c>MainWindow</c>.
/// </summary>
internal interface IDeployOnTheFlyWorkspaceControllerHost
{
    AppSettings DeploymentSettings { get; }

    IReadOnlyList<string> AvailableSwitches { get; }

    IReadOnlyList<VhdxCatalogItem> LoadCatalogItems();

    bool TryApplyVmFields(bool showSuccessStatus, bool showValidationErrors = true);

    void SetActionStatus(string statusText);

    LabTemplate BuildTemplate();

    Task EnsureReferenceDataAsync(bool forceRefresh);

    Task<DeploymentReadinessReport> RunReadinessChecksAsync(MultiVmDeploymentContext context, DeploymentPreflightMode mode);

    /// <summary>
    /// Marshals workflow callback updates back onto the shell UI thread without routing the workflow ownership back through the shell.
    /// </summary>
    void EnqueueUiUpdate(Action updateAction);

    void UpdateUi();

    Task<DeploymentOutcomeSummary> DeployAllAsync(MultiVmDeploymentContext context);
}
