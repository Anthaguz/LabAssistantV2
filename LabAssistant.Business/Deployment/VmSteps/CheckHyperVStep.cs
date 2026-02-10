using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using LabAssistant.Services;
using LabAssistant.Services.Logging;

namespace LabAssistant.Business.Deployment;
public class CheckHyperVStep : DeploymentStep
{
    protected override async Task HandleAsync(VmDeploymentContext context)
    {
        if (!HyperVHelper.IsHyperVEnabled())
        {
            context.MarkFailure(DeploymentStepKeys.CheckHyperV, "Hyper-V is not enabled.");
            return;
        }

        await Task.CompletedTask;
    }
}
