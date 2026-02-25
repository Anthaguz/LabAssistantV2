namespace LabAssistant.Models.Validation;

public enum VhdxIntegrityStatus
{
    Valid,
    Missing,
    Unreadable,
    Invalid
}

public enum VhdxIntegrityValidationDepth
{
    AccessibilityOnly,
    Full
}

public sealed class VhdxIntegrityValidationResult
{
    public VhdxIntegrityStatus Status { get; init; }
    public string Path { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? Detail { get; init; }

    public bool IsValid => Status == VhdxIntegrityStatus.Valid;
}
