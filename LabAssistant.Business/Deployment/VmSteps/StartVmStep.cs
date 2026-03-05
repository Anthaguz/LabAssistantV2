using LabAssistant.Models.Deployment;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;

namespace LabAssistant.Business.Deployment;

public class StartVmStep : DeploymentStep
{
    private readonly ISessionResolver _resolver;
    private readonly Func<IPersistentPowerShellSession, IHyperVService> _hyperVFactory;
    protected override string StepKey => DeploymentStepKeys.StartVm;
    protected override string StepLabel => "Start VM";

    public StartVmStep(ISessionResolver resolver, Func<IPersistentPowerShellSession, IHyperVService> hyperVFactory)
    {
        _resolver = resolver;
        _hyperVFactory = hyperVFactory;
    }
    protected override async Task HandleAsync(VmDeploymentContext context)
    {
        context.LogCallback?.Invoke($"Starting VM '{context.VmName}'...");
        DebugLogger.Log($"Starting VM: {context.VmName}");

        if (context.PowerShellHandle == null)
        {
            context.LogCallback?.Invoke("❌ Missing PowerShell handle.");
            context.MarkFailure(DeploymentStepKeys.StartVm, "Missing PowerShell handle.");
            return;
        }

        var session = _resolver.Resolve(context.PowerShellHandle);
        var hyperV = _hyperVFactory(session);

        bool success = await hyperV.StartVmAsync(context.VmName);

        if (success)
        {
            context.VmStarted = true;
            context.LogCallback?.Invoke($"✅ Started VM '{context.VmName}'.");
            DebugLogger.Log($"Success: Started VM: {context.VmName}");
        }
        else
        {
            context.LogCallback?.Invoke($"❌ Failed to start VM '{context.VmName}'.");
            DebugLogger.Log($"Error: Failed to start VM: {context.VmName}");
            context.MarkFailure(DeploymentStepKeys.StartVm, $"Failed to start VM '{context.VmName}'.");
        }
    }
}
