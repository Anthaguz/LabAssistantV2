using LabAssistant.Models.Deployment;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;
using LabAssistant.Services.Diagnostics;
using System.IO;

namespace LabAssistant.Business.Deployment;

public class CreateVmFolderStep : DeploymentStep
{
    protected override string StepKey => DeploymentStepKeys.CreateVmFolder;
    protected override string StepLabel => "Create VM folder";

    protected override async Task HandleAsync(VmDeploymentContext context)
    {
        context.LogCallback?.Invoke($"Creating folder for VM '{context.VmName}'...");
        DebugLogger.Log($"Creating folder for VM: {context.VmName}");

        try
        {
            if (!Directory.Exists(context.VmPath))
            {
                Directory.CreateDirectory(context.VmPath);
            }
        }
        catch (Exception ex)
        {
            var userMessage = $"Failed to create VM folder for '{context.VmName}': {context.VmPath}";
            context.LogCallback?.Invoke($"❌ {userMessage}");
            DebugLogger.Log($"Error creating VM folder '{context.VmPath}' for '{context.VmName}': {ex}");
            context.MarkFailure(
                DeploymentStepKeys.CreateVmFolder,
                userMessage,
                RuntimeErrorMetadataNormalizer.Merge(
                    new Dictionary<string, object?> { ["vmPath"] = context.VmPath },
                    RuntimeErrorMetadataNormalizer.FromException(ex)));
            return;
        }

        context.VmFolderCreated = true;
    }
}
