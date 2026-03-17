using LabAssistant.Models.Deployment;

namespace LabAssistant.WinUI.Models.Deploy;

internal sealed record DeployCompatibilityIssue(
    string VmName,
    bool IsBlocking,
    string Message,
    string Guidance);

internal sealed record DeployDiskResolution(
    string EffectiveBasePath,
    string? EffectiveId,
    string? EffectiveSignature,
    IReadOnlyList<DeployCompatibilityIssue> Issues);

internal sealed record DeploySwitchResolution(
    string EffectiveSwitch,
    IReadOnlyList<DeployCompatibilityIssue> Issues);

internal sealed record DeployContextBuildResult(
    MultiVmDeploymentContext MultiVmContext,
    IReadOnlyList<DeployCompatibilityIssue> CompatibilityIssues);
