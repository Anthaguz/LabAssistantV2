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
}
