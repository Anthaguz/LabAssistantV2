namespace LabAssistant.Models.Deployment;

public enum DeploymentReadinessStatus
{
    Pass,
    Warn,
    Fail
}

public enum DeploymentReadinessCategory
{
    Environment,
    TemplateConfig,
    VhdxBaseDisk,
    NetworkSwitch,
    DestinationPathStorage
}

public enum DeploymentPreflightMode
{
    Quick,
    Full
}

public sealed class DeploymentReadinessCheckResult
{
    public DeploymentReadinessStatus Status { get; init; }
    public DeploymentReadinessCategory Category { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string ActionableGuidance { get; init; } = string.Empty;
    public IReadOnlyList<string> AffectedVmNames { get; init; } = [];
    public string? ResourcePath { get; init; }
    public string? ResourceName { get; init; }
}

public sealed class DeploymentReadinessReport
{
    public DeploymentPreflightMode Mode { get; init; }
    public IReadOnlyList<DeploymentReadinessCheckResult> Results { get; init; } = [];

    public bool HasBlockingFailures => Results.Any(r => r.Status == DeploymentReadinessStatus.Fail);
    public bool HasWarnings => Results.Any(r => r.Status == DeploymentReadinessStatus.Warn);
    public bool CanDeploy => !HasBlockingFailures;
    public bool IsAuthoritative => Mode == DeploymentPreflightMode.Full;
}
