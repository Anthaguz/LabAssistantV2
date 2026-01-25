namespace LabAssistant.Models.Validation;

/// <summary>
/// Validation result for VHDX catalog data.
/// </summary>
public class VhdxCatalogValidationResult
{
    public List<string> Errors { get; } = new();

    public bool IsValid => Errors.Count == 0;
}
