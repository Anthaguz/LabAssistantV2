using LabAssistant.Models.Deployment;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;

namespace LabAssistant.Business.Deployment;

public class DisableVmCheckpointsStep : DeploymentStep
{
    private readonly ISessionResolver _resolver;
    private readonly Func<IPersistentPowerShellSession, IHyperVService> _hyperVFactory;

    public DisableVmCheckpointsStep(ISessionResolver resolver, Func<IPersistentPowerShellSession, IHyperVService> hyperVFactory)
    {
        _resolver = resolver;
        _hyperVFactory = hyperVFactory;
    }
    protected override async Task HandleAsync(VmDeploymentContext context)
    {
        context.LogCallback?.Invoke($"Disabling VM checkpoints for VM '{context.VmName}'...");
        DebugLogger.Log($"Disabling VM: {context.VmName}");

        if (context.PowerShellHandle == null)
        {
            context.LogCallback?.Invoke("❌ Missing PowerShell handle.");
            context.MarkFailure(DeploymentStepKeys.DisableVmCheckpoints, "Missing PowerShell handle.");
            return;
        }

        var session = _resolver.Resolve(context.PowerShellHandle);
        var hyperV = _hyperVFactory(session);

        bool success = await hyperV.DisableVmCheckpointsAsync(context.VmName);

        if (success)
        {
            context.LogCallback?.Invoke($"✅ Disabled VM checkpoints for VM '{context.VmName}'.");
            DebugLogger.Log($"Success: Disabled VM checkpoints for VM: {context.VmName}");
        }
        else
        {
            context.LogCallback?.Invoke($"❌ Failed to disable VM checkpoints for VM '{context.VmName}'.");
            DebugLogger.Log($"Error: Failed to disable VM checkpoints for VM: {context.VmName}");
            context.MarkFailure(DeploymentStepKeys.DisableVmCheckpoints, $"Failed to disable VM checkpoints for VM '{context.VmName}'.");
        }
    }
}
