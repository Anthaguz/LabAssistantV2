using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;
using Xunit;

namespace LabAssistant.Services.Tests;

/// <summary>
/// Verifies that <see cref="HyperVService"/> emits coded structured logs for Hyper-V operations with the
/// operationId + vmName + result context required by the logging contract, and that a JSON parse
/// failure is logged rather than silently swallowed.
/// </summary>
public class HyperVServiceStructuredLoggingTests
{
    [Fact]
    public async Task Operation_Success_EmitsInfoEventWithVmNameAndSuccessResult()
    {
        var logger = new CapturingStructuredLogger();
        var session = new StubSession((string.Empty, string.Empty));
        var service = new HyperVService(session, logger);

        await service.StartVmAsync("Router01");

        var entry = Assert.Single(logger.Events);
        Assert.Equal(Hex(LaStatus.Hyperv_VMStarted), entry.Code);
        Assert.Equal("success", entry.Result);
        Assert.Equal("info", entry.Level);
        Assert.False(string.IsNullOrWhiteSpace(entry.OperationId));
        Assert.Equal("Router01", entry.Context!["vmName"]);
        Assert.True(entry.Context.ContainsKey("durationMs"));
    }

    [Fact]
    public async Task Operation_Failure_EmitsErrorEventWithErrorMessage()
    {
        var logger = new CapturingStructuredLogger();
        var session = new StubSession((string.Empty, "Get-VM : VM not found"));
        var service = new HyperVService(session, logger);

        await service.StopVmAsync("Router01");

        var entry = Assert.Single(logger.Events);
        Assert.Equal(Hex(LaStatus.Hyperv_VMStopFailed), entry.Code);
        Assert.Equal("failure", entry.Result);
        Assert.Equal("error", entry.Level);
        Assert.Equal("Get-VM : VM not found", entry.Context!["errorMessage"]);
    }

    [Fact]
    public async Task GetVmNetworkAdapters_MalformedJson_LogsParseFailureInsteadOfSwallowing()
    {
        var logger = new CapturingStructuredLogger();
        var session = new StubSession(("{ this is not valid json", string.Empty));
        var service = new HyperVService(session, logger);

        var adapters = await service.GetVmNetworkAdaptersAsync("Router01");

        Assert.Empty(adapters);
        Assert.Contains(
            logger.Events,
            e => e.Code == Hex(LaStatus.Hyperv_VMNetworkAdapterParseWarning)
                && e.Result == "failure"
                && e.Level == "warn"
                && Equals(e.Context!["vmName"], "Router01"));
    }

    private static string Hex(uint code) => $"0x{code:X8}";

    private sealed class StubSession : IPersistentPowerShellSession
    {
        private readonly (string Output, string Error) _result;

        public StubSession((string Output, string Error) result)
        {
            _result = result;
        }

        public Task<(string Output, string Error)> ExecuteAsync(string command) => Task.FromResult(_result);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingStructuredLogger : IStructuredLogger
    {
        public List<StructuredLogEvent> Events { get; } = [];

        public void Log(StructuredLogEvent logEvent)
        {
            Events.Add(logEvent);
        }

        public void Log(
            StructuredLogLevel level,
            string eventName,
            string operationId,
            string? result = null,
            IReadOnlyDictionary<string, object?>? context = null)
        {
            // HyperVService emits exclusively through the coded overload, which routes to
            // Log(StructuredLogEvent); this legacy overload is unused here.
        }
    }
}
