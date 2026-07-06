namespace LabAssistant.Services.PowerShell;

/// <summary>
/// Shared helper for safely composing PowerShell command text.
/// All Hyper-V and guest PowerShell string building must route through this type so that
/// caller-supplied values (VM names, switch names, paths, credentials) cannot break out of
/// their quoted literal and inject arbitrary script.
/// </summary>
public static class PowerShellCommandBuilder
{
    /// <summary>
    /// Wraps a value in a single-quoted PowerShell literal, doubling any embedded single quotes.
    /// This is the only supported way to embed a caller-supplied string into a PowerShell command,
    /// because a single quote inside the value would otherwise terminate the literal early and allow
    /// injection (for example a VM name of <c>x'; Remove-VM *; '</c>).
    /// </summary>
    /// <param name="value">The raw value to embed. Null is treated as an empty string.</param>
    /// <returns>A single-quoted, injection-safe PowerShell string literal.</returns>
    public static string Quote(string? value)
    {
        return $"'{Escape(value)}'";
    }

    /// <summary>
    /// Escapes a value for use inside an existing single-quoted PowerShell literal by doubling
    /// single quotes. Prefer <see cref="Quote(string?)"/> unless the surrounding quotes are supplied
    /// by the caller.
    /// </summary>
    /// <param name="value">The raw value to escape. Null is treated as an empty string.</param>
    /// <returns>The escaped value without surrounding quotes.</returns>
    public static string Escape(string? value)
    {
        return (value ?? string.Empty).Replace("'", "''", StringComparison.Ordinal);
    }
}
