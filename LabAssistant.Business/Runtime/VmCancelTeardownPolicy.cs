using LabAssistant.Models.Deployment;

namespace LabAssistant.Business.Runtime;

/// <summary>
/// Single source of truth for "will the runtime tear this VM down on the cancel/failure path?".
///
/// A VM is torn down when it actually created host resources (a VM folder, a differencing disk, a Hyper-V
/// registration, or a started VM) AND one of the abort conditions holds: the VM itself failed, the VM was
/// cancelled mid-step, or the run as a whole is being aborted (a user cancel, or a stop-all VM failure that
/// requested cancellation via <see cref="MultiVmDeploymentContext.IsCancellationRequested"/>). The run-level
/// term is the finding-86 addition: a VM whose own per-VM steps all completed before the abort landed keeps
/// IsSuccess=true and WasCancelled=false, yet must still be torn down so it is not orphaned. A fully
/// successful run never reaches this with the cancellation flag set, so successful deployments stay intact.
///
/// Two call paths depend on this decision being identical: the terminal cleanup gate
/// (V2RuntimeCapabilityService.NeedsCleanup) that performs the teardown, and the forest-trust cleanup stage
/// (V2ForestTrustRuntimeStage) that must PREDICT which anchors teardown will remove so it can skip the moot
/// in-guest trust delete on an anchor about to be destroyed. The trust cleanup stage runs before VM teardown
/// in the same cancel path, so a divergence here silently reintroduces either a dangling trust (over-skip in
/// the stage) or a mid-reboot hang (under-skip). Consolidating both inline copies into this one predicate
/// (finding 88) removes that drift risk; the equivalence matrix test pins the truth table so any future change
/// to the rule fails CI unless both call paths move together.
/// </summary>
internal static class VmCancelTeardownPolicy
{
    /// <summary>
    /// Returns whether the runtime will tear down the given VM on the cancel/failure path.
    /// </summary>
    public static bool ShouldTearDown(VmDeploymentContext context, MultiVmDeploymentContext multiContext)
    {
        var createdResources = context.VmFolderCreated
            || context.DifferencingDiskCreated
            || context.VmRegistered
            || context.VmStarted;

        return createdResources
            && (!context.IsSuccess || context.WasCancelled || multiContext.IsCancellationRequested);
    }
}
