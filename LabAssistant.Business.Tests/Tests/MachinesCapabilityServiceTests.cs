using LabAssistant.Business.Machines;
using LabAssistant.Models.Configuration;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;
using Xunit;

namespace LabAssistant.Business.Tests;

public class MachinesCapabilityServiceTests
{
    [Fact]
    public async Task LoadInventoryAsync_MapsOriginAndEmitsStructuredLogs()
    {
        var adminService = new FakeMachineAdminService
        {
            Inventory =
            [
                new HyperVHostMachineVmInfo
                {
                    VmId = "vm-1",
                    VmName = "LabVm01",
                    State = "Running",
                    VmPath = @"D:\LabAssistant\VMs\LabVm01"
                },
                new HyperVHostMachineVmInfo
                {
                    VmId = "vm-2",
                    VmName = "ExternalVm",
                    State = "Off",
                    VmPath = @"E:\HyperV\ExternalVm"
                }
            ]
        };
        var logger = new RecordingStructuredLogger();
        var service = new MachinesCapabilityService(
            adminService,
            new FakeSettingsStore(@"D:\LabAssistant\VMs"),
            logger);

        var result = await service.LoadInventoryAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("LabAssistant", result.Single(vm => vm.VmName == "LabVm01").OriginLabel);
        Assert.Equal("External/Unknown", result.Single(vm => vm.VmName == "ExternalVm").OriginLabel);

        Assert.Contains(logger.Events, e => e.Event == "MachineInventoryLoadStarted");
        Assert.Contains(logger.Events, e => e.Event == "MachineInventoryLoadCompleted");
    }

    [Fact]
    public async Task DeleteVmAsync_WithVmAndStorage_EmitsDeleteScopeInFailureContext()
    {
        var adminService = new FakeMachineAdminService
        {
            DeleteResult = new HyperVMachineActionResult
            {
                Success = false,
                ErrorMessage = "remove failed",
                FailureMetadata = new Dictionary<string, object?> { ["hresult"] = "0x80070005" }
            }
        };
        var logger = new RecordingStructuredLogger();
        var service = new MachinesCapabilityService(
            adminService,
            new FakeSettingsStore(@"D:\LabAssistant\VMs"),
            logger);
        var vm = new MachineInventoryItem
        {
            VmId = "vm-1",
            VmName = "LabVm01",
            State = "Off",
            OriginLabel = "LabAssistant"
        };

        var result = await service.DeleteVmAsync(vm, MachineDeleteScope.VmAndStorage);

        Assert.False(result.Success);
        Assert.Contains("Failed to delete", result.UserMessage);

        var failedEvent = Assert.Single(logger.Events, e => e.Event == "MachineDeleteFailed");
        Assert.Equal("vm_and_storage", failedEvent.Context?["deleteScope"]?.ToString());
        Assert.Equal("0x80070005", failedEvent.Context?["hresult"]?.ToString());
    }

    private sealed class FakeMachineAdminService : IHyperVMachineAdminService
    {
        public IReadOnlyList<HyperVHostMachineVmInfo> Inventory { get; set; } = Array.Empty<HyperVHostMachineVmInfo>();

        public HyperVMachineActionResult ActionResult { get; set; } = new() { Success = true };

        public HyperVMachineActionResult DeleteResult { get; set; } = new() { Success = true };

        public Task<IReadOnlyList<HyperVHostMachineVmInfo>> ListHostVmsAsync() => Task.FromResult(Inventory);

        public Task<HyperVMachineActionResult> StartVmAsync(string vmName) => Task.FromResult(ActionResult);

        public Task<HyperVMachineActionResult> StopVmAsync(string vmName) => Task.FromResult(ActionResult);

        public Task<HyperVMachineActionResult> RestartVmAsync(string vmName) => Task.FromResult(ActionResult);

        public Task<HyperVMachineActionResult> OpenConsoleAsync(string vmName) => Task.FromResult(ActionResult);

        public Task<HyperVMachineActionResult> DeleteVmAsync(string vmName, bool includeStorage) => Task.FromResult(DeleteResult);
    }

    private sealed class RecordingStructuredLogger : IStructuredLogger
    {
        public List<StructuredLogEvent> Events { get; } = new();

        public void Log(StructuredLogEvent logEvent) => Events.Add(logEvent);

        public void Log(StructuredLogLevel level, string eventName, string operationId, string? result = null, IReadOnlyDictionary<string, object?>? context = null)
            => Events.Add(StructuredLogEvent.Create(level, eventName, operationId, result, context));
    }

    private sealed class FakeSettingsStore : IAppSettingsStore
    {
        public FakeSettingsStore(string vmBasePath)
        {
            Settings = new AppSettings { VmBasePath = vmBasePath };
        }

        public AppSettings Settings { get; private set; }

        public string SettingsPath => string.Empty;

        public void LoadOrCreate() { }

        public void Reload() { }

        public void Save() { }

        public void ResetToDefault() { }

        public void SetTemplateFolder(string path) { }

        public void SetLogFolder(string path) { }

        public void SetVmBasePath(string path)
        {
            Settings.VmBasePath = path;
        }

        public void SetDifferencingDiskBasePath(string path) { }
    }
}
