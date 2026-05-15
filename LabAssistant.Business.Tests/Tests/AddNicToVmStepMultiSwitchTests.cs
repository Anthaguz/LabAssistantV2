using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.PowerShell;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.PowerShell;
using Xunit;

namespace LabAssistant.Business.Tests.Tests;

public sealed class AddNicToVmStepMultiSwitchTests
{
    [Fact]
    public async Task ExecuteAsync_AttachesAllAssignedSwitchesInOrder()
    {
        var handle = new PowerShellHandle();
        var hyperV = new FakeHyperVService();
        var step = new AddNicToVmStep(new SingleSessionResolver(handle, new FakeSession()), _ => hyperV);
        var context = new VmDeploymentContext
        {
            VmId = Guid.NewGuid(),
            VmName = "APP1",
            PowerShellHandle = handle,
            OperationId = "op-1",
            VirtualSwitchNames = ["Lab-A", "Lab-B"]
        };

        await step.ExecuteAsync(context);

        Assert.True(context.IsSuccess);
        Assert.Equal(["Lab-A", "Lab-B"], hyperV.LastAttachedSwitches);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsWhenNoSwitchAssignmentsExist()
    {
        var emitted = new List<DeployStepStateUpdate>();
        var handle = new PowerShellHandle();
        var hyperV = new FakeHyperVService();
        var step = new AddNicToVmStep(new SingleSessionResolver(handle, new FakeSession()), _ => hyperV);
        var context = new VmDeploymentContext
        {
            VmId = Guid.NewGuid(),
            VmName = "APP1",
            PowerShellHandle = handle,
            OperationId = "op-2",
            StepStateEmitter = update => emitted.Add(update)
        };

        await step.ExecuteAsync(context);

        Assert.True(context.IsSuccess);
        Assert.Empty(hyperV.LastAttachedSwitches);
        Assert.Equal(DeployStepState.Skipped, emitted[^1].State);
    }

    private sealed class SingleSessionResolver(PowerShellHandle handle, IPersistentPowerShellSession session) : ISessionResolver
    {
        public IPersistentPowerShellSession Resolve(PowerShellHandle requestHandle)
            => requestHandle.SessionId == handle.SessionId ? session : throw new KeyNotFoundException();

        public void RegisterSession(PowerShellHandle handle, IPersistentPowerShellSession session) { }

        public void RemoveSession(PowerShellHandle handle) { }
    }

    private sealed class FakeSession : IPersistentPowerShellSession
    {
        public Task<(string Output, string Error)> ExecuteAsync(string command) => Task.FromResult((string.Empty, string.Empty));

        public void Dispose() { }
    }

    private sealed class FakeHyperVService : IHyperVService
    {
        public List<string> LastAttachedSwitches { get; } = [];

        public Task<bool> CreateVmAsync(string vmName, string vmPath, string vhdPath, int memoryMb, int cpuCount) => Task.FromResult(true);
        public Task<bool> EnableGuestServicesAsync(string vmName) => Task.FromResult(true);
        public Task<bool> StartVmAsync(string vmName) => Task.FromResult(true);
        public Task<bool> StopVmAsync(string vmName) => Task.FromResult(true);
        public Task<bool> VmExistsAsync(string vmName) => Task.FromResult(false);
        public Task<bool> IsVmRunningAsync(string vmName) => Task.FromResult(false);
        public Task<bool> RemoveVmAsync(string vmName) => Task.FromResult(true);
        public Task<bool> CreateVhdDifferencingAsync(string parentDiskPath, string vhdPath) => Task.FromResult(true);
        public Task<bool> CreateVhdFixedSizeAsync(string vhdPath, long sizeBytes) => Task.FromResult(true);
        public Task<bool> DisableVmCheckpointsAsync(string vmName) => Task.FromResult(true);
        public Task<List<string>> GetVirtualSwitchNamesAsync() => Task.FromResult(new List<string>());
        public Task<bool> AddVirtualSwitchToVmAsync(string vmName, string switchName) => Task.FromResult(true);

        public Task<bool> AddVirtualSwitchesToVmAsync(string vmName, IReadOnlyList<string> switchNames)
        {
            LastAttachedSwitches.Clear();
            LastAttachedSwitches.AddRange(switchNames);
            return Task.FromResult(true);
        }
    }
}
