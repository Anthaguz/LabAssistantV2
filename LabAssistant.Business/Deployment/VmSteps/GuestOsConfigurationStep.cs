using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using LabAssistant.Services.Logging;

namespace LabAssistant.Business.Deployment
{ 
    public abstract class GuestOsConfigurationStep : DeploymentStep
    {
        protected override async Task HandleAsync(VmDeploymentContext context)
        {
            if (!context.GuestServicesEnabled)
            {
                context.Logs.Add("Skipped Guest OS step – Guest Services not enabled.");
                DebugLogger.Log("✅ [GuestOsConfigurationStep] skipped due to Guest Services not being enabled.");
                return;
            }

            ExecuteGuestStep(context);
        }

        protected abstract void ExecuteGuestStep(VmDeploymentContext context);
    }
}