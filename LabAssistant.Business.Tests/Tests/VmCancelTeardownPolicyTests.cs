using LabAssistant.Business.Runtime;
using LabAssistant.Models.Deployment;
using Xunit;

namespace LabAssistant.Business.Tests.Tests;

/// <summary>
/// Pins the shared cancel-teardown truth table (finding 88). Both the terminal cleanup gate
/// (V2RuntimeCapabilityService.NeedsCleanup) and the forest-trust teardown prediction
/// (V2ForestTrustRuntimeStage.AnchorBeingTornDown) now delegate to
/// <see cref="VmCancelTeardownPolicy.ShouldTearDown"/>, so this single matrix locks both call paths:
/// any future change to the rule that would let them drift fails here in CI.
/// </summary>
public sealed class VmCancelTeardownPolicyTests
{
    private static VmDeploymentContext Context(
        bool vmFolderCreated = false,
        bool differencingDiskCreated = false,
        bool vmRegistered = false,
        bool vmStarted = false,
        bool isSuccess = true,
        bool wasCancelled = false)
        => new()
        {
            VmName = "vm",
            VmFolderCreated = vmFolderCreated,
            DifferencingDiskCreated = differencingDiskCreated,
            VmRegistered = vmRegistered,
            VmStarted = vmStarted,
            IsSuccess = isSuccess,
            WasCancelled = wasCancelled,
        };

    private static MultiVmDeploymentContext MultiContext(bool cancellationRequested)
    {
        var multi = new MultiVmDeploymentContext();
        if (cancellationRequested)
        {
            multi.RequestCancellation();
        }

        return multi;
    }

    // Abort matrix over a VM that DID create a resource (VmStarted): the full truth table of
    // (isSuccess, wasCancelled, cancellationRequested). Expected values are hardcoded, not recomputed
    // from the predicate, so a rule change is caught rather than silently mirrored.
    [Theory]
    [InlineData(true, false, false, false)]  // green run, run not aborted -> intact
    [InlineData(false, false, false, true)]  // VM failed
    [InlineData(true, true, false, true)]    // VM cancelled mid-step
    [InlineData(true, false, true, true)]    // finding-86: run-level abort of an otherwise-successful VM
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(true, true, true, true)]
    [InlineData(false, true, true, true)]
    public void ShouldTearDown_AbortMatrix_OverCreatedResource(
        bool isSuccess,
        bool wasCancelled,
        bool cancellationRequested,
        bool expected)
    {
        var context = Context(vmStarted: true, isSuccess: isSuccess, wasCancelled: wasCancelled);
        var multi = MultiContext(cancellationRequested);

        Assert.Equal(expected, VmCancelTeardownPolicy.ShouldTearDown(context, multi));
    }

    // Any one of the four created-resource flags counts as "created"; none set means nothing to tear down.
    // Held under a fixed abort condition (VM failed) so the gate is the only variable.
    [Theory]
    [InlineData(true, false, false, false, true)]
    [InlineData(false, true, false, false, true)]
    [InlineData(false, false, true, false, true)]
    [InlineData(false, false, false, true, true)]
    [InlineData(true, true, true, true, true)]
    [InlineData(false, false, false, false, false)]
    public void ShouldTearDown_CreatedResourceGate(
        bool vmFolderCreated,
        bool differencingDiskCreated,
        bool vmRegistered,
        bool vmStarted,
        bool expected)
    {
        var context = Context(
            vmFolderCreated: vmFolderCreated,
            differencingDiskCreated: differencingDiskCreated,
            vmRegistered: vmRegistered,
            vmStarted: vmStarted,
            isSuccess: false);

        Assert.Equal(expected, VmCancelTeardownPolicy.ShouldTearDown(context, MultiContext(cancellationRequested: false)));
    }

    // A VM that created no host resources is never torn down, no matter which abort condition holds.
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]  // isSuccess flipped false
    [InlineData(false, true, false)]  // wasCancelled
    [InlineData(false, false, true)]  // run-level cancellation
    public void ShouldTearDown_NoCreatedResource_NeverTornDown(
        bool failed,
        bool wasCancelled,
        bool cancellationRequested)
    {
        var context = Context(isSuccess: !failed, wasCancelled: wasCancelled);

        Assert.False(VmCancelTeardownPolicy.ShouldTearDown(context, MultiContext(cancellationRequested)));
    }

    [Fact]
    public void ShouldTearDown_FullySuccessfulRun_LeavesVmIntact()
    {
        var context = Context(vmFolderCreated: true, differencingDiskCreated: true, vmRegistered: true, vmStarted: true);

        Assert.False(VmCancelTeardownPolicy.ShouldTearDown(context, MultiContext(cancellationRequested: false)));
    }

    [Fact]
    public void ShouldTearDown_RunLevelCancelOfSuccessfulVm_TearsItDown()
    {
        // finding 86: a VM whose own steps all finished (IsSuccess=true, WasCancelled=false) before a cross-VM
        // stage was cancelled must still be torn down so it is not orphaned.
        var context = Context(vmRegistered: true, vmStarted: true);

        Assert.True(VmCancelTeardownPolicy.ShouldTearDown(context, MultiContext(cancellationRequested: true)));
    }
}
