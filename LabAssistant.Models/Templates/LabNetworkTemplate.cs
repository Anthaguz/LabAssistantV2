using System.Text.Json.Serialization;

namespace LabAssistant.Models.Templates;

/// <summary>
/// Shared lab-network definition used by V2 planning to group NIC intent and network-level validation.
/// </summary>
public class LabNetworkTemplate
{
    public string NetworkId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? SwitchName { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SwitchType { get; set; }

    public string? Subnet { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Notes { get; set; }
}
