using System;
using System.Text.Json.Serialization;

namespace LabAssistant.Models.Templates;

/// <summary>
/// Top-level lab template definition.
/// </summary>
public class LabTemplate
{
    public const string CurrentSchemaVersion = "1.0.0";
    public const string SupportedTemplateType = "lab-template";

    /// <summary>
    /// Stable identifier for the template (required).
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Display name for the template (required).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Short summary of the lab (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Canonical schema version for compatibility checks (required).
    /// </summary>
    public string SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>
    /// User-controlled revision number for template content (required).
    /// </summary>
    public int TemplateRevision { get; set; } = 1;

    /// <summary>
    /// App version used to create the template (required).
    /// </summary>
    public string CreatedWithAppVersion { get; set; } = "0.0.0";

    /// <summary>
    /// Canonical template type (required).
    /// </summary>
    public string TemplateType { get; set; } = SupportedTemplateType;

    /// <summary>
    /// VM definitions included in the template (required).
    /// </summary>
    public List<VmTemplate> VmTemplates { get; set; } = new();

    /// <summary>
    /// Network defaults for the lab (optional).
    /// </summary>
    public NetworkConfig? NetworkConfig { get; set; }

    /// <summary>
    /// Optional V2 deployment-profile selection used by the unified orchestration planner.
    /// </summary>
    public string? DeploymentProfile { get; set; }

    /// <summary>
    /// Optional V2 network inventory for shared network validation and multi-NIC planning.
    /// </summary>
    public List<LabNetworkTemplate>? LabNetworks { get; set; }

    /// <summary>
    /// Optional V2 directory topology inventory for forests, domains, and trusts.
    /// </summary>
    public V2DirectoryTopologyTemplate? DirectoryTopology { get; set; }

    /// <summary>
    /// Runtime-only execution-engine classification resolved from schema-version routing.
    /// </summary>
    [JsonIgnore]
    public TemplateExecutionEngine ExecutionEngine { get; set; }

    /// <summary>
    /// Legacy alias kept for UI compatibility. Maps to <see cref="SchemaVersion"/>.
    /// </summary>
    [JsonIgnore]
    public string Version
    {
        get => SchemaVersion;
        set => SchemaVersion = value;
    }

    /// <summary>
    /// Legacy JSON compatibility hook. Reads legacy "version" values without writing them back.
    /// </summary>
    [JsonPropertyName("version")]
    public string? LegacyVersion
    {
        get => null;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                SchemaVersion = value;
            }
        }
    }
}
