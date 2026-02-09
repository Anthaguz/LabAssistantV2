using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using LabAssistant.Services.Logging;

namespace LabAssistant.Business.Deployment
{
    public class ConfigureNetworkInformationStep : GuestOsConfigurationStep
    {
        protected override void ExecuteGuestStep(VmDeploymentContext context)
        {
            if (!context.InstallSoftware) return;

            DebugLogger.Log("Configuring network information in the Guest OS...");
            DebugLogger.Log($"VM Name: {context.VmName}");
            context.Logs.Add("Network information configured in Guest OS.");
            DebugLogger.Log("✅ [ConfigureNetworkInformationStep] simulated configuration of network information.");
        }
    }
}
