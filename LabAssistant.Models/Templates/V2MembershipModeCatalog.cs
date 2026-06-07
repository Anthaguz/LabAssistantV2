namespace LabAssistant.Models.Templates;

public static class V2MembershipModeCatalog
{
    public const string DomainMember = "DomainMember";
    public const string Standalone = "Standalone";

    public static IReadOnlyList<string> SupportedModes { get; } =
    [
        DomainMember,
        Standalone
    ];

    public static bool IsSupported(string? value)
        => !string.IsNullOrWhiteSpace(value) &&
           SupportedModes.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return SupportedModes.FirstOrDefault(mode => string.Equals(mode, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsDomainMember(string? value)
        => string.Equals(Normalize(value), DomainMember, StringComparison.Ordinal);

    public static bool IsStandalone(string? value)
        => string.Equals(Normalize(value), Standalone, StringComparison.Ordinal);
}
