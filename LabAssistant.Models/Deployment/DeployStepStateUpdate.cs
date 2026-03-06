namespace LabAssistant.Models.Deployment;

public enum DeployStepState
{
    Pending = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3,
    Skipped = 4
}

public sealed record DeployStepStateUpdate(
    string OperationId,
    Guid VmId,
    string VmName,
    string StepKey,
    string StepLabel,
    DeployStepState State,
    string? Message,
    DateTimeOffset TimestampUtc,
    long Sequence);
