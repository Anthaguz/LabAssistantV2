using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using LabAssistant.Services.Logging;

namespace LabAssistant.Business.Deployment
{
    public class InstallSoftwareStep : GuestOsConfigurationStep
    {
        protected override void ExecuteGuestStep(VmDeploymentContext context)
        {
            if (!context.InstallSoftware) return;

            DebugLogger.Log("Installing software in the Guest OS...");
            DebugLogger.Log($"VM Name: {context.VmName}");
            context.Logs.Add("Software installed in Guest OS.");
            DebugLogger.Log("✅ [InstallSoftwareStep] simulated installation of software.");
        }

        protected override async Task HandleAsync(VmDeploymentContext context)
        {
            context.Logs.Add("✅ [CreateVhdStep] simulated creation.");
            await Task.CompletedTask;
        }


    }
}