using System;
using System.Linq;
using System.Threading.Tasks;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.PowerShell;
using Xunit;

namespace LabAssistant.Services.Tests;

/// <summary>
/// Guards the Get-VMNetworkAdapter query path in <see cref="HyperVService"/>.
///
/// Regression context: the query script previously assigned its pipeline to a variable
/// (<c>$items = Get-VMNetworkAdapter ... | ConvertTo-Json</c>) and never emitted the variable. A
/// PowerShell assignment writes nothing to the success stream, so the persistent session captured
/// empty stdout, <see cref="HyperVService.GetVmNetworkAdaptersAsync"/> returned zero adapters on
/// every call, and every guest-network deploy (single-NIC and multi-NIC alike) failed downstream
/// with "could not resolve a Hyper-V adapter MAC". These tests lock in that the built script emits
/// its JSON and that a populated payload flows through the parser.
/// </summary>
public class HyperVServiceAdapterQueryTests
{
    [Fact]
    public void BuildGetVmNetworkAdaptersScript_EmitsPipelineInsteadOfSwallowingIntoAssignment()
    {
        var script = HyperVService.BuildGetVmNetworkAdaptersScript("vm1");

        // The first non-empty line must be the emitting pipeline, not a "$var =" capture that
        // swallows stdout. This is the exact shape bug that produced the empty adapter list.
        var firstLine = script
            .Split('\n')
            .Select(line => line.Trim())
            .First(line => line.Length > 0);

        Assert.StartsWith("Get-VMNetworkAdapter", firstLine, StringComparison.Ordinal);
        Assert.DoesNotContain("$items =", script, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"^\s*\$\w+\s*=", script);
        Assert.Contains("ConvertTo-Json", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetVmNetworkAdaptersAsync_SendsEmittingScriptToSession()
    {
        var session = new CapturingSession(string.Empty);
        var service = new HyperVService(session);

        await service.GetVmNetworkAdaptersAsync("vm1");

        Assert.NotNull(session.LastCommand);
        Assert.StartsWith("Get-VMNetworkAdapter", session.LastCommand!.TrimStart(), StringComparison.Ordinal);
        Assert.DoesNotContain("$items =", session.LastCommand!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetVmNetworkAdaptersAsync_ParsesSingleObjectPayload()
    {
        // Get-VMNetworkAdapter | ConvertTo-Json emits a single JSON object (not an array) when the
        // VM has exactly one adapter - the single-NIC DC case.
        const string json = """
        {
          "AdapterName": "Network Adapter",
          "SwitchName": "LAT-20260728-024352-switch",
          "MacAddress": "00155D0006BC",
          "Status": "Ok",
          "Connected": true,
          "IsManagementOs": false
        }
        """;
        var service = new HyperVService(new CapturingSession(json));

        var adapters = await service.GetVmNetworkAdaptersAsync("vm1");

        var adapter = Assert.Single(adapters);
        Assert.Equal("Network Adapter", adapter.AdapterName);
        Assert.Equal("LAT-20260728-024352-switch", adapter.SwitchName);
        Assert.Equal("00155D0006BC", adapter.MacAddress);
    }

    [Fact]
    public async Task GetVmNetworkAdaptersAsync_ParsesArrayPayloadWithDistinctMacs()
    {
        // Multi-NIC router case: an array of adapters, each on its own switch with a distinct MAC.
        const string json = """
        [
          { "AdapterName": "Network Adapter", "SwitchName": "Default Switch", "MacAddress": "00155D0006B7", "Status": "Ok" },
          { "AdapterName": "Network Adapter 2", "SwitchName": "LAT-20260728-023205-switch", "MacAddress": "00155D0006B8", "Status": "Ok" }
        ]
        """;
        var service = new HyperVService(new CapturingSession(json));

        var adapters = await service.GetVmNetworkAdaptersAsync("vm1");

        Assert.Equal(2, adapters.Count);
        Assert.Equal("00155D0006B7", adapters[0].MacAddress);
        Assert.Equal("00155D0006B8", adapters[1].MacAddress);
        Assert.Equal("Default Switch", adapters[0].SwitchName);
        Assert.Equal("LAT-20260728-023205-switch", adapters[1].SwitchName);
    }

    [Fact]
    public async Task GetVmNetworkAdaptersAsync_BenignStderrWithValidJson_StillReturnsAdapters()
    {
        // A non-terminating warning on the error stream (module-autoload noise, a per-adapter CIM hiccup) can arrive
        // alongside a valid adapter payload. Because the query uses -ErrorAction Stop, a real terminating failure
        // would have left the success stream empty, so a populated payload proves the error was non-fatal. The
        // adapters must still be returned - blanking them on any stderr is the same no-adapters/no-IP failure class.
        const string json = """
        {
          "AdapterName": "Network Adapter",
          "SwitchName": "LAT-20260728-024352-switch",
          "MacAddress": "00155D0006BC",
          "Status": "Ok"
        }
        """;
        var service = new HyperVService(new CapturingSession(json, "WARNING: The 'Hyper-V' module loaded with warnings."));

        var adapters = await service.GetVmNetworkAdaptersAsync("vm1");

        var adapter = Assert.Single(adapters);
        Assert.Equal("LAT-20260728-024352-switch", adapter.SwitchName);
        Assert.Equal("00155D0006BC", adapter.MacAddress);
        // A tolerated benign warning must not be recorded as a query failure.
        Assert.Null(service.LastFailureMetadata);
    }

    [Fact]
    public async Task GetVmNetworkAdaptersAsync_EmptyOutputWithError_ReturnsEmptyAndCapturesFailureMetadata()
    {
        // A genuine terminating failure (VM missing, access denied) empties the success stream and leaves only an
        // error. That IS a real failure: return no adapters AND capture the error as failure metadata so callers
        // surface an actionable runtime diagnostic instead of a silently-empty list.
        var service = new HyperVService(new CapturingSession(
            string.Empty,
            "Get-VMNetworkAdapter : Hyper-V was unable to find a virtual machine named \"vm1\"."));

        var adapters = await service.GetVmNetworkAdaptersAsync("vm1");

        Assert.Empty(adapters);
        Assert.NotNull(service.LastFailureMetadata);
    }

    private sealed class CapturingSession : IPersistentPowerShellSession
    {
        private readonly string _output;
        private readonly string _error;

        public CapturingSession(string output)
            : this(output, string.Empty)
        {
        }

        public CapturingSession(string output, string error)
        {
            _output = output;
            _error = error;
        }

        public string? LastCommand { get; private set; }

        public Task<(string Output, string Error)> ExecuteAsync(string command)
        {
            LastCommand = command;
            return Task.FromResult((_output, _error));
        }

        public void Dispose()
        {
        }
    }
}
