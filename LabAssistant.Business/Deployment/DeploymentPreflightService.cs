using LabAssistant.Models.Deployment;

namespace LabAssistant.Business.Deployment;

public sealed class DeploymentPreflightService : IDeploymentPreflightService
{
    private readonly IReadOnlyList<IDeploymentPreflightCheck> _checks;

    public DeploymentPreflightService(IEnumerable<IDeploymentPreflightCheck> checks)
    {
        _checks = (checks ?? throw new ArgumentNullException(nameof(checks)))
            .OrderBy(check => check.Order)
            .ThenBy(check => check.Key, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<DeploymentReadinessReport> RunAsync(
        MultiVmDeploymentContext deploymentContext,
        DeploymentPreflightMode mode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deploymentContext);

        var orderedResults = new List<DeploymentReadinessCheckResult>();

        foreach (var check in _checks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!check.SupportsMode(mode))
            {
                continue;
            }

            var checkResults = await check.ExecuteAsync(deploymentContext, mode, cancellationToken);
            if (checkResults == null)
            {
                continue;
            }

            foreach (var result in checkResults)
            {
                if (result == null)
                {
                    continue;
                }

                orderedResults.Add(Normalize(result));
            }
        }

        return new DeploymentReadinessReport
        {
            Mode = mode,
            Results = orderedResults
        };
    }

    private static DeploymentReadinessCheckResult Normalize(DeploymentReadinessCheckResult result)
    {
        var code = result.Code?.Trim() ?? string.Empty;
        var message = result.Message?.Trim() ?? string.Empty;
        var guidance = result.ActionableGuidance?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("Preflight check result code is required.");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new InvalidOperationException($"Preflight check result message is required for code '{code}'.");
        }

        if (string.IsNullOrWhiteSpace(guidance))
        {
            throw new InvalidOperationException($"Preflight check actionable guidance is required for code '{code}'.");
        }

        return new DeploymentReadinessCheckResult
        {
            Status = result.Status,
            Category = result.Category,
            Code = code,
            Message = message,
            ActionableGuidance = guidance,
            AffectedVmNames = result.AffectedVmNames?.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).ToList() ?? [],
            ResourcePath = string.IsNullOrWhiteSpace(result.ResourcePath) ? null : result.ResourcePath.Trim(),
            ResourceName = string.IsNullOrWhiteSpace(result.ResourceName) ? null : result.ResourceName.Trim()
        };
    }
}
