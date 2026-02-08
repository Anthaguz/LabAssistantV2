using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;

namespace LabAssistant.Business.Deployment
{
    public class EnableGuestServicesStep : DeploymentStep
    {
        private readonly ISessionResolver _resolver;
        private readonly Func<IPersistentPowerShellSession, IHyperVService> _hyperVFactory;

        public EnableGuestServicesStep(ISessionResolver resolver, Func<IPersistentPowerShellSession, IHyperVService> hyperVFactory)
        {
            _resolver = resolver;
            _hyperVFactory = hyperVFactory;
        }
        protected override async Task HandleAsync(VmDeploymentContext context)
        {
            context.LogCallback?.Invoke($"Enabling VM guest services on VM '{context.VmName}'...");
            DebugLogger.Log($"Enabling VM guest services on VM: {context.VmName}");

            if (context.PowerShellHandle == null)
            {
                context.LogCallback?.Invoke("❌ Missing PowerShell handle.");
                context.IsSuccess = false;
                return;
            }

            var session = _resolver.Resolve(context.PowerShellHandle);
            var hyperV = _hyperVFactory(session);

            bool success = await hyperV.EnableGuestServicesAsync(context.VmName);

            if (success)
            {
                context.LogCallback?.Invoke($"✅ Enabled VM guest services on VM '{context.VmName}'.");
                DebugLogger.Log($"Success: Enabled VM guest services on VM: {context.VmName}");
            }
            else
            {
                context.LogCallback?.Invoke($"❌ Failed to enable VM guest services on VM '{context.VmName}'.");
                DebugLogger.Log($"Error: Failed to enable VM guest services on VM: {context.VmName}");
            }
        }
    }
}
