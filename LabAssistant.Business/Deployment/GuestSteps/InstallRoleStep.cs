using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using LabAssistant.Services.Logging;

namespace LabAssistant.Business.Deployment
{
    public class InstallRoleStep : GuestOsConfigurationStep
    {
        protected override string GuestStepKey => DeploymentStepKeys.InstallRole;
        protected override string GuestStepDisplayName => "Install Role";
        protected override bool IsSelected(VmDeploymentContext context) => context.InstallRole;

        protected override void ExecuteGuestStep(VmDeploymentContext context)
        {
            DebugLogger.Log("Installing roles in the Guest OS...");
            DebugLogger.Log($"VM Name: {context.VmName}");
            context.Logs.Add("Roles installed in Guest OS.");
            DebugLogger.Log("✅ [InstallRoleStep] simulated installation of roles.");
        }
    }
}
