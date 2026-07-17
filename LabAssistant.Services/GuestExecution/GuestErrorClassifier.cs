namespace LabAssistant.Services.GuestExecution;

/// <summary>
/// Classifies a guest-command error message into a <see cref="GuestCommandErrorCategory"/>.
/// </summary>
/// <remarks>
/// The guest transport (PowerShell Direct) reports a credential rejection as a plain error string surfaced by
/// <c>Invoke-Command -VMName</c> - for example "The credential is invalid", "Logon failure: unknown user name or
/// bad password", or "Access is denied". These are deterministic: the same wrong password will be rejected every
/// time, so the readiness loops must NOT burn their full retry budget on them. Everything else (a probe that has
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

    /// <summary>
    /// Classifies <paramref name="error"/>. Returns <see cref="GuestCommandErrorCategory.None"/> for a null or
    /// whitespace message, <see cref="GuestCommandErrorCategory.AuthenticationRejected"/> when the message
    /// matches a known credential-rejection signature, and <see cref="GuestCommandErrorCategory.Transient"/>
    /// otherwise.
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

        return GuestCommandErrorCategory.Transient;
    }
}
