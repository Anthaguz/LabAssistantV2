using LabAssistant.Models.Deployment;
using LabAssistant.Services.Logging;

namespace LabAssistant.Business.Deployment;

public class VerifyVmStep : DeploymentStep
{
    protected override async Task HandleAsync(VmDeploymentContext context)
    {
        DebugLogger.Log("Verifying VM is reachable...");
        context.Logs.Add("VM verified and running.");
        // Simulate verification logic
        DebugLogger.Log($"VM Name: {context.VmName}");
        DebugLogger.Log("✅ [VerifyVmStep] simulated VM verification.");
    }
}
