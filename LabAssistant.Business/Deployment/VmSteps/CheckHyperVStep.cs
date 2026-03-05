using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using LabAssistant.Services;
using LabAssistant.Services.Logging;

namespace LabAssistant.Business.Deployment;
public class CheckHyperVStep : DeploymentStep
{
    protected override string StepKey => DeploymentStepKeys.CheckHyperV;
    protected override string StepLabel => "Check Hyper-V";

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
