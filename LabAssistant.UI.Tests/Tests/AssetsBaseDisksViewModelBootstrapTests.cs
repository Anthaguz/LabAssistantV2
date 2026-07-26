using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LabAssistant.Business.Assets;
using LabAssistant.WinUI.ViewModels.Assets;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Runtime-independent tests for the guest bootstrap-profile authoring surface on
/// <see cref="AssetsBaseDisksViewModel"/>. They pin the behavior that lets a base disk be marked
/// bootstrap-capable through the UI (a prerequisite for every guest-configured deploy) and guard the
/// data-loss regression where saving unrelated metadata silently wiped an existing bootstrap profile.
/// </summary>
public sealed class AssetsBaseDisksViewModelBootstrapTests
{
    [Fact]
    public async Task SelectingDisk_PopulatesBootstrapEditorFieldsFromRecord()
    {
        var service = new FakeCapabilityService();
        service.Items.Add(new AssetsBaseDiskRecord
        {
            Id = "disk-1",
            Path = @"C:\BaseDisks\WinServer2022-Base.vhdx",
            OsName = "Windows Server 2022",
            OsVersion = "21H2",
            Generation = 2,
            BootstrapExpectedLocalUser = "Administrator",
            BootstrapLocalCredentialSlotRef = "disk.win-server-2022.local-admin",
            BootstrapGuestOsFamily = "WindowsServer",
            BootstrapGuestTransport = "powershell-direct",
            BootstrapNotes = "OOBE complete; local admin ready."
        });

        var vm = new AssetsBaseDisksViewModel(service);
        await vm.InitializeAsync();

        Assert.NotNull(vm.SelectedDisk);
        Assert.Equal("Administrator", vm.BootstrapExpectedLocalUser);
        Assert.Equal("disk.win-server-2022.local-admin", vm.BootstrapLocalCredentialSlotRef);
        Assert.Equal("WindowsServer", vm.BootstrapGuestOsFamily);
        Assert.Equal("powershell-direct", vm.BootstrapGuestTransport);
        Assert.Equal("OOBE complete; local admin ready.", vm.BootstrapNotes);
    }

    [Fact]
    public async Task SavingUnrelatedMetadata_PreservesExistingBootstrapProfile()
    {
        var service = new FakeCapabilityService();
        service.Items.Add(new AssetsBaseDiskRecord
        {
            Id = "disk-1",
            Path = @"C:\BaseDisks\WinServer2022-Base.vhdx",
            OsName = "Windows Server 2022",
            OsVersion = "21H2",
            Generation = 2,
            BootstrapExpectedLocalUser = "Administrator",
            BootstrapLocalCredentialSlotRef = "disk.win-server-2022.local-admin",
            BootstrapGuestOsFamily = "WindowsServer",
            BootstrapGuestTransport = "powershell-direct",
            BootstrapNotes = "OOBE complete."
        });

        var vm = new AssetsBaseDisksViewModel(service);
        await vm.InitializeAsync();

        // Edit only an unrelated field, then save - this is the path that used to wipe the profile.
        vm.OsName = "Windows Server 2022 (patched)";
        await vm.SaveMetadataCommand.ExecuteAsync(null);

        var saved = Assert.Single(service.SavedDrafts);
        Assert.Equal("Windows Server 2022 (patched)", saved.OsName);
        Assert.Equal("Administrator", saved.BootstrapExpectedLocalUser);
        Assert.Equal("disk.win-server-2022.local-admin", saved.BootstrapLocalCredentialSlotRef);
        Assert.Equal("WindowsServer", saved.BootstrapGuestOsFamily);
        Assert.Equal("powershell-direct", saved.BootstrapGuestTransport);
        Assert.Equal("OOBE complete.", saved.BootstrapNotes);
    }

    [Fact]
    public async Task AuthoringBootstrapFields_FlowsThroughToSavedDraft()
    {
        var service = new FakeCapabilityService();
        var vm = new AssetsBaseDisksViewModel(service)
        {
            PickBaseDiskFilePath = () => @"C:\BaseDisks\NewImage.vhdx"
        };
        await vm.InitializeAsync();

        await vm.AddDiskCommand.ExecuteAsync(null);
        vm.OsName = "Windows Server 2022";
        vm.OsVersion = "21H2";
        vm.GenerationText = 2.ToString();
        vm.BootstrapExpectedLocalUser = "Administrator";
        vm.BootstrapLocalCredentialSlotRef = "disk.new-image.local-admin";
        vm.BootstrapGuestOsFamily = "WindowsServer";
        vm.BootstrapGuestTransport = "powershell-direct";
        vm.BootstrapNotes = "Freshly prepared.";

        await vm.SaveMetadataCommand.ExecuteAsync(null);

        var saved = Assert.Single(service.SavedDrafts);
        Assert.True(saved.IsNew);
        Assert.Equal("Administrator", saved.BootstrapExpectedLocalUser);
        Assert.Equal("disk.new-image.local-admin", saved.BootstrapLocalCredentialSlotRef);
        Assert.Equal("WindowsServer", saved.BootstrapGuestOsFamily);
        Assert.Equal("powershell-direct", saved.BootstrapGuestTransport);
        Assert.Equal("Freshly prepared.", saved.BootstrapNotes);
    }

    [Fact]
    public async Task ClearingAllBootstrapFields_ClearsProfileOnSavedDraft()
    {
        var service = new FakeCapabilityService();
        service.Items.Add(new AssetsBaseDiskRecord
        {
            Id = "disk-1",
            Path = @"C:\BaseDisks\WinServer2022-Base.vhdx",
            OsName = "Windows Server 2022",
            OsVersion = "21H2",
            Generation = 2,
            BootstrapExpectedLocalUser = "Administrator",
            BootstrapLocalCredentialSlotRef = "disk.win-server-2022.local-admin",
            BootstrapGuestOsFamily = "WindowsServer",
            BootstrapGuestTransport = "powershell-direct",
            BootstrapNotes = "OOBE complete."
        });

        var vm = new AssetsBaseDisksViewModel(service);
        await vm.InitializeAsync();

        vm.BootstrapExpectedLocalUser = string.Empty;
        vm.BootstrapLocalCredentialSlotRef = string.Empty;
        vm.BootstrapGuestOsFamily = string.Empty;
        vm.BootstrapGuestTransport = "   ";
        vm.BootstrapNotes = string.Empty;

        await vm.SaveMetadataCommand.ExecuteAsync(null);

        var saved = Assert.Single(service.SavedDrafts);
        Assert.Null(saved.BootstrapExpectedLocalUser);
        Assert.Null(saved.BootstrapLocalCredentialSlotRef);
        Assert.Null(saved.BootstrapGuestOsFamily);
        Assert.Null(saved.BootstrapGuestTransport);
        Assert.Null(saved.BootstrapNotes);
    }

    private sealed class FakeCapabilityService : IAssetsBaseDisksCapabilityService
    {
        public List<AssetsBaseDiskRecord> Items { get; } = new();

        public List<AssetsBaseDiskDraft> SavedDrafts { get; } = new();

        public Task<AssetsBaseDisksCatalogResult> LoadAsync(bool isRefresh = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AssetsBaseDisksCatalogResult
            {
                Items = Items.ToArray()
            });
        }

        public Task<AssetsBaseDiskOperationResult> SaveAsync(AssetsBaseDiskDraft draft, CancellationToken cancellationToken = default)
        {
            SavedDrafts.Add(draft);

            var record = new AssetsBaseDiskRecord
            {
                Id = string.IsNullOrEmpty(draft.Id) ? $"disk-{Items.Count + 1}" : draft.Id,
                Path = draft.Path,
                OsName = draft.OsName,
                OsVersion = draft.OsVersion,
                Generation = draft.Generation,
                Notes = draft.Notes,
                BootstrapExpectedLocalUser = draft.BootstrapExpectedLocalUser,
                BootstrapLocalCredentialSlotRef = draft.BootstrapLocalCredentialSlotRef,
                BootstrapGuestOsFamily = draft.BootstrapGuestOsFamily,
                BootstrapGuestTransport = draft.BootstrapGuestTransport,
                BootstrapNotes = draft.BootstrapNotes
            };

            var index = Items.FindIndex(item => string.Equals(item.Id, record.Id, System.StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                Items[index] = record;
            }
            else
            {
                Items.Add(record);
            }

            return Task.FromResult(new AssetsBaseDiskOperationResult
            {
                Success = true,
                UserMessage = "Saved.",
                Item = record
            });
        }

        public Task<AssetsBaseDiskValidationResult> ValidateAsync(AssetsBaseDiskDraft draft, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AssetsBaseDiskValidationResult
            {
                Severity = "Pass",
                Summary = "Ready."
            });
        }

        public Task<AssetsBaseDiskRemovalAssessment> AssessRemoveAsync(string baseDiskId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AssetsBaseDiskRemovalAssessment { Exists = true, CanRemove = true });
        }

        public Task<AssetsBaseDiskOperationResult> RemoveAsync(string baseDiskId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AssetsBaseDiskOperationResult { Success = true, UserMessage = "Removed." });
        }
    }
}
