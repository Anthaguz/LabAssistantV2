using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using LabAssistant.Services.Logging;

namespace LabAssistant.Business.Deployment;
public class CheckHyperVStep : DeploymentStep
{
    protected override async Task HandleAsync(VmDeploymentContext context)
    {
        // Simulate check
        bool isEnabled = true; // Replace with actual service call
        if (!isEnabled)
        {
            context.MarkFailure(DeploymentStepKeys.CheckHyperV, "Hyper-V is not enabled.");
            //DebugLogger.Log("Hyper-V check failed.");
            return;
        }


        //DebugLogger.Log("Hyper-V is enabled and running
        //context.Logs.Add("Hyper-V check passed.");
    }
}
