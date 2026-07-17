using LabAssistant.Services.Diagnostics;
using Xunit;

namespace LabAssistant.Services.Tests;

public class StatusCodeCatalogTests
{
    [Fact]
    public void Catalog_IsNotEmpty()
    {
        Assert.NotEmpty(StatusCodeCatalog.All);
        Assert.NotEmpty(StatusCodeCatalog.Facilities);
    }

    [Fact]
    public void Catalog_HasNoDuplicateCodes()
    {
        var codes = StatusCodeCatalog.All.Select(descriptor => descriptor.Code).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    [Fact]
    public void EveryDescriptor_DecomposesConsistentlyWithItsCode()
    {
        foreach (var descriptor in StatusCodeCatalog.All)
        {
            Assert.Equal(StatusCodes.SeverityOf(descriptor.Code), descriptor.Severity);
            Assert.Equal(StatusCodes.FlagsOf(descriptor.Code), descriptor.Flags);
            Assert.Equal(StatusCodes.FacilityOf(descriptor.Code), descriptor.Facility);
            Assert.Equal(StatusCodes.OperationOf(descriptor.Code), descriptor.Operation);
            Assert.Equal(StatusCodes.StatusOf(descriptor.Code), descriptor.Status);
        }
    }

    [Fact]
    public void EveryDescriptor_ProjectsLevelConsistentlyWithSeverity()
    {
        foreach (var descriptor in StatusCodeCatalog.All)
        {
            Assert.Equal(StatusCodes.LevelForSeverity(descriptor.Severity), descriptor.Level);
        }
    }

    [Fact]
    public void EveryDescriptor_HasAResolvableFacility()
    {
        foreach (var descriptor in StatusCodeCatalog.All)
        {
            var facility = StatusCodeCatalog.FacilityFor(descriptor.Facility);
            Assert.NotNull(facility);
            Assert.Equal(descriptor.FacilityName, facility!.Name);
        }
    }

    [Fact]
    public void Describe_ResolvesKnownCode()
    {
        var descriptor = StatusCodes.Describe(LaStatus.DeployGuest_GuestCredentialRejected);

        Assert.NotNull(descriptor);
        Assert.Equal("deploy.guest", descriptor!.FacilityName);
        Assert.Equal("transport-ready", descriptor.OperationName);
        Assert.Equal(StatusCodeSeverity.Error, descriptor.Severity);
        Assert.True(descriptor.Flags.HasFlag(StatusCodeFlags.UserActionable));
        Assert.Equal(StatusCodePhase.End, descriptor.Phase);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Remediation));
    }

    [Fact]
    public void Describe_ReturnsNullForUnknownCode()
    {
        Assert.Null(StatusCodes.Describe(0xFFFFFFFFu));
    }

    [Theory]
    [InlineData(0x64410103u)] // Error | UserActionable | deploy.guest(0x41) | transport-ready(0x01) | rejected(0x03)
    [InlineData(0x00410100u)] // Success | deploy.guest | transport-ready | ok
    public void GeneratedConstants_ComposeToExpectedLayout(uint code)
    {
        Assert.Equal(0x41, StatusCodes.FacilityOf(code));
        Assert.Equal(0x01, StatusCodes.OperationOf(code));
    }

    [Fact]
    public void WorkedExample_GuestCredentialRejected_EncodesTheFrozenNumber()
    {
        // The 2026-07-16 deploy failure: guest rejected the bootstrap credential (fatal, actionable).
        Assert.Equal(0x64410103u, LaStatus.DeployGuest_GuestCredentialRejected);
    }

    [Fact]
    public void SuccessOutcome_IsRecognised()
    {
        Assert.True(StatusCodes.IsSuccess(LaStatus.DeployGuest_GuestReady));
        Assert.False(StatusCodes.IsSuccess(LaStatus.DeployGuest_GuestCredentialRejected));
    }

    [Fact]
    public void FacilityBitmask_SelectsAllEventsInAFacility()
    {
        // Filter payoff: masking byte 2 isolates a whole facility regardless of severity/flags/status.
        var deployGuest = StatusCodeCatalog.All
            .Where(descriptor => (descriptor.Code & 0x00FF0000u) == 0x00410000u)
            .ToList();

        Assert.NotEmpty(deployGuest);
        Assert.All(deployGuest, descriptor => Assert.Equal("deploy.guest", descriptor.FacilityName));
    }

    [Fact]
    public void SeverityBitmask_SelectsErrorsAndWorse()
    {
        var errors = StatusCodeCatalog.All
            .Where(descriptor => ((descriptor.Code >> 28) & 0xF) >= (uint)StatusCodeSeverity.Error)
            .ToList();

        Assert.All(errors, descriptor => Assert.Equal("error", descriptor.Level));
    }
}
