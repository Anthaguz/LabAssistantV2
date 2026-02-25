using System.Collections.Concurrent;
using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.PowerShell;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.PowerShell;
using Xunit;

namespace LabAssistant.Business.Tests;

public class MultiVmDeploymentCoordinatorCancellationTests
{
    [Fact]
    public async Task Cancel_TransitionsToCancelling_ThenCancelled()
    {
        var builder = new FakePipelineBuilder(_ => new NoOpWaitStep());
        var cleanup = new FakeCleanupOrchestrator();
        var coordinator = CreateCoordinator(builder, cleanup);
        var context = new MultiVmDeploymentContext
        {
            VmContexts = { new VmDeploymentContext { VmName = "vm1" } }
        };
        var states = new List<DeploymentOperationState>();
        context.OperationStateChanged += (_, e) => states.Add(e.State);

        var deployTask = coordinator.DeployAllAsync(context);
        await Task.Delay(20);
        context.RequestUserCancellation();
        await deployTask;

        Assert.Contains(DeploymentOperationState.Cancelling, states);
        Assert.Equal(DeploymentOperationState.Cancelled, context.OperationState);
        Assert.Equal(0, cleanup.CallCount);
    }

    [Fact]
    public async Task Cancel_RunsCleanup_WhenResourcesExist()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder = new FakePipelineBuilder(_ => new ResourceCreatingWaitStep(entered, release));
        var cleanup = new FakeCleanupOrchestrator();
        var coordinator = CreateCoordinator(builder, cleanup);
        var vm = new VmDeploymentContext { VmName = "vm1" };
        var context = new MultiVmDeploymentContext { VmContexts = { vm } };

        var deployTask = coordinator.DeployAllAsync(context);
        await entered.Task;
        context.RequestUserCancellation();
        release.SetResult(true);
        await deployTask;

        Assert.Equal(1, cleanup.CallCount);
        Assert.NotNull(vm.CleanupResult);
    }

    [Fact]
    public async Task Cancel_WithCleanupResiduals_SetsCancelledWithResiduals()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder = new FakePipelineBuilder(_ => new ResourceCreatingWaitStep(entered, release));
        var cleanup = new FakeCleanupOrchestrator
        {
            ResultFactory = vm => new VmCleanupResult
            {
                VmName = vm.VmName,
                Residuals = { new CleanupResidual { ResourceType = "vm-directory", Identifier = vm.VmPath, SuggestedAction = "Delete manually" } }
            }
        };
        var coordinator = CreateCoordinator(builder, cleanup);
        var context = new MultiVmDeploymentContext
        {
            VmContexts = { new VmDeploymentContext { VmName = "vm1", VmPath = @"C:\vm\vm1", VhdPath = @"C:\vm\vm1\vm1.vhdx" } }
        };

        var deployTask = coordinator.DeployAllAsync(context);
        await entered.Task;
        context.RequestUserCancellation();
        release.SetResult(true);
        await deployTask;

        Assert.Equal(DeploymentOperationState.CancelledWithResiduals, context.OperationState);
    }

    [Fact]
    public async Task Cancel_DoesNotRunFurtherNonCleanupSteps_AfterBoundary()
    {
        var secondStepExecuted = 0;
        var firstStepEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowFirstStepToFinish = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder = new FakePipelineBuilder(_ =>
        {
            var first = new CancelAwareStep(firstStepEntered, allowFirstStepToFinish);
            var second = new CountStep(() => Interlocked.Increment(ref secondStepExecuted));
            first.SetNext(second);
            return first;
        });
        var cleanup = new FakeCleanupOrchestrator();
        var coordinator = CreateCoordinator(builder, cleanup);
        var context = new MultiVmDeploymentContext
        {
            VmContexts = { new VmDeploymentContext { VmName = "vm1" } }
        };

        var deployTask = coordinator.DeployAllAsync(context);
        await firstStepEntered.Task;
        context.RequestUserCancellation();
        allowFirstStepToFinish.SetResult(true);
        await deployTask;

        Assert.Equal(0, secondStepExecuted);
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

    private sealed class NoOpWaitStep : DeploymentStep
    {
        protected override async Task HandleAsync(VmDeploymentContext context)
        {
            await Task.Delay(50);
        }
    }

    private sealed class ResourceCreatingWaitStep : DeploymentStep
    {
        private readonly TaskCompletionSource<bool>? _entered;
        private readonly TaskCompletionSource<bool>? _release;

        public ResourceCreatingWaitStep()
        {
        }

        public ResourceCreatingWaitStep(TaskCompletionSource<bool> entered, TaskCompletionSource<bool> release)
        {
            _entered = entered;
            _release = release;
        }

        protected override async Task HandleAsync(VmDeploymentContext context)
        {
            context.VmFolderCreated = true;
            _entered?.TrySetResult(true);

            if (_release != null)
            {
                await _release.Task;
                return;
            }

            await Task.Delay(50);
        }
    }

    private sealed class CancelAwareStep : DeploymentStep
    {
        private readonly TaskCompletionSource<bool> _entered;
        private readonly TaskCompletionSource<bool> _allowFinish;

        public CancelAwareStep(TaskCompletionSource<bool> entered, TaskCompletionSource<bool> allowFinish)
        {
            _entered = entered;
            _allowFinish = allowFinish;
        }

        protected override async Task HandleAsync(VmDeploymentContext context)
        {
            _entered.TrySetResult(true);
            await _allowFinish.Task;
        }
    }

    private sealed class CountStep : DeploymentStep
    {
        private readonly Action _action;

        public CountStep(Action action)
        {
            _action = action;
        }

        protected override Task HandleAsync(VmDeploymentContext context)
        {
            _action();
            return Task.CompletedTask;
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
