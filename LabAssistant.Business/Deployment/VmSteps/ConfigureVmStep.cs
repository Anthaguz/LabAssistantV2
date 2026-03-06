using LabAssistant.Models.Deployment;
using LabAssistant.Services.Logging;

namespace LabAssistant.Business.Deployment;

public class ConfigureVmStep : DeploymentStep
{
    protected override string StepKey => DeploymentStepKeys.ConfigureVm;
    protected override string StepLabel => "Configure VM";

    protected override async Task HandleAsync(VmDeploymentContext context)
    {
        //DebugLogger.Log("Applying VM configuration (RAM, CPU, NIC)...");
        //DebugLogger.Log($"VM Name: {context.VmName}");

        //context.Logs.Add("VM configuration applied.");
        //DebugLogger.Log("✅ [ConfigureVmStep] simulated VM configuration application.");
    }
}
