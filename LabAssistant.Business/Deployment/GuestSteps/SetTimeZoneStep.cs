using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using LabAssistant.Services.Logging;

namespace LabAssistant.Business.Deployment
{
    public class SetTimeZoneStep : GuestOsConfigurationStep
    {
        protected override void ExecuteGuestStep(VmDeploymentContext context)
        {
            if (!context.ConfigureTimeZone) return;

            DebugLogger.Log("Setting time zone inside the Guest OS...");
            DebugLogger.Log($"VM Name: {context.VmName}");
            context.Logs.Add("Time zone configured in Guest OS.");
            DebugLogger.Log("✅ [SetTimeZoneStep] simulated time zone configuration.");
        }
        protected override async Task HandleAsync(VmDeploymentContext context)
        {
            context.Logs.Add("✅ [CreateVhdStep] simulated creation.");
            await Task.CompletedTask;
        }
    }
}