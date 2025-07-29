namespace LabAssistant.Models.PowerShell;

/// <summary>
/// Holds a reference token for an active PowerShell session.
/// This is passed around in the models layer without exposing the actual session implementation.
/// </summary>
public class PowerShellHandle
{
    public Guid SessionId { get; } = Guid.NewGuid(); // internal use only

    // Optional: Add any metadata or context tracking
}
