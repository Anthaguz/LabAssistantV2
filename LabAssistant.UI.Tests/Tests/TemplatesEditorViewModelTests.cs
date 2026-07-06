using System.Linq;
using System.Threading.Tasks;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels.Templates;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Behavior tests for the migrated <see cref="TemplatesEditorViewModel"/>: document load, VM slot
/// add/remove with confirmation, cancel routing, and the save gate. The view model is dispatcher-free
/// and tolerates a null reference-data service (empty VHDX catalog), so the VHDX-resolution gate can be
/// exercised without faking the heavy host reference-data services.
/// </summary>
public sealed class TemplatesEditorViewModelTests
{
    private static TemplateEditorDocument DocumentWith(params VmTemplate[] vms)
        => new()
        {
            Template = new LabTemplate { Name = "Doc", VmTemplates = vms.ToList() },
            SourceFilePath = null
        };

    private static (TemplatesEditorViewModel Vm, RecordingTemplatesCapabilityService Service, RecordingTemplatesEditorHost Host) CreateSut()
    {
        var service = new RecordingTemplatesCapabilityService();
        var host = new RecordingTemplatesEditorHost();
        var vm = new TemplatesEditorViewModel(service);
        // Null reference-data service => empty VHDX catalog, exercised deliberately by the resolution-gate test.
        vm.Attach(null!, host);
        return (vm, service, host);
    }

    [Fact]
    public async Task ShowDocument_PopulatesHeaderAndSlots()
    {
        var (vm, _, _) = CreateSut();
        var doc = DocumentWith(
            new VmTemplate { Name = "VM-1", MemoryMb = 2048, CpuCount = 2 },
            new VmTemplate { Name = "VM-2", MemoryMb = 4096, CpuCount = 4 });

        await vm.ShowDocumentAsync(doc, "Template loaded.");

        Assert.Equal("Doc", vm.TemplateName);
        Assert.Equal(2, vm.Slots.Count);
        Assert.Equal("Template loaded.", vm.StatusMessage);
        Assert.NotNull(vm.SelectedSlot);
    }

    [Fact]
    public async Task Save_BlockedWhenSlotHasUnresolvableVhdx()
    {
        var (vm, service, host) = CreateSut();
        var doc = DocumentWith(new VmTemplate { Name = "VM-1", MemoryMb = 2048, CpuCount = 2, VhdxId = "ghost" });
        await vm.ShowDocumentAsync(doc, "Template loaded.");

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(0, service.SaveCallCount);
        Assert.Empty(host.ReloadLibraryCalls);
        Assert.Equal(vm.BaseDiskGuidanceText, vm.StatusMessage);
    }

    [Fact]
    public async Task Save_SucceedsForCleanSlotAndReloadsLibrary()
    {
        var (vm, service, host) = CreateSut();
        service.SaveResult = new TemplateOperationResult { Success = true, UserMessage = "Saved.", FilePath = "saved.json" };
        var doc = DocumentWith(new VmTemplate { Name = "VM-1", MemoryMb = 2048, CpuCount = 2 });
        await vm.ShowDocumentAsync(doc, "Template loaded.");

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(1, service.SaveCallCount);
        Assert.Equal("Saved.", vm.StatusMessage);
        var reload = Assert.Single(host.ReloadLibraryCalls);
        Assert.True(reload);
    }

    [Fact]
    public async Task AddSlot_AppendsAndSelectsNewEntry()
    {
        var (vm, _, _) = CreateSut();
        await vm.ShowDocumentAsync(DocumentWith(new VmTemplate { Name = "VM-1", MemoryMb = 2048, CpuCount = 2 }), "Template loaded.");

        vm.AddSlotCommand.Execute(null);

        Assert.Equal(2, vm.Slots.Count);
        Assert.Same(vm.Slots.Last(), vm.SelectedSlot);
    }

    [Fact]
    public async Task RemoveSlot_Confirmed_RemovesEntry()
    {
        var (vm, _, host) = CreateSut();
        host.ConfirmRemoveVmResult = true;
        await vm.ShowDocumentAsync(
            DocumentWith(
                new VmTemplate { Name = "VM-1", MemoryMb = 2048, CpuCount = 2 },
                new VmTemplate { Name = "VM-2", MemoryMb = 2048, CpuCount = 2 }),
            "Template loaded.");

        await vm.RemoveSlotCommand.ExecuteAsync(null);

        Assert.Single(host.RemoveVmConfirmationRequests);
        Assert.Single(vm.Slots);
    }

    [Fact]
    public async Task RemoveSlot_Declined_KeepsEntry()
    {
        var (vm, _, host) = CreateSut();
        host.ConfirmRemoveVmResult = false;
        await vm.ShowDocumentAsync(
            DocumentWith(
                new VmTemplate { Name = "VM-1", MemoryMb = 2048, CpuCount = 2 },
                new VmTemplate { Name = "VM-2", MemoryMb = 2048, CpuCount = 2 }),
            "Template loaded.");

        await vm.RemoveSlotCommand.ExecuteAsync(null);

        Assert.Single(host.RemoveVmConfirmationRequests);
        Assert.Equal(2, vm.Slots.Count);
    }

    [Fact]
    public void Cancel_RoutesToLibrary()
    {
        var (vm, _, host) = CreateSut();

        vm.CancelCommand.Execute(null);

        Assert.Equal(1, host.NavigateToLibraryCallCount);
    }
}
