using LabAssistant.Models.Deployment;

namespace LabAssistant.Business.Deployment;

public sealed class DeploymentOutcomeSummaryBuilder : IDeploymentOutcomeSummaryBuilder
{
    public DeploymentOutcomeSummary Build(MultiVmDeploymentContext multiVmContext)
    {
        var vmOutcomes = new List<VmDeploymentOutcomeSummary>();
        var residuals = new List<DeploymentResidualSummaryItem>();

        foreach (var vm in multiVmContext.VmContexts)
        {
            var status = ResolveVmStatus(vm);
            var cleanupResult = vm.CleanupResult;
            var cleanupSummary = new VmCleanupOutcomeSummary
            {
                CleanupRan = cleanupResult != null,
                Status = cleanupResult == null
                    ? VmCleanupOutcomeStatus.NotNeeded
                    : cleanupResult.HasResiduals ? VmCleanupOutcomeStatus.Residuals : VmCleanupOutcomeStatus.Succeeded,
                StepCount = cleanupResult?.StepResults.Count ?? 0,
                ResidualCount = cleanupResult?.Residuals.Count ?? 0
            };

            if (cleanupResult != null)
            {
                foreach (var residual in cleanupResult.Residuals)
                {
                    residuals.Add(new DeploymentResidualSummaryItem
                    {
                        VmName = vm.VmName,
                        ResourceType = residual.ResourceType,
                        Identifier = residual.Identifier,
                        SuggestedAction = residual.SuggestedAction
                    });
                }
            }

            vmOutcomes.Add(new VmDeploymentOutcomeSummary
            {
                VmId = vm.VmId,
                VmName = vm.VmName,
                Status = status,
                Reason = vm.FailureMessage,
                FailureStepKey = vm.FailureStepKey,
                Cleanup = cleanupSummary,
                Residuals = cleanupResult?.Residuals.ToList() ?? [],
                GuestStepOutcomes = vm.GuestStepOutcomes.ToList()
            });
        }

        return new DeploymentOutcomeSummary
        {
            OperationState = multiVmContext.OperationState,
            TotalVmCount = vmOutcomes.Count,
            SucceededVmCount = vmOutcomes.Count(vm => vm.Status == VmDeploymentOutcomeStatus.Succeeded),
            FailedVmCount = vmOutcomes.Count(vm => vm.Status == VmDeploymentOutcomeStatus.Failed),
            CancelledVmCount = vmOutcomes.Count(vm => vm.Status == VmDeploymentOutcomeStatus.Cancelled),
            CleanupVmCount = vmOutcomes.Count(vm => vm.Cleanup.CleanupRan),
            ResidualVmCount = vmOutcomes.Count(vm => vm.Cleanup.ResidualCount > 0),
            VmOutcomes = vmOutcomes,
            Residuals = residuals
        };
    }

    private static VmDeploymentOutcomeStatus ResolveVmStatus(VmDeploymentContext vm)
    {
        if (!vm.IsSuccess)
        {
            return VmDeploymentOutcomeStatus.Failed;
        }

        if (vm.WasCancelled)
        {
            return VmDeploymentOutcomeStatus.Cancelled;
        }

        return VmDeploymentOutcomeStatus.Succeeded;
    }
}
