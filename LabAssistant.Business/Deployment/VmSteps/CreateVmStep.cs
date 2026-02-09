using LabAssistant.Models.Deployment;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;

namespace LabAssistant.Business.Deployment;

public class CreateVmStep : DeploymentStep
{
    private readonly ISessionResolver _resolver;
    private readonly Func<IPersistentPowerShellSession, IHyperVService> _hyperVFactory;

    public CreateVmStep(ISessionResolver resolver, Func<IPersistentPowerShellSession, IHyperVService> hyperVFactory)
    {
        _resolver = resolver;
        _hyperVFactory = hyperVFactory;
    }

    protected override async Task HandleAsync(VmDeploymentContext context)
    {
        context.LogCallback?.Invoke($"Creating VM '{context.VmName}'...");
        DebugLogger.Log($"Creating VM: {context.VmName}");

        if (context.PowerShellHandle == null)
        {
            context.LogCallback?.Invoke("❌ Missing PowerShell handle.");
            context.MarkFailure(DeploymentStepKeys.CreateVm, "Missing PowerShell handle.");
            return;
        }

        var session = _resolver.Resolve(context.PowerShellHandle);
        var hyperV = _hyperVFactory(session);

        bool success = await hyperV.CreateVmAsync(
            context.VmName,
            context.VmPath,
            context.VhdPath,
            context.MemoryMb,
            context.CpuCount);

        if (success)
        {
            context.LogCallback?.Invoke($"✅ Created VM '{context.VmName}'.");
            DebugLogger.Log($"Success: Created VM: {context.VmName}");
        }
        else
        {
            context.LogCallback?.Invoke($"❌ Failed to create VM '{context.VmName}'.");
            DebugLogger.Log($"Error: Failed to create VM: {context.VmName}");
            context.MarkFailure(DeploymentStepKeys.CreateVm, $"Failed to create VM '{context.VmName}'.");
        }
    }
}
