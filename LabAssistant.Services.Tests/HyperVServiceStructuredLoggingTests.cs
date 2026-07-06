using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;
using Xunit;

namespace LabAssistant.Services.Tests;

/// <summary>
/// Verifies that <see cref="HyperVService"/> emits structured logs for Hyper-V operations with the
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
        Assert.Equal("hyperv.start_vm", entry.EventName);
        Assert.Equal("success", entry.Result);
        Assert.Equal(StructuredLogLevel.Info, entry.Level);
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
        Assert.Equal("hyperv.stop_vm", entry.EventName);
        Assert.Equal("failure", entry.Result);
        Assert.Equal(StructuredLogLevel.Error, entry.Level);
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
            e => e.EventName == "hyperv.get_vm_network_adapters_parse"
                && e.Result == "failure"
                && e.Level == StructuredLogLevel.Warn
                && Equals(e.Context!["vmName"], "Router01"));
    }

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

    private sealed record LogEntry(
        StructuredLogLevel Level,
        string EventName,
        string OperationId,
        string? Result,
        IReadOnlyDictionary<string, object?>? Context);

    private sealed class CapturingStructuredLogger : IStructuredLogger
    {
        public List<LogEntry> Events { get; } = [];

        public void Log(StructuredLogEvent logEvent)
        {
            // HyperVService uses the parameterized overload below; this overload is unused here.
        }

        public void Log(
            StructuredLogLevel level,
            string eventName,
            string operationId,
            string? result = null,
            IReadOnlyDictionary<string, object?>? context = null)
        {
            Events.Add(new LogEntry(level, eventName, operationId, result, context));
        }
    }
}
