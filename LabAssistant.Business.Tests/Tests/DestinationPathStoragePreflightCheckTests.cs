using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using LabAssistant.Services.FileSystem;
using Xunit;

namespace LabAssistant.Business.Tests;

public class DestinationPathStoragePreflightCheckTests
{
    [Fact]
    public async Task ExecuteAsync_ValidPaths_FullMode_ReturnsPassResults()
    {
        var pathProbe = new FakeDestinationPathFeasibilityProbe();
        var freeSpace = new FakeFreeSpaceInfoProvider
        {
            ResultFactory = path => new FreeSpaceProbeResult
            {
                Status = FreeSpaceProbeStatus.Known,
                Path = path,
                RootPath = @"C:\",
                AvailableBytes = 50L * 1024 * 1024 * 1024,
                Message = "Known"
            }
        };
        var check = new DestinationPathStoragePreflightCheck(pathProbe, freeSpace);
        var context = CreateContext(("vm-a", @"C:\Labs\vm-a", @"C:\Labs\vm-a\vm-a.vhdx"));

        var results = await check.ExecuteAsync(context, DeploymentPreflightMode.Full);

        Assert.Contains(results, r => r.Code == "DST.VM_PATH.READY" && r.Status == DeploymentReadinessStatus.Pass);
        Assert.Contains(results, r => r.Code == "DST.VHD_PATH.READY" && r.Status == DeploymentReadinessStatus.Pass);
        Assert.Contains(results, r => r.Code == "DST.STORAGE.FREE_SPACE_OK" && r.Status == DeploymentReadinessStatus.Pass);
        Assert.All(results, r => Assert.Equal(DeploymentReadinessCategory.DestinationPathStorage, r.Category));
    }

    [Fact]
    public async Task ExecuteAsync_InvalidVmPath_ReturnsContractAlignedFailure()
    {
        var pathProbe = new FakeDestinationPathFeasibilityProbe
        {
            ResultFactory = (path, kind, _) =>
                kind == DestinationPathTargetKind.Directory
                    ? new DestinationPathFeasibilityResult
                    {
                        Status = DestinationPathFeasibilityStatus.Invalid,
                        Path = path ?? string.Empty,
                        Message = "Invalid"
                    }
                    : ValidPath(path)
        };
        var check = new DestinationPathStoragePreflightCheck(pathProbe, HighFreeSpaceProvider());
        var context = CreateContext(("vm-a", @"C:\<>bad", @"C:\Labs\vm-a\vm-a.vhdx"));

        var results = await check.ExecuteAsync(context, DeploymentPreflightMode.Full);
        var failure = Assert.Single(results, r => r.Code == "DST.VM_PATH.INVALID");

        Assert.Equal(DeploymentReadinessStatus.Fail, failure.Status);
        Assert.Equal(@"C:\<>bad", failure.ResourcePath);
        Assert.Contains("vm-a", failure.AffectedVmNames);
        Assert.False(string.IsNullOrWhiteSpace(failure.Message));
        Assert.False(string.IsNullOrWhiteSpace(failure.ActionableGuidance));
    }

    [Fact]
    public async Task ExecuteAsync_UnreachableRoot_ReturnsFail()
    {
        var pathProbe = new FakeDestinationPathFeasibilityProbe
        {
            ResultFactory = (path, kind, _) =>
                kind == DestinationPathTargetKind.File
                    ? new DestinationPathFeasibilityResult
                    {
                        Status = DestinationPathFeasibilityStatus.RootUnavailable,
                        Path = path ?? string.Empty,
                        RootPath = @"Z:\",
                        Message = "Root unavailable"
                    }
                    : ValidPath(path)
        };
        var check = new DestinationPathStoragePreflightCheck(pathProbe, HighFreeSpaceProvider());
        var context = CreateContext(("vm-a", @"C:\Labs\vm-a", @"Z:\Labs\vm-a\vm-a.vhdx"));

        var result = Assert.Single((await check.ExecuteAsync(context, DeploymentPreflightMode.Full))
            .Where(r => r.Code == "DST.VHD_PATH.ROOT_UNAVAILABLE"));

        Assert.Equal(DeploymentReadinessStatus.Fail, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_UnwritablePath_ReturnsFail()
    {
        var pathProbe = new FakeDestinationPathFeasibilityProbe
        {
            ResultFactory = (path, kind, _) =>
                kind == DestinationPathTargetKind.File
                    ? new DestinationPathFeasibilityResult
                    {
                        Status = DestinationPathFeasibilityStatus.Unwritable,
                        Path = path ?? string.Empty,
                        Message = "Unwritable"
                    }
                    : ValidPath(path)
        };
        var check = new DestinationPathStoragePreflightCheck(pathProbe, HighFreeSpaceProvider());
        var context = CreateContext(("vm-a", @"C:\Labs\vm-a", @"C:\Labs\vm-a\vm-a.vhdx"));

        var result = Assert.Single((await check.ExecuteAsync(context, DeploymentPreflightMode.Full))
            .Where(r => r.Code == "DST.VHD_PATH.UNWRITABLE"));

        Assert.Equal(DeploymentReadinessStatus.Fail, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_LowSpace_ReturnsWarn_NotFail()
    {
        var check = new DestinationPathStoragePreflightCheck(
            new FakeDestinationPathFeasibilityProbe(),
            new FakeFreeSpaceInfoProvider
            {
                ResultFactory = path => new FreeSpaceProbeResult
                {
                    Status = FreeSpaceProbeStatus.Known,
                    Path = path,
                    RootPath = @"C:\",
                    AvailableBytes = 1L * 1024 * 1024 * 1024,
                    Message = "Low"
                }
            });
        var context = CreateContext(("vm-a", @"C:\Labs\vm-a", @"C:\Labs\vm-a\vm-a.vhdx"));

        var result = Assert.Single((await check.ExecuteAsync(context, DeploymentPreflightMode.Full))
            .Where(r => r.Code == "DST.STORAGE.LOW_FREE_SPACE"));

        Assert.Equal(DeploymentReadinessStatus.Warn, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownSpace_ReturnsWarn_NotFail()
    {
        var check = new DestinationPathStoragePreflightCheck(
            new FakeDestinationPathFeasibilityProbe(),
            new FakeFreeSpaceInfoProvider
            {
                ResultFactory = path => new FreeSpaceProbeResult
                {
                    Status = FreeSpaceProbeStatus.Unknown,
                    Path = path,
                    RootPath = @"\\server\share\",
                    Message = "Unknown"
                }
            });
        var context = CreateContext(("vm-a", @"\\server\share\Labs\vm-a", @"\\server\share\Labs\vm-a\vm-a.vhdx"));

        var result = Assert.Single((await check.ExecuteAsync(context, DeploymentPreflightMode.Full))
            .Where(r => r.Code == "DST.STORAGE.FREE_SPACE_UNKNOWN"));

        Assert.Equal(DeploymentReadinessStatus.Warn, result.Status);
        Assert.Contains("vm-a", result.AffectedVmNames);
    }

    [Fact]
    public async Task ExecuteAsync_QuickVsFull_UsesExpectedProbeBehavior()
    {
        var pathProbe = new FakeDestinationPathFeasibilityProbe();
        var freeSpace = new FakeFreeSpaceInfoProvider();
        var check = new DestinationPathStoragePreflightCheck(pathProbe, freeSpace);
        var context = CreateContext(("vm-a", @"C:\Labs\vm-a", @"C:\Labs\vm-a\vm-a.vhdx"));

        var quickResults = await check.ExecuteAsync(context, DeploymentPreflightMode.Quick);
        var fullResults = await check.ExecuteAsync(context, DeploymentPreflightMode.Full);

        Assert.Contains(pathProbe.Calls, c => c.VerifyWriteAccess == false && c.TargetKind == DestinationPathTargetKind.Directory);
        Assert.Contains(pathProbe.Calls, c => c.VerifyWriteAccess == true && c.TargetKind == DestinationPathTargetKind.File);
        Assert.DoesNotContain(quickResults, r => r.Code.StartsWith("DST.STORAGE.", StringComparison.Ordinal));
        Assert.Contains(fullResults, r => r.Code.StartsWith("DST.STORAGE.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_DuplicatePathsAcrossVms_ReturnsBlockingFailure()
    {
        var check = new DestinationPathStoragePreflightCheck(new FakeDestinationPathFeasibilityProbe(), HighFreeSpaceProvider());
        var context = CreateContext(
            ("vm-a", @"C:\Labs\same", @"C:\Labs\same\vm-a.vhdx"),
            ("vm-b", @"C:\Labs\same", @"C:\Labs\vm-b\vm-b.vhdx"));

        var result = Assert.Single((await check.ExecuteAsync(context, DeploymentPreflightMode.Quick))
            .Where(r => r.Code == "DST.VM_PATH.DUPLICATE_IN_DEPLOYMENT"));

        Assert.Equal(DeploymentReadinessStatus.Fail, result.Status);
        Assert.Equal(["vm-a", "vm-b"], result.AffectedVmNames);
    }

    private static MultiVmDeploymentContext CreateContext(params (string VmName, string VmPath, string VhdPath)[] vms)
    {
        return new MultiVmDeploymentContext
        {
            VmContexts = vms.Select(v => new VmDeploymentContext
            {
                VmName = v.VmName,
                VmPath = v.VmPath,
                VhdPath = v.VhdPath
            }).ToList()
        };
    }

    private static DestinationPathFeasibilityResult ValidPath(string? path)
        => new()
        {
            Status = DestinationPathFeasibilityStatus.Valid,
            Path = path ?? string.Empty,
            RootPath = @"C:\",
            Message = "Valid"
        };

    private static FakeFreeSpaceInfoProvider HighFreeSpaceProvider()
        => new()
        {
            ResultFactory = path => new FreeSpaceProbeResult
            {
                Status = FreeSpaceProbeStatus.Known,
                Path = path,
                RootPath = @"C:\",
                AvailableBytes = 100L * 1024 * 1024 * 1024,
                Message = "Known"
            }
        };

    private sealed class FakeDestinationPathFeasibilityProbe : IDestinationPathFeasibilityProbe
    {
        public List<(string? Path, DestinationPathTargetKind TargetKind, bool VerifyWriteAccess)> Calls { get; } = [];

        public Func<string?, DestinationPathTargetKind, bool, DestinationPathFeasibilityResult> ResultFactory { get; set; } =
            (path, _, _) => ValidPath(path);

        public DestinationPathFeasibilityResult Probe(string? path, DestinationPathTargetKind targetKind, bool verifyWriteAccess)
        {
            Calls.Add((path, targetKind, verifyWriteAccess));
            return ResultFactory(path, targetKind, verifyWriteAccess);
        }
    }

    private sealed class FakeFreeSpaceInfoProvider : IFreeSpaceInfoProvider
    {
        public List<string?> Calls { get; } = [];
        public Func<string?, FreeSpaceProbeResult> ResultFactory { get; set; } =
            path => new FreeSpaceProbeResult
            {
                Status = FreeSpaceProbeStatus.Known,
                Path = path ?? string.Empty,
                RootPath = @"C:\",
                AvailableBytes = 50L * 1024 * 1024 * 1024,
                Message = "Known"
            };

        public FreeSpaceProbeResult Probe(string? path)
        {
            Calls.Add(path);
            return ResultFactory(path);
        }
    }
}
