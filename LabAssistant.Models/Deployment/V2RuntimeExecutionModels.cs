using LabAssistant.Models.Configuration;
using LabAssistant.Models.Templates;

namespace LabAssistant.Models.Deployment;

public sealed class V2RuntimeExecutionRequest
{
    public LabTemplate Template { get; set; } = new();

    public V2PlanBuildResult Plan { get; set; } = new();

    public AppSettings Settings { get; set; } = new();

    public IReadOnlyDictionary<string, V2RuntimeCredential> CredentialSlotValues { get; set; } =
        new Dictionary<string, V2RuntimeCredential>(StringComparer.OrdinalIgnoreCase);

    public MultiVmDeploymentContext? DeploymentContext { get; set; }

    public int GuestTransportMaxRetries { get; set; } = 90;

    public TimeSpan GuestTransportRetryDelay { get; set; } = TimeSpan.FromSeconds(10);
}

public sealed class V2RuntimeExecutionResult
{
    public bool Success { get; init; }

    public MultiVmDeploymentContext DeploymentContext { get; init; } = new();

    public IReadOnlyList<string> ExecutedNodeIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> DeferredNodeIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> BlockingMessages { get; init; } = Array.Empty<string>();
}

public sealed class V2RuntimeCredential
{
    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}
