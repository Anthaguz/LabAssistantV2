using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using Xunit;

namespace LabAssistant.Business.Tests;

public class DeploymentOutcomeSummaryBuilderTests
{
    private readonly DeploymentOutcomeSummaryBuilder _builder = new();

    [Fact]
    public void Build_MapsVmContextsAndCleanupResults_ToPerVmSummaries()
    {
        var vm1 = new VmDeploymentContext { VmId = Guid.NewGuid(), VmName = "vm1", IsSuccess = true };
        var vm2 = new VmDeploymentContext { VmId = Guid.NewGuid(), VmName = "vm2", IsSuccess = false };
        vm2.MarkFailure("CreateVm", "Failed to create VM");
        vm2.CleanupResult = new VmCleanupResult
        {
            VmName = "vm2",
            Residuals =
            {
                new CleanupResidual { ResourceType = "vm-directory", Identifier = @"C:\vm\vm2", SuggestedAction = "Delete manually" }
            }
        };
        var multi = new MultiVmDeploymentContext
        {
            VmContexts = { vm1, vm2 }
        };
        multi.CompleteTerminalState(hasFailures: true, hasCleanupResiduals: true);

        var summary = _builder.Build(multi);

        Assert.Equal(2, summary.VmOutcomes.Count);
        var failed = Assert.Single(summary.VmOutcomes.Where(v => v.VmName == "vm2"));
        Assert.Equal(VmDeploymentOutcomeStatus.Failed, failed.Status);
        Assert.Equal(VmCleanupOutcomeStatus.Residuals, failed.Cleanup.Status);
        Assert.Equal(1, failed.Cleanup.ResidualCount);
        Assert.Equal("CreateVm", failed.FailureStepKey);
    }

    [Fact]
    public void Build_ProducesCorrectGlobalCountsAndTerminalState()
    {
        var vm1 = new VmDeploymentContext { VmId = Guid.NewGuid(), VmName = "ok", IsSuccess = true };
        var vm2 = new VmDeploymentContext { VmId = Guid.NewGuid(), VmName = "fail", IsSuccess = false };
        vm2.MarkFailure("StartVm", "Failed to start");
        vm2.CleanupResult = new VmCleanupResult { VmName = "fail" };
        var vm3 = new VmDeploymentContext { VmId = Guid.NewGuid(), VmName = "cancel", IsSuccess = true, WasCancelled = true };

        var multi = new MultiVmDeploymentContext
        {
            VmContexts = { vm1, vm2, vm3 }
        };
        multi.CompleteTerminalState(hasFailures: true, hasCleanupResiduals: false);

        var summary = _builder.Build(multi);

        Assert.Equal(DeploymentOperationState.Failed, summary.OperationState);
        Assert.Equal(3, summary.TotalVmCount);
        Assert.Equal(1, summary.SucceededVmCount);
        Assert.Equal(1, summary.FailedVmCount);
        Assert.Equal(1, summary.CancelledVmCount);
        Assert.Equal(1, summary.CleanupVmCount);
        Assert.Equal(0, summary.ResidualVmCount);
    }

    [Fact]
    public void Build_AggregatesResidualsAcrossVms()
    {
        var vm1 = new VmDeploymentContext { VmId = Guid.NewGuid(), VmName = "vm1", IsSuccess = false };
        vm1.MarkFailure("CreateVhd", "Failed");
        vm1.CleanupResult = new VmCleanupResult
        {
            VmName = "vm1",
            Residuals = { new CleanupResidual { ResourceType = "disk", Identifier = @"C:\a.vhdx", SuggestedAction = "Delete" } }
        };
        var vm2 = new VmDeploymentContext { VmId = Guid.NewGuid(), VmName = "vm2", IsSuccess = false };
        vm2.MarkFailure("CreateVm", "Failed");
        vm2.CleanupResult = new VmCleanupResult
        {
            VmName = "vm2",
            Residuals = { new CleanupResidual { ResourceType = "vm", Identifier = "vm2", SuggestedAction = "Remove" } }
        };

        var multi = new MultiVmDeploymentContext { VmContexts = { vm1, vm2 } };
        multi.CompleteTerminalState(hasFailures: true, hasCleanupResiduals: true);

        var summary = _builder.Build(multi);

        Assert.Equal(2, summary.Residuals.Count);
        Assert.Contains(summary.Residuals, r => r.VmName == "vm1" && r.Identifier == @"C:\a.vhdx");
        Assert.Contains(summary.Residuals, r => r.VmName == "vm2" && r.Identifier == "vm2");
    }

    [Fact]
    public void Build_MapsCancellationResidualTerminalState()
    {
        var vm = new VmDeploymentContext { VmId = Guid.NewGuid(), VmName = "vm1", IsSuccess = true, WasCancelled = true };
        vm.CleanupResult = new VmCleanupResult
        {
            VmName = "vm1",
            Residuals = { new CleanupResidual { ResourceType = "vm-directory", Identifier = @"C:\vm\vm1", SuggestedAction = "Delete" } }
        };

        var multi = new MultiVmDeploymentContext { VmContexts = { vm } };
        multi.RequestUserCancellation();
        multi.MarkCleanupInProgress();
        multi.CompleteTerminalState(hasFailures: false, hasCleanupResiduals: true);

        var summary = _builder.Build(multi);

        Assert.Equal(DeploymentOperationState.CancelledWithResiduals, summary.OperationState);
        Assert.Equal(1, summary.CancelledVmCount);
        Assert.Equal(1, summary.ResidualVmCount);
    }

    [Fact]
    public void Build_MapsGuestStepOutcomesIntoPerVmSummary()
    {
        var vm = new VmDeploymentContext { VmId = Guid.NewGuid(), VmName = "vm1", IsSuccess = true };
        vm.RecordGuestStepOutcome(DeploymentStepKeys.SetTimeZone, "Set Time Zone", GuestStepOutcomeResults.Executed, message: "done");
        vm.RecordGuestStepOutcome(DeploymentStepKeys.ConfigureNetworkInformation, "Configure Network Information", GuestStepOutcomeResults.Skipped, GuestStepSkipReasons.NotImplemented, "not implemented");

        var multi = new MultiVmDeploymentContext { VmContexts = { vm } };
        multi.CompleteTerminalState(hasFailures: false, hasCleanupResiduals: false);

        var summary = _builder.Build(multi);
        var vmSummary = Assert.Single(summary.VmOutcomes);

        Assert.Equal(2, vmSummary.GuestStepOutcomes.Count);
        Assert.Contains(vmSummary.GuestStepOutcomes, o => o.StepKey == DeploymentStepKeys.SetTimeZone && o.Result == GuestStepOutcomeResults.Executed);
        Assert.Contains(vmSummary.GuestStepOutcomes, o => o.StepKey == DeploymentStepKeys.ConfigureNetworkInformation && o.Result == GuestStepOutcomeResults.Skipped && o.SkipReason == GuestStepSkipReasons.NotImplemented);
    }
}
