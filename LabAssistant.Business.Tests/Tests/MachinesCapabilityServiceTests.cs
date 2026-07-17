using LabAssistant.Business.Machines;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;
using System.Net.Sockets;
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
            new FakeCatalogStore(),
            new FakeSettingsStore(@"D:\LabAssistant\VMs"),
            logger);

        var result = await service.LoadInventoryAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("LabAssistant", result.Single(vm => vm.VmName == "LabVm01").OriginLabel);
        Assert.Equal("External/Unknown", result.Single(vm => vm.VmName == "ExternalVm").OriginLabel);

        Assert.Contains(logger.Events, e => e.Code == $"0x{LaStatus.Machines_LoadingMachineInventory:X8}");
        Assert.Contains(logger.Events, e => e.Code == $"0x{LaStatus.Machines_MachineInventoryLoaded:X8}");
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
            new FakeCatalogStore(),
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

        var failedEvent = Assert.Single(logger.Events, e => e.Code == $"0x{LaStatus.Machines_MachineDeleteFailed:X8}");
        Assert.Equal("vm_and_storage", failedEvent.Context?["deleteScope"]?.ToString());
        Assert.Equal("0x80070005", failedEvent.Context?["hresult"]?.ToString());
    }

    [Fact]
    public async Task EvaluateRdpReadinessAsync_WhenVmNotRunning_ReturnsNotReady()
    {
        var adminService = new FakeMachineAdminService();
        var service = new MachinesCapabilityService(
            adminService,
            new FakeCatalogStore(),
            new FakeSettingsStore(@"D:\LabAssistant\VMs"));

        var readiness = await service.EvaluateRdpReadinessAsync(new MachineInventoryItem
        {
            VmId = "vm-1",
            VmName = "Vm01",
            State = "Off"
        });

        Assert.Equal(MachineRdpReadinessState.NotReady, readiness.State);
        Assert.Equal(MachineRdpReadinessReasonCodes.VmNotRunning, readiness.ReasonCode);
    }

    [Fact]
    public async Task EvaluateRdpReadinessAsync_WhenNoIpv4_ReturnsNotReady()
    {
        var adminService = new FakeMachineAdminService
        {
            IpAddresses = ["fe80::1"]
        };
        var service = new MachinesCapabilityService(
            adminService,
            new FakeCatalogStore(),
            new FakeSettingsStore(@"D:\LabAssistant\VMs"));

        var readiness = await service.EvaluateRdpReadinessAsync(new MachineInventoryItem
        {
            VmId = "vm-1",
            VmName = "Vm01",
            State = "Running"
        });

        Assert.Equal(MachineRdpReadinessState.NotReady, readiness.State);
        Assert.Equal(MachineRdpReadinessReasonCodes.NoIpv4, readiness.ReasonCode);
    }

    [Fact]
    public async Task EvaluateRdpReadinessAsync_WhenPortReachable_ReturnsReadyWithIpv4()
    {
        var adminService = new FakeMachineAdminService
        {
            IpAddresses = ["192.168.10.15"]
        };
        var service = new MachinesCapabilityService(
            adminService,
            new FakeCatalogStore(),
            new FakeSettingsStore(@"D:\LabAssistant\VMs"),
            tcpProbe: (_, _, _, _) => Task.FromResult(true));

        var readiness = await service.EvaluateRdpReadinessAsync(new MachineInventoryItem
        {
            VmId = "vm-1",
            VmName = "Vm01",
            State = "Running"
        });

        Assert.Equal(MachineRdpReadinessState.Ready, readiness.State);
        Assert.Equal(MachineRdpReadinessReasonCodes.Ready, readiness.ReasonCode);
        Assert.Equal("192.168.10.15", readiness.TargetIpv4);
    }

    [Fact]
    public async Task EvaluateRdpReadinessAsync_WhenProbeFails_ReturnsNotReady()
    {
        var adminService = new FakeMachineAdminService
        {
            IpAddresses = ["192.168.10.15"]
        };
        var service = new MachinesCapabilityService(
            adminService,
            new FakeCatalogStore(),
            new FakeSettingsStore(@"D:\LabAssistant\VMs"),
            tcpProbe: (_, _, _, _) => Task.FromResult(false));

        var readiness = await service.EvaluateRdpReadinessAsync(new MachineInventoryItem
        {
            VmId = "vm-1",
            VmName = "Vm01",
            State = "Running"
        });

        Assert.Equal(MachineRdpReadinessState.NotReady, readiness.State);
        Assert.Equal(MachineRdpReadinessReasonCodes.Port3389Unreachable, readiness.ReasonCode);
    }

    [Fact]
    public async Task EvaluateRdpReadinessAsync_WhenProbeThrows_ReturnsUnknown()
    {
        var adminService = new FakeMachineAdminService
        {
            IpAddresses = ["192.168.10.15"]
        };
        var service = new MachinesCapabilityService(
            adminService,
            new FakeCatalogStore(),
            new FakeSettingsStore(@"D:\LabAssistant\VMs"),
            tcpProbe: (_, _, _, _) => throw new SocketException());

        var readiness = await service.EvaluateRdpReadinessAsync(new MachineInventoryItem
        {
            VmId = "vm-1",
            VmName = "Vm01",
            State = "Running"
        });

        Assert.Equal(MachineRdpReadinessState.Unknown, readiness.State);
        Assert.Equal(MachineRdpReadinessReasonCodes.CheckFailed, readiness.ReasonCode);
    }

    [Fact]
    public async Task ApplyEditsAsync_EmitsChangedFieldsAndCallsAdminService()
    {
        var adminService = new FakeMachineAdminService();
        var logger = new RecordingStructuredLogger();
        var service = new MachinesCapabilityService(
            adminService,
            new FakeCatalogStore(),
            new FakeSettingsStore(@"D:\LabAssistant\VMs"),
            logger);
        var vm = new MachineInventoryItem
        {
            VmId = "vm-1",
            VmName = "Vm01",
            State = "Running",
            OriginLabel = "LabAssistant"
        };
        var draft = new MachineEditDraft
        {
            CpuCount = 4,
            StartupMemoryMb = 4096,
            DynamicMemoryEnabled = true,
            MinimumMemoryMb = 2048,
            MaximumMemoryMb = 8192,
            MemoryBufferPercent = 30,
            NetworkAdapters =
            [
                new MachineNetworkAdapterConfig { AdapterName = "Network Adapter", SwitchName = "Default Switch" }
            ],
            ChangedFieldKeys = ["cpuCount", "switch:Network Adapter"]
        };

        var result = await service.ApplyEditsAsync(vm, draft);

        Assert.True(result.Success);
        Assert.NotNull(adminService.LastEditRequest);
        Assert.Equal(4, adminService.LastEditRequest!.ProcessorCount);
        Assert.Single(adminService.LastEditRequest.NetworkAdapterAssignments);
        var started = Assert.Single(logger.Events, e => e.Code == $"0x{LaStatus.Machines_ApplyingMachineEdits:X8}");
        Assert.Contains("cpuCount", (object[]?)started.Context?["changedFields"] ?? Array.Empty<object>());
    }

    [Fact]
    public async Task LoadEditSnapshotAsync_MapsNetworkAdapters()
    {
        var adminService = new FakeMachineAdminService
        {
            EditSnapshot = new HyperVMachineEditSnapshot
            {
                ProcessorCount = 2,
                StartupMemoryBytes = 2L * 1024 * 1024 * 1024,
                DynamicMemoryEnabled = true,
                MinimumMemoryBytes = 1L * 1024 * 1024 * 1024,
                MaximumMemoryBytes = 4L * 1024 * 1024 * 1024,
                MemoryBufferPercent = 25,
                NetworkAdapters =
                [
                    new HyperVMachineNetworkAdapterInfo { AdapterName = "Network Adapter", SwitchName = "Default Switch" }
                ]
            }
        };
        var service = new MachinesCapabilityService(
            adminService,
            new FakeCatalogStore(),
            new FakeSettingsStore(@"D:\LabAssistant\VMs"));
        var vm = new MachineInventoryItem { VmId = "vm-1", VmName = "Vm01" };

        var snapshot = await service.LoadEditSnapshotAsync(vm);

        Assert.NotNull(snapshot);
        Assert.Equal(2, snapshot!.CpuCount);
        Assert.Equal(2048, snapshot.StartupMemoryMb);
        Assert.Single(snapshot.NetworkAdapters);
        Assert.Equal("Default Switch", snapshot.NetworkAdapters[0].SwitchName);
    }

    [Fact]
    public async Task GetDeletePreviewAsync_WhenPolicyAlwaysDeleteAndUncertainDisk_FallsBackToVmOnly()
    {
        var adminService = new FakeMachineAdminService
        {
            DiskClassifications =
            [
                new HyperVMachineDiskClassificationResult
                {
                    DiskPath = @"D:\Labs\Vm01\disk.vhdx",
                    Classification = HyperVMachineDiskSafetyClassification.PotentialBaseOrUncertain,
                    Reason = "vhd_probe_failed"
                }
            ]
        };
        var settings = new FakeSettingsStore(@"D:\LabAssistant\VMs");
        settings.Settings.MachineDeletionPolicy = MachineDeletionPolicyMode.AlwaysDeleteDisks.ToString();

        var service = new MachinesCapabilityService(
            adminService,
            new FakeCatalogStore(),
            settings);
        var vm = new MachineInventoryItem { VmId = "vm-1", VmName = "Vm01", OriginLabel = "LabAssistant" };

        var preview = await service.GetDeletePreviewAsync(vm);

        Assert.Equal(MachineDeleteScope.VmRegistrationOnly, preview.DefaultScope);
        Assert.False(preview.SafeForAutomaticStorageDeletion);
    }

    [Fact]
    public async Task SetDeletionPolicyAsync_PersistsAndReturnsConfiguredMode()
    {
        var adminService = new FakeMachineAdminService();
        var settings = new FakeSettingsStore(@"D:\LabAssistant\VMs");
        var service = new MachinesCapabilityService(
            adminService,
            new FakeCatalogStore(),
            settings);

        await service.SetDeletionPolicyAsync(MachineDeletionPolicyMode.AlwaysDeleteDisksForDifferencingOnly);
        var configured = await service.GetDeletionPolicyAsync();

        Assert.Equal(MachineDeletionPolicyMode.AlwaysDeleteDisksForDifferencingOnly, configured);
        Assert.Equal(MachineDeletionPolicyMode.AlwaysDeleteDisksForDifferencingOnly.ToString(), settings.Settings.MachineDeletionPolicy);
    }

    private sealed class FakeMachineAdminService : IHyperVMachineAdminService
    {
        public IReadOnlyList<HyperVHostMachineVmInfo> Inventory { get; set; } = Array.Empty<HyperVHostMachineVmInfo>();
        public IReadOnlyList<string> IpAddresses { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> SwitchNames { get; set; } = ["Default Switch"];
        public IReadOnlyList<HyperVMachineDiskClassificationResult> DiskClassifications { get; set; } = [];
        public HyperVMachineEditSnapshot? EditSnapshot { get; set; }
        public HyperVMachineEditRequest? LastEditRequest { get; private set; }

        public HyperVMachineActionResult ActionResult { get; set; } = new() { Success = true };

        public HyperVMachineActionResult DeleteResult { get; set; } = new() { Success = true };

        public Task<IReadOnlyList<HyperVHostMachineVmInfo>> ListHostVmsAsync() => Task.FromResult(Inventory);

        public Task<HyperVMachineActionResult> StartVmAsync(string vmName) => Task.FromResult(ActionResult);

        public Task<HyperVMachineActionResult> StopVmAsync(string vmName) => Task.FromResult(ActionResult);

        public Task<HyperVMachineActionResult> RestartVmAsync(string vmName) => Task.FromResult(ActionResult);

        public Task<HyperVMachineActionResult> OpenConsoleAsync(string vmName) => Task.FromResult(ActionResult);

        public Task<IReadOnlyList<string>> GetVmIpAddressesAsync(string vmName) => Task.FromResult(IpAddresses);

        public Task<HyperVMachineActionResult> OpenRdpAsync(string targetIpv4) => Task.FromResult(ActionResult);

        public Task<HyperVMachineEditSnapshot?> GetVmEditSnapshotAsync(string vmName) => Task.FromResult(EditSnapshot);

        public Task<IReadOnlyList<string>> GetVirtualSwitchNamesAsync() => Task.FromResult(SwitchNames);

        public Task<IReadOnlyList<HyperVVirtualSwitchInfo>> ListVirtualSwitchesAsync() => Task.FromResult<IReadOnlyList<HyperVVirtualSwitchInfo>>(Array.Empty<HyperVVirtualSwitchInfo>());

        public Task<IReadOnlyList<string>> GetAttachedVmNamesForSwitchAsync(string switchName) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<HyperVMachineActionResult> CreateVirtualSwitchAsync(HyperVVirtualSwitchCreateRequest request) => Task.FromResult(ActionResult);

        public Task<HyperVMachineActionResult> RenameVirtualSwitchAsync(string currentName, string newName) => Task.FromResult(ActionResult);

        public Task<HyperVMachineActionResult> DeleteVirtualSwitchAsync(string switchName) => Task.FromResult(ActionResult);

        public Task<HyperVMachineActionResult> ApplyVmEditAsync(string vmName, HyperVMachineEditRequest request)
        {
            LastEditRequest = request;
            return Task.FromResult(ActionResult);
        }

        public Task<IReadOnlyList<HyperVMachineDiskClassificationResult>> ClassifyVmDisksAsync(
            string vmName,
            IReadOnlyCollection<string> knownBaseDiskPaths,
            string? differencingDiskBasePath)
        {
            return Task.FromResult(DiskClassifications);
        }

        public Task<HyperVMachineActionResult> DeleteVmAsync(string vmName, bool includeStorage) => Task.FromResult(DeleteResult);
    }

    private sealed class FakeCatalogStore : IVhdxCatalogStore
    {
        public VhdxCatalogLoadResult Load(string catalogPath) => new();

        public VhdxCatalogSaveResult Save(string catalogPath, IEnumerable<VhdxCatalogItem> items) => new();

        public void EnsureCatalogFileExists(string catalogPath) { }
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
