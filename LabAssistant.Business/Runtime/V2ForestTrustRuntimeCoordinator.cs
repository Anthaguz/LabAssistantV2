using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.Services.GuestExecution;

namespace LabAssistant.Business.Runtime;

internal sealed class V2ForestTrustRuntimeCoordinator
{
    private readonly IGuestCommandExecutor _guestCommandExecutor;

    public V2ForestTrustRuntimeCoordinator(IGuestCommandExecutor guestCommandExecutor)
    {
        _guestCommandExecutor = guestCommandExecutor;
    }

    public Task<GuestCommandResult> PrepareDnsForwarderAsync(
        string vmName,
        V2RuntimeCredential domainAdminCredential,
        string targetDomainName,
        IReadOnlyList<string> targetDnsServers,
        CancellationToken cancellationToken)
        => _guestCommandExecutor.ExecutePowerShellDirectAsync(
            vmName,
            domainAdminCredential,
            ForestTrustGuestCommandBuilder.BuildPrepareDnsForwarderCommand(targetDomainName, targetDnsServers),
            cancellationToken);

    public Task<GuestCommandResult> CreateBidirectionalForestTrustAsync(
        string vmName,
        V2RuntimeCredential sourceDomainAdminCredential,
        V2ResolvedTrustPlanningContext trust,
        V2RuntimeCredential targetDomainAdminCredential,
        CancellationToken cancellationToken)
        => _guestCommandExecutor.ExecutePowerShellDirectAsync(
            vmName,
            sourceDomainAdminCredential,
            ForestTrustGuestCommandBuilder.BuildCreateBidirectionalForestTrustCommand(trust, targetDomainAdminCredential),
            cancellationToken);

    public Task<GuestCommandResult> ValidateForestTrustAsync(
        string vmName,
        V2RuntimeCredential domainAdminCredential,
        string trustedDomainName,
        CancellationToken cancellationToken)
        => _guestCommandExecutor.ExecutePowerShellDirectAsync(
            vmName,
            domainAdminCredential,
            ForestTrustGuestCommandBuilder.BuildValidateForestTrustCommand(trustedDomainName),
            cancellationToken);

    public Task<GuestCommandResult> CleanupForestTrustAsync(
        string vmName,
        V2RuntimeCredential domainAdminCredential,
        string trustedDomainName,
        CancellationToken cancellationToken)
        => _guestCommandExecutor.ExecutePowerShellDirectAsync(
            vmName,
            domainAdminCredential,
            ForestTrustGuestCommandBuilder.BuildCleanupForestTrustCommand(trustedDomainName),
            cancellationToken);
}
