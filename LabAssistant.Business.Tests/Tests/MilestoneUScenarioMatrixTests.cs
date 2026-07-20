using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
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
}
