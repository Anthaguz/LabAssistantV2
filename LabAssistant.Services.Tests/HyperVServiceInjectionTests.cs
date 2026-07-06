using LabAssistant.Services.HyperV;
using LabAssistant.Services.PowerShell;
using Xunit;

namespace LabAssistant.Services.Tests;

/// <summary>
/// Verifies that every Hyper-V command built by <see cref="HyperVService"/> routes caller-supplied
/// names through the shared quoting helper, so a name containing a single quote cannot break out of
/// its literal and inject arbitrary PowerShell.
/// </summary>
public class HyperVServiceInjectionTests
{
    private const string MaliciousName = "evil'; Remove-VM * -Force; '";
    private const string EscapedName = "evil''; Remove-VM * -Force; ''";

    [Fact]
    public async Task CreateVmAsync_EscapesVmName()
    {
        var session = new CapturingSession();
        var service = new HyperVService(session);

        await service.CreateVmAsync(MaliciousName, @"C:\vms", @"C:\vms\disk.vhdx", 2048, 2);

        AssertEscapedAndNoBreakout(session.LastCommand);
    }

    [Fact]
    public async Task AddVirtualSwitchToVmAsync_EscapesVmAndSwitchNames()
    {
        var session = new CapturingSession();
        var service = new HyperVService(session);

        await service.AddVirtualSwitchToVmAsync(MaliciousName, MaliciousName);

        AssertEscapedAndNoBreakout(session.LastCommand);
        // Both the -VMName and -SwitchName arguments must be escaped, so the doubled form appears twice.
        var occurrences = CountOccurrences(session.LastCommand!, $"'{EscapedName}'");
        Assert.Equal(2, occurrences);
    }

    [Fact]
    public async Task StartVmAsync_EscapesVmName()
    {
        var session = new CapturingSession();
        var service = new HyperVService(session);

        await service.StartVmAsync(MaliciousName);

        AssertEscapedAndNoBreakout(session.LastCommand);
    }

    private static void AssertEscapedAndNoBreakout(string? command)
    {
        Assert.NotNull(command);

        // The escaped literal is present...
        Assert.Contains($"'{EscapedName}'", command, StringComparison.Ordinal);

        // ...and the raw, un-doubled single-quote sequence that would terminate the literal early is not.
        Assert.DoesNotContain($"'{MaliciousName}'", command, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
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
