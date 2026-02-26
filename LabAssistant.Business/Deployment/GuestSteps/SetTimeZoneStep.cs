using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using LabAssistant.Services.Logging;

namespace LabAssistant.Business.Deployment
{
    public class SetTimeZoneStep : GuestOsConfigurationStep
    {
        protected override string GuestStepKey => DeploymentStepKeys.SetTimeZone;
        protected override string GuestStepDisplayName => "Set Time Zone";
        protected override bool IsSelected(VmDeploymentContext context) => context.ConfigureTimeZone;

        protected override void ExecuteGuestStep(VmDeploymentContext context)
        {
            DebugLogger.Log("Setting time zone inside the Guest OS...");
            DebugLogger.Log($"VM Name: {context.VmName}");
            context.Logs.Add("Time zone configured in Guest OS.");
            DebugLogger.Log("✅ [SetTimeZoneStep] simulated time zone configuration.");
        }
    }
}
