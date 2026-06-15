using System.Text.Json.Serialization;

namespace LabAssistant.Models.Templates;

/// <summary>
/// Carries template-side references to reusable local credential slots without embedding secret values.
/// </summary>
public class VmCredentialSlotBindings
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LocalBootstrap { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DomainAdmin { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DomainJoin { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Dsrm { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ParentDomainAdmin { get; set; }
}
