namespace LabAssistant.Models.Validation;

/// <summary>
/// Validation result for lab templates.
/// </summary>
public class LabTemplateValidationResult
{
    public List<string> Errors { get; } = new();

    /// <summary>
    /// VHDX references that need user selection/resolution.
    /// </summary>
    public List<string> MissingVhdxIds { get; } = new();

    public bool IsValid => Errors.Count == 0;
}
