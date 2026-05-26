using System.Text.Json.Serialization;

namespace LabAssistant.Models.Catalog;

/// <summary>
/// Image-owned bootstrap assumptions used by V2 planning to establish guest access without storing secret values.
/// </summary>
public class VhdxBootstrapProfile
{
    /// <summary>
    /// Expected local or bootstrap user for the prepared image.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ExpectedLocalUser { get; set; }

    /// <summary>
    /// Reference to a reusable local credential slot required to unlock bootstrap access.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LocalCredentialSlotRef { get; set; }

    /// <summary>
    /// Optional guest OS family detail used by executor/planner assumptions.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GuestOsFamily { get; set; }

    /// <summary>
    /// Guest transport expectation for the image. Current V2 baseline is powershell-direct.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GuestTransport { get; set; }

    /// <summary>
    /// Optional image/bootstrap operational notes.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Notes { get; set; }
}
