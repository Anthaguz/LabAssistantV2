using LabAssistant.Business.Machines;
using Xunit;

namespace LabAssistant.Business.Tests;

/// <summary>
/// Unit coverage for the Hyper-V-free rename validation policy (F08).
/// </summary>
public class MachineNameValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenNameBlank_IsInvalid(string? proposed)
    {
        var result = MachineNameValidator.Validate(proposed);

        Assert.False(result.IsValid);
        Assert.Equal("Enter a name for the virtual machine.", result.ErrorMessage);
    }

    [Fact]
    public void Validate_WhenNameTooLong_IsInvalid()
    {
        var proposed = new string('a', MachineNameValidator.MaxNameLength + 1);

        var result = MachineNameValidator.Validate(proposed);

        Assert.False(result.IsValid);
        Assert.Contains("characters or fewer", result.ErrorMessage);
    }

    [Fact]
    public void Validate_WhenNameHasControlChars_IsInvalid()
    {
        var result = MachineNameValidator.Validate("bad\tname");

        Assert.False(result.IsValid);
        Assert.Equal("The name cannot contain control characters.", result.ErrorMessage);
    }

    [Fact]
    public void Validate_WhenNameMatchesCurrent_IsInvalid()
    {
        var result = MachineNameValidator.Validate("  LabVm01 ", "LabVm01");

        Assert.False(result.IsValid);
        Assert.Equal("Enter a name different from the current one.", result.ErrorMessage);
    }

    [Fact]
    public void Validate_WhenNameValid_TrimsAndReturnsNormalized()
    {
        var result = MachineNameValidator.Validate("  NewLabVm  ", "LabVm01");

        Assert.True(result.IsValid);
        Assert.Equal("NewLabVm", result.NormalizedName);
        Assert.Null(result.ErrorMessage);
    }
}
