using LabAssistant.Models.Deployment;

namespace LabAssistant.Services.GuestExecution;

public interface IGuestCommandExecutor
{
    Task<GuestCommandResult> ExecutePowerShellDirectAsync(
        string vmName,
        V2RuntimeCredential credential,
        string script,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops the persistent guest session for <paramref name="vmName"/> (if any) so the next guest step
    /// re-establishes a fresh connection. Call this around a step that reboots the guest, since a reboot
    /// severs the in-guest runspace. Implementations that keep no per-VM session may treat this as a no-op;
    /// self-healing implementations also detect a broken session on the next call, so this is an explicit
    /// optimization rather than a correctness requirement.
    /// </summary>
    /// <remarks>Default no-op so stateless implementations and test fakes remain valid.</remarks>
    void InvalidateVmSession(string vmName)
    {
    }

    /// <summary>
    /// Disposes the persistent guest session for a single VM, releasing its dedicated host runspace and the
    /// in-guest connection. Call this once a VM's guest steps are complete.
    /// </summary>
    /// <remarks>Default no-op so stateless implementations and test fakes remain valid.</remarks>
    void DisposeVmSession(string vmName)
    {
    }

    /// <summary>
    /// Disposes every persistent guest session this executor is holding. Because a single executor instance
    /// can outlive an individual deployment (for example when registered as a shared singleton), the deploy
    /// orchestrator must call this at the end of every run so no host runspace or guest connection leaks
    /// across deployments. Cleanup is mandatory even on failure or cancellation.
    /// </summary>
    /// <remarks>Default no-op so stateless implementations and test fakes remain valid.</remarks>
    void DisposeAllVmSessions()
    {
    }
}

public sealed class GuestCommandResult
{
    public bool Success { get; init; }

    public string Output { get; init; } = string.Empty;

    public string Error { get; init; } = string.Empty;

    /// <summary>
    /// Coarse classification of <see cref="Error"/> (none/transient/authentication-rejected). Set by the guest
    /// executor at the transport boundary so callers can distinguish a deterministic credential rejection from a
    /// transient not-ready-yet failure without re-parsing the message. Defaults to
    /// <see cref="GuestCommandErrorCategory.None"/>.
    /// </summary>
    public GuestCommandErrorCategory ErrorCategory { get; init; } = GuestCommandErrorCategory.None;
}
