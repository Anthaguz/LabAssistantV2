using LabAssistant.Services.HyperV;
using LabAssistant.Services.PowerShell;
using Xunit;

namespace LabAssistant.Services.Tests;

/// <summary>
/// Guards that <see cref="HyperVService.CreateVmAsync"/> applies the requested vCPU count.
/// New-VM defaults ProcessorCount to 1, so without an explicit Set-VMProcessor every deployed
/// VM would silently get a single processor regardless of the template's CpuCount. The UI
/// automation harness caught this exact regression (a 2-vCPU request materialized as 1 vCPU).
/// </summary>
public class HyperVServiceProcessorCountTests
{
    [Fact]
    public async Task CreateVmAsync_AppliesRequestedProcessorCount()
    {
        var session = new CapturingSession();
        var service = new HyperVService(session);

        await service.CreateVmAsync("vm1", @"C:\vms", @"C:\vms\disk.vhdx", 2048, 4);

        Assert.NotNull(session.LastCommand);
        Assert.Contains("Set-VMProcessor -VMName 'vm1' -Count 4", session.LastCommand, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateVmAsync_ClampsNonPositiveProcessorCountToOne()
    {
        var session = new CapturingSession();
        var service = new HyperVService(session);

        await service.CreateVmAsync("vm1", @"C:\vms", @"C:\vms\disk.vhdx", 2048, 0);

        Assert.NotNull(session.LastCommand);
        Assert.Contains("Set-VMProcessor -VMName 'vm1' -Count 1", session.LastCommand, StringComparison.Ordinal);
    }

    private sealed class CapturingSession : IPersistentPowerShellSession
    {
        public string? LastCommand { get; private set; }

        public Task<(string Output, string Error)> ExecuteAsync(string command)
        {
            LastCommand = command;
            return Task.FromResult((string.Empty, string.Empty));
        }

        public void Dispose()
        {
        }
    }
}
