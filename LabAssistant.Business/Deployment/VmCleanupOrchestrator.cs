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

        // Each step is isolated: a single step throwing (for example a Hyper-V query fault) must never abort
        // the remaining steps, or a partial failure would orphan VMs/disks/directories. Order is preserved and
        // best-effort continues on failure; unexpected throws are captured as residuals for manual remediation.
        await RunStepAsync(() => StopVmAsync(context, hyperVService, result), CleanupStepName.StopVm, context.VmName, result);
        await RunStepAsync(() => RemoveVmRegistrationAsync(context, hyperVService, result), CleanupStepName.RemoveVmRegistration, context.VmName, result);
        RunStep(() => RemoveVmDirectory(context, result), CleanupStepName.RemoveVmDirectory, context.VmPath, result);
        RunStep(() => RemoveDifferencingDisk(context, result), CleanupStepName.RemoveDifferencingDisk, context.VhdPath, result);

        return result;
    }

    private static async Task RunStepAsync(Func<Task> step, CleanupStepName stepName, string target, VmCleanupResult result)
    {
        try
        {
            await step();
        }
        catch (Exception ex)
        {
            RecordStepException(stepName, target, ex, result);
        }
    }

    private static void RunStep(Action step, CleanupStepName stepName, string target, VmCleanupResult result)
    {
        try
        {
            step();
        }
        catch (Exception ex)
        {
            RecordStepException(stepName, target, ex, result);
        }
    }

    private static void RecordStepException(CleanupStepName stepName, string target, Exception ex, VmCleanupResult result)
    {
        result.StepResults.Add(Failed(stepName, target, $"Cleanup step threw: {ex.Message}"));
        result.Residuals.Add(new CleanupResidual
        {
            ResourceType = stepName.ToString(),
            Identifier = target,
            SuggestedAction = $"Verify and clean up '{target}' manually; cleanup step {stepName} failed unexpectedly: {ex.Message}"
        });
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

        // RemoveVmAsync reports failure whenever Remove-VM emits any error, which includes the benign
        // "VM not found" case. That case is reachable because the provisioning path sets VmRegistered
        // eagerly (before CreateVmAsync) so cleanup still runs on a mid-registration failure. Re-check
        // existence here so a VM that was never actually registered is reported as Skipped rather than
        // surfacing a spurious "remove manually" residual for a VM that does not exist.
        if (!await hyperVService.VmExistsAsync(context.VmName))
        {
            result.StepResults.Add(Skipped(CleanupStepName.RemoveVmRegistration, context.VmName, "VM registration not found; remove skipped."));
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
