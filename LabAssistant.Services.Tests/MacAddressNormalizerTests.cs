using LabAssistant.Services.PowerShell;
using Xunit;

namespace LabAssistant.Services.Tests;

public class MacAddressNormalizerTests
{
    [Theory]
    [InlineData("00-15-5D-AB-CD-EF", "00155DABCDEF")]
    [InlineData("00:15:5d:ab:cd:ef", "00155DABCDEF")]
    [InlineData("0015.5dab.cdef", "00155DABCDEF")]
    [InlineData("00 15 5d ab cd ef", "00155DABCDEF")]
    [InlineData("00155DABCDEF", "00155DABCDEF")]
    public void Normalize_StripsSeparatorsAndUppercases(string input, string expected)
    {
        Assert.Equal(expected, MacAddressNormalizer.NormalizeMacAddress(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_NullOrBlank_ReturnsEmpty(string? input)
    {
        Assert.Equal(string.Empty, MacAddressNormalizer.NormalizeMacAddress(input));
    }

    [Fact]
    public void AreEqual_TreatsDifferentSeparatorsAndCaseAsEqual()
    {
        Assert.True(MacAddressNormalizer.AreEqual("00-15-5D-AB-CD-EF", "00155dabcdef"));
    }

    [Fact]
    public void AreEqual_DistinctAddresses_AreNotEqual()
    {
        Assert.False(MacAddressNormalizer.AreEqual("00-15-5D-AB-CD-EF", "00-15-5D-AB-CD-01"));
    }
}
