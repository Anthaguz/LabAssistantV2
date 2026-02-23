using LabAssistant.Models.Deployment;

namespace LabAssistant.Business.Deployment;

public interface IDeploymentOutcomeSummaryBuilder
{
    DeploymentOutcomeSummary Build(MultiVmDeploymentContext multiVmContext);
}
