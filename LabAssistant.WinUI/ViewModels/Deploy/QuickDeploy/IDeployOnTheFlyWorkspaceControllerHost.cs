using LabAssistant.Business.Deployment;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Temporary residual bridge for controller dependencies that still cross into shell/shared Deploy ownership during the cleanup chain.
/// It exposes only the current shell-owned state, UI refresh, and deployment services the controller still needs, and should not be read as approval for capability-specific controller contracts to scale permanently through the shell.
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
    /// Marshals workflow callback updates onto the shell UI thread.
    /// This is a true shell boundary and remains acceptable even while other capability-specific bridge members are treated as temporary residual integration.
    /// </summary>
    void EnqueueUiUpdate(Action updateAction);

    void UpdateUi();

    Task<DeploymentOutcomeSummary> DeployAllAsync(MultiVmDeploymentContext context);
}
