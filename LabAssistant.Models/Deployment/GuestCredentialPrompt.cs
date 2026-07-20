namespace LabAssistant.Models.Deployment
{
    /// <summary>
    /// Describes a guest sign-in that a running VM rejected as a credential failure, so the deploy runtime can ask the
    /// user to correct the credential and retry in place instead of failing the whole deployment.
    /// </summary>
    /// <remarks>
    /// This carries only redacted, display-safe fields. The guest password is injected out of band as a secure
    /// runspace variable and never appears in <see cref="GuestError"/>, so surfacing this request in the UI cannot
    /// leak a secret.
    /// </remarks>
    public sealed class GuestCredentialPromptRequest
    {
        /// <summary>Name of the running guest that rejected the credential.</summary>
        public string VmName { get; init; } = string.Empty;

        /// <summary>Credential slot key whose stored value was rejected.</summary>
        public string CredentialSlotKey { get; init; } = string.Empty;

        /// <summary>Username the runtime attempted to sign in with (the value the corrected credential should keep).</summary>
        public string ExpectedUsername { get; init; } = string.Empty;

        /// <summary>Redacted, summarized guest error explaining why the credential was rejected.</summary>
        public string? GuestError { get; init; }
    }

    /// <summary>
    /// The user's response to a <see cref="GuestCredentialPromptRequest"/>.
    /// </summary>
    public sealed class GuestCredentialPromptResponse
    {
        /// <summary>Indicates the user dismissed the prompt without supplying a credential; the deploy should fail.</summary>
        public bool Cancelled { get; init; }

        /// <summary>Corrected username to retry with.</summary>
        public string Username { get; init; } = string.Empty;

        /// <summary>Corrected password to retry with.</summary>
        public string Password { get; init; } = string.Empty;

        /// <summary>
        /// Indicates the corrected credential should be persisted to the local credential slot store so future
        /// deployments reuse it.
        /// </summary>
        public bool RememberForSlot { get; init; }

        /// <summary>Creates a cancelled response (the user declined to supply a corrected credential).</summary>
        public static GuestCredentialPromptResponse Cancel() => new() { Cancelled = true };
    }
}
