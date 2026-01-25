namespace LabAssistant.Models.Templates;

/// <summary>
/// Top-level lab template definition.
/// </summary>
public class LabTemplate
{
    /// <summary>
    /// Stable identifier for the template (required).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the template (required).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Short summary of the lab (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// VM definitions included in the template (required).
    /// </summary>
    public List<VmTemplate> VmTemplates { get; set; } = new();

    /// <summary>
    /// Network defaults for the lab (optional).
    /// </summary>
    public NetworkConfig? NetworkConfig { get; set; }

    /// <summary>
    /// Schema version for the template (required).
    /// </summary>
    public string Version { get; set; } = "v0";
}
