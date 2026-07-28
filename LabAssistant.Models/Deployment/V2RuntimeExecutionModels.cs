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
    /// Wall-clock window during which an unbroken run of guest-login credential rejections is tolerated as the
    /// transient specialize window before the loop offers an interactive re-prompt and otherwise fails fast. A
    /// freshly cloned VM applies its answer-file password during specialize, so the first PowerShell Direct hops
    /// can legitimately report "the credential is invalid" even though the password is correct; that rejection is
    /// indistinguishable in content from a genuinely wrong password, so elapsed time is the only signal that
    /// separates them. Time (not an attempt count) is the correct primitive because it is robust to attempt
    /// cadence: under concurrent cold-start the host is CPU-bound and the same physical specialize window costs
    /// more retries, which is exactly what tripped a fixed attempt-count grace and parked a still-specializing
    /// guest. The window is measured from the first rejection of the current unbroken streak and reset by any
    /// non-rejection outcome (a reboot or transient), so only a persistent rejection reaches the grace. A
    /// genuinely wrong password (on an already-specialized guest, which produces only rejections and no reboots)
    /// therefore fails after a fixed duration regardless of fleet size. Default 6 minutes: derived from the
    /// live forest-trust park (finding 81), where two independent roots cold-started concurrently and a guest
    /// legitimately kept rejecting until its final specialize reboot at ~270s and did not reach transport-ready
    /// until ~340-360s; 6 minutes clears that observed tail with margin for heavier concurrency while staying
    /// well below the ~15 min <see cref="GuestTransportMaxRetries"/> x <see cref="GuestTransportRetryDelay"/>
    /// transport budget, so the re-prompt path always fires before the outer transport timeout.
    /// </summary>
    public TimeSpan GuestAuthGraceWindow { get; set; } = TimeSpan.FromMinutes(6);

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
