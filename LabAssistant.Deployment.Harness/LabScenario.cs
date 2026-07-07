using LabAssistant.Models.Templates;

namespace LabAssistant.Deployment.Harness;

/// <summary>
/// A named lab topology: the V2 template to deploy, any extra credential slots it needs beyond the image
/// bootstrap credential, and the VM names it is expected to create (used for teardown to guarantee no orphans).
/// </summary>
public sealed class LabScenario
{
    /// <summary>Short scenario name for logs and test display.</summary>
    public required string Name { get; init; }

    /// <summary>The V2 template driven through the planner and runtime.</summary>
    public required LabTemplate Template { get; init; }

    /// <summary>Credential slots beyond the image bootstrap slot that this scenario requires.</summary>
    public IReadOnlyList<CredentialSeed> ExtraCredentials { get; init; } = Array.Empty<CredentialSeed>();

    /// <summary>VM names the scenario is expected to create, torn down after the run.</summary>
    public IReadOnlyList<string> ExpectedVmNames { get; init; } = Array.Empty<string>();
}
