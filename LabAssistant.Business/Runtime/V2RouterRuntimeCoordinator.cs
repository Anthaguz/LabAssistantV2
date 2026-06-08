using LabAssistant.Models.Deployment;
using LabAssistant.Services.GuestExecution;

namespace LabAssistant.Business.Runtime;

internal sealed class V2RouterRuntimeCoordinator
{
    private readonly IGuestCommandExecutor _guestCommandExecutor;

    public V2RouterRuntimeCoordinator(IGuestCommandExecutor guestCommandExecutor)
    {
        _guestCommandExecutor = guestCommandExecutor;
    }

    public Task<GuestCommandResult> PrepareRouterNetworkAsync(
        string vmName,
        V2RuntimeCredential bootstrapCredential,
        IReadOnlyList<RouterNicPlan> nicPlans,
        CancellationToken cancellationToken)
        => _guestCommandExecutor.ExecutePowerShellDirectAsync(
            vmName,
            bootstrapCredential,
            RouterGuestScriptBuilder.BuildPrepareRouterNetworkScript(nicPlans),
            cancellationToken);

    public Task<GuestCommandResult> InstallRemoteAccessFeatureAsync(
        string vmName,
        V2RuntimeCredential bootstrapCredential,
        CancellationToken cancellationToken)
        => _guestCommandExecutor.ExecutePowerShellDirectAsync(
            vmName,
            bootstrapCredential,
            RouterGuestScriptBuilder.BuildInstallRouterRemoteAccessFeatureScript(),
            cancellationToken);

    public Task<GuestCommandResult> EnableRoutingAsync(
        string vmName,
        V2RuntimeCredential bootstrapCredential,
        string externalMacAddress,
        CancellationToken cancellationToken)
        => _guestCommandExecutor.ExecutePowerShellDirectAsync(
            vmName,
            bootstrapCredential,
            RouterGuestScriptBuilder.BuildEnableRouterRoutingScript(externalMacAddress),
            cancellationToken);

    public Task<GuestCommandResult> ConfigureNatAsync(
        string vmName,
        V2RuntimeCredential bootstrapCredential,
        string externalMacAddress,
        IReadOnlyList<string> internalMacAddresses,
        CancellationToken cancellationToken)
        => _guestCommandExecutor.ExecutePowerShellDirectAsync(
            vmName,
            bootstrapCredential,
            RouterGuestScriptBuilder.BuildConfigureRouterNatScript(externalMacAddress, internalMacAddresses),
            cancellationToken);

    public Task<GuestCommandResult> ProbeExternalReadinessAsync(
        string vmName,
        V2RuntimeCredential bootstrapCredential,
        string externalMacAddress,
        CancellationToken cancellationToken)
        => _guestCommandExecutor.ExecutePowerShellDirectAsync(
            vmName,
            bootstrapCredential,
            RouterGuestScriptBuilder.BuildProbeRouterExternalReadinessScript(externalMacAddress),
            cancellationToken);

    public Task<GuestCommandResult> ValidateCrossSwitchRoutingAsync(
        string vmName,
        V2RuntimeCredential bootstrapCredential,
        string expectedDomainName,
        CancellationToken cancellationToken)
        => _guestCommandExecutor.ExecutePowerShellDirectAsync(
            vmName,
            bootstrapCredential,
            RouterGuestScriptBuilder.BuildValidateCrossSwitchRoutingScript(expectedDomainName),
            cancellationToken);

    public Task<GuestCommandResult> ValidateRouterEgressAsync(
        string vmName,
        V2RuntimeCredential bootstrapCredential,
        CancellationToken cancellationToken)
        => _guestCommandExecutor.ExecutePowerShellDirectAsync(
            vmName,
            bootstrapCredential,
            RouterGuestScriptBuilder.BuildValidateRouterEgressScript(),
            cancellationToken);
}
