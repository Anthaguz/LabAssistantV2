using LabAssistant.Models.Deployment;

namespace LabAssistant.Business.Deployment;

public interface IDeploymentPipelineBuilder
{
    DeploymentStep Build(VmDeploymentContext context);
}
