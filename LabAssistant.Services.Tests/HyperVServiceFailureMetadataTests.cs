using LabAssistant.Services.HyperV;
using LabAssistant.Services.PowerShell;
using Xunit;

namespace LabAssistant.Services.Tests;

public class HyperVServiceFailureMetadataTests
{
    [Fact]
    public async Task CreateVhdDifferencingAsync_Failure_PopulatesNormalizedLastFailureMetadata()
    {
        var session = new FakeSession
        {
            Result = (string.Empty, "System.Management.Automation.RuntimeException: boom 0x80070005\r\nFullyQualifiedErrorId : OperationFailed,Microsoft.Vhd.PowerShell.Cmdlets.NewVHD")
        };
        var service = new HyperVService(session);

        var ok = await service.CreateVhdDifferencingAsync(@"D:\base.vhdx", @"D:\vm\disk.vhdx");

        Assert.False(ok);
        Assert.NotNull(service.LastFailureMetadata);
        Assert.Equal("RuntimeException", service.LastFailureMetadata!["exceptionType"]);
        Assert.Equal("0x80070005", service.LastFailureMetadata!["hresult"]);
        Assert.Equal("OperationFailed", service.LastFailureMetadata!["errorCode"]);
    }

    [Fact]
    public async Task CreateVmAsync_Success_ClearsLastFailureMetadata()
    {
        var session = new FakeSession
        {
            Result = (string.Empty, "System.Management.Automation.RuntimeException: boom 0x80070005")
        };
        var service = new HyperVService(session);
        _ = await service.CreateVmAsync("vm1", @"C:\vm", @"C:\vm\vm.vhdx", 2048, 2);

        session.Result = ("ok", string.Empty);
        var ok = await service.CreateVmAsync("vm1", @"C:\vm", @"C:\vm\vm.vhdx", 2048, 2);

        Assert.True(ok);
        Assert.Null(service.LastFailureMetadata);
    }

    private sealed class FakeSession : IPersistentPowerShellSession
    {
        public (string Output, string Error) Result { get; set; }

        public Task<(string Output, string Error)> ExecuteAsync(string command) => Task.FromResult(Result);
        public void Dispose() { }
    }
}
