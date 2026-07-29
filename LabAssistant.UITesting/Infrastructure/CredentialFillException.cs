namespace LabAssistant.UITesting.Infrastructure;

/// <summary>
/// Raised when the credential-slot fill automation cannot get a slot to persist after its bounded
/// retries. This is deliberately loud: the harness must never silently spin to a timeout and report
/// a slot "resolved" when the app's credential store never gained the record, because that both
/// wedges the deploy (the plan stays unstartable) and masks a genuine product Save/Upsert bug behind
/// a harmless-looking retry. The message states whether keyboard focus ever landed on the password
/// box - the seam that separates a harness fill-miss from a real product persist failure.
/// </summary>
public sealed class CredentialFillException : Exception
{
    public CredentialFillException(string message) : base(message)
    {
    }
}
