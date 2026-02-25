using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;

namespace LabAssistant.Business.Deployment;
public class CreateVhdStep : DeploymentStep
{
    private readonly ISessionResolver _resolver;
    private readonly Func<IPersistentPowerShellSession, IHyperVService> _hyperVFactory;

    public CreateVhdStep(ISessionResolver resolver, Func<IPersistentPowerShellSession, IHyperVService> hyperVFactory)
    {
        _resolver = resolver;
        _hyperVFactory = hyperVFactory;
    }
    protected override async Task HandleAsync(VmDeploymentContext context)
    {
        if (context.PowerShellHandle == null)
        {
            context.LogCallback?.Invoke("❌ Missing PowerShell handle.");
            context.MarkFailure(DeploymentStepKeys.CreateVhd, "Missing PowerShell handle.");
            return;
        }

        var session = _resolver.Resolve(context.PowerShellHandle);
        var hyperV = _hyperVFactory(session);
        var success = false;

        context.LogCallback?.Invoke($"Creating differencing VHD for {context.VmName}");
        DebugLogger.Log($"Creating differencing VHD for VM: {context.VmName}");
        success = await hyperV.CreateVhdDifferencingAsync(context.BaseVhdPath, context.VhdPath);
        if (success)
        {
            context.DifferencingDiskCreated = true;
            context.LogCallback?.Invoke($"✅ Created VHD for '{context.VmName}'.");
            DebugLogger.Log($"Success: Created VHD for VM: {context.VmName}");
        }
        else
        {
            var userMessage = $"Failed to create differencing disk for '{context.VmName}'. Base VHDX may be invalid or unreadable: {context.BaseVhdPath}";
            context.LogCallback?.Invoke($"❌ {userMessage}");
            DebugLogger.Log($"Error: Failed to create VHD for VM: {context.VmName}");
            context.MarkFailure(
                DeploymentStepKeys.CreateVhd,
                userMessage,
                new Dictionary<string, object?>
                {
                    ["parentVhdPath"] = context.BaseVhdPath,
                    ["targetVhdPath"] = context.VhdPath
                });
        }
    }
}
