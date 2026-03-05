using System.Collections.Concurrent;
using System.Text.Json;
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
        Assert.All(logger.Events, AssertHasRequiredFields);
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
            VmContexts =
            {
                new VmDeploymentContext
                {
                    VmId = Guid.NewGuid(),
                    VmName = "vm1",
                    VmPath = @"C:\vm\vm1",
                    VhdPath = @"C:\vm\vm1\vm1.vhdx",
                    BaseVhdPath = @"D:\base\parent.vhdx"
                }
            }
        };

        await coordinator.DeployAllAsync(multi);

        Assert.Contains(logger.Events, e => e.Event == "StepFailed" && e.OperationId == "op-deploy-fail");
        Assert.Contains(logger.Events, e => e.Event == "CleanupStarted" && e.OperationId == "op-deploy-fail");
        Assert.Contains(logger.Events, e => e.Event == "CleanupStepCompleted" && e.OperationId == "op-deploy-fail");
        Assert.Contains(logger.Events, e => e.Event == "CleanupCompleted" && e.OperationId == "op-deploy-fail");
        Assert.Contains(logger.Events, e => e.Event == "DeployLabFailed" && e.OperationId == "op-deploy-fail");

        var stepFailed = logger.Events.First(e => e.Event == "StepFailed");
        Assert.Equal(@"C:\vm\vm1", stepFailed.Context?["vmPath"]?.ToString());
        Assert.Equal(@"C:\vm\vm1\vm1.vhdx", stepFailed.Context?["targetVhdPath"]?.ToString());
        Assert.Equal(@"D:\base\parent.vhdx", stepFailed.Context?["parentVhdPath"]?.ToString());
        Assert.Equal("VirtualizationException", stepFailed.Context?["exceptionType"]?.ToString());
        Assert.Equal("0x80070005", stepFailed.Context?["hresult"]?.ToString());
        Assert.Equal("OperationFailed", stepFailed.Context?["errorCode"]?.ToString());

        var vmFailed = logger.Events.First(e => e.Event == "VmDeployFailed");
        Assert.Equal(@"C:\vm\vm1", vmFailed.Context?["vmPath"]?.ToString());
        Assert.Equal(@"C:\vm\vm1\vm1.vhdx", vmFailed.Context?["targetVhdPath"]?.ToString());
        Assert.Equal(@"D:\base\parent.vhdx", vmFailed.Context?["parentVhdPath"]?.ToString());
        Assert.Equal("VirtualizationException", vmFailed.Context?["exceptionType"]?.ToString());
        Assert.Equal("0x80070005", vmFailed.Context?["hresult"]?.ToString());
        Assert.Equal("OperationFailed", vmFailed.Context?["errorCode"]?.ToString());

        var deployFailed = logger.Events.First(e => e.Event == "DeployLabFailed");
        Assert.Equal("vm1", deployFailed.Context?["failedVmName"]?.ToString());
        Assert.Equal(@"C:\vm\vm1", deployFailed.Context?["vmPath"]?.ToString());
        Assert.Equal(@"C:\vm\vm1\vm1.vhdx", deployFailed.Context?["targetVhdPath"]?.ToString());
        Assert.Equal(@"D:\base\parent.vhdx", deployFailed.Context?["parentVhdPath"]?.ToString());
        Assert.Equal("VirtualizationException", deployFailed.Context?["exceptionType"]?.ToString());
        Assert.Equal("0x80070005", deployFailed.Context?["hresult"]?.ToString());
        Assert.Equal("OperationFailed", deployFailed.Context?["errorCode"]?.ToString());
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

    [Fact]
    public async Task DeployAll_WithJsonLinesSink_WritesParseableCanonicalJsonl()
    {
        var logPath = Path.Combine(Path.GetTempPath(), $"labassistant-structured-{Guid.NewGuid():N}.jsonl");
        try
        {
            var sink = new JsonLinesLogEventSink(logPath);
            var logger = new StructuredLogger(new[] { sink });
            var coordinator = CreateCoordinator(new FakePipelineBuilder(_ => new FailingStep()), new FakeCleanupOrchestrator(), logger);
            var multi = new MultiVmDeploymentContext
            {
                OperationId = "op-jsonl",
                VmContexts = { new VmDeploymentContext { VmId = Guid.NewGuid(), VmName = "vm1", VmPath = @"C:\vm\vm1" } }
            };

            await coordinator.DeployAllAsync(multi);

            var lines = File.ReadAllLines(logPath);
            Assert.NotEmpty(lines);

            foreach (var line in lines)
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                Assert.True(root.TryGetProperty("ts", out _));
                Assert.True(root.TryGetProperty("level", out _));
                Assert.True(root.TryGetProperty("event", out _));
                Assert.True(root.TryGetProperty("operationId", out var op));
                Assert.Equal("op-jsonl", op.GetString());
            }
        }
        finally
        {
            if (File.Exists(logPath))
            {
                File.Delete(logPath);
            }
        }
    }

    [Fact]
    public async Task DeployAll_EmittedContexts_DoNotContainSecretFieldNames()
    {
        var logger = new RecordingStructuredLogger();
        var coordinator = CreateCoordinator(new FakePipelineBuilder(_ => new NoOpStep()), new FakeCleanupOrchestrator(), logger);
        var multi = new MultiVmDeploymentContext
        {
            OperationId = "op-redaction",
            VmContexts = { new VmDeploymentContext { VmId = Guid.NewGuid(), VmName = "vm1" } }
        };

        await coordinator.DeployAllAsync(multi);

        foreach (var logEvent in logger.Events)
        {
            if (logEvent.Context == null)
            {
                continue;
            }

            foreach (var pair in logEvent.Context)
            {
                Assert.DoesNotContain("password", pair.Key, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("token", pair.Key, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("secret", pair.Key, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public async Task DeployAll_EmitsStepStateUpdates_PerVmWithDeterministicSequence()
    {
        var logger = new RecordingStructuredLogger();
        var coordinator = CreateCoordinator(new FakePipelineBuilder(_ => new NoOpStep()), new FakeCleanupOrchestrator(), logger);
        var vm1Updates = new List<DeployStepStateUpdate>();
        var vm2Updates = new List<DeployStepStateUpdate>();
        var vm1 = new VmDeploymentContext
        {
            VmId = Guid.NewGuid(),
            VmName = "vm1",
            StepStateEmitter = update => vm1Updates.Add(update)
        };
        var vm2 = new VmDeploymentContext
        {
            VmId = Guid.NewGuid(),
            VmName = "vm2",
            StepStateEmitter = update => vm2Updates.Add(update)
        };
        var multi = new MultiVmDeploymentContext
        {
            OperationId = "op-step-state",
            VmContexts = { vm1, vm2 }
        };

        await coordinator.DeployAllAsync(multi);

        Assert.NotEmpty(vm1Updates);
        Assert.NotEmpty(vm2Updates);
        Assert.All(vm1Updates, update =>
        {
            Assert.Equal("op-step-state", update.OperationId);
            Assert.Equal(vm1.VmId, update.VmId);
            Assert.Equal("vm1", update.VmName);
        });
        Assert.All(vm2Updates, update =>
        {
            Assert.Equal("op-step-state", update.OperationId);
            Assert.Equal(vm2.VmId, update.VmId);
            Assert.Equal("vm2", update.VmName);
        });
        Assert.Equal([DeployStepState.Pending, DeployStepState.Running, DeployStepState.Succeeded], vm1Updates.Select(update => update.State).ToArray());
        Assert.Equal([DeployStepState.Pending, DeployStepState.Running, DeployStepState.Succeeded], vm2Updates.Select(update => update.State).ToArray());
        Assert.True(vm1Updates[0].Sequence < vm1Updates[1].Sequence && vm1Updates[1].Sequence < vm1Updates[2].Sequence);
        Assert.True(vm2Updates[0].Sequence < vm2Updates[1].Sequence && vm2Updates[1].Sequence < vm2Updates[2].Sequence);
    }

    private static void AssertHasRequiredFields(StructuredLogEvent logEvent)
    {
        Assert.False(string.IsNullOrWhiteSpace(logEvent.Ts));
        Assert.False(string.IsNullOrWhiteSpace(logEvent.Level));
        Assert.False(string.IsNullOrWhiteSpace(logEvent.Event));
        Assert.False(string.IsNullOrWhiteSpace(logEvent.OperationId));

        var parsedTs = DateTimeOffset.Parse(logEvent.Ts);
        Assert.Equal(TimeSpan.Zero, parsedTs.Offset);

        Assert.Contains(logEvent.Level, new[] { "debug", "info", "warn", "error" });
        Assert.Contains(logEvent.Event, CanonicalEventNames);
    }

    private static readonly HashSet<string> CanonicalEventNames =
    [
        "DeployLabStarted",
        "DeployLabCompleted",
        "DeployLabFailed",
        "DeployLabCancelled",
        "VmDeployStarted",
        "VmDeployCompleted",
        "VmDeployFailed",
        "StepStarted",
        "StepCompleted",
        "StepFailed",
        "CleanupStarted",
        "CleanupStepCompleted",
        "CleanupStepFailed",
        "CleanupCompleted",
        "CleanupResidualsDetected"
    ];

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
            context.MarkFailure("CreateVm", "simulated failure", new Dictionary<string, object?>
            {
                ["exceptionType"] = "VirtualizationException",
                ["hresult"] = "0x80070005",
                ["errorCode"] = "OperationFailed"
            });
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
