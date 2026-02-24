using System.Collections.Concurrent;
using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.PowerShell;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;
using Xunit;

namespace LabAssistant.Business.Tests;

public class MultiVmDeploymentCoordinatorStructuredLoggingTests
{
    [Fact]
    public async Task DeployAll_Success_EmitsCanonicalEvents_WithSharedOperationId()
    {
        var logger = new RecordingStructuredLogger();
        var coordinator = CreateCoordinator(new FakePipelineBuilder(_ => new NoOpStep()), new FakeCleanupOrchestrator(), logger);
        var multi = new MultiVmDeploymentContext
        {
            OperationId = "op-deploy-success",
            VmContexts = { new VmDeploymentContext { VmId = Guid.NewGuid(), VmName = "vm1" } }
        };

        await coordinator.DeployAllAsync(multi);

        Assert.Contains(logger.Events, e => e.Event == "DeployLabStarted" && e.OperationId == "op-deploy-success");
        Assert.Contains(logger.Events, e => e.Event == "VmDeployStarted" && e.OperationId == "op-deploy-success");
        Assert.Contains(logger.Events, e => e.Event == "StepStarted" && e.OperationId == "op-deploy-success");
        Assert.Contains(logger.Events, e => e.Event == "StepCompleted" && e.OperationId == "op-deploy-success");
        Assert.Contains(logger.Events, e => e.Event == "VmDeployCompleted" && e.OperationId == "op-deploy-success");
        Assert.Contains(logger.Events, e => e.Event == "DeployLabCompleted" && e.OperationId == "op-deploy-success");

        Assert.All(logger.Events, e => Assert.Equal("op-deploy-success", e.OperationId));
        var stepStarted = logger.Events.First(e => e.Event == "StepStarted");
        Assert.Equal("vm1", stepStarted.Context?["vmName"]?.ToString());
        Assert.False(string.IsNullOrWhiteSpace(stepStarted.Context?["stepKey"]?.ToString()));
    }

    [Fact]
    public async Task DeployAll_Failure_EmitsFailureAndCleanupEvents()
    {
        var logger = new RecordingStructuredLogger();
        var cleanup = new FakeCleanupOrchestrator
        {
            ResultFactory = vm =>
            {
                var result = new VmCleanupResult { VmName = vm.VmName };
                result.StepResults.Add(new CleanupStepResult
                {
                    Step = CleanupStepName.RemoveVmDirectory,
                    Status = CleanupStepStatus.Succeeded,
                    Target = vm.VmPath,
                    Message = "removed"
                });
                return result;
            }
        };
        var coordinator = CreateCoordinator(new FakePipelineBuilder(_ => new FailingStep()), cleanup, logger);
        var multi = new MultiVmDeploymentContext
        {
            OperationId = "op-deploy-fail",
            VmContexts = { new VmDeploymentContext { VmId = Guid.NewGuid(), VmName = "vm1", VmPath = @"C:\vm\vm1" } }
        };

        await coordinator.DeployAllAsync(multi);

        Assert.Contains(logger.Events, e => e.Event == "StepFailed" && e.OperationId == "op-deploy-fail");
        Assert.Contains(logger.Events, e => e.Event == "CleanupStarted" && e.OperationId == "op-deploy-fail");
        Assert.Contains(logger.Events, e => e.Event == "CleanupStepCompleted" && e.OperationId == "op-deploy-fail");
        Assert.Contains(logger.Events, e => e.Event == "CleanupCompleted" && e.OperationId == "op-deploy-fail");
        Assert.Contains(logger.Events, e => e.Event == "DeployLabFailed" && e.OperationId == "op-deploy-fail");
    }

    [Fact]
    public async Task DeployAll_UserCancellationWithResiduals_EmitsCancelledAndResidualEvents()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var logger = new RecordingStructuredLogger();
        var cleanup = new FakeCleanupOrchestrator
        {
            ResultFactory = vm =>
            {
                var result = new VmCleanupResult { VmName = vm.VmName };
                result.StepResults.Add(new CleanupStepResult
                {
                    Step = CleanupStepName.RemoveDifferencingDisk,
                    Status = CleanupStepStatus.Failed,
                    Target = vm.VhdPath,
                    Message = "access denied"
                });
                result.Residuals.Add(new CleanupResidual
                {
                    ResourceType = "differencing-disk",
                    Identifier = vm.VhdPath,
                    SuggestedAction = "Delete manually"
                });
                return result;
            }
        };
        var coordinator = CreateCoordinator(
            new FakePipelineBuilder(_ => new ResourceWaitStep(entered, release)),
            cleanup,
            logger);
        var multi = new MultiVmDeploymentContext
        {
            OperationId = "op-deploy-cancel",
            VmContexts = { new VmDeploymentContext { VmId = Guid.NewGuid(), VmName = "vm1", VmPath = @"C:\vm\vm1", VhdPath = @"C:\vm\vm1\vm1.vhdx" } }
        };

        var deployTask = coordinator.DeployAllAsync(multi);
        await entered.Task;
        multi.RequestUserCancellation();
        release.SetResult(true);
        await deployTask;

        Assert.Contains(logger.Events, e => e.Event == "CleanupStarted" && e.OperationId == "op-deploy-cancel");
        Assert.Contains(logger.Events, e => e.Event == "CleanupStepFailed" && e.OperationId == "op-deploy-cancel");
        Assert.Contains(logger.Events, e => e.Event == "CleanupResidualsDetected" && e.OperationId == "op-deploy-cancel");
        Assert.Contains(logger.Events, e => e.Event == "DeployLabCancelled" && e.OperationId == "op-deploy-cancel" && e.Result == "cancelled_with_residuals");
    }

    private static MultiVmDeploymentCoordinator CreateCoordinator(
        IDeploymentPipelineBuilder pipelineBuilder,
        IVmCleanupOrchestrator cleanupOrchestrator,
        IStructuredLogger logger)
    {
        return new MultiVmDeploymentCoordinator(
            new FakeSessionResolver(),
            pipelineBuilder,
            () => new FakeSession(),
            _ => new FakeHyperVService(),
            cleanupOrchestrator,
            logger);
    }

    private sealed class FakePipelineBuilder : IDeploymentPipelineBuilder
    {
        private readonly Func<VmDeploymentContext, DeploymentStep> _factory;

        public FakePipelineBuilder(Func<VmDeploymentContext, DeploymentStep> factory) => _factory = factory;

        public DeploymentStep Build(VmDeploymentContext context) => _factory(context);
    }

    private sealed class NoOpStep : DeploymentStep
    {
        protected override Task HandleAsync(VmDeploymentContext context) => Task.CompletedTask;
    }

    private sealed class FailingStep : DeploymentStep
    {
        protected override Task HandleAsync(VmDeploymentContext context)
        {
            context.VmFolderCreated = true;
            context.MarkFailure("CreateVm", "simulated failure");
            return Task.CompletedTask;
        }
    }

    private sealed class ResourceWaitStep : DeploymentStep
    {
        private readonly TaskCompletionSource<bool> _entered;
        private readonly TaskCompletionSource<bool> _release;

        public ResourceWaitStep(TaskCompletionSource<bool> entered, TaskCompletionSource<bool> release)
        {
            _entered = entered;
            _release = release;
        }

        protected override async Task HandleAsync(VmDeploymentContext context)
        {
            context.VmFolderCreated = true;
            _entered.TrySetResult(true);
            await _release.Task;
        }
    }

    private sealed class FakeCleanupOrchestrator : IVmCleanupOrchestrator
    {
        public Func<VmDeploymentContext, VmCleanupResult>? ResultFactory { get; set; }

        public Task<VmCleanupResult> CleanupAsync(VmDeploymentContext context, IHyperVService hyperVService)
            => Task.FromResult(ResultFactory?.Invoke(context) ?? new VmCleanupResult { VmName = context.VmName });
    }

    private sealed class RecordingStructuredLogger : IStructuredLogger
    {
        public List<StructuredLogEvent> Events { get; } = new();

        public void Log(StructuredLogEvent logEvent) => Events.Add(logEvent);

        public void Log(StructuredLogLevel level, string eventName, string operationId, string? result = null, IReadOnlyDictionary<string, object?>? context = null)
            => Events.Add(StructuredLogEvent.Create(level, eventName, operationId, result, context));
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
        public void Dispose() { }
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
