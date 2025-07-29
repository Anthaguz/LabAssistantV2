using LabAssistant.Models.Deployment;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;

namespace LabAssistant.Business.Deployment;

public class AddNicToVmStep : DeploymentStep
{
    private readonly ISessionResolver _resolver;
    private readonly Func<IPersistentPowerShellSession, IHyperVService> _hyperVFactory;

    public AddNicToVmStep(ISessionResolver resolver, Func<IPersistentPowerShellSession, IHyperVService> hyperVFactory)
    {
        _resolver = resolver;
        _hyperVFactory = hyperVFactory;
    }

    protected override async Task HandleAsync(VmDeploymentContext context)
    {
        context.LogCallback?.Invoke($"Adding network adapter to '{context.VmName}'...");
        DebugLogger.Log($"Adding network adapter to VM: {context.VmName}");

        var session = _resolver.Resolve(context.PowerShellHandle);
        var hyperV = _hyperVFactory(session);

        bool success = await hyperV.AddVirtualSwitchToVmAsync(
            context.VmName,
            context.VirtualSwitchName);

        if (success)
        {
            context.LogCallback?.Invoke($"✅ Added {context.VirtualSwitchName} to '{context.VmName}'.");
            DebugLogger.Log($"Success: Added {context.VirtualSwitchName} to VM: {context.VmName}");
        }
        else
        {
            context.LogCallback?.Invoke($"❌ Failed to add {context.VirtualSwitchName} to '{context.VmName}'.");
            DebugLogger.Log($"Error: Failed to add {context.VirtualSwitchName} to VM: {context.VmName}");
        }
    }
}