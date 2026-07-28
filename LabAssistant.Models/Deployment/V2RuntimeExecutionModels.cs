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

    public V2BaseRemoteAccessOptions BaseRemoteAccessOptions { get; set; } = new();

    public MultiVmDeploymentContext? DeploymentContext { get; set; }

    public int GuestTransportMaxRetries { get; set; } = 90;

    public TimeSpan GuestTransportRetryDelay { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Number of initial guest-login attempts during which a credential rejection is tolerated as transient
    /// before the loop fails fast. A freshly cloned VM applies its answer-file password during specialize, so
    /// the very first PowerShell Direct hops can legitimately report "the credential is invalid" for a short
    /// window even though the password is correct. Past this many attempts a persistent rejection is treated as
    /// a genuine password mismatch and the loop stops instead of exhausting <see cref="GuestTransportMaxRetries"/>.
    /// Default 9 (~90s at the default 10s retry delay).
    /// </summary>
    public int GuestAuthGraceAttempts { get; set; } = 9;

    /// <summary>
    /// Number of consecutive successful "guest stable" probe hops required, before mutating guest steps run, to
    /// conclude the guest is past its reboot-prone specialize/OOBE window. A single successful transport hop only
    /// proves the guest is reachable right now, not that it is done rebooting, so the runtime drains the volatile
    /// window by requiring several clean probes in a row (no pending reboot, setup complete). Default 3.
    /// </summary>
    public int GuestStabilizationRequiredStableProbes { get; set; } = 3;

    /// <summary>
    /// Delay between guest-stabilization probe hops. Default 5s.
    /// </summary>
    public TimeSpan GuestStabilizationProbeInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Overall cap on the guest-stabilization gate. If the guest has not reported the required consecutive stable
    /// probes within this budget the runtime proceeds anyway (logging that it did so) rather than failing the
    /// deploy, because the bounded retry on the mutating steps still survives a stray late reboot. Default 3 min.
    /// </summary>
    public TimeSpan GuestStabilizationTimeout { get; set; } = TimeSpan.FromMinutes(3);
}

public sealed class V2BaseRemoteAccessOptions
{
    public bool EnableRemoteDesktop { get; set; } = true;

    public bool SetPrivateNetworkProfile { get; set; } = true;

    public bool DisableFirewall { get; set; } = true;

    public bool DisableRdpNla { get; set; } = true;
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
