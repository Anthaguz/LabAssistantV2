using System.Collections.Concurrent;
using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.PowerShell;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.PowerShell;
using Xunit;

namespace LabAssistant.Business.Tests;

public class MilestoneRScenarioMatrixTests
{
    private readonly DeploymentOutcomeSummaryBuilder _summaryBuilder = new();

    [Fact]
    public async Task Success_NoCleanup_ProducesCompletedSummary()
    {
        var coordinator = CreateCoordinator(new FakePipelineBuilder(_ => new NoOpStep()), new FakeCleanupOrchestrator());
        var multi = new MultiVmDeploymentContext
        {
            VmContexts =
            {
                new VmDeploymentContext { VmId = Guid.NewGuid(), VmName = "vm1" },
                new VmDeploymentContext { VmId = Guid.NewGuid(), VmName = "vm2" }
            }
        };

        await coordinator.DeployAllAsync(multi);
        var summary = _summaryBuilder.Build(multi);

        Assert.Equal(DeploymentOperationState.Completed, multi.OperationState);
        Assert.Equal(DeploymentOperationState.Completed, summary.OperationState);
        Assert.Equal(2, summary.TotalVmCount);
        Assert.Equal(2, summary.SucceededVmCount);
        Assert.Equal(0, summary.FailedVmCount);
        Assert.Equal(0, summary.CancelledVmCount);
        Assert.Equal(0, summary.CleanupVmCount);
        Assert.Empty(summary.Residuals);
    }

    [Fact]
    public async Task RuntimeFailure_CleanupSuccess_ProducesFailedSummary()
    {
        var cleanup = new FakeCleanupOrchestrator();
        var coordinator = CreateCoordinator(new FakePipelineBuilder(_ => new FailingResourceStep()), cleanup);
        var vm = new VmDeploymentContext { VmId = Guid.NewGuid(), VmName = "vm1", VmPath = @"C:\vm\vm1" };
        var multi = new MultiVmDeploymentContext { VmContexts = { vm } };

        await coordinator.DeployAllAsync(multi);
        var summary = _summaryBuilder.Build(multi);

        Assert.Equal(1, cleanup.CallCount);
        Assert.Equal(DeploymentOperationState.Failed, multi.OperationState);
        Assert.Equal(DeploymentOperationState.Failed, summary.OperationState);

        var outcome = Assert.Single(summary.VmOutcomes);
        Assert.Equal(VmDeploymentOutcomeStatus.Failed, outcome.Status);
        Assert.True(outcome.Cleanup.CleanupRan);
        Assert.Equal(VmCleanupOutcomeStatus.Succeeded, outcome.Cleanup.Status);
        Assert.Equal(1, summary.FailedVmCount);
        Assert.Equal(1, summary.CleanupVmCount);
        Assert.Equal(0, summary.ResidualVmCount);
        Assert.Empty(summary.Residuals);
    }

    [Fact]
    public async Task RuntimeFailure_CleanupResiduals_ProducesFailedWithResidualsSummary()
    {
        var cleanup = new FakeCleanupOrchestrator
        {
            ResultFactory = vm => new VmCleanupResult
            {
                VmName = vm.VmName,
                Residuals =
                {
                    new CleanupResidual
                    {
                        ResourceType = "differencing-disk",
                        Identifier = vm.VhdPath,
                        SuggestedAction = "Delete manually"
                    }
                }
            }
        };
        var coordinator = CreateCoordinator(new FakePipelineBuilder(_ => new FailingResourceStep()), cleanup);
        var vm = new VmDeploymentContext
        {
            VmId = Guid.NewGuid(),
            VmName = "vm1",
            VmPath = @"C:\vm\vm1",
            VhdPath = @"C:\vm\vm1\vm1.vhdx"
        };
        var multi = new MultiVmDeploymentContext { VmContexts = { vm } };

        await coordinator.DeployAllAsync(multi);
        var summary = _summaryBuilder.Build(multi);

        Assert.Equal(DeploymentOperationState.FailedWithResiduals, multi.OperationState);
        Assert.Equal(DeploymentOperationState.FailedWithResiduals, summary.OperationState);
        Assert.Equal(1, summary.FailedVmCount);
        Assert.Equal(1, summary.CleanupVmCount);
        Assert.Equal(1, summary.ResidualVmCount);
        Assert.Single(summary.Residuals);

        var outcome = Assert.Single(summary.VmOutcomes);
        Assert.Equal(VmCleanupOutcomeStatus.Residuals, outcome.Cleanup.Status);
        Assert.Equal(1, outcome.Cleanup.ResidualCount);
        Assert.Single(outcome.Residuals);
    }

    [Fact]
    public async Task UserCancellation_NoResources_ProducesCancelledSummary_AndNoCleanup()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanup = new FakeCleanupOrchestrator();
        var coordinator = CreateCoordinator(
            new FakePipelineBuilder(_ => new WaitForSignalStep(entered, release, createResource: false)),
            cleanup);
        var vm = new VmDeploymentContext { VmId = Guid.NewGuid(), VmName = "vm1" };
        var multi = new MultiVmDeploymentContext { VmContexts = { vm } };

        var deployTask = coordinator.DeployAllAsync(multi);
        await entered.Task;
        multi.RequestUserCancellation();
        release.SetResult(true);
        await deployTask;

        var summary = _summaryBuilder.Build(multi);

        Assert.Equal(0, cleanup.CallCount);
        Assert.Equal(DeploymentOperationState.Cancelled, multi.OperationState);
        Assert.Equal(DeploymentOperationState.Cancelled, summary.OperationState);
        Assert.Equal(1, summary.CancelledVmCount);
        Assert.Equal(0, summary.CleanupVmCount);
        Assert.Equal(VmDeploymentOutcomeStatus.Cancelled, Assert.Single(summary.VmOutcomes).Status);
    }

    [Fact]
    public async Task UserCancellation_WithResiduals_ProducesCancelledWithResidualsSummary()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanup = new FakeCleanupOrchestrator
        {
            ResultFactory = vm => new VmCleanupResult
            {
                VmName = vm.VmName,
                Residuals =
                {
                    new CleanupResidual
                    {
                        ResourceType = "vm-directory",
                        Identifier = vm.VmPath,
                        SuggestedAction = "Delete manually"
                    }
                }
            }
        };
        var coordinator = CreateCoordinator(
            new FakePipelineBuilder(_ => new WaitForSignalStep(entered, release, createResource: true)),
            cleanup);
        var vm = new VmDeploymentContext { VmId = Guid.NewGuid(), VmName = "vm1", VmPath = @"C:\vm\vm1" };
        var multi = new MultiVmDeploymentContext { VmContexts = { vm } };

        var deployTask = coordinator.DeployAllAsync(multi);
        await entered.Task;
        multi.RequestUserCancellation();
        release.SetResult(true);
        await deployTask;

        var summary = _summaryBuilder.Build(multi);

        Assert.Equal(DeploymentOperationState.CancelledWithResiduals, multi.OperationState);
        Assert.Equal(DeploymentOperationState.CancelledWithResiduals, summary.OperationState);
        Assert.Equal(1, summary.CancelledVmCount);
        Assert.Equal(1, summary.CleanupVmCount);
        Assert.Equal(1, summary.ResidualVmCount);
        Assert.Single(summary.Residuals);

        var outcome = Assert.Single(summary.VmOutcomes);
        Assert.Equal(VmDeploymentOutcomeStatus.Cancelled, outcome.Status);
        Assert.Equal(VmCleanupOutcomeStatus.Residuals, outcome.Cleanup.Status);
    }

    [Fact]
    public async Task StopAllPolicy_FailureCancelsOtherVm_AndSummaryShowsMixedOutcomes()
    {
        var vm2Entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanup = new FakeCleanupOrchestrator();
        var coordinator = CreateCoordinator(
            new FakePipelineBuilder(vm =>
                vm.VmName == "vm1"
                    ? new CoordinatedFailingStep(vm2Entered)
                    : new WaitForCancellationStep(vm2Entered)),
            cleanup);

        var vm1 = new VmDeploymentContext { VmId = Guid.NewGuid(), VmName = "vm1", VmPath = @"C:\vm\vm1" };
        var vm2 = new VmDeploymentContext { VmId = Guid.NewGuid(), VmName = "vm2", VmPath = @"C:\vm\vm2" };
        var multi = new MultiVmDeploymentContext
        {
            StopAllOnAnyVmFailure = true,
            VmContexts = { vm1, vm2 }
        };

        await coordinator.DeployAllAsync(multi);
        var summary = _summaryBuilder.Build(multi);

        Assert.Equal(DeploymentOperationState.Failed, multi.OperationState);
        Assert.Equal(DeploymentOperationState.Failed, summary.OperationState);
        Assert.Equal(1, summary.FailedVmCount);
        Assert.Equal(1, summary.CancelledVmCount);
        Assert.Equal(2, summary.CleanupVmCount);

        Assert.Contains(summary.VmOutcomes, o => o.VmName == "vm1" && o.Status == VmDeploymentOutcomeStatus.Failed);
        Assert.Contains(summary.VmOutcomes, o => o.VmName == "vm2" && o.Status == VmDeploymentOutcomeStatus.Cancelled);
    }

    private static MultiVmDeploymentCoordinator CreateCoordinator(
        IDeploymentPipelineBuilder pipelineBuilder,
        IVmCleanupOrchestrator cleanupOrchestrator)
    {
        return new MultiVmDeploymentCoordinator(
            new FakeSessionResolver(),
            pipelineBuilder,
            () => new FakeSession(),
            _ => new FakeHyperVService(),
            cleanupOrchestrator);
    }

    private sealed class FakePipelineBuilder : IDeploymentPipelineBuilder
    {
        private readonly Func<VmDeploymentContext, DeploymentStep> _factory;

        public FakePipelineBuilder(Func<VmDeploymentContext, DeploymentStep> factory)
        {
            _factory = factory;
        }

        public DeploymentStep Build(VmDeploymentContext context) => _factory(context);
    }

    private sealed class NoOpStep : DeploymentStep
    {
        protected override Task HandleAsync(VmDeploymentContext context) => Task.CompletedTask;
    }

    private sealed class FailingResourceStep : DeploymentStep
    {
        protected override Task HandleAsync(VmDeploymentContext context)
        {
            context.VmFolderCreated = true;
            context.MarkFailure("CreateVm", "Failed to create VM");
            return Task.CompletedTask;
        }
    }

    private sealed class WaitForSignalStep : DeploymentStep
    {
        private readonly TaskCompletionSource<bool> _entered;
        private readonly TaskCompletionSource<bool> _release;
        private readonly bool _createResource;

        public WaitForSignalStep(
            TaskCompletionSource<bool> entered,
            TaskCompletionSource<bool> release,
            bool createResource)
        {
            _entered = entered;
            _release = release;
            _createResource = createResource;
        }

        protected override async Task HandleAsync(VmDeploymentContext context)
        {
            if (_createResource)
            {
                context.VmFolderCreated = true;
            }

            _entered.TrySetResult(true);
            await _release.Task;
        }
    }

    private sealed class CoordinatedFailingStep : DeploymentStep
    {
        private readonly TaskCompletionSource<bool> _otherVmEntered;

        public CoordinatedFailingStep(TaskCompletionSource<bool> otherVmEntered)
        {
            _otherVmEntered = otherVmEntered;
        }

        protected override async Task HandleAsync(VmDeploymentContext context)
        {
            context.VmFolderCreated = true;
            await _otherVmEntered.Task;
            context.MarkFailure("CreateVm", "Primary VM failed");
        }
    }

    private sealed class WaitForCancellationStep : DeploymentStep
    {
        private readonly TaskCompletionSource<bool> _entered;

        public WaitForCancellationStep(TaskCompletionSource<bool> entered)
        {
            _entered = entered;
        }

        protected override async Task HandleAsync(VmDeploymentContext context)
        {
            context.VmFolderCreated = true;
            _entered.TrySetResult(true);

            while (context.ShouldAbort?.Invoke() != true)
            {
                await Task.Delay(5);
            }
        }
    }

    private sealed class FakeCleanupOrchestrator : IVmCleanupOrchestrator
    {
        public int CallCount { get; private set; }
        public Func<VmDeploymentContext, VmCleanupResult>? ResultFactory { get; set; }

        public Task<VmCleanupResult> CleanupAsync(VmDeploymentContext context, IHyperVService hyperVService)
        {
            CallCount++;
            var result = ResultFactory?.Invoke(context) ?? new VmCleanupResult { VmName = context.VmName };
            return Task.FromResult(result);
        }
    }

    private sealed class FakeSessionResolver : ISessionResolver
    {
        private readonly ConcurrentDictionary<Guid, IPersistentPowerShellSession> _sessions = new();

        public IPersistentPowerShellSession Resolve(PowerShellHandle handle) => _sessions[handle.SessionId];

        public void RegisterSession(PowerShellHandle handle, IPersistentPowerShellSession session) => _sessions[handle.SessionId] = session;

        public void RemoveSession(PowerShellHandle handle) => _sessions.TryRemove(handle.SessionId, out _);
    }

    private sealed class FakeSession : IPersistentPowerShellSession
    {
        public void Dispose()
        {
        }

        public Task<(string Output, string Error)> ExecuteAsync(string command) => Task.FromResult((string.Empty, string.Empty));
    }

    private sealed class FakeHyperVService : IHyperVService
    {
        public Task<bool> CreateVmAsync(string vmName, string vmPath, string vhdPath, int memoryMb, int cpuCount) => Task.FromResult(true);
        public Task<bool> EnableGuestServicesAsync(string vmName) => Task.FromResult(true);
        public Task<bool> StartVmAsync(string vmName) => Task.FromResult(true);
        public Task<bool> StopVmAsync(string vmName) => Task.FromResult(true);
        public Task<bool> VmExistsAsync(string vmName) => Task.FromResult(false);
        public Task<bool> IsVmRunningAsync(string vmName) => Task.FromResult(false);
        public Task<bool> RemoveVmAsync(string vmName) => Task.FromResult(true);
        public Task<bool> CreateVhdDifferencingAsync(string parentDiskPath, string vhdPath) => Task.FromResult(true);
        public Task<bool> CreateVhdFixedSizeAsync(string vhdPath, long sizeBytes) => Task.FromResult(true);
        public Task<bool> DisableVmCheckpointsAsync(string vmName) => Task.FromResult(true);
        public Task<List<string>> GetVirtualSwitchNamesAsync() => Task.FromResult(new List<string>());
        public Task<bool> AddVirtualSwitchToVmAsync(string vmName, string switchName) => Task.FromResult(true);
    }
}
