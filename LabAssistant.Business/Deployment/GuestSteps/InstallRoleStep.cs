using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using LabAssistant.Services.Logging;

namespace LabAssistant.Business.Deployment
{
    public class InstallRoleStep : GuestOsConfigurationStep
    {
        protected override void ExecuteGuestStep(VmDeploymentContext context)
        {
            if (!context.InstallSoftware) return;

            DebugLogger.Log("Installing roles in the Guest OS...");
            DebugLogger.Log($"VM Name: {context.VmName}");
            context.Logs.Add("Roles installed in Guest OS.");
            DebugLogger.Log("✅ [InstallRoleStep] simulated installation of roles.");
        }

        protected override async Task HandleAsync(VmDeploymentContext context)
        {
            context.Logs.Add("✅ [CreateVhdStep] simulated creation.");
            await Task.CompletedTask;
        }

    }
}