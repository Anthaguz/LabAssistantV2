namespace LabAssistant.Services.GuestExecution;

/// <summary>
/// Classifies a guest-command error message into a <see cref="GuestCommandErrorCategory"/>.
/// </summary>
/// <remarks>
/// The guest transport (PowerShell Direct) reports a credential rejection as a plain error string surfaced by
/// <c>Invoke-Command -VMName</c> - for example "The credential is invalid", "Logon failure: unknown user name or
/// bad password", or "Access is denied". These are deterministic: the same wrong password will be rejected every
/// time, so the readiness loops must NOT burn their full retry budget on them. A broken PowerShell Direct
/// session (the guest rebooted mid-hop) is called out as <c>GuestRebooting</c> so a mutating step can retry
/// specifically on a lost transport. Everything else (a probe that has
/// not passed yet, a service still starting, a timeout) is treated as transient and worth retrying.
///
/// The password is supplied out-of-band as a secure runspace variable and never appears in the command text or
/// in these error strings, so classifying (and logging) the error message never risks leaking a secret. This
/// type only reads the message; it never emits one.
/// </remarks>
public static class GuestErrorClassifier
{
    // Substrings (matched case-insensitively) that mark a deterministic credential rejection. Kept deliberately
    // specific so a transient boot-time error is never misread as a permanent auth failure.
    private static readonly string[] AuthenticationRejectionSignatures =
    {
        "the credential is invalid",
        "logon failure",
        "unknown user name or bad password",
        "user name or password is incorrect",
        "username or password is incorrect",
        "access is denied",
        "authentication failed",
        "the user name or password is incorrect"
    };

    // Substrings (matched case-insensitively) that mark a PowerShell Direct session that broke because the guest
    // went away mid-hop (a reboot during specialize/OOBE). Kept specific to the transport-teardown signatures so
    // a genuine in-guest script failure is never misread as a reboot and retried forever.
    private static readonly string[] GuestRebootingSignatures =
    {
        "the hyper-v socket target process has ended",
        "socket target process has ended",
        "target process has ended",
        "pssessionstatebroken",
        "psdirectexception",
        "psremotingtransportexception"
    };

    /// <summary>
    /// Classifies <paramref name="error"/>. Returns <see cref="GuestCommandErrorCategory.None"/> for a null or
    /// whitespace message, <see cref="GuestCommandErrorCategory.AuthenticationRejected"/> when the message
    /// matches a known credential-rejection signature, <see cref="GuestCommandErrorCategory.GuestRebooting"/>
    /// when the PowerShell Direct session broke because the guest went away mid-hop, and
    /// <see cref="GuestCommandErrorCategory.Transient"/> otherwise.
    /// </summary>
    public static GuestCommandErrorCategory Classify(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return GuestCommandErrorCategory.None;
        }

        foreach (var signature in AuthenticationRejectionSignatures)
        {
            if (error.Contains(signature, StringComparison.OrdinalIgnoreCase))
            {
                return GuestCommandErrorCategory.AuthenticationRejected;
            }
        }

        foreach (var signature in GuestRebootingSignatures)
        {
            if (error.Contains(signature, StringComparison.OrdinalIgnoreCase))
            {
                return GuestCommandErrorCategory.GuestRebooting;
            }
        }

        return GuestCommandErrorCategory.Transient;
    }
}
