namespace LabAssistant.Services.HyperV;

/// <summary>
/// Carries raw PowerShell output plus timing metadata for a Hyper-V execution request.
/// </summary>
public sealed class HyperVPowerShellExecutionResult
{
    public string Output { get; init; } = string.Empty;

    public string Error { get; init; } = string.Empty;

    public bool SessionCreated { get; init; }

    public long SessionCreationDurationMs { get; init; }

    public long CommandDurationMs { get; init; }
}
