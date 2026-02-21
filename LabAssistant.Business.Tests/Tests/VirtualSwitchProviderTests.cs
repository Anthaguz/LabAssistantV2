using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LabAssistant.Business.Deployment;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.PowerShell;
using Xunit;

namespace LabAssistant.Business.Tests.Tests;

public class VirtualSwitchProviderTests
{
    private sealed class FakeSession : IPersistentPowerShellSession
    {
        public void Dispose()
        {
        }

        public Task<(string Output, string Error)> ExecuteAsync(string command)
        {
            return Task.FromResult((string.Empty, string.Empty));
        }
    }

    private sealed class FakeHyperVService : IHyperVService
    {
        private readonly List<string> _switches;

        public FakeHyperVService(IEnumerable<string> switches)
        {
            _switches = new List<string>(switches);
        }

        public Task<List<string>> GetVirtualSwitchNamesAsync() => Task.FromResult(_switches);

        public Task<bool> CreateVmAsync(string vmName, string vmPath, string vhdPath, int memoryMb, int cpuCount) => Task.FromResult(true);
        public Task<bool> EnableGuestServicesAsync(string vmName) => Task.FromResult(true);
        public Task<bool> StartVmAsync(string vmName) => Task.FromResult(true);
        public Task<bool> StopVmAsync(string vmName) => Task.FromResult(true);
        public Task<bool> CreateVhdDifferencingAsync(string parentDiskPath, string vhdPath) => Task.FromResult(true);
        public Task<bool> CreateVhdFixedSizeAsync(string vhdPath, long sizeBytes) => Task.FromResult(true);
        public Task<bool> DisableVmCheckpointsAsync(string vmName) => Task.FromResult(true);
        public Task<bool> AddVirtualSwitchToVmAsync(string vmName, string switchName) => Task.FromResult(true);
    }

    [Fact]
    public async Task GetVirtualSwitchesAsync_ReturnsSwitches()
    {
        var provider = new VirtualSwitchProvider(
            () => new FakeSession(),
            _ => new FakeHyperVService(new[] { "Default Switch", "LabNet" }));

        var switches = await provider.GetVirtualSwitchesAsync();

        Assert.Equal(2, switches.Count);
        Assert.Contains("LabNet", switches);
    }

    [Fact]
    public async Task GetVirtualSwitchesAsync_ReturnsEmpty_WhenFactoryThrows()
    {
        var provider = new VirtualSwitchProvider(
            () => new FakeSession(),
            _ => throw new InvalidOperationException("boom"));

        var switches = await provider.GetVirtualSwitchesAsync();

        Assert.Empty(switches);
    }
}
