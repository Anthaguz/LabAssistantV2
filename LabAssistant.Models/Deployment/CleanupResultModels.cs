using System.Collections.Generic;

namespace LabAssistant.Models.Deployment;

public enum CleanupStepName
{
    StopVm,
    RemoveVmRegistration,
    RemoveVmDirectory,
    RemoveDifferencingDisk
}

public enum CleanupStepStatus
{
    Succeeded,
    Skipped,
    Failed
}

public sealed class CleanupStepResult
{
    public CleanupStepName Step { get; init; }
    public CleanupStepStatus Status { get; init; }
    public string Target { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public sealed class CleanupResidual
{
    public string ResourceType { get; init; } = string.Empty;
    public string Identifier { get; init; } = string.Empty;
    public string SuggestedAction { get; init; } = string.Empty;
}

public sealed class VmCleanupResult
{
    public string VmName { get; init; } = string.Empty;
    public List<CleanupStepResult> StepResults { get; } = new();
    public List<CleanupResidual> Residuals { get; } = new();
    public bool HasResiduals => Residuals.Count > 0;
}
