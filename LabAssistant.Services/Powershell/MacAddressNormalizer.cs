namespace LabAssistant.Services.PowerShell;

/// <summary>
/// Normalizes MAC addresses to a canonical form for reliable comparison.
/// Hyper-V's <c>Get-VMNetworkAdapter</c> returns MACs as bare uppercase hex (<c>00155DABCDEF</c>),
/// while a guest's <c>Get-NetAdapter</c> returns separator-delimited hex (<c>00-15-5D-AB-CD-EF</c>).
/// Comparing the raw values silently mismatches, so both sides must be normalized first.
/// </summary>
public static class MacAddressNormalizer
{
    /// <summary>
    /// Normalizes a MAC address by removing common separators and upper-casing the hex digits.
    /// </summary>
    /// <param name="value">The raw MAC address. Null is treated as an empty string.</param>
    /// <returns>The separator-stripped, upper-cased MAC address.</returns>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[value.Length];
        var length = 0;
        foreach (var character in value)
        {
            if (character is '-' or ':' or '.' or ' ')
            {
                continue;
            }

            buffer[length++] = char.ToUpperInvariant(character);
        }

        return new string(buffer[..length]);
    }

    /// <summary>
    /// Compares two MAC addresses after normalizing separators and case.
    /// </summary>
    /// <param name="left">The first MAC address.</param>
    /// <param name="right">The second MAC address.</param>
    /// <returns><c>true</c> when the normalized forms are equal; otherwise <c>false</c>.</returns>
    public static bool AreEqual(string? left, string? right)
    {
        return string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);
    }
}
