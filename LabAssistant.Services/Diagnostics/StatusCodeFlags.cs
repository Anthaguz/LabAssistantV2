using System;

namespace LabAssistant.Services.Diagnostics;

/// <summary>
/// Orthogonal hints held in the flags nibble (bits 24-27) of the 32-bit code. Bitwise; combinable.
/// </summary>
[Flags]
public enum StatusCodeFlags : byte
{
    None = 0x0,

    /// <summary>The operation may be retried.</summary>
    Retryable = 0x1,

    /// <summary>The condition is expected to clear on its own (still booting, transient contention).</summary>
    Transient = 0x2,

    /// <summary>Resolving it requires a user action; the remediation should be surfaced.</summary>
    UserActionable = 0x4
}
