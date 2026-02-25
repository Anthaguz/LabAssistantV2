using LabAssistant.Models.Deployment;

namespace LabAssistant.Business.Deployment;

public interface IDeploymentCoordinator
{
    Task DeployAllAsync(MultiVmDeploymentContext multiContext);
}
