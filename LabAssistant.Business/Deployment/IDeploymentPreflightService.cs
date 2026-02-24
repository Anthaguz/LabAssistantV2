using LabAssistant.Models.Deployment;

namespace LabAssistant.Business.Deployment;

public interface IDeploymentPreflightService
{
    Task<DeploymentReadinessReport> RunAsync(
        MultiVmDeploymentContext deploymentContext,
        DeploymentPreflightMode mode,
        CancellationToken cancellationToken = default);
}
