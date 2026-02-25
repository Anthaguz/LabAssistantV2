using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.PowerShell;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.PowerShell;
using Xunit;

namespace LabAssistant.Business.Tests.Tests;

public class MilestoneUScenarioMatrixTests
{
    [Fact]
    public async Task PreflightClassification_MixedResults_ComputesCountsAndBlockingCorrectly()
    {
        var service = new DeploymentPreflightService(
        [
            new InlineCheck("vhdx", 10, DeploymentPreflightMode.Full, _ =>
            [
                Result(DeploymentReadinessStatus.Fail, DeploymentReadinessCategory.VhdxBaseDisk, "VHDX.INVALID", @"D:\base\bad.vhdx", ["VM1"])
            ]),
            new InlineCheck("storage", 20, DeploymentPreflightMode.Full, _ =>
            [
                Result(DeploymentReadinessStatus.Warn, DeploymentReadinessCategory.DestinationPathStorage, "DST.STORAGE.LOW_FREE_SPACE", @"D:\labs", ["VM1"]),
                Result(DeploymentReadinessStatus.Warn, DeploymentReadinessCategory.DestinationPathStorage, "DST.STORAGE.FREE_SPACE_UNKNOWN", @"Z:\labs", ["VM2"])
            ]),
            new InlineCheck("env", 30, DeploymentPreflightMode.Full, _ =>
            [
                Result(DeploymentReadinessStatus.Pass, DeploymentReadinessCategory.Environment, "ENV.OK")
            ])
        ]);

        var report = await service.RunAsync(new MultiVmDeploymentContext
        {
            VmContexts =
            {
                new VmDeploymentContext { VmName = "VM1" },
                new VmDeploymentContext { VmName = "VM2" }
            }
        }, DeploymentPreflightMode.Full);

        Assert.Equal(4, report.Results.Count);
        Assert.Equal(1, report.Results.Count(r => r.Status == DeploymentReadinessStatus.Fail));
        Assert.Equal(2, report.Results.Count(r => r.Status == DeploymentReadinessStatus.Warn));
        Assert.Equal(1, report.Results.Count(r => r.Status == DeploymentReadinessStatus.Pass));
        Assert.True(report.HasBlockingFailures);
        Assert.True(report.HasWarnings);
        Assert.False(report.CanDeploy);
        Assert.Contains(report.Results, r => r.Category == DeploymentReadinessCategory.VhdxBaseDisk && r.Code == "VHDX.INVALID");
        Assert.Contains(report.Results, r => r.Category == DeploymentReadinessCategory.DestinationPathStorage && r.Code == "DST.STORAGE.LOW_FREE_SPACE");
    }

    [Fact]
    public async Task PreflightQuickVsFull_FullOnlyFailureCanBlockAfterCleanQuickReport()
    {
        var service = new DeploymentPreflightService(
        [
            new InlineCheck("quick-common", 10, DeploymentPreflightMode.Quick, _ =>
            [
                Result(DeploymentReadinessStatus.Pass, DeploymentReadinessCategory.TemplateConfig, "CFG.OK")
            ]),
            new InlineCheck("full-storage", 20, DeploymentPreflightMode.Full, _ =>
            [
                Result(DeploymentReadinessStatus.Fail, DeploymentReadinessCategory.DestinationPathStorage, "DST.VM_PATH.UNWRITABLE", @"D:\Labs\VM1", ["VM1"])
            ])
        ]);

        var context = new MultiVmDeploymentContext
        {
            VmContexts = { new VmDeploymentContext { VmName = "VM1" } }
        };

        var quick = await service.RunAsync(context, DeploymentPreflightMode.Quick);
        var full = await service.RunAsync(context, DeploymentPreflightMode.Full);

        Assert.False(quick.HasBlockingFailures);
        Assert.True(quick.CanDeploy);
        Assert.Equal(DeploymentPreflightMode.Quick, quick.Mode);

        Assert.True(full.HasBlockingFailures);
        Assert.False(full.CanDeploy);
        Assert.Equal(DeploymentPreflightMode.Full, full.Mode);
        Assert.Contains(full.Results, r => r.Code == "DST.VM_PATH.UNWRITABLE");
    }

    [Fact]
    public async Task PreflightWarningsOnly_LowAndUnknownSpace_DoNotBlockDeploy()
    {
        var service = new DeploymentPreflightService(
        [
            new InlineCheck("storage", 10, DeploymentPreflightMode.Full, _ =>
            [
                Result(DeploymentReadinessStatus.Warn, DeploymentReadinessCategory.DestinationPathStorage, "DST.STORAGE.LOW_FREE_SPACE", @"D:\labs", ["VM1"]),
                Result(DeploymentReadinessStatus.Warn, DeploymentReadinessCategory.DestinationPathStorage, "DST.STORAGE.FREE_SPACE_UNKNOWN", @"\\server\\share", ["VM2"])
            ])
        ]);

        var report = await service.RunAsync(new MultiVmDeploymentContext
        {
            VmContexts =
            {
                new VmDeploymentContext { VmName = "VM1" },
                new VmDeploymentContext { VmName = "VM2" }
            }
        }, DeploymentPreflightMode.Full);

        Assert.False(report.HasBlockingFailures);
        Assert.True(report.HasWarnings);
        Assert.True(report.CanDeploy);
        Assert.All(report.Results, r => Assert.Equal(DeploymentReadinessStatus.Warn, r.Status));
    }

    [Fact]
    public async Task RuntimeDiagnostics_CreateVhdFailure_EmitsPathContext_AndConciseMessage()
    {
        var handle = new PowerShellHandle();
        var resolver = new SingleSessionResolver(handle, new FakeSession());
        var hyperV = new FakeHyperVService { CreateVhdDifferencingResult = false };
        var events = new List<(string EventName, IReadOnlyDictionary<string, object?>? Context)>();
        var context = new VmDeploymentContext
        {
            VmId = Guid.NewGuid(),
            VmName = "VM1",
            BaseVhdPath = @"D:\Base\bad.vhdx",
            VhdPath = @"D:\Labs\VM1\VM1.vhdx",
            PowerShellHandle = handle,
            StructuredEventEmitter = (eventName, _, _, extra) => events.Add((eventName, extra))
        };

        var step = new CreateVhdStep(resolver, _ => hyperV);
        await step.ExecuteAsync(context);

        Assert.False(context.IsSuccess);
        Assert.NotNull(context.FailureMessage);
        Assert.Contains(context.BaseVhdPath, context.FailureMessage);
        Assert.DoesNotContain(Environment.NewLine, context.FailureMessage);

        var stepFailed = Assert.Single(events, e => e.EventName == "StepFailed");
        Assert.Equal(context.BaseVhdPath, stepFailed.Context?["parentVhdPath"]?.ToString());
        Assert.Equal(context.VhdPath, stepFailed.Context?["targetVhdPath"]?.ToString());
    }

    private static DeploymentReadinessCheckResult Result(
        DeploymentReadinessStatus status,
        DeploymentReadinessCategory category,
        string code,
        string? resourcePath = null,
        IReadOnlyList<string>? affectedVmNames = null)
    {
        return new DeploymentReadinessCheckResult
        {
            Status = status,
            Category = category,
            Code = code,
            Message = code,
            ActionableGuidance = "Follow guidance",
            ResourcePath = resourcePath,
            AffectedVmNames = affectedVmNames ?? []
        };
    }

    private sealed class InlineCheck(
        string key,
        int order,
        DeploymentPreflightMode supportedMode,
        Func<MultiVmDeploymentContext, IReadOnlyList<DeploymentReadinessCheckResult>> execute) : IDeploymentPreflightCheck
    {
        public string Key { get; } = key;
        public int Order { get; } = order;
        public bool SupportsMode(DeploymentPreflightMode mode) => mode == supportedMode;
        public Task<IReadOnlyList<DeploymentReadinessCheckResult>> ExecuteAsync(MultiVmDeploymentContext deploymentContext, DeploymentPreflightMode mode, CancellationToken cancellationToken = default)
            => Task.FromResult(execute(deploymentContext));
    }

    private sealed class SingleSessionResolver(PowerShellHandle handle, IPersistentPowerShellSession session) : ISessionResolver
    {
        public IPersistentPowerShellSession Resolve(PowerShellHandle requestHandle)
            => requestHandle.SessionId == handle.SessionId ? session : throw new KeyNotFoundException();
        public void RegisterSession(PowerShellHandle handle, IPersistentPowerShellSession session) { }
        public void RemoveSession(PowerShellHandle handle) { }
    }

    private sealed class FakeSession : IPersistentPowerShellSession
    {
        public Task<(string Output, string Error)> ExecuteAsync(string command) => Task.FromResult((string.Empty, string.Empty));
        public void Dispose() { }
    }

    private sealed class FakeHyperVService : IHyperVService
    {
        public bool CreateVhdDifferencingResult { get; set; } = true;
        public Task<bool> AddVirtualSwitchToVmAsync(string vmName, string switchName) => Task.FromResult(true);
        public Task<bool> CreateVhdDifferencingAsync(string parentDiskPath, string vhdPath) => Task.FromResult(CreateVhdDifferencingResult);
        public Task<bool> CreateVhdFixedSizeAsync(string vhdPath, long sizeBytes) => Task.FromResult(true);
        public Task<bool> CreateVmAsync(string vmName, string vmPath, string vhdPath, int memoryMb, int cpuCount) => Task.FromResult(true);
        public Task<bool> DisableVmCheckpointsAsync(string vmName) => Task.FromResult(true);
        public Task<bool> EnableGuestServicesAsync(string vmName) => Task.FromResult(true);
        public Task<List<string>> GetVirtualSwitchNamesAsync() => Task.FromResult(new List<string>());
        public Task<bool> IsVmRunningAsync(string vmName) => Task.FromResult(false);
        public Task<bool> RemoveVmAsync(string vmName) => Task.FromResult(true);
        public Task<bool> StartVmAsync(string vmName) => Task.FromResult(true);
        public Task<bool> StopVmAsync(string vmName) => Task.FromResult(true);
        public Task<bool> VmExistsAsync(string vmName) => Task.FromResult(false);
    }
}
