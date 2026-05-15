using LabAssistant.Models.Deployment;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;

namespace LabAssistant.Business.Deployment;

public class AddNicToVmStep : DeploymentStep
{
    private readonly ISessionResolver _resolver;
    private readonly Func<IPersistentPowerShellSession, IHyperVService> _hyperVFactory;
    protected override string StepKey => DeploymentStepKeys.AddNicToVm;
    protected override string StepLabel => "Add network adapter";

    public AddNicToVmStep(ISessionResolver resolver, Func<IPersistentPowerShellSession, IHyperVService> hyperVFactory)
    {
        _resolver = resolver;
        _hyperVFactory = hyperVFactory;
    }

    protected override async Task HandleAsync(VmDeploymentContext context)
    {
        context.LogCallback?.Invoke($"Adding network adapter to '{context.VmName}'...");
        DebugLogger.Log($"Adding network adapter to VM: {context.VmName}");

        if (context.PowerShellHandle == null)
        {
            context.LogCallback?.Invoke("❌ Missing PowerShell handle.");
            context.MarkFailure(DeploymentStepKeys.AddNicToVm, "Missing PowerShell handle.");
            return;
        }

        var session = _resolver.Resolve(context.PowerShellHandle);
        var hyperV = _hyperVFactory(session);
        var switches = context.VirtualSwitchNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (switches.Count == 0 && !string.IsNullOrWhiteSpace(context.VirtualSwitchName))
        {
            switches.Add(context.VirtualSwitchName.Trim());
        }

        if (switches.Count == 0)
        {
            const string skippedMessage = "Skipped network adapter attach because no switch assignments were provided.";
            context.LogCallback?.Invoke(skippedMessage);
            context.SetStepTerminalOverride(DeploymentStepKeys.AddNicToVm, DeployStepState.Skipped, skippedMessage);
            return;
        }

        bool success = await hyperV.AddVirtualSwitchesToVmAsync(
            context.VmName,
            switches);

        var switchSummary = switches.Count == 0
            ? context.VirtualSwitchName
            : string.Join(", ", switches);

        if (success)
        {
            context.LogCallback?.Invoke($"✅ Added {switchSummary} to '{context.VmName}'.");
            DebugLogger.Log($"Success: Added {switchSummary} to VM: {context.VmName}");
        }
        else
        {
            context.LogCallback?.Invoke($"❌ Failed to add {switchSummary} to '{context.VmName}'.");
            DebugLogger.Log($"Error: Failed to add {switchSummary} to VM: {context.VmName}");
            context.MarkFailure(DeploymentStepKeys.AddNicToVm, $"Failed to add {switchSummary} to '{context.VmName}'.");
        }
    }
}
