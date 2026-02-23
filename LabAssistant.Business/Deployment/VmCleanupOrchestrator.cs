using LabAssistant.Models.Deployment;
using LabAssistant.Services.FileSystem;
using LabAssistant.Services.HyperV;

namespace LabAssistant.Business.Deployment;

public sealed class VmCleanupOrchestrator : IVmCleanupOrchestrator
{
    private readonly IDeploymentFileSystem _fileSystem;

    public VmCleanupOrchestrator(IDeploymentFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public async Task<VmCleanupResult> CleanupAsync(VmDeploymentContext context, IHyperVService hyperVService)
    {
        var result = new VmCleanupResult { VmName = context.VmName };

        await StopVmAsync(context, hyperVService, result);
        await RemoveVmRegistrationAsync(context, hyperVService, result);
        RemoveVmDirectory(context, result);
        RemoveDifferencingDisk(context, result);

        return result;
    }

    private static async Task StopVmAsync(VmDeploymentContext context, IHyperVService hyperVService, VmCleanupResult result)
    {
        var vmExists = context.VmRegistered || await hyperVService.VmExistsAsync(context.VmName);
        if (!vmExists)
        {
            result.StepResults.Add(Skipped(CleanupStepName.StopVm, context.VmName, "VM not registered; stop skipped."));
            return;
        }

        var isRunning = context.VmStarted || await hyperVService.IsVmRunningAsync(context.VmName);
        if (!isRunning)
        {
            result.StepResults.Add(Skipped(CleanupStepName.StopVm, context.VmName, "VM not running; stop skipped."));
            return;
        }

        if (await hyperVService.StopVmAsync(context.VmName))
        {
            result.StepResults.Add(Succeeded(CleanupStepName.StopVm, context.VmName, "VM stopped."));
            return;
        }

        result.StepResults.Add(Failed(CleanupStepName.StopVm, context.VmName, "Failed to stop VM."));
        result.Residuals.Add(new CleanupResidual
        {
            ResourceType = "vm",
            Identifier = context.VmName,
            SuggestedAction = $"Stop VM '{context.VmName}' manually in Hyper-V Manager or PowerShell."
        });
    }

    private static async Task RemoveVmRegistrationAsync(VmDeploymentContext context, IHyperVService hyperVService, VmCleanupResult result)
    {
        var vmExists = context.VmRegistered || await hyperVService.VmExistsAsync(context.VmName);
        if (!vmExists)
        {
            result.StepResults.Add(Skipped(CleanupStepName.RemoveVmRegistration, context.VmName, "VM registration not found; remove skipped."));
            return;
        }

        if (await hyperVService.RemoveVmAsync(context.VmName))
        {
            result.StepResults.Add(Succeeded(CleanupStepName.RemoveVmRegistration, context.VmName, "VM registration removed."));
            return;
        }

        result.StepResults.Add(Failed(CleanupStepName.RemoveVmRegistration, context.VmName, "Failed to remove VM registration."));
        result.Residuals.Add(new CleanupResidual
        {
            ResourceType = "vm-registration",
            Identifier = context.VmName,
            SuggestedAction = $"Remove VM '{context.VmName}' registration manually from Hyper-V."
        });
    }

    private void RemoveVmDirectory(VmDeploymentContext context, VmCleanupResult result)
    {
        if (!_fileSystem.DirectoryExists(context.VmPath))
        {
            result.StepResults.Add(Skipped(CleanupStepName.RemoveVmDirectory, context.VmPath, "VM directory not found; delete skipped."));
            return;
        }

        if (_fileSystem.DeleteDirectory(context.VmPath, out var error))
        {
            result.StepResults.Add(Succeeded(CleanupStepName.RemoveVmDirectory, context.VmPath, "VM directory removed."));
            return;
        }

        result.StepResults.Add(Failed(CleanupStepName.RemoveVmDirectory, context.VmPath, $"Failed to remove VM directory: {error}"));
        result.Residuals.Add(new CleanupResidual
        {
            ResourceType = "vm-directory",
            Identifier = context.VmPath,
            SuggestedAction = $"Delete VM directory manually: {context.VmPath}"
        });
    }

    private void RemoveDifferencingDisk(VmDeploymentContext context, VmCleanupResult result)
    {
        if (!_fileSystem.FileExists(context.VhdPath))
        {
            result.StepResults.Add(Skipped(CleanupStepName.RemoveDifferencingDisk, context.VhdPath, "Differencing disk not found; delete skipped."));
            return;
        }

        if (_fileSystem.DeleteFile(context.VhdPath, out var error))
        {
            result.StepResults.Add(Succeeded(CleanupStepName.RemoveDifferencingDisk, context.VhdPath, "Differencing disk removed."));
            return;
        }

        result.StepResults.Add(Failed(CleanupStepName.RemoveDifferencingDisk, context.VhdPath, $"Failed to remove differencing disk: {error}"));
        result.Residuals.Add(new CleanupResidual
        {
            ResourceType = "differencing-disk",
            Identifier = context.VhdPath,
            SuggestedAction = $"Delete differencing disk manually: {context.VhdPath}"
        });
    }

    private static CleanupStepResult Succeeded(CleanupStepName step, string target, string message) => new()
    {
        Step = step,
        Status = CleanupStepStatus.Succeeded,
        Target = target,
        Message = message
    };

    private static CleanupStepResult Skipped(CleanupStepName step, string target, string message) => new()
    {
        Step = step,
        Status = CleanupStepStatus.Skipped,
        Target = target,
        Message = message
    };

    private static CleanupStepResult Failed(CleanupStepName step, string target, string message) => new()
    {
        Step = step,
        Status = CleanupStepStatus.Failed,
        Target = target,
        Message = message
    };
}
