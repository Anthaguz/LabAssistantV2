using LabAssistant.Models.Deployment;

namespace LabAssistant.Business.Deployment;

public interface IDeploymentPreflightCheck
{
    string Key { get; }
    int Order { get; }

    bool SupportsMode(DeploymentPreflightMode mode);

    Task<IReadOnlyList<DeploymentReadinessCheckResult>> ExecuteAsync(
        MultiVmDeploymentContext deploymentContext,
        DeploymentPreflightMode mode,
        CancellationToken cancellationToken = default);
}
