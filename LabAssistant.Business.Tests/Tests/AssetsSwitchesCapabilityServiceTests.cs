using LabAssistant.Business.Assets;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;
using Xunit;

namespace LabAssistant.Business.Tests;

public sealed class AssetsSwitchesCapabilityServiceTests
{
    [Fact]
    public async Task LoadAsync_ReturnsSwitchInventory_AndOperationId()
    {
        var machineAdmin = new FakeHyperVMachineAdminService
        {
            Switches =
            [
                new HyperVVirtualSwitchInfo
                {
                    Name = "External Lab",
                    SwitchType = "External",
                    AdapterName = "Intel Ethernet"
                }
            ]
        };
        var service = CreateService(machineAdmin);

        var result = await service.LoadAsync();

        Assert.Single(result.Items);
        Assert.Equal("External Lab", result.Items[0].Name);
        Assert.False(string.IsNullOrWhiteSpace(result.OperationId));
    }

    [Fact]
    public async Task SaveAsync_BlocksDuplicateSwitchName()
    {
        var machineAdmin = new FakeHyperVMachineAdminService
        {
            Switches =
            [
                new HyperVVirtualSwitchInfo
                {
                    Name = "External Lab",
                    SwitchType = "External",
                    AdapterName = "Intel Ethernet"
                }
            ]
        };
        var service = CreateService(machineAdmin);

        var result = await service.SaveAsync(new AssetsSwitchDraft
        {
            IsNew = true,
            Name = "External Lab",
            SwitchType = "External",
            AdapterName = "Intel Ethernet"
        });

        Assert.False(result.Success);
        Assert.Contains("already exists", result.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAsync_UsesKnownInventoryWithoutListingHost()
    {
        var machineAdmin = new FakeHyperVMachineAdminService();
        var service = CreateService(machineAdmin);
        var knownInventory = new[]
        {
            new AssetsSwitchRecord
            {
                Name = "External Lab",
                SwitchType = "External",
                AdapterName = "Intel Ethernet"
            }
        };

        var result = await service.ValidateAsync(
            new AssetsSwitchDraft
            {
                IsNew = true,
                Name = "External Lab",
                SwitchType = "External",
                AdapterName = "Intel Ethernet"
            },
            knownInventory);

        Assert.Equal("Block", result.Severity);
        Assert.Equal(0, machineAdmin.ListVirtualSwitchesCallCount);
    }

    [Fact]
    public async Task AssessDeleteAsync_BlocksWhenAttachedVmsExist()
    {
        var machineAdmin = new FakeHyperVMachineAdminService
        {
            Switches =
            [
                new HyperVVirtualSwitchInfo
                {
                    Name = "External Lab",
                    SwitchType = "External",
                    AdapterName = "Intel Ethernet"
                }
            ],
            AttachedVmNamesBySwitch =
            {
                ["External Lab"] = ["vm01", "vm02"]
            }
        };
        var service = CreateService(machineAdmin);

        var result = await service.AssessDeleteAsync("External Lab");

        Assert.True(result.Exists);
        Assert.False(result.CanDelete);
        Assert.Contains("vm01", string.Join(" ", result.BlockingReasons), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AssessDeleteAsync_UsesKnownInventoryWithoutListingHost()
    {
        var machineAdmin = new FakeHyperVMachineAdminService
        {
            AttachedVmNamesBySwitch =
            {
                ["External Lab"] = ["vm01"]
            }
        };
        var service = CreateService(machineAdmin);
        var knownInventory = new[]
        {
            new AssetsSwitchRecord
            {
                Name = "External Lab",
                SwitchType = "External",
                AdapterName = "Intel Ethernet"
            }
        };

        var result = await service.AssessDeleteAsync("External Lab", knownInventory);

        Assert.True(result.Exists);
        Assert.False(result.CanDelete);
        Assert.Equal(0, machineAdmin.ListVirtualSwitchesCallCount);
        Assert.Equal(1, machineAdmin.GetAttachedVmNamesForSwitchCallCount);
    }

    [Fact]
    public async Task DeleteAsync_SucceedsWhenNoAttachedVmsExist()
    {
        var machineAdmin = new FakeHyperVMachineAdminService
        {
            Switches =
            [
                new HyperVVirtualSwitchInfo
                {
                    Name = "Private Lab",
                    SwitchType = "Private"
                }
            ]
        };
        var service = CreateService(machineAdmin);

        var result = await service.DeleteAsync("Private Lab");

        Assert.True(result.Success);
        Assert.Equal("Private Lab", machineAdmin.DeletedSwitchName);
        Assert.Contains("deleted successfully", result.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteAsync_ReusesProvidedAssessmentWithoutRepeatingHostQueries()
    {
        var machineAdmin = new FakeHyperVMachineAdminService();
        var service = CreateService(machineAdmin);

        var assessment = new AssetsSwitchDeleteAssessment
        {
            Exists = true,
            CanDelete = true,
            Item = new AssetsSwitchRecord
            {
                Name = "Private Lab",
                SwitchType = "Private"
            }
        };

        var result = await service.DeleteAsync("Private Lab", assessment);

        Assert.True(result.Success);
        Assert.Equal("Private Lab", machineAdmin.DeletedSwitchName);
        Assert.Equal(0, machineAdmin.ListVirtualSwitchesCallCount);
        Assert.Equal(0, machineAdmin.GetAttachedVmNamesForSwitchCallCount);
    }

    private static IAssetsSwitchesCapabilityService CreateService(
        FakeHyperVMachineAdminService machineAdmin,
        RecordingStructuredLogger? logger = null)
    {
        return new AssetsSwitchesCapabilityService(machineAdmin, logger ?? new RecordingStructuredLogger());
    }

    private sealed class FakeHyperVMachineAdminService : IHyperVMachineAdminService
    {
        public List<HyperVVirtualSwitchInfo> Switches { get; set; } = [];

        public Dictionary<string, IReadOnlyList<string>> AttachedVmNamesBySwitch { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string? DeletedSwitchName { get; private set; }

        public int ListVirtualSwitchesCallCount { get; private set; }

        public int GetAttachedVmNamesForSwitchCallCount { get; private set; }

        public Task<IReadOnlyList<HyperVHostMachineVmInfo>> ListHostVmsAsync() => Task.FromResult<IReadOnlyList<HyperVHostMachineVmInfo>>(Array.Empty<HyperVHostMachineVmInfo>());
        public Task<HyperVMachineEditSnapshot?> GetVmEditSnapshotAsync(string vmName) => Task.FromResult<HyperVMachineEditSnapshot?>(null);
        public Task<IReadOnlyList<string>> GetVirtualSwitchNamesAsync() => Task.FromResult<IReadOnlyList<string>>(Switches.Select(item => item.Name).ToList());
        public Task<IReadOnlyList<HyperVVirtualSwitchInfo>> ListVirtualSwitchesAsync()
        {
            ListVirtualSwitchesCallCount++;
            return Task.FromResult<IReadOnlyList<HyperVVirtualSwitchInfo>>(Switches.ToList());
        }

        public Task<IReadOnlyList<string>> GetAttachedVmNamesForSwitchAsync(string switchName)
        {
            GetAttachedVmNamesForSwitchCallCount++;
            return Task.FromResult(AttachedVmNamesBySwitch.TryGetValue(switchName, out var value) ? value : (IReadOnlyList<string>)Array.Empty<string>());
        }

        public Task<HyperVMachineActionResult> CreateVirtualSwitchAsync(HyperVVirtualSwitchCreateRequest request)
        {
            Switches.Add(new HyperVVirtualSwitchInfo
            {
                Name = request.Name,
                SwitchType = request.SwitchType,
                AdapterName = request.AdapterName
            });
            return Task.FromResult(new HyperVMachineActionResult { Success = true });
        }

        public Task<HyperVMachineActionResult> RenameVirtualSwitchAsync(string currentName, string newName)
        {
            var existing = Switches.First(item => string.Equals(item.Name, currentName, StringComparison.OrdinalIgnoreCase));
            Switches.Remove(existing);
            Switches.Add(new HyperVVirtualSwitchInfo
            {
                Name = newName,
                SwitchType = existing.SwitchType,
                AdapterName = existing.AdapterName
            });
            return Task.FromResult(new HyperVMachineActionResult { Success = true });
        }

        public Task<HyperVMachineActionResult> DeleteVirtualSwitchAsync(string switchName)
        {
            DeletedSwitchName = switchName;
            Switches = Switches.Where(item => !string.Equals(item.Name, switchName, StringComparison.OrdinalIgnoreCase)).ToList();
            return Task.FromResult(new HyperVMachineActionResult { Success = true });
        }

        public Task<HyperVMachineActionResult> StartVmAsync(string vmName) => Task.FromResult(new HyperVMachineActionResult());
        public Task<HyperVMachineActionResult> StopVmAsync(string vmName) => Task.FromResult(new HyperVMachineActionResult());
        public Task<HyperVMachineActionResult> TurnOffVmAsync(string vmName) => Task.FromResult(new HyperVMachineActionResult());
        public Task<HyperVMachineActionResult> RestartVmAsync(string vmName) => Task.FromResult(new HyperVMachineActionResult());
        public Task<HyperVMachineActionResult> RenameVmAsync(string currentName, string newName) => Task.FromResult(new HyperVMachineActionResult());
        public Task<HyperVMachineActionResult> OpenConsoleAsync(string vmName) => Task.FromResult(new HyperVMachineActionResult());
        public Task<IReadOnlyList<string>> GetVmIpAddressesAsync(string vmName) => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<HyperVMachineActionResult> OpenRdpAsync(string targetIpv4) => Task.FromResult(new HyperVMachineActionResult());
        public Task<IReadOnlyList<HyperVMachineDiskClassificationResult>> ClassifyVmDisksAsync(string vmName, IReadOnlyCollection<string> knownBaseDiskPaths, string? differencingDiskBasePath) => Task.FromResult<IReadOnlyList<HyperVMachineDiskClassificationResult>>(Array.Empty<HyperVMachineDiskClassificationResult>());
        public Task<HyperVMachineActionResult> ApplyVmEditAsync(string vmName, HyperVMachineEditRequest request) => Task.FromResult(new HyperVMachineActionResult());
        public Task<HyperVMachineActionResult> DeleteVmAsync(string vmName, bool includeStorage) => Task.FromResult(new HyperVMachineActionResult());
    }

    private sealed class RecordingStructuredLogger : IStructuredLogger
    {
        public List<StructuredLogEvent> Events { get; } = [];

        public void Log(StructuredLogEvent logEvent) => Events.Add(logEvent);

        public void Log(StructuredLogLevel level, string eventName, string operationId, string? result = null, IReadOnlyDictionary<string, object?>? context = null)
        {
            Events.Add(StructuredLogEvent.Create(level, eventName, operationId, result, context));
        }
    }
}
