using LabAssistant.Models.Deployment;

namespace LabAssistant.Business.Deployment;

/// <summary>
/// Validates completeness of enabled optional guest-step configuration before runtime execution.
/// Uses the Milestone U readiness channel (not template schema validation) so Deploy-page gating
/// can block enabled-but-misconfigured optional guest steps.
/// </summary>
public sealed class GuestStepConfigurationCompletenessPreflightCheck : IDeploymentPreflightCheck
{
    public string Key => "GuestStepConfigurationCompleteness";
    public int Order => 250;

    // In-memory checks only; safe and cheap for both quick and full preflight.
    public bool SupportsMode(DeploymentPreflightMode mode) => true;

    public Task<IReadOnlyList<DeploymentReadinessCheckResult>> ExecuteAsync(
        MultiVmDeploymentContext deploymentContext,
        DeploymentPreflightMode mode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deploymentContext);

        var results = new List<DeploymentReadinessCheckResult>();

        foreach (var vm in deploymentContext.VmContexts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            AddTimeZoneResult(vm, results);
            AddSoftwareResult(vm, results);
            AddRoleResult(vm, results);

            // Placeholder-only guest network visibility must not block readiness by itself (#230/#234).
            // We intentionally do not emit failures for ConfigureNetworkInformation placeholder state here.
        }

        return Task.FromResult<IReadOnlyList<DeploymentReadinessCheckResult>>(results);
    }

    private static void AddTimeZoneResult(VmDeploymentContext vm, ICollection<DeploymentReadinessCheckResult> results)
    {
        if (!IsTimeZoneEnabled(vm))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(vm.TimeZoneConfig?.TimeZoneId))
        {
            results.Add(CreateFailure(
                vm,
                "GST.TIMEZONE.MISSING_CONFIG",
                "time zone",
                "Time zone step is enabled but no time zone is selected.",
                "Select a time zone for this VM or disable the time zone step before deploying."));
            return;
        }

        results.Add(CreatePass(
            vm,
            "GST.TIMEZONE.CONFIG_OK",
            "time zone",
            "Time zone step configuration is complete."));
    }

    private static void AddSoftwareResult(VmDeploymentContext vm, ICollection<DeploymentReadinessCheckResult> results)
    {
        if (!IsSoftwareEnabled(vm))
        {
            return;
        }

        var packages = vm.SoftwareConfig?.Packages?.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToList();
        if (packages == null || packages.Count == 0)
        {
            results.Add(CreateFailure(
                vm,
                "GST.SOFTWARE.EMPTY_PACKAGE_LIST",
                "software install",
                "Software install step is enabled but no packages are configured.",
                "Add at least one software package for this VM or disable the software step before deploying."));
            return;
        }

        results.Add(CreatePass(
            vm,
            "GST.SOFTWARE.CONFIG_OK",
            "software install",
            $"Software install step has {packages.Count} configured package(s)."));
    }

    private static void AddRoleResult(VmDeploymentContext vm, ICollection<DeploymentReadinessCheckResult> results)
    {
        if (!IsRoleEnabled(vm))
        {
            return;
        }

        var roles = vm.RoleConfig?.Roles?.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).ToList();
        if (roles == null || roles.Count == 0)
        {
            results.Add(CreateFailure(
                vm,
                "GST.ROLE.MISSING_SELECTION",
                "role install",
                "Role install step is enabled but no roles are selected.",
                "Select one or more roles for this VM or disable the role step before deploying."));
            return;
        }

        results.Add(CreatePass(
            vm,
            "GST.ROLE.CONFIG_OK",
            "role install",
            $"Role install step has {roles.Count} selected role(s)."));
    }

    private static bool IsTimeZoneEnabled(VmDeploymentContext vm)
        => vm.ConfigureTimeZone || vm.TimeZoneConfig?.Enabled == true;

    private static bool IsSoftwareEnabled(VmDeploymentContext vm)
        => vm.InstallSoftware || vm.SoftwareConfig?.Enabled == true;

    private static bool IsRoleEnabled(VmDeploymentContext vm)
        => vm.InstallRole || vm.RoleConfig?.Enabled == true;

    private static DeploymentReadinessCheckResult CreateFailure(
        VmDeploymentContext vm,
        string code,
        string resourceName,
        string message,
        string guidance)
    {
        return new DeploymentReadinessCheckResult
        {
            Status = DeploymentReadinessStatus.Fail,
            Category = DeploymentReadinessCategory.TemplateConfig,
            Code = code,
            Message = PrefixVmName(vm, message),
            ActionableGuidance = guidance,
            AffectedVmNames = string.IsNullOrWhiteSpace(vm.VmName) ? [] : [vm.VmName],
            ResourceName = resourceName
        };
    }

    private static DeploymentReadinessCheckResult CreatePass(
        VmDeploymentContext vm,
        string code,
        string resourceName,
        string message)
    {
        return new DeploymentReadinessCheckResult
        {
            Status = DeploymentReadinessStatus.Pass,
            Category = DeploymentReadinessCategory.TemplateConfig,
            Code = code,
            Message = PrefixVmName(vm, message),
            ActionableGuidance = "No action required.",
            AffectedVmNames = string.IsNullOrWhiteSpace(vm.VmName) ? [] : [vm.VmName],
            ResourceName = resourceName
        };
    }

    private static string PrefixVmName(VmDeploymentContext vm, string message)
        => string.IsNullOrWhiteSpace(vm.VmName) ? message : $"VM '{vm.VmName}': {message}";
}
