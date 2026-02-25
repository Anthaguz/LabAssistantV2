using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.PowerShell;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.PowerShell;
using Xunit;

namespace LabAssistant.Business.Tests.Tests;

public class RuntimeFailureDiagnosticsEnrichmentTests
{
    [Fact]
    public async Task CreateVhdStep_Failure_RecordsPathContextInStructuredEvent_AndActionableMessage()
    {
        var handle = new PowerShellHandle();
        var session = new FakeSession();
        var resolver = new FakeSessionResolver(handle, session);
        var hyperv = new FakeHyperVService { CreateVhdDifferencingResult = false };
        hyperv.LastFailureMetadata = new Dictionary<string, object?> { ["exceptionType"] = "RuntimeException", ["hresult"] = "0x80070005", ["errorCode"] = "OperationFailed" };
        var events = new List<RecordedStructuredEvent>();

        var step = new CreateVhdStep(resolver, _ => hyperv);
        var context = new VmDeploymentContext
        {
            VmId = Guid.NewGuid(),
            VmName = "VM1",
            BaseVhdPath = @"D:\Base\bad.vhdx",
            VhdPath = @"D:\Labs\VM1\VM1.vhdx",
            PowerShellHandle = handle,
            StructuredEventEmitter = (eventName, level, result, extra) =>
            {
                events.Add(new RecordedStructuredEvent(eventName, level, result, extra));
            }
        };

        await step.ExecuteAsync(context);

        Assert.False(context.IsSuccess);
        Assert.Equal(DeploymentStepKeys.CreateVhd, context.FailureStepKey);
        Assert.Contains(context.BaseVhdPath, context.FailureMessage);
        Assert.Contains(context.VmName, context.FailureMessage);

        var stepFailed = Assert.Single(events.Where(e => e.EventName == "StepFailed"));
        Assert.Equal("failed", stepFailed.Result);
        Assert.Equal(DeploymentStepKeys.CreateVhd, stepFailed.Extra?["stepKey"]?.ToString());
        Assert.Equal(context.BaseVhdPath, stepFailed.Extra?["parentVhdPath"]?.ToString());
        Assert.Equal(context.VhdPath, stepFailed.Extra?["targetVhdPath"]?.ToString());
        Assert.Equal("RuntimeException", stepFailed.Extra?["exceptionType"]?.ToString());
        Assert.Equal("0x80070005", stepFailed.Extra?["hresult"]?.ToString());
        Assert.Equal("OperationFailed", stepFailed.Extra?["errorCode"]?.ToString());
    }

    [Fact]
    public async Task CreateVmStep_Failure_RecordsVmAndDiskPathContext_AndActionableMessage()
    {
        var handle = new PowerShellHandle();
        var session = new FakeSession();
        var resolver = new FakeSessionResolver(handle, session);
        var hyperv = new FakeHyperVService { CreateVmResult = false };
        hyperv.LastFailureMetadata = new Dictionary<string, object?> { ["exceptionType"] = "VirtualizationException", ["hresult"] = "0x80070570" };
        var events = new List<RecordedStructuredEvent>();

        var step = new CreateVmStep(resolver, _ => hyperv);
        var context = new VmDeploymentContext
        {
            VmId = Guid.NewGuid(),
            VmName = "VM2",
            VmPath = @"D:\Labs\VM2",
            VhdPath = @"D:\Labs\VM2\VM2.vhdx",
            MemoryMb = 2048,
            CpuCount = 2,
            PowerShellHandle = handle,
            StructuredEventEmitter = (eventName, level, result, extra) =>
            {
                events.Add(new RecordedStructuredEvent(eventName, level, result, extra));
            }
        };

        await step.ExecuteAsync(context);

        Assert.False(context.IsSuccess);
        Assert.Equal(DeploymentStepKeys.CreateVm, context.FailureStepKey);
        Assert.Contains(context.VmPath, context.FailureMessage);
        Assert.Contains(context.VhdPath, context.FailureMessage);

        var stepFailed = Assert.Single(events.Where(e => e.EventName == "StepFailed"));
        Assert.Equal(context.VmPath, stepFailed.Extra?["vmPath"]?.ToString());
        Assert.Equal(context.VhdPath, stepFailed.Extra?["targetVhdPath"]?.ToString());
        Assert.Equal("VirtualizationException", stepFailed.Extra?["exceptionType"]?.ToString());
        Assert.Equal("0x80070570", stepFailed.Extra?["hresult"]?.ToString());
    }

    private sealed record RecordedStructuredEvent(
        string EventName,
        string Level,
        string? Result,
        IReadOnlyDictionary<string, object?>? Extra);

    private sealed class FakeSessionResolver : ISessionResolver
    {
        private readonly Dictionary<Guid, IPersistentPowerShellSession> _sessions = new();

        public FakeSessionResolver(PowerShellHandle handle, IPersistentPowerShellSession session)
        {
            _sessions[handle.SessionId] = session;
        }

        public IPersistentPowerShellSession Resolve(PowerShellHandle handle) => _sessions[handle.SessionId];
        public void RegisterSession(PowerShellHandle handle, IPersistentPowerShellSession session) => _sessions[handle.SessionId] = session;
        public void RemoveSession(PowerShellHandle handle) => _sessions.Remove(handle.SessionId);
    }

    private sealed class FakeSession : IPersistentPowerShellSession
    {
        public Task<(string Output, string Error)> ExecuteAsync(string command) => Task.FromResult((string.Empty, string.Empty));
        public void Dispose() { }
    }

    private sealed class FakeHyperVService : IHyperVService, IHyperVFailureDiagnosticsProvider
    {
        public bool CreateVmResult { get; set; } = true;
        public bool CreateVhdDifferencingResult { get; set; } = true;
        public IReadOnlyDictionary<string, object?>? LastFailureMetadata { get; set; }
        public void ClearLastFailureMetadata() => LastFailureMetadata = null;

        public Task<bool> AddVirtualSwitchToVmAsync(string vmName, string switchName) => Task.FromResult(true);
        public Task<bool> CreateVhdDifferencingAsync(string parentDiskPath, string vhdPath) => Task.FromResult(CreateVhdDifferencingResult);
        public Task<bool> CreateVhdFixedSizeAsync(string vhdPath, long sizeBytes) => Task.FromResult(true);
        public Task<bool> CreateVmAsync(string vmName, string vmPath, string vhdPath, int memoryMb, int cpuCount) => Task.FromResult(CreateVmResult);
        public Task<bool> DisableVmCheckpointsAsync(string vmName) => Task.FromResult(true);
        public Task<bool> EnableGuestServicesAsync(string vmName) => Task.FromResult(true);
        public Task<List<string>> GetVirtualSwitchNamesAsync() => Task.FromResult(new List<string>());
        public Task<bool> IsVmRunningAsync(string vmName) => Task.FromResult(false);
        public Task<bool> RemoveVmAsync(string vmName) => Task.FromResult(true);
        public Task<bool> StartVmAsync(string vmName) => Task.FromResult(true);
        public Task<bool> StopVmAsync(string vmName) => Task.FromResult(true);
        public Task<bool> VmExistsAsync(string vmName) => Task.FromResult(false);
    }
}
