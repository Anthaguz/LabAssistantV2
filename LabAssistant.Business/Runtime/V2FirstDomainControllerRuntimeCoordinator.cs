using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.Services.GuestExecution;

namespace LabAssistant.Business.Runtime;

internal sealed class V2FirstDomainControllerRuntimeCoordinator
{
    private readonly IGuestCommandExecutor _guestCommandExecutor;

    public V2FirstDomainControllerRuntimeCoordinator(IGuestCommandExecutor guestCommandExecutor)
    {
        _guestCommandExecutor = guestCommandExecutor;
    }

    public async Task<GuestCommandResult> EnsureAdDomainServicesInstalledAsync(
        string vmName,
        V2RuntimeCredential bootstrapCredential,
        CancellationToken cancellationToken)
    {
        return await _guestCommandExecutor.ExecutePowerShellDirectAsync(
            vmName,
            bootstrapCredential,
            FirstDomainControllerGuestScriptBuilder.BuildInstallAdDomainServicesFeatureScript(),
            cancellationToken);
    }

    public async Task<GuestCommandResult> PromoteFirstDomainControllerAsync(
        string vmName,
        V2RuntimeCredential bootstrapCredential,
        V2ResolvedDomainPlanningContext domain,
        string dsrmPassword,
        V2RuntimeCredential? parentDomainAdminCredential,
        V2ResolvedDomainPlanningContext? parentDomain,
        CancellationToken cancellationToken)
    {
        return await _guestCommandExecutor.ExecutePowerShellDirectAsync(
            vmName,
            bootstrapCredential,
            FirstDomainControllerGuestScriptBuilder.BuildPromoteFirstDomainControllerScript(domain, dsrmPassword, parentDomainAdminCredential, parentDomain),
            cancellationToken);
    }

    public async Task<GuestCommandResult> ProbeParentDomainDnsReadyAsync(
        string vmName,
        V2RuntimeCredential bootstrapCredential,
        string parentDomainName,
        CancellationToken cancellationToken)
    {
        return await _guestCommandExecutor.ExecutePowerShellDirectAsync(
            vmName,
            bootstrapCredential,
            FirstDomainControllerGuestScriptBuilder.BuildWaitForParentDomainDnsScript(parentDomainName),
            cancellationToken);
    }

    public async Task<GuestCommandResult> VerifyDomainControllerAsync(
        string vmName,
        V2RuntimeCredential domainAdminCredential,
        string expectedDomainName,
        CancellationToken cancellationToken)
    {
        return await _guestCommandExecutor.ExecutePowerShellDirectAsync(
            vmName,
            domainAdminCredential,
            FirstDomainControllerGuestScriptBuilder.BuildVerifyDomainControllerScript(expectedDomainName),
            cancellationToken);
    }

    public async Task<GuestCommandResult> ProbeDomainReadyAsync(
        string vmName,
        V2RuntimeCredential domainAdminCredential,
        string expectedDomainName,
        CancellationToken cancellationToken)
    {
        return await _guestCommandExecutor.ExecutePowerShellDirectAsync(
            vmName,
            domainAdminCredential,
            FirstDomainControllerGuestScriptBuilder.BuildDomainReadyProbeScript(expectedDomainName),
            cancellationToken);
    }

    public static bool IsExpectedRestartBoundaryError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return false;
        }

        return error.Contains("reboot", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("restart", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("shut down", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("WSMan", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("WinRM", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("RPC server is unavailable", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("I/O operation has been aborted", StringComparison.OrdinalIgnoreCase);
    }
}
