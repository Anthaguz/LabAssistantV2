using LabAssistant.Models.Validation;
using LabAssistant.Services.HyperV;
using Xunit;

namespace LabAssistant.Services.Tests;

public class VhdxIntegrityValidatorTests
{
    [Fact]
    public async Task ValidateAsync_WhenPathMissing_ReturnsMissing()
    {
        var validator = CreateValidator(
            new FakeFileAccessProbe(_ => new VhdxFileAccessProbeResult { Status = VhdxFileAccessStatus.Missing, Path = @"C:\missing.vhdx" }),
            new FakeHyperVVhdxProbe());

        var result = await validator.ValidateAsync(@"C:\missing.vhdx");

        Assert.Equal(VhdxIntegrityStatus.Missing, result.Status);
        Assert.Contains("not found", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_WhenPathUnreadable_ReturnsUnreadable()
    {
        var validator = CreateValidator(
            new FakeFileAccessProbe(_ => new VhdxFileAccessProbeResult
            {
                Status = VhdxFileAccessStatus.Unreadable,
                Path = @"C:\locked.vhdx",
                Detail = "Access denied"
            }),
            new FakeHyperVVhdxProbe());

        var result = await validator.ValidateAsync(@"C:\locked.vhdx");

        Assert.Equal(VhdxIntegrityStatus.Unreadable, result.Status);
        Assert.Contains("unreadable", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_WhenAccessibilityOnly_ValidatesWithoutHyperVProbe()
    {
        var hypervProbe = new FakeHyperVVhdxProbe
        {
            ResultFactory = _ => throw new InvalidOperationException("Should not be called in accessibility-only mode.")
        };
        var validator = CreateValidator(
            new FakeFileAccessProbe(_ => new VhdxFileAccessProbeResult { Status = VhdxFileAccessStatus.Accessible, Path = @"C:\base\good.vhdx" }),
            hypervProbe);

        var result = await validator.ValidateAsync(@"C:\base\good.vhdx", VhdxIntegrityValidationDepth.AccessibilityOnly);

        Assert.Equal(VhdxIntegrityStatus.Valid, result.Status);
        Assert.Equal(0, hypervProbe.Calls);
        Assert.Contains("accessible", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_WhenHyperVProbeSaysInvalid_ReturnsInvalid()
    {
        var validator = CreateValidator(
            new FakeFileAccessProbe(_ => new VhdxFileAccessProbeResult { Status = VhdxFileAccessStatus.Accessible, Path = @"C:\base\fake.vhdx" }),
            new FakeHyperVVhdxProbe
            {
                ResultFactory = path => new HyperVVhdxProbeResult
                {
                    Status = HyperVVhdxProbeStatus.Invalid,
                    Path = path,
                    Message = "Invalid VHDX",
                    Detail = "Not recognized"
                }
            });

        var result = await validator.ValidateAsync(@"C:\base\fake.vhdx");

        Assert.Equal(VhdxIntegrityStatus.Invalid, result.Status);
        Assert.Contains("valid Hyper-V VHDX", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_WhenHyperVProbeSaysValid_ReturnsValid()
    {
        var validator = CreateValidator(
            new FakeFileAccessProbe(_ => new VhdxFileAccessProbeResult { Status = VhdxFileAccessStatus.Accessible, Path = @"C:\base\good.vhdx" }),
            new FakeHyperVVhdxProbe
            {
                ResultFactory = path => new HyperVVhdxProbeResult
                {
                    Status = HyperVVhdxProbeStatus.Valid,
                    Path = path,
                    Message = "OK"
                }
            });

        var result = await validator.ValidateAsync(@"C:\base\good.vhdx");

        Assert.Equal(VhdxIntegrityStatus.Valid, result.Status);
        Assert.Contains("valid", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static VhdxIntegrityValidator CreateValidator(IVhdxFileAccessProbe fileAccessProbe, IHyperVVhdxProbe hyperVVhdxProbe)
        => new(fileAccessProbe, hyperVVhdxProbe);

    private sealed class FakeFileAccessProbe(Func<string, VhdxFileAccessProbeResult> factory) : IVhdxFileAccessProbe
    {
        public VhdxFileAccessProbeResult Probe(string path) => factory(path);
    }

    private sealed class FakeHyperVVhdxProbe : IHyperVVhdxProbe
    {
        public int Calls { get; private set; }
        public Func<string, HyperVVhdxProbeResult> ResultFactory { get; set; } = path => new HyperVVhdxProbeResult
        {
            Status = HyperVVhdxProbeStatus.Valid,
            Path = path,
            Message = "OK"
        };

        public Task<HyperVVhdxProbeResult> ProbeAsync(string path, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(ResultFactory(path));
        }
    }
}
