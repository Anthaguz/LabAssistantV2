using LabAssistant.Business.Machines;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;
using System.Xml.Linq;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

public sealed class MilestoneAAScenarioMatrixTests
{
    [Fact]
    public void Shell_LayoutContract_KeepsInsightsCollapsedByDefault()
    {
        var xaml = LoadMainWindowXaml();
        var insights = FindByName(xaml, "InsightsPanel");

        Assert.Equal("Collapsed", insights.Attribute("Visibility")?.Value);
    }

    [Fact]
    public void Shell_DrawerContract_UsesFixedWidthAndContentRowOverlay()
    {
        var xaml = LoadMainWindowXaml();
        var drawer = FindByName(xaml, "CapabilityDrawer");
        var scrim = FindByName(xaml, "DrawerScrim");

        Assert.Equal("280", drawer.Attribute("Width")?.Value);
        Assert.Equal("1", GetAttributeValue(drawer, "Grid.Row"));
        Assert.Equal("1", GetAttributeValue(scrim, "Grid.Row"));
    }

    [Fact]
    public void Shell_CapabilityNavigation_ListsExpectedCapabilitiesInRailAndDrawer()
    {
        var xaml = LoadMainWindowXaml();
        var expectedTags = new[] { "Machines", "Deploy", "Templates", "Assets", "Diagnostics", "Settings" };

        foreach (var tag in expectedTags)
        {
            Assert.Contains(xaml.Descendants().Where(e => e.Name.LocalName == "Button"),
                b => b.Attribute("Tag")?.Value == tag && (b.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value?.Contains("RailButton") ?? false));
            Assert.Contains(xaml.Descendants().Where(e => e.Name.LocalName == "Button"),
                b => b.Attribute("Tag")?.Value == tag && (b.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value?.Contains("DrawerButton") ?? false));
        }
    }

    [Fact]
    public void Machines_RdpAction_IsVisibleButDisabledWithReasonText()
    {
        var xaml = LoadMainWindowXaml();
        var rdpButton = FindByName(xaml, "OpenRdpButton");

        Assert.Equal("False", rdpButton.Attribute("IsEnabled")?.Value);
        var readinessText = FindByName(xaml, "RdpReadinessTextBlock");
        Assert.Contains("RDP readiness", readinessText.Attribute("Text")?.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Machines_InventoryMapping_ExposesVmStateAndOrigin()
    {
        var machineAdmin = new RecordingMachineAdminService
        {
            Inventory =
            [
                new HyperVHostMachineVmInfo
                {
                    VmId = "vm-1",
                    VmName = "VM-01",
                    State = "Running",
                    VmPath = @"D:\Labs\VM-01",
                    DiskPaths = [@"D:\Labs\VM-01\VM-01.vhdx"]
                },
                new HyperVHostMachineVmInfo
                {
                    VmId = "vm-ext",
                    VmName = "External-VM",
                    State = "Off",
                    VmPath = @"E:\External\External-VM"
                }
            ]
        };

        var service = CreateMachinesService(machineAdmin);
        var inventory = await service.LoadInventoryAsync();

        Assert.Equal(2, inventory.Count);
        var first = Assert.Single(inventory, vm => vm.VmName == "VM-01");
        var second = Assert.Single(inventory, vm => vm.VmName == "External-VM");

        Assert.Equal("Running", first.State);
        Assert.Equal("LabAssistant", first.OriginLabel);
        Assert.Equal("External/Unknown", second.OriginLabel);
    }

    [Fact]
    public async Task Machines_DeleteScopePolicy_PassesExpectedIncludeStorageFlag()
    {
        var machineAdmin = new RecordingMachineAdminService();
        var service = CreateMachinesService(machineAdmin);
        var vm = new MachineInventoryItem
        {
            VmName = "VM-Delete",
            VmId = "vm-delete",
            State = "Off",
            OriginLabel = "LabAssistant"
        };

        await service.DeleteVmAsync(vm, MachineDeleteScope.VmRegistrationOnly);
        await service.DeleteVmAsync(vm, MachineDeleteScope.VmAndStorage);

        Assert.Equal(2, machineAdmin.DeleteCalls.Count);
        Assert.Equal(("VM-Delete", false), machineAdmin.DeleteCalls[0]);
        Assert.Equal(("VM-Delete", true), machineAdmin.DeleteCalls[1]);
    }

    [Fact]
    public async Task Machines_ActionLogging_IncludesOperationContextFields()
    {
        var logger = new RecordingStructuredLogger();
        var machineAdmin = new RecordingMachineAdminService();
        var service = CreateMachinesService(machineAdmin, logger);
        var vm = new MachineInventoryItem
        {
            VmName = "VM-Action",
            VmId = "vm-action",
            State = "Running",
            OriginLabel = "LabAssistant"
        };

        await service.StartVmAsync(vm);

        var started = Assert.Single(logger.Events, e => e.Event == "MachineActionStarted");
        var completed = Assert.Single(logger.Events, e => e.Event == "MachineActionCompleted");

        Assert.False(string.IsNullOrWhiteSpace(started.OperationId));
        Assert.False(string.IsNullOrWhiteSpace(completed.OperationId));
        Assert.Equal("start", started.Context?["action"]?.ToString());
        Assert.Equal("VM-Action", started.Context?["vmName"]?.ToString());
        Assert.Equal("vm-action", started.Context?["vmId"]?.ToString());
        Assert.Equal("Running", started.Context?["vmState"]?.ToString());
        Assert.Equal("LabAssistant", started.Context?["vmOrigin"]?.ToString());
    }

    [Fact]
    public async Task Machines_DeleteLogging_RecordsDeleteScopeInContext()
    {
        var logger = new RecordingStructuredLogger();
        var machineAdmin = new RecordingMachineAdminService
        {
            DeleteResult = new HyperVMachineActionResult
            {
                Success = false,
                ErrorMessage = "delete failed"
            }
        };
        var service = CreateMachinesService(machineAdmin, logger);
        var vm = new MachineInventoryItem
        {
            VmName = "VM-Delete",
            VmId = "vm-delete",
            State = "Off",
            OriginLabel = "LabAssistant"
        };

        var result = await service.DeleteVmAsync(vm, MachineDeleteScope.VmAndStorage);

        Assert.False(result.Success);
        var failed = Assert.Single(logger.Events, e => e.Event == "MachineDeleteFailed");
        Assert.Equal("vm_and_storage", failed.Context?["deleteScope"]?.ToString());
        Assert.Equal("AskEveryTime", failed.Context?["policyMode"]?.ToString());
        Assert.Equal("VM-Delete", failed.Context?["vmName"]?.ToString());
    }

    [Fact]
    public void Machines_EditWorkflow_ShowsApplyWithoutResetButton()
    {
        var xaml = LoadMainWindowXaml();
        var applyButton = FindByName(xaml, "ApplyMachineEditsButton");

        Assert.Equal("Apply", applyButton.Attribute("Content")?.Value);
        Assert.DoesNotContain(
            xaml.Descendants().Where(e => e.Name.LocalName == "Button"),
            button => string.Equals(button.Attribute("Content")?.Value, "Reset", StringComparison.OrdinalIgnoreCase));
    }

    private static MachinesCapabilityService CreateMachinesService(
        RecordingMachineAdminService machineAdmin,
        RecordingStructuredLogger? logger = null)
    {
        return new MachinesCapabilityService(
            machineAdmin,
            new FakeCatalogStore(),
            new FakeAppSettingsStore
            {
                Settings = new AppSettings
                {
                    VmBasePath = @"D:\Labs"
                }
            },
            logger);
    }

    private static XDocument LoadMainWindowXaml()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabAssistant.WinUI", "MainWindow.xaml");
        return XDocument.Load(Path.GetFullPath(path));
    }

    private static XElement FindByName(XDocument xaml, string name)
    {
        return xaml
            .Descendants()
            .Single(element => element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == name);
    }

    private static string? GetAttributeValue(XElement element, string attributeName)
    {
        return element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == attributeName)?.Value;
    }

    private sealed class RecordingMachineAdminService : IHyperVMachineAdminService
    {
        public IReadOnlyList<HyperVHostMachineVmInfo> Inventory { get; set; } = [];

        public List<(string VmName, bool IncludeStorage)> DeleteCalls { get; } = [];

        public HyperVMachineActionResult StartResult { get; set; } = new() { Success = true };

        public HyperVMachineActionResult StopResult { get; set; } = new() { Success = true };

        public HyperVMachineActionResult RestartResult { get; set; } = new() { Success = true };

        public HyperVMachineActionResult OpenConsoleResult { get; set; } = new() { Success = true };
        public HyperVMachineActionResult OpenRdpResult { get; set; } = new() { Success = true };
        public HyperVMachineEditSnapshot? EditSnapshot { get; set; }

        public HyperVMachineActionResult DeleteResult { get; set; } = new() { Success = true };

        public Task<IReadOnlyList<HyperVHostMachineVmInfo>> ListHostVmsAsync()
        {
            return Task.FromResult(Inventory);
        }

        public Task<HyperVMachineActionResult> StartVmAsync(string vmName)
        {
            return Task.FromResult(StartResult);
        }

        public Task<HyperVMachineActionResult> StopVmAsync(string vmName)
        {
            return Task.FromResult(StopResult);
        }

        public Task<HyperVMachineActionResult> RestartVmAsync(string vmName)
        {
            return Task.FromResult(RestartResult);
        }

        public Task<HyperVMachineActionResult> OpenConsoleAsync(string vmName)
        {
            return Task.FromResult(OpenConsoleResult);
        }

        public Task<IReadOnlyList<string>> GetVmIpAddressesAsync(string vmName)
        {
            return Task.FromResult<IReadOnlyList<string>>(["192.168.1.50"]);
        }

        public Task<HyperVMachineActionResult> OpenRdpAsync(string targetIpv4)
        {
            return Task.FromResult(OpenRdpResult);
        }

        public Task<HyperVMachineEditSnapshot?> GetVmEditSnapshotAsync(string vmName)
        {
            return Task.FromResult(EditSnapshot);
        }

        public Task<IReadOnlyList<string>> GetVirtualSwitchNamesAsync()
        {
            return Task.FromResult<IReadOnlyList<string>>(["Default Switch"]);
        }

        public Task<HyperVMachineActionResult> ApplyVmEditAsync(string vmName, HyperVMachineEditRequest request)
        {
            return Task.FromResult(new HyperVMachineActionResult { Success = true });
        }

        public Task<IReadOnlyList<HyperVMachineDiskClassificationResult>> ClassifyVmDisksAsync(
            string vmName,
            IReadOnlyCollection<string> knownBaseDiskPaths,
            string? differencingDiskBasePath)
        {
            return Task.FromResult<IReadOnlyList<HyperVMachineDiskClassificationResult>>([]);
        }

        public Task<HyperVMachineActionResult> DeleteVmAsync(string vmName, bool includeStorage)
        {
            DeleteCalls.Add((vmName, includeStorage));
            return Task.FromResult(DeleteResult);
        }
    }

    private sealed class FakeCatalogStore : IVhdxCatalogStore
    {
        public VhdxCatalogLoadResult Load(string catalogPath) => new();

        public VhdxCatalogSaveResult Save(string catalogPath, IEnumerable<VhdxCatalogItem> items) => new();

        public void EnsureCatalogFileExists(string catalogPath) { }
    }

    private sealed class RecordingStructuredLogger : IStructuredLogger
    {
        public List<StructuredLogEvent> Events { get; } = [];

        public void Log(StructuredLogEvent logEvent)
        {
            Events.Add(logEvent);
        }

        public void Log(
            StructuredLogLevel level,
            string eventName,
            string operationId,
            string? result = null,
            IReadOnlyDictionary<string, object?>? context = null)
        {
            Events.Add(StructuredLogEvent.Create(level, eventName, operationId, result, context));
        }
    }

    private sealed class FakeAppSettingsStore : IAppSettingsStore
    {
        public AppSettings Settings { get; set; } = new();

        public string SettingsPath => string.Empty;

        public void LoadOrCreate()
        {
        }

        public void Reload()
        {
        }

        public void Save()
        {
        }

        public void ResetToDefault()
        {
        }

        public void SetTemplateFolder(string path)
        {
            Settings.TemplateFolder = path;
        }

        public void SetLogFolder(string path)
        {
            Settings.LogFolder = path;
        }

        public void SetVmBasePath(string path)
        {
            Settings.VmBasePath = path;
        }

        public void SetDifferencingDiskBasePath(string path)
        {
            Settings.DifferencingDiskBasePath = path;
        }
    }
}
