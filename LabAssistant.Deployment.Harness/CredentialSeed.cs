namespace LabAssistant.Deployment.Harness;

/// <summary>A credential slot to seed into an isolated environment before planning or deploying a scenario.</summary>
public sealed class CredentialSeed
{
    /// <summary>The slot key templates and the planner reference (for example a domain-admin slot).</summary>
    public required string SlotKey { get; init; }

    /// <summary>The username stored in the slot.</summary>
    public required string Username { get; init; }

    /// <summary>The password stored in the slot (protected at rest by the underlying store).</summary>
    public required string Password { get; init; }
}
