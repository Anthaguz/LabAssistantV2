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
    /// Per-attempt wall-clock ceiling for a single in-guest PowerShell Direct call issued by the rollback
    /// forest-trust cleanup retry. A cleanup delete runs into a DC that is rebooting or being torn down, and
    /// PowerShell Direct connection negotiation can BLOCK (not fail fast) while the guest KVP exchange service is
    /// mid-restart. The one-shot session already enforces a 5 minute host-side timeout, but 5 minutes is far too
    /// long for a retry cadence that must cycle several times across a ~340-360s reboot, and - more importantly -
    /// that timeout surfaces as a thrown exception, which the retry loop must convert into a bounded, retryable
    /// transport drop rather than let escape. This shorter bound abandons a frozen call quickly (killing its
    /// process tree, so no orphan powershell.exe and no held per-VM lock) and hands control back to the retry loop
    /// so it can retry through the reboot or bounded-fail cleanly. Default 90s: long enough for a healthy delete to
    /// complete, short enough that roughly four to five attempts span the ~360s reboot window. Applies ONLY to the
    /// cleanup path; the deploy path keeps the one-shot session's own timeout unchanged.
    /// </summary>
    public TimeSpan GuestCleanupAttemptTimeout { get; set; } = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Wall-clock ceiling for a single in-guest forest-trust cleanup delete retry. Each side of a trust (the source
    /// and target deletes) is bounded independently by this budget, so a dual-DC trust can retry up to two of these
    /// windows serially during rollback. Because a cleanup attempt can either fail fast (guest off) in seconds or
    /// block up to <see cref="GuestCleanupAttemptTimeout"/> (guest mid-reboot), a fixed attempt count cannot bound
    /// both cases correctly: enough attempts to span the reboot at the fast-fail cadence would let blocked attempts
    /// run for hours, while few enough attempts to cap blocked time would give up before a fast-failing guest
    /// finishes rebooting. Wall-clock is the correct primitive (the same lesson as <see cref="GuestAuthGraceWindow"/>):
    /// the cleanup retries until either the delete succeeds or this budget elapses, regardless of how each attempt
    /// failed. Default 15 minutes, matching the ~15 min transport budget (<see cref="GuestTransportMaxRetries"/> x
    /// <see cref="GuestTransportRetryDelay"/>) so it comfortably outlasts the documented ~340-360s rollback reboot
    /// with roughly 2.5x margin and never expires mid-recovery to leave a dangling trust; a genuinely gone DC
    /// bounded-fails at this ceiling and flags residual rather than hanging.
    /// </summary>
    public TimeSpan GuestCleanupRetryBudget { get; set; } = TimeSpan.FromMinutes(15);

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
