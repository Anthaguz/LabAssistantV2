using LabAssistant.Models.Deployment;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;
using System.IO;

namespace LabAssistant.Business.Deployment;

public class CreateVmFolderStep : DeploymentStep
{
    protected override async Task HandleAsync(VmDeploymentContext context)
    {
        context.LogCallback?.Invoke($"Creating folder for VM '{context.VmName}'...");
        DebugLogger.Log($"Creating folder for VM: {context.VmName}");

        if (!Directory.Exists(context.VmPath))
            Directory.CreateDirectory(context.VmPath);

        context.VmFolderCreated = true;
    }
}
