using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.Services.GuestExecution;

namespace LabAssistant.Business.Runtime;

internal sealed class V2DomainProgressionRuntimeCoordinator
{
    private readonly IGuestCommandExecutor _guestCommandExecutor;

    public V2DomainProgressionRuntimeCoordinator(IGuestCommandExecutor guestCommandExecutor)
    {
        _guestCommandExecutor = guestCommandExecutor;
    }

    public Task<GuestCommandResult> PrepareGuestNetworkAsync(
        string vmName,
        V2RuntimeCredential bootstrapCredential,
        IReadOnlyList<V2ResolvedVmNetworkInterface> nics,
        CancellationToken cancellationToken)
        => _guestCommandExecutor.ExecutePowerShellDirectAsync(
            vmName,
            bootstrapCredential,
            GuestNetworkGuestScriptBuilder.BuildPrepareGuestNetworkScript(nics),
            cancellationToken);

    public Task<GuestCommandResult> PromoteReplicaDomainControllerAsync(
        string vmName,
        V2RuntimeCredential bootstrapCredential,
        V2ResolvedDomainPlanningContext domain,
        V2RuntimeCredential domainJoinCredential,
        string dsrmPassword,
        CancellationToken cancellationToken)
        => _guestCommandExecutor.ExecutePowerShellDirectAsync(
            vmName,
            bootstrapCredential,
            DomainProgressionGuestScriptBuilder.BuildPromoteReplicaDomainControllerScript(domain, domainJoinCredential, dsrmPassword),
            cancellationToken);

    public Task<GuestCommandResult> VerifyReplicaDomainControllerAsync(
        string vmName,
        V2RuntimeCredential domainAdminCredential,
        string expectedDomainName,
        CancellationToken cancellationToken)
        => _guestCommandExecutor.ExecutePowerShellDirectAsync(
            vmName,
            domainAdminCredential,
            FirstDomainControllerGuestScriptBuilder.BuildVerifyDomainControllerScript(expectedDomainName),
            cancellationToken);

    public Task<GuestCommandResult> ProbeReplicaDomainReadyAsync(
        string vmName,
        V2RuntimeCredential domainAdminCredential,
        string expectedDomainName,
        CancellationToken cancellationToken)
        => _guestCommandExecutor.ExecutePowerShellDirectAsync(
            vmName,
            domainAdminCredential,
            FirstDomainControllerGuestScriptBuilder.BuildDomainReadyProbeScript(expectedDomainName),
            cancellationToken);

    public Task<GuestCommandResult> StabilizeDomainDnsAsync(
        string vmName,
        V2RuntimeCredential domainAdminCredential,
        IReadOnlyList<string> dnsServers,
        CancellationToken cancellationToken)
        => _guestCommandExecutor.ExecutePowerShellDirectAsync(
            vmName,
            domainAdminCredential,
            GuestNetworkGuestScriptBuilder.BuildStabilizeDomainDnsScript(dnsServers),
            cancellationToken);

    public Task<GuestCommandResult> JoinDomainAsync(
        string vmName,
        V2RuntimeCredential bootstrapCredential,
        string expectedDomainName,
        V2RuntimeCredential joinCredential,
        CancellationToken cancellationToken)
        => _guestCommandExecutor.ExecutePowerShellDirectAsync(
            vmName,
            bootstrapCredential,
            DomainProgressionGuestScriptBuilder.BuildJoinDomainScript(expectedDomainName, joinCredential),
            cancellationToken);

    public Task<GuestCommandResult> VerifyJoinedDomainLocallyAsync(
        string vmName,
        V2RuntimeCredential bootstrapCredential,
        string expectedDomainName,
        CancellationToken cancellationToken)
        => _guestCommandExecutor.ExecutePowerShellDirectAsync(
            vmName,
            bootstrapCredential,
            DomainProgressionGuestScriptBuilder.BuildVerifyJoinedDomainScript(expectedDomainName),
            cancellationToken);

    public Task<GuestCommandResult> VerifyJoinedDomainWithDomainCredentialAsync(
        string vmName,
        V2RuntimeCredential domainAdminCredential,
        string expectedDomainName,
        CancellationToken cancellationToken)
        => _guestCommandExecutor.ExecutePowerShellDirectAsync(
            vmName,
            domainAdminCredential,
            DomainProgressionGuestScriptBuilder.BuildVerifyJoinedDomainScript(expectedDomainName),
            cancellationToken);

    public static bool IsExpectedRestartBoundaryError(string? error)
        => V2FirstDomainControllerRuntimeCoordinator.IsExpectedRestartBoundaryError(error);
}
