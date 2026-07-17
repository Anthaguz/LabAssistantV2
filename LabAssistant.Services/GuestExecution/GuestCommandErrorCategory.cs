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
    AuthenticationRejected
}
