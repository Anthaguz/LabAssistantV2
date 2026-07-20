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
internal interface IDeployQuickDeployWorkspaceControllerHost
{
    AppSettings DeploymentSettings { get; }

    IReadOnlyList<string> AvailableSwitches { get; }

    IReadOnlyList<VhdxCatalogItem> LoadCatalogItems();

    bool TryApplyVmFields(bool showSuccessStatus, bool showValidationErrors = true);

    void SetActionStatus(string statusText);

    LabTemplate BuildTemplate();

    /// <summary>
    /// Builds the V2-engine variant of the current Quick Deploy template used for planning and execution. It carries
    /// the same VM basics as <see cref="BuildTemplate"/> but is stamped as a Standalone V2 unified-planning template
    /// so the graph planner emits provisioning-only work. The classic shape stays for the engine-agnostic readiness
    /// preflight, which the CoR-shaped context builder rejects for V2 templates.
    /// </summary>
    LabTemplate BuildV2Template();

    Task EnsureReferenceDataAsync(bool forceRefresh);

    Task<DeploymentReadinessReport> RunReadinessChecksAsync(MultiVmDeploymentContext context, DeploymentPreflightMode mode);

    /// <summary>
    /// Marshals workflow callback updates onto the UI thread through the narrow shell bridge retained by the Quick Deploy-local owner.
    /// </summary>
    void EnqueueUiUpdate(Action updateAction);

    void UpdateUi();

    /// <summary>
    /// Builds the V2 deployment plan for the current Quick Deploy template. Quick Deploy authors bare standalone
    /// VMs, so the plan is provisioning-only (no guest work, no credential slots), but it runs through the same
    /// unified V2 planner the From-Template lane uses.
    /// </summary>
    Task<V2PlanBuildResult> BuildV2PlanAsync(LabTemplate template);

    /// <summary>
    /// Executes the built V2 plan on the graph runtime for Quick Deploy. The supplied deployment context is the
    /// cancellation and per-VM callback anchor; the runtime rebuilds its per-VM contexts under it during execution.
    /// </summary>
    Task<V2RuntimeExecutionResult> ExecuteV2DeployAsync(
        LabTemplate template,
        V2PlanBuildResult plan,
        MultiVmDeploymentContext context);
}
