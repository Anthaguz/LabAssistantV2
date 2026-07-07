using LabAssistant.Models.Deployment;
using LabAssistant.Services.GuestExecution;

namespace LabAssistant.Business.Runtime;

internal sealed class V2BaseRemoteAccessRuntimeCoordinator
{
    private readonly IGuestCommandExecutor _guestCommandExecutor;

    public V2BaseRemoteAccessRuntimeCoordinator(IGuestCommandExecutor guestCommandExecutor)
    {
        _guestCommandExecutor = guestCommandExecutor;
    }

    public Task<GuestCommandResult> ConfigureBaseRemoteAccessAsync(
        string vmName,
        V2RuntimeCredential bootstrapCredential,
        V2BaseRemoteAccessOptions options,
        CancellationToken cancellationToken)
        => _guestCommandExecutor.ExecutePowerShellDirectAsync(
            vmName,
            bootstrapCredential,
            BaseRemoteAccessGuestScriptBuilder.BuildConfigureBaseRemoteAccessScript(options),
            cancellationToken);

    /// <summary>
    /// Probes the guest to confirm base remote access is actually usable
    /// (RDP enabled and an RDP-tcp listener bound to 3389), backing the
    /// <c>BaseRemoteAccessReady</c> gate. Returns a failed result when readiness cannot be confirmed.
    /// </summary>
    public Task<GuestCommandResult> ProbeBaseRemoteAccessReadyAsync(
        string vmName,
        V2RuntimeCredential bootstrapCredential,
        CancellationToken cancellationToken)
        => _guestCommandExecutor.ExecutePowerShellDirectAsync(
            vmName,
            bootstrapCredential,
            BaseRemoteAccessGuestScriptBuilder.BuildProbeBaseRemoteAccessReadyScript(),
            cancellationToken);
}
