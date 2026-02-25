using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Validation;
using LabAssistant.Services.HyperV;
using Xunit;

namespace LabAssistant.Business.Tests;

public class BaseVhdxIntegrityPreflightCheckTests
{
    [Fact]
    public async Task ExecuteAsync_FullMode_InvalidVhdx_ReturnsContractAlignedFailure()
    {
        var validator = new FakeVhdxIntegrityValidator
        {
            ResultFactory = (path, _) => new VhdxIntegrityValidationResult
            {
                Status = VhdxIntegrityStatus.Invalid,
                Path = path,
                Message = "Invalid"
            }
        };
        var check = new BaseVhdxIntegrityPreflightCheck(validator);
        var context = new MultiVmDeploymentContext
        {
            VmContexts =
            {
                new VmDeploymentContext { VmName = "vm-a", BaseVhdPath = @"C:\base\bad.vhdx" }
            }
        };

        var results = await check.ExecuteAsync(context, DeploymentPreflightMode.Full);
        var result = Assert.Single(results);

        Assert.Equal(DeploymentReadinessCategory.VhdxBaseDisk, result.Category);
        Assert.Equal(DeploymentReadinessStatus.Fail, result.Status);
        Assert.Equal("VHDX.INVALID", result.Code);
        Assert.Equal(@"C:\base\bad.vhdx", result.ResourcePath);
        Assert.Contains("vm-a", result.AffectedVmNames);
        Assert.Contains("valid Hyper-V VHDX", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_QuickMode_UsesAccessibilityOnlyDepth()
    {
        var validator = new FakeVhdxIntegrityValidator
        {
            ResultFactory = (path, depth) => new VhdxIntegrityValidationResult
            {
                Status = VhdxIntegrityStatus.Valid,
                Path = path,
                Message = depth == VhdxIntegrityValidationDepth.AccessibilityOnly ? "Accessible" : "Valid"
            }
        };
        var check = new BaseVhdxIntegrityPreflightCheck(validator);
        var context = new MultiVmDeploymentContext
        {
            VmContexts = { new VmDeploymentContext { VmName = "vm-a", BaseVhdPath = @"C:\base\good.vhdx" } }
        };

        var results = await check.ExecuteAsync(context, DeploymentPreflightMode.Quick);
        var result = Assert.Single(results);

        Assert.Equal(VhdxIntegrityValidationDepth.AccessibilityOnly, Assert.Single(validator.Calls).Depth);
        Assert.Equal(DeploymentReadinessStatus.Pass, result.Status);
        Assert.Equal("VHDX.ACCESSIBLE", result.Code);
    }

    [Fact]
    public async Task ExecuteAsync_MissingReference_ReturnsFailWithoutCallingValidator()
    {
        var validator = new FakeVhdxIntegrityValidator();
        var check = new BaseVhdxIntegrityPreflightCheck(validator);
        var context = new MultiVmDeploymentContext
        {
            VmContexts = { new VmDeploymentContext { VmName = "vm-a", BaseVhdPath = "" } }
        };

        var results = await check.ExecuteAsync(context, DeploymentPreflightMode.Full);
        var result = Assert.Single(results);

        Assert.Equal(DeploymentReadinessStatus.Fail, result.Status);
        Assert.Equal("VHDX.MISSING_REFERENCE", result.Code);
        Assert.Empty(validator.Calls);
    }

    private sealed class FakeVhdxIntegrityValidator : IVhdxIntegrityValidator
    {
        public List<(string Path, VhdxIntegrityValidationDepth Depth)> Calls { get; } = [];

        public Func<string, VhdxIntegrityValidationDepth, VhdxIntegrityValidationResult> ResultFactory { get; set; } =
            (path, _) => new VhdxIntegrityValidationResult { Status = VhdxIntegrityStatus.Valid, Path = path, Message = "OK" };

        public Task<VhdxIntegrityValidationResult> ValidateAsync(
            string path,
            VhdxIntegrityValidationDepth depth = VhdxIntegrityValidationDepth.Full,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((path, depth));
            return Task.FromResult(ResultFactory(path, depth));
        }
    }
}
