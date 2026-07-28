namespace LabAssistant.Services.GuestExecution;

/// <summary>
/// Coarse classification of a guest-command failure, used by the runtime readiness loops to decide whether a
/// failure is worth retrying (the guest is still coming up) or is deterministic and should fail fast.
/// </summary>
public enum GuestCommandErrorCategory
{
    /// <summary>No error - the command succeeded.</summary>
    None = 0,

    /// <summary>
    /// A transient failure that is expected to clear on its own (the guest is still booting, a service has not
    /// started yet, a probe timed out). Retrying is the correct response.
    /// </summary>
    Transient,

    /// <summary>
    /// The guest rejected the supplied credential (invalid password, logon failure, access denied). This is
    /// deterministic: retrying the same credential cannot succeed, so the loop should stop and surface an
    /// actionable error (or prompt for a corrected credential) instead of exhausting its retry budget.
    /// </summary>
    AuthenticationRejected,

    /// <summary>
    /// The PowerShell Direct session was torn down mid-hop because the guest went away - almost always a guest
    /// reboot during the volatile specialize/OOBE window (surfaced as "The Hyper-V socket target process has
    /// ended", <c>PSSessionStateBroken</c>, or <c>PSDirectException</c>). This is transient and self-heals: the
    /// next fresh hop reconnects once the guest finishes booting, so a mutating step that hit it should re-run
    /// the whole hop rather than fail the deploy. It is called out as its own category (rather than folded into
    /// <see cref="Transient"/>) so a step can retry specifically on a lost transport while still failing fast on
    /// a genuine in-guest script error.
    /// </summary>
    GuestRebooting
}
