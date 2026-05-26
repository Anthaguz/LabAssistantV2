namespace LabAssistant.Models.Templates;

/// <summary>
/// Identifies which deployment engine family a template targets after schema-version routing is evaluated.
/// </summary>
public enum TemplateExecutionEngine
{
    Unknown = 0,
    V1Deployment = 1,
    V2UnifiedPlanning = 2
}
