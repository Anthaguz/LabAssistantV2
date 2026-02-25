using LabAssistant.Models.Deployment;
using LabAssistant.Models.Validation;
using LabAssistant.Services.HyperV;

namespace LabAssistant.Business.Deployment;

public sealed class BaseVhdxIntegrityPreflightCheck : IDeploymentPreflightCheck
{
    private readonly IVhdxIntegrityValidator _validator;

    public BaseVhdxIntegrityPreflightCheck(IVhdxIntegrityValidator validator)
    {
        _validator = validator;
    }

    public string Key => "BaseVhdxIntegrity";
    public int Order => 200;

    public bool SupportsMode(DeploymentPreflightMode mode) => true;

    public async Task<IReadOnlyList<DeploymentReadinessCheckResult>> ExecuteAsync(
        MultiVmDeploymentContext deploymentContext,
        DeploymentPreflightMode mode,
        CancellationToken cancellationToken = default)
    {
        var results = new List<DeploymentReadinessCheckResult>();
        var depth = mode == DeploymentPreflightMode.Full
            ? VhdxIntegrityValidationDepth.Full
            : VhdxIntegrityValidationDepth.AccessibilityOnly;

        foreach (var vm in deploymentContext.VmContexts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var path = ResolveBaseVhdPath(vm);
            if (string.IsNullOrWhiteSpace(path))
            {
                results.Add(new DeploymentReadinessCheckResult
                {
                    Status = DeploymentReadinessStatus.Fail,
                    Category = DeploymentReadinessCategory.VhdxBaseDisk,
                    Code = "VHDX.MISSING_REFERENCE",
                    Message = $"Base VHDX reference is missing for VM '{vm.VmName}'.",
                    ActionableGuidance = "Select a valid base VHDX for this VM before deploying.",
                    AffectedVmNames = [vm.VmName]
                });
                continue;
            }

            var integrity = await _validator.ValidateAsync(path, depth, cancellationToken);
            results.Add(MapResult(vm, integrity, mode));
        }

        return results;
    }

    private static string? ResolveBaseVhdPath(VmDeploymentContext vm)
    {
        if (!string.IsNullOrWhiteSpace(vm.BaseVhdPath))
        {
            return vm.BaseVhdPath;
        }

        if (!string.IsNullOrWhiteSpace(vm.VhdDifferencingParentPath))
        {
            return vm.VhdDifferencingParentPath;
        }

        return null;
    }

    private static DeploymentReadinessCheckResult MapResult(
        VmDeploymentContext vm,
        VhdxIntegrityValidationResult integrity,
        DeploymentPreflightMode mode)
    {
        var (status, code, message, guidance) = integrity.Status switch
        {
            VhdxIntegrityStatus.Valid when mode == DeploymentPreflightMode.Quick => (
                DeploymentReadinessStatus.Pass,
                "VHDX.ACCESSIBLE",
                $"Base VHDX path is accessible: {integrity.Path}",
                "No action required."),
            VhdxIntegrityStatus.Valid => (
                DeploymentReadinessStatus.Pass,
                "VHDX.VALID",
                $"Base VHDX is valid: {integrity.Path}",
                "No action required."),
            VhdxIntegrityStatus.Missing => (
                DeploymentReadinessStatus.Fail,
                "VHDX.MISSING",
                $"Base VHDX file not found: {integrity.Path}",
                "Update the base VHDX path or catalog entry to a valid file before deploying."),
            VhdxIntegrityStatus.Unreadable => (
                DeploymentReadinessStatus.Fail,
                "VHDX.UNREADABLE",
                $"Base VHDX is unreadable or inaccessible: {integrity.Path}",
                "Verify the file path, permissions, and that the disk is not locked or unavailable."),
            _ => (
                DeploymentReadinessStatus.Fail,
                "VHDX.INVALID",
                $"Selected base disk is not a valid Hyper-V VHDX: {integrity.Path}",
                "Select a valid base VHDX file or re-import a known-good base disk.")
        };

        return new DeploymentReadinessCheckResult
        {
            Status = status,
            Category = DeploymentReadinessCategory.VhdxBaseDisk,
            Code = code,
            Message = message,
            ActionableGuidance = guidance,
            AffectedVmNames = string.IsNullOrWhiteSpace(vm.VmName) ? [] : [vm.VmName],
            ResourcePath = integrity.Path,
            ResourceName = string.IsNullOrWhiteSpace(vm.VmName) ? null : vm.VmName
        };
    }
}
