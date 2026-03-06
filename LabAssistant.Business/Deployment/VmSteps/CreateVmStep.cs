using LabAssistant.Models.Deployment;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;

namespace LabAssistant.Business.Deployment;

public class CreateVmStep : DeploymentStep
{
    private readonly ISessionResolver _resolver;
    private readonly Func<IPersistentPowerShellSession, IHyperVService> _hyperVFactory;
    protected override string StepKey => DeploymentStepKeys.CreateVm;
    protected override string StepLabel => "Create VM";

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
            context.VmRegistered = true;
            context.LogCallback?.Invoke($"✅ Created VM '{context.VmName}'.");
            DebugLogger.Log($"Success: Created VM: {context.VmName}");
        }
        else
        {
            var userMessage = $"Failed to create VM '{context.VmName}' at '{context.VmPath}' using disk '{context.VhdPath}'.";
            context.LogCallback?.Invoke($"❌ {userMessage}");
            DebugLogger.Log($"Error: Failed to create VM: {context.VmName}");
            var failureMetadata = (hyperV as IHyperVFailureDiagnosticsProvider)?.LastFailureMetadata;
            context.MarkFailure(
                DeploymentStepKeys.CreateVm,
                userMessage,
                MergeFailureMetadata(context, failureMetadata));
        }
    }

    private static IReadOnlyDictionary<string, object?> MergeFailureMetadata(
        VmDeploymentContext context,
        IReadOnlyDictionary<string, object?>? failureMetadata)
    {
        var payload = new Dictionary<string, object?>
        {
            ["vmPath"] = context.VmPath,
            ["targetVhdPath"] = context.VhdPath
        };

        if (failureMetadata != null)
        {
            foreach (var pair in failureMetadata)
            {
                payload[pair.Key] = pair.Value;
            }
        }

        return payload;
    }
}
