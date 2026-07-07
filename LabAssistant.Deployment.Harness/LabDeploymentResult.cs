using LabAssistant.Models.Templates;

namespace LabAssistant.Deployment.Harness;

/// <summary>Outcome of a harness deploy: the built plan, runtime success, executed nodes, and any messages.</summary>
public sealed class LabDeploymentResult
{
    /// <summary>True when the plan built cleanly (no blocking issues, no unresolved requirements).</summary>
    public required bool PlanDeployable { get; init; }

    /// <summary>The plan that was built for the scenario.</summary>
    public required V2PlanBuildResult Plan { get; init; }

    /// <summary>True when the runtime was actually executed (false when the plan was not deployable).</summary>
    public bool Executed { get; init; }

    /// <summary>True when the runtime reported overall success.</summary>
    public bool RuntimeSuccess { get; init; }

    /// <summary>Node ids the runtime executed, in completion order.</summary>
    public IReadOnlyList<string> ExecutedNodeIds { get; init; } = Array.Empty<string>();

    /// <summary>Blocking messages surfaced by the runtime, if any.</summary>
    public IReadOnlyList<string> BlockingMessages { get; init; } = Array.Empty<string>();

    /// <summary>True when a post-deploy PowerShell Direct guest probe succeeded (only attempted when requested).</summary>
    public bool GuestProbeSucceeded { get; init; }

    /// <summary>Raw guest probe output when the probe ran and succeeded.</summary>
    public string? GuestProbeOutput { get; init; }
}
