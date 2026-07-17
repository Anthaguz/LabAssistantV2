namespace LabAssistant.Services.Diagnostics;

/// <summary>
/// Entry point for resolving and decomposing 32-bit logging status codes. The numeric code is the
/// canonical identity; these helpers never require the registry for pure bit decomposition, and
/// resolve to a full <see cref="StatusCodeDescriptor"/> through the generated catalog.
/// </summary>
public static class StatusCodes
{
    /// <summary>Resolves the full description of a code, or null when the code is not registered.</summary>
    public static StatusCodeDescriptor? Describe(uint code)
        => StatusCodeCatalog.TryGet(code, out var descriptor) ? descriptor : null;

    /// <summary>Severity nibble (bits 28-31).</summary>
    public static StatusCodeSeverity SeverityOf(uint code) => (StatusCodeSeverity)((code >> 28) & 0xF);

    /// <summary>Flags nibble (bits 24-27).</summary>
    public static StatusCodeFlags FlagsOf(uint code) => (StatusCodeFlags)((code >> 24) & 0xF);

    /// <summary>The set flag names (bits 24-27), in ascending bit order. Empty when no flags are set.</summary>
    public static IReadOnlyList<string> FlagNames(uint code)
    {
        var flags = FlagsOf(code);
        if (flags == StatusCodeFlags.None)
        {
            return System.Array.Empty<string>();
        }

        var names = new System.Collections.Generic.List<string>(3);
        if (flags.HasFlag(StatusCodeFlags.Retryable)) names.Add(nameof(StatusCodeFlags.Retryable));
        if (flags.HasFlag(StatusCodeFlags.Transient)) names.Add(nameof(StatusCodeFlags.Transient));
        if (flags.HasFlag(StatusCodeFlags.UserActionable)) names.Add(nameof(StatusCodeFlags.UserActionable));
        return names;
    }

    /// <summary>Facility byte (bits 16-23).</summary>
    public static byte FacilityOf(uint code) => (byte)((code >> 16) & 0xFF);

    /// <summary>Operation byte (bits 8-15).</summary>
    public static byte OperationOf(uint code) => (byte)((code >> 8) & 0xFF);

    /// <summary>Status/outcome byte (bits 0-7). 0x00 means OK.</summary>
    public static byte StatusOf(uint code) => (byte)(code & 0xFF);

    /// <summary>True when the code represents a successful outcome (Success severity and status 0x00).</summary>
    public static bool IsSuccess(uint code) => SeverityOf(code) == StatusCodeSeverity.Success && StatusOf(code) == 0x00;

    /// <summary>
    /// Projects a code's severity onto the legacy level string (debug/info/warn/error). Prefers the
    /// registry's declared level when the code is registered, otherwise maps the severity nibble.
    /// </summary>
    public static string LevelOf(uint code)
        => Describe(code)?.Level ?? LevelForSeverity(SeverityOf(code));

    /// <summary>Maps a severity to the legacy level contract.</summary>
    public static string LevelForSeverity(StatusCodeSeverity severity) => severity switch
    {
        StatusCodeSeverity.Trace => "debug",
        StatusCodeSeverity.Debug => "debug",
        StatusCodeSeverity.Warning => "warn",
        StatusCodeSeverity.Error => "error",
        StatusCodeSeverity.Critical => "error",
        StatusCodeSeverity.Fatal => "error",
        _ => "info"
    };
}
