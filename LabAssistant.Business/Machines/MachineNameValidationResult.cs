namespace LabAssistant.Business.Machines;

/// <summary>
/// Outcome of <see cref="MachineNameValidator.Validate"/>. On success it carries the normalized
/// (trimmed) name callers should use; on failure it carries an actionable, user-facing message.
/// </summary>
public sealed class MachineNameValidationResult
{
    private MachineNameValidationResult(bool isValid, string normalizedName, string? errorMessage)
    {
        IsValid = isValid;
        NormalizedName = normalizedName;
        ErrorMessage = errorMessage;
    }

    public bool IsValid { get; }

    public string NormalizedName { get; }

    public string? ErrorMessage { get; }

    public static MachineNameValidationResult Valid(string normalizedName) =>
        new(true, normalizedName, null);

    public static MachineNameValidationResult Invalid(string errorMessage) =>
        new(false, string.Empty, errorMessage);
}
