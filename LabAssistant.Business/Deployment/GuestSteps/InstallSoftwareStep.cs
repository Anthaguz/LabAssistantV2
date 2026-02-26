using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using LabAssistant.Services.Logging;

namespace LabAssistant.Business.Deployment
{
    public class InstallSoftwareStep : GuestOsConfigurationStep
    {
        protected override string GuestStepKey => DeploymentStepKeys.InstallSoftware;
        protected override string GuestStepDisplayName => "Install Software";
        protected override bool IsSelected(VmDeploymentContext context) => context.InstallSoftware;

        protected override void ExecuteGuestStep(VmDeploymentContext context)
        {
            DebugLogger.Log("Installing software in the Guest OS...");
            DebugLogger.Log($"VM Name: {context.VmName}");
            context.Logs.Add("Software installed in Guest OS.");
            DebugLogger.Log("✅ [InstallSoftwareStep] simulated installation of software.");
        }
    }
}
