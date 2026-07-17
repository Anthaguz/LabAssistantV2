using System.Text.Json;
using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.Logging;
using Xunit;

namespace LabAssistant.Services.Tests;

/// <summary>
/// Covers the code-based emit path added to <see cref="StructuredLogEvent"/>: resolving every field
/// from a canonical status code, projecting the level from the severity nibble, decomposing flags,
/// degrading gracefully for unregistered codes, and serializing the new fields.
/// </summary>
public sealed class StructuredLogEventTests
{
    [Fact]
    public void Create_FromRegisteredCode_ResolvesEveryFieldFromTheCode()
    {
        var e = StructuredLogEvent.Create(
            LaStatus.DeployGuest_GuestCredentialRejected,
            operationId: "op-1",
            result: "rejected");

        Assert.Equal("0x64410103", e.Code);
        Assert.Equal("error", e.Level);
        Assert.Equal("deploy.guest", e.Facility);
        Assert.Equal("transport-ready", e.Operation);
        Assert.Equal("end", e.Phase);
        Assert.Equal("Error", e.Severity);
        Assert.Equal(new[] { "UserActionable" }, e.Flags);
        Assert.Equal("deploy.guest.transport-ready.end", e.Event);
        Assert.Equal("op-1", e.OperationId);
        Assert.Equal("rejected", e.Result);
        Assert.NotNull(e.Thread);
    }

    [Theory]
    [InlineData(LaStatus.InfraPowershell_SessionCreated, "debug")] // Debug severity
    [InlineData(LaStatus.InfraApp_Ready, "info")]                  // Success severity projects to info
    [InlineData(LaStatus.InfraApp_StartingUp, "info")]            // Info severity
    [InlineData(LaStatus.InfraPowershell_SessionRetired, "warn")] // Warning severity
    [InlineData(LaStatus.DeployGuest_GuestCredentialRejected, "error")] // Error severity
    public void Create_ProjectsLevelFromSeverityNibble(uint code, string expectedLevel)
    {
        var e = StructuredLogEvent.Create(code, "op");
        Assert.Equal(expectedLevel, e.Level);
    }

    [Fact]
    public void Create_DecomposesMultipleFlagsInBitOrder()
    {
        // 0x23410101: flags nibble 0x3 = Retryable | Transient.
        var e = StructuredLogEvent.Create(LaStatus.DeployGuest_GuestNotReadyYet, "op");
        Assert.Equal(new[] { "Retryable", "Transient" }, e.Flags);
        Assert.Equal("progress", e.Phase);
    }

    [Fact]
    public void Create_OmitsFlagsWhenNoneSet()
    {
        var e = StructuredLogEvent.Create(LaStatus.InfraPowershell_SessionCreated, "op");
        Assert.Null(e.Flags);
    }

    [Theory]
    [InlineData(LaStatus.InfraApp_StartingUp, "start")]
    [InlineData(LaStatus.InfraPowershell_HealthCheck, "atomic")]
    [InlineData(LaStatus.InfraPowershell_SessionCreated, "end")]
    public void Create_CarriesThePhaseFromTheRegistry(uint code, string expectedPhase)
    {
        var e = StructuredLogEvent.Create(code, "op");
        Assert.Equal(expectedPhase, e.Phase);
    }

    [Fact]
    public void Create_PassesThroughCallsiteAndCapturesThread()
    {
        var e = StructuredLogEvent.Create(
            LaStatus.InfraPowershell_SessionCreated,
            "op",
            callsite: "Foo.cs:42",
            threadId: 7);

        Assert.Equal("Foo.cs:42", e.Callsite);
        Assert.Equal(7, e.Thread);
    }

    [Fact]
    public void Create_UnregisteredCode_DegradesGracefullyWithoutThrowing()
    {
        // 0x60FF0000: Error severity, no flags, facility 0xFF (unregistered), operation 0x00.
        const uint unregistered = 0x60FF0000u;
        var e = StructuredLogEvent.Create(unregistered, "op");

        Assert.Equal("0x60FF0000", e.Code);
        Assert.Equal("0xFF", e.Facility);
        Assert.Equal("0x00", e.Operation);
        Assert.Equal("0xFF.0x00", e.Event);
        Assert.Equal("Error", e.Severity);
        Assert.Equal("error", e.Level);
        Assert.Equal("atomic", e.Phase);
        Assert.Null(e.Flags);
    }

    [Fact]
    public void Create_RequiresOperationId()
    {
        Assert.Throws<ArgumentException>(() =>
            StructuredLogEvent.Create(LaStatus.InfraApp_Ready, operationId: " "));
    }

    [Fact]
    public void Create_LegacyOverload_LeavesCodeFieldsUnset()
    {
        var e = StructuredLogEvent.Create(StructuredLogLevel.Info, "some.event", "op");

        Assert.Null(e.Code);
        Assert.Null(e.Facility);
        Assert.Null(e.Operation);
        Assert.Null(e.Phase);
        Assert.Null(e.Severity);
        Assert.Null(e.Flags);
        Assert.Null(e.Thread);
        Assert.Null(e.Callsite);
        Assert.Equal("info", e.Level);
        Assert.Equal("some.event", e.Event);
    }

    [Fact]
    public void Serialize_CodeBasedEvent_EmitsTheNewFields()
    {
        var e = StructuredLogEvent.Create(
            LaStatus.DeployGuest_GuestCredentialRejected,
            "op",
            callsite: "Foo.cs:1",
            threadId: 3);

        var json = JsonSerializer.Serialize(e);

        Assert.Contains("\"code\":\"0x64410103\"", json);
        Assert.Contains("\"facility\":\"deploy.guest\"", json);
        Assert.Contains("\"operation\":\"transport-ready\"", json);
        Assert.Contains("\"phase\":\"end\"", json);
        Assert.Contains("\"severity\":\"Error\"", json);
        Assert.Contains("\"flags\":[\"UserActionable\"]", json);
        Assert.Contains("\"thread\":3", json);
        Assert.Contains("\"callsite\":\"Foo.cs:1\"", json);
    }

    [Fact]
    public void Serialize_LegacyEvent_OmitsTheNewFields()
    {
        var e = StructuredLogEvent.Create(StructuredLogLevel.Info, "some.event", "op");

        var json = JsonSerializer.Serialize(e);

        Assert.DoesNotContain("\"code\"", json);
        Assert.DoesNotContain("\"severity\"", json);
        Assert.DoesNotContain("\"thread\"", json);
        Assert.DoesNotContain("\"callsite\"", json);
    }
}
