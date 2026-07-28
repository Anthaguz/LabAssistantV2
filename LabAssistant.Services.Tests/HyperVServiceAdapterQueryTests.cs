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

    private sealed class CapturingSession : IPersistentPowerShellSession
    {
        private readonly string _output;

        public CapturingSession(string output)
        {
            _output = output;
        }

        public string? LastCommand { get; private set; }

        public Task<(string Output, string Error)> ExecuteAsync(string command)
        {
            LastCommand = command;
            return Task.FromResult((_output, string.Empty));
        }

        public void Dispose()
        {
        }
    }
}
