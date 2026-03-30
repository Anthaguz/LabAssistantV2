using LabAssistant.Business.Deployment;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Quick Deploy-local controller host contract.
/// Implementations own the lane-local workflow boundary and may forward only the narrow shared Deploy or shell callbacks that still remain outside the local owner.
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
    /// Marshals workflow callback updates onto the UI thread through the narrow shell bridge retained by the Quick Deploy-local owner.
    /// </summary>
    void EnqueueUiUpdate(Action updateAction);

    void UpdateUi();

    Task<DeploymentOutcomeSummary> DeployAllAsync(MultiVmDeploymentContext context);
}
