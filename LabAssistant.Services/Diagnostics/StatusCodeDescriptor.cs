namespace LabAssistant.Services.Diagnostics;

/// <summary>
/// The resolved description of a single logging status code. The numeric <see cref="Code"/> is the
/// canonical identity; every other member is derived from the registry and cannot drift from it.
/// The severity, flags, and byte fields are decomposed from the code so they are always consistent.
/// </summary>
public sealed class StatusCodeDescriptor
{
    public StatusCodeDescriptor(
        uint code,
        string facilityName,
        string operationName,
        string dottedName,
        string? title,
        string message,
        string? remediation,
        string level,
        StatusCodePhase phase)
    {
        Code = code;
        FacilityName = facilityName;
        OperationName = operationName;
        DottedName = dottedName;
        Title = title;
        Message = message;
        Remediation = remediation;
        Level = level;
        Phase = phase;
    }

    /// <summary>The canonical 32-bit code.</summary>
    public uint Code { get; }

    /// <summary>The code formatted as <c>0xSSFFOOCC</c>.</summary>
    public string CodeHex => $"0x{Code:X8}";

    /// <summary>Severity, decoded from bits 28-31.</summary>
    public StatusCodeSeverity Severity => (StatusCodeSeverity)((Code >> 28) & 0xF);

    /// <summary>Flags, decoded from bits 24-27.</summary>
    public StatusCodeFlags Flags => (StatusCodeFlags)((Code >> 24) & 0xF);

    /// <summary>Facility byte, decoded from bits 16-23.</summary>
    public byte Facility => (byte)((Code >> 16) & 0xFF);

    /// <summary>Operation byte, decoded from bits 8-15.</summary>
    public byte Operation => (byte)((Code >> 8) & 0xFF);

    /// <summary>Status/outcome byte, decoded from bits 0-7. 0x00 means OK.</summary>
    public byte Status => (byte)(Code & 0xFF);

    /// <summary>The stable dotted facility name.</summary>
    public string FacilityName { get; }

    /// <summary>The stable operation name within the facility.</summary>
    public string OperationName { get; }

    /// <summary>The generated dotted alias, for example <c>deploy.guest.transport-ready.end</c>.</summary>
    public string DottedName { get; }

    /// <summary>An optional friendly title for display.</summary>
    public string? Title { get; }

    /// <summary>The plain-language message.</summary>
    public string Message { get; }

    /// <summary>An optional remediation shown when the event needs a user action.</summary>
    public string? Remediation { get; }

    /// <summary>The legacy level projection (debug/info/warn/error) of the severity nibble.</summary>
    public string Level { get; }

    /// <summary>The lifecycle phase this code is emitted with.</summary>
    public StatusCodePhase Phase { get; }
}
