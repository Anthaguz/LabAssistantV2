using System;
using System.Collections.Generic;

namespace LabAssistant.Models.Deployment;

public enum VmDeploymentOutcomeStatus
{
    Succeeded,
    Failed,
    Cancelled
}

public enum VmCleanupOutcomeStatus
{
    NotNeeded,
    Succeeded,
    Residuals
}

public sealed class VmCleanupOutcomeSummary
{
    public bool CleanupRan { get; init; }
    public VmCleanupOutcomeStatus Status { get; init; }
    public int StepCount { get; init; }
    public int ResidualCount { get; init; }
}

public sealed class VmDeploymentOutcomeSummary
{
    public Guid VmId { get; init; }
    public string VmName { get; init; } = string.Empty;
    public VmDeploymentOutcomeStatus Status { get; init; }
    public string? Reason { get; init; }
    public string? FailureStepKey { get; init; }
    public VmCleanupOutcomeSummary Cleanup { get; init; } = new();
    public IReadOnlyList<CleanupResidual> Residuals { get; init; } = [];
}

public sealed class DeploymentResidualSummaryItem
{
    public string VmName { get; init; } = string.Empty;
    public string ResourceType { get; init; } = string.Empty;
    public string Identifier { get; init; } = string.Empty;
    public string SuggestedAction { get; init; } = string.Empty;
}

public sealed class DeploymentOutcomeSummary
{
    public DeploymentOperationState OperationState { get; init; }
    public int TotalVmCount { get; init; }
    public int SucceededVmCount { get; init; }
    public int FailedVmCount { get; init; }
    public int CancelledVmCount { get; init; }
    public int CleanupVmCount { get; init; }
    public int ResidualVmCount { get; init; }
    public IReadOnlyList<VmDeploymentOutcomeSummary> VmOutcomes { get; init; } = [];
    public IReadOnlyList<DeploymentResidualSummaryItem> Residuals { get; init; } = [];
}
