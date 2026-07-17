namespace LabAssistant.Services.Diagnostics;

/// <summary>
/// Severity of a logging status code, held in the high nibble (bits 28-31) of the 32-bit code.
/// Ordered from least to most severe. The legacy <c>level</c> string is a projection of this value.
/// </summary>
public enum StatusCodeSeverity : byte
{
    /// <summary>Successful completion of an operation. Pairs with status byte 0x00.</summary>
    Success = 0x0,
    Trace = 0x1,
    Debug = 0x2,
    Info = 0x3,
    Notice = 0x4,
    Warning = 0x5,
    Error = 0x6,
    Critical = 0x7,
    Fatal = 0x8
}
