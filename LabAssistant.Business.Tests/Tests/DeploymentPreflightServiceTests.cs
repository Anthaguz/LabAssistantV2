using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using Xunit;

namespace LabAssistant.Business.Tests;

public class DeploymentPreflightServiceTests
{
    [Fact]
    public void DeploymentReadinessReport_ComputesBlockingWarningAndCanDeploySemantics()
    {
        var report = new DeploymentReadinessReport
        {
            Mode = DeploymentPreflightMode.Full,
            Results =
            [
                CreateResult(DeploymentReadinessStatus.Pass, DeploymentReadinessCategory.Environment, "ENV.OK"),
                CreateResult(DeploymentReadinessStatus.Warn, DeploymentReadinessCategory.DestinationPathStorage, "STORAGE.LOW_SPACE"),
                CreateResult(DeploymentReadinessStatus.Fail, DeploymentReadinessCategory.NetworkSwitch, "SWITCH.MISSING")
            ]
        };

        Assert.True(report.IsAuthoritative);
        Assert.True(report.HasWarnings);
        Assert.True(report.HasBlockingFailures);
        Assert.False(report.CanDeploy);
    }

    [Fact]
    public async Task RunAsync_UsesDeterministicOrdering_AndPreservesResultOrderWithinCheck()
    {
        var service = new DeploymentPreflightService(
        [
            new FakeCheck("z-check", 20, DeploymentPreflightMode.Full, ["Z.ONE"]),
            new FakeCheck("a-check", 10, DeploymentPreflightMode.Full, ["A.ONE", "A.TWO"]),
            new FakeCheck("b-check", 10, DeploymentPreflightMode.Full, ["B.ONE"])
        ]);

        var report = await service.RunAsync(new MultiVmDeploymentContext(), DeploymentPreflightMode.Full);

        Assert.Equal(DeploymentPreflightMode.Full, report.Mode);
        Assert.Equal(
            ["A.ONE", "A.TWO", "B.ONE", "Z.ONE"],
            report.Results.Select(r => r.Code).ToArray());
    }

    [Fact]
    public async Task RunAsync_AggregatesMixedStatuses_AndCalculatesCanDeploy()
    {
        var service = new DeploymentPreflightService(
        [
            new FakeCheck(
                "mixed",
                1,
                DeploymentPreflightMode.Full,
                [
                    ("ENV.OK", DeploymentReadinessStatus.Pass, DeploymentReadinessCategory.Environment),
                    ("STORAGE.LOW_SPACE", DeploymentReadinessStatus.Warn, DeploymentReadinessCategory.DestinationPathStorage),
                    ("VHDX.INVALID", DeploymentReadinessStatus.Fail, DeploymentReadinessCategory.VhdxBaseDisk)
                ])
        ]);

        var report = await service.RunAsync(new MultiVmDeploymentContext(), DeploymentPreflightMode.Full);

        Assert.Equal(3, report.Results.Count);
        Assert.True(report.HasWarnings);
        Assert.True(report.HasBlockingFailures);
        Assert.False(report.CanDeploy);

        var failure = Assert.Single(report.Results.Where(r => r.Status == DeploymentReadinessStatus.Fail));
        Assert.Equal("VHDX.INVALID", failure.Code);
        Assert.Equal(DeploymentReadinessCategory.VhdxBaseDisk, failure.Category);
    }

    [Fact]
    public async Task RunAsync_DistinguishesQuickAndFullModes()
    {
        var dualModeCheck = new RecordingModeCheck();
        var fullOnlyCheck = new FakeCheck("full-only", 2, DeploymentPreflightMode.Full, ["FULL.ONLY"]);
        var service = new DeploymentPreflightService([dualModeCheck, fullOnlyCheck]);

        var quickReport = await service.RunAsync(new MultiVmDeploymentContext(), DeploymentPreflightMode.Quick);
        var fullReport = await service.RunAsync(new MultiVmDeploymentContext(), DeploymentPreflightMode.Full);

        Assert.Equal([DeploymentPreflightMode.Quick, DeploymentPreflightMode.Full], dualModeCheck.SeenModes);
        Assert.Equal(["MODE.QUICK"], quickReport.Results.Select(r => r.Code).ToArray());
        Assert.Equal(["MODE.FULL", "FULL.ONLY"], fullReport.Results.Select(r => r.Code).ToArray());
        Assert.False(quickReport.IsAuthoritative);
        Assert.True(fullReport.IsAuthoritative);
    }

    [Fact]
    public async Task RunAsync_PreservesMachineReadableCodes()
    {
        var service = new DeploymentPreflightService(
        [
            new FakeCheck(
                "codes",
                1,
                DeploymentPreflightMode.Full,
                [
                    ("PATH.UNWRITABLE", DeploymentReadinessStatus.Fail, DeploymentReadinessCategory.DestinationPathStorage),
                    ("SWITCH.MISSING", DeploymentReadinessStatus.Fail, DeploymentReadinessCategory.NetworkSwitch)
                ])
        ]);

        var report = await service.RunAsync(new MultiVmDeploymentContext(), DeploymentPreflightMode.Full);

        Assert.Equal(["PATH.UNWRITABLE", "SWITCH.MISSING"], report.Results.Select(r => r.Code).ToArray());
        Assert.All(report.Results, result => Assert.Matches("^[A-Z0-9_.-]+$", result.Code));
    }

    [Fact]
    public async Task RunAsync_NormalizesOptionalFields_AndKeepsVmNames()
    {
        var service = new DeploymentPreflightService(
        [
            new FakeInlineCheck(_ =>
                [
                    new DeploymentReadinessCheckResult
                    {
                        Status = DeploymentReadinessStatus.Warn,
                        Category = DeploymentReadinessCategory.DestinationPathStorage,
                        Code = " STORAGE.LOW_SPACE ",
                        Message = " Low free space ",
                        ActionableGuidance = " Review host disk usage ",
                        AffectedVmNames = [" vm1 ", "", "vm2"],
                        ResourcePath = " C:\\labs ",
                        ResourceName = " Disk C "
                    }
                ])
        ]);

        var report = await service.RunAsync(new MultiVmDeploymentContext(), DeploymentPreflightMode.Full);
        var result = Assert.Single(report.Results);

        Assert.Equal("STORAGE.LOW_SPACE", result.Code);
        Assert.Equal("Low free space", result.Message);
        Assert.Equal("Review host disk usage", result.ActionableGuidance);
        Assert.Equal(["vm1", "vm2"], result.AffectedVmNames);
        Assert.Equal("C:\\labs", result.ResourcePath);
        Assert.Equal("Disk C", result.ResourceName);
    }

    private static DeploymentReadinessCheckResult CreateResult(
        DeploymentReadinessStatus status,
        DeploymentReadinessCategory category,
        string code)
    {
        return new DeploymentReadinessCheckResult
        {
            Status = status,
            Category = category,
            Code = code,
            Message = code,
            ActionableGuidance = "Fix and retry."
        };
    }

    private sealed class FakeCheck : IDeploymentPreflightCheck
    {
        private readonly DeploymentPreflightMode _supportedMode;
        private readonly IReadOnlyList<(string Code, DeploymentReadinessStatus Status, DeploymentReadinessCategory Category)> _results;

        public FakeCheck(string key, int order, DeploymentPreflightMode supportedMode, IReadOnlyList<string> codes)
            : this(key, order, supportedMode, codes.Select(code => (code, DeploymentReadinessStatus.Pass, DeploymentReadinessCategory.TemplateConfig)).ToArray())
        {
        }

        public FakeCheck(
            string key,
            int order,
            DeploymentPreflightMode supportedMode,
            IReadOnlyList<(string Code, DeploymentReadinessStatus Status, DeploymentReadinessCategory Category)> results)
        {
            Key = key;
            Order = order;
            _supportedMode = supportedMode;
            _results = results;
        }

        public string Key { get; }
        public int Order { get; }

        public bool SupportsMode(DeploymentPreflightMode mode) => mode == _supportedMode;

        public Task<IReadOnlyList<DeploymentReadinessCheckResult>> ExecuteAsync(
            MultiVmDeploymentContext deploymentContext,
            DeploymentPreflightMode mode,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<DeploymentReadinessCheckResult> results = _results
                .Select(r => new DeploymentReadinessCheckResult
                {
                    Status = r.Status,
                    Category = r.Category,
                    Code = r.Code,
                    Message = $"{r.Code} message",
                    ActionableGuidance = "Fix and retry."
                })
                .ToList();

            return Task.FromResult(results);
        }
    }

    private sealed class RecordingModeCheck : IDeploymentPreflightCheck
    {
        public string Key => "mode-recorder";
        public int Order => 1;
        public List<DeploymentPreflightMode> SeenModes { get; } = [];

        public bool SupportsMode(DeploymentPreflightMode mode) => true;

        public Task<IReadOnlyList<DeploymentReadinessCheckResult>> ExecuteAsync(
            MultiVmDeploymentContext deploymentContext,
            DeploymentPreflightMode mode,
            CancellationToken cancellationToken = default)
        {
            SeenModes.Add(mode);

            IReadOnlyList<DeploymentReadinessCheckResult> results =
            [
                new DeploymentReadinessCheckResult
                {
                    Status = DeploymentReadinessStatus.Pass,
                    Category = DeploymentReadinessCategory.TemplateConfig,
                    Code = mode == DeploymentPreflightMode.Quick ? "MODE.QUICK" : "MODE.FULL",
                    Message = "Mode marker",
                    ActionableGuidance = "None"
                }
            ];

            return Task.FromResult(results);
        }
    }

    private sealed class FakeInlineCheck(Func<DeploymentPreflightMode, IReadOnlyList<DeploymentReadinessCheckResult>> factory) : IDeploymentPreflightCheck
    {
        public string Key => "inline";
        public int Order => 1;
        public bool SupportsMode(DeploymentPreflightMode mode) => true;

        public Task<IReadOnlyList<DeploymentReadinessCheckResult>> ExecuteAsync(
            MultiVmDeploymentContext deploymentContext,
            DeploymentPreflightMode mode,
            CancellationToken cancellationToken = default)
            => Task.FromResult(factory(mode));
    }
}
