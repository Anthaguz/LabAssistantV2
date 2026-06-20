namespace LabAssistant.Models.Templates;

/// <summary>
/// Canonical switch-type names supported by V2 lab-network switch intent.
/// </summary>
public static class V2SwitchTypeCatalog
{
    /// <summary>
    /// Hyper-V External switch type.
    /// </summary>
    public const string External = "External";

    /// <summary>
    /// Hyper-V Internal switch type.
    /// </summary>
    public const string Internal = "Internal";

    /// <summary>
    /// Hyper-V Private switch type.
    /// </summary>
    public const string Private = "Private";

    /// <summary>
    /// Supported V2 lab-network switch types in canonical display order.
    /// </summary>
    public static readonly IReadOnlyList<string> SupportedTypes =
    [
        External,
        Internal,
        Private
    ];

    /// <summary>
    /// Normalizes a switch type to its canonical V2 value when supported.
    /// </summary>
    public static bool TryNormalize(string? value, out string switchType)
    {
        var trimmed = value?.Trim();
        switchType = SupportedTypes.FirstOrDefault(
            type => string.Equals(type, trimmed, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        return switchType.Length > 0;
    }

    /// <summary>
    /// Returns whether the supplied switch type is supported by V2 lab-network switch intent.
    /// </summary>
    public static bool IsSupported(string? value) => TryNormalize(value, out _);
}
