using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels.Templates;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Behavior tests for the migrated <see cref="TemplatesLibraryViewModel"/>: inventory load status
/// surfacing, selection preservation across reloads, and the cross-subview hand-offs (open in editor
/// on success, editor-status report on failure, V2-only builder gate, delete with confirmation). The
/// view model is dispatcher-free, so it is driven directly with fakes.
/// </summary>
public sealed class TemplatesLibraryViewModelTests
{
    private static TemplateLibraryItem Item(string id, string filePath, TemplateExecutionEngine engine = TemplateExecutionEngine.V2UnifiedPlanning)
        => new()
        {
            TemplateId = id,
            Name = id,
            FilePath = filePath,
            VmCount = 1,
            ExecutionEngine = engine
        };

    private static (TemplatesLibraryViewModel Vm, RecordingTemplatesCapabilityService Service, RecordingTemplatesLibraryHost Host) CreateSut()
    {
        var service = new RecordingTemplatesCapabilityService();
        var host = new RecordingTemplatesLibraryHost();
        var vm = new TemplatesLibraryViewModel(service);
        vm.Attach(host);
        return (vm, service, host);
    }

    [Fact]
    public async Task Reload_NoTemplatesNoErrors_ShowsEmptyStatus()
    {
        var (vm, service, _) = CreateSut();
        service.LibraryResult = new TemplateLibraryLoadResult();

        await vm.ReloadLibraryAsync(forceRefresh: true);

        Assert.Equal("No templates found in configured template folder.", vm.StatusMessage);
        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public async Task Reload_NoTemplatesWithErrors_SurfacesFirstError()
    {
        var (vm, service, _) = CreateSut();
        service.LibraryResult = new TemplateLibraryLoadResult { Errors = new[] { "bad file" } };

        await vm.ReloadLibraryAsync(forceRefresh: true);

        Assert.Equal("No templates loaded. bad file", vm.StatusMessage);
    }

    [Fact]
    public async Task Reload_WithItems_ShowsLoadedCount()
    {
        var (vm, service, _) = CreateSut();
        service.LibraryResult = new TemplateLibraryLoadResult { Items = new[] { Item("a", "a.json"), Item("b", "b.json") } };

        await vm.ReloadLibraryAsync(forceRefresh: true);

        Assert.Equal("Loaded 2 template(s).", vm.StatusMessage);
        Assert.False(vm.IsEmpty);
    }

    [Fact]
    public async Task Reload_WithItemsAndErrors_ShowsWarningsSuffix()
    {
        var (vm, service, _) = CreateSut();
        service.LibraryResult = new TemplateLibraryLoadResult
        {
            Items = new[] { Item("a", "a.json") },
            Errors = new[] { "skipped one" }
        };

        await vm.ReloadLibraryAsync(forceRefresh: true);

        Assert.Equal("Loaded 1 template(s) with warnings.", vm.StatusMessage);
    }

    [Fact]
    public async Task Reload_PreservesSelectionByFilePath()
    {
        var (vm, service, _) = CreateSut();
        service.LibraryResult = new TemplateLibraryLoadResult { Items = new[] { Item("a", "a.json"), Item("b", "b.json") } };
        await vm.ReloadLibraryAsync(forceRefresh: true);
        vm.SelectedTemplate = vm.Templates.First(t => t.FilePath == "b.json");

        // A fresh result with new instances at different order still restores the b.json selection.
        service.LibraryResult = new TemplateLibraryLoadResult { Items = new[] { Item("b", "b.json"), Item("a", "a.json") } };
        await vm.ReloadLibraryAsync(forceRefresh: true);

        Assert.NotNull(vm.SelectedTemplate);
        Assert.Equal("b.json", vm.SelectedTemplate!.FilePath);
    }

    [Fact]
    public async Task Reload_ServiceThrows_SurfacesFailure()
    {
        var (vm, service, _) = CreateSut();
        service.LoadLibraryException = new InvalidOperationException("boom");

        await vm.ReloadLibraryAsync(forceRefresh: true);

        Assert.Equal("Failed to load templates. boom", vm.StatusMessage);
        Assert.Equal("Failed to load templates. boom", vm.ErrorMessage);
    }

    [Fact]
    public async Task OpenInEditor_Success_HandsOffToEditor()
    {
        var (vm, service, host) = CreateSut();
        service.LibraryResult = new TemplateLibraryLoadResult { Items = new[] { Item("a", "a.json") } };
        await vm.ReloadLibraryAsync(forceRefresh: true);
        vm.SelectedTemplate = vm.Templates.First();

        await vm.OpenInEditorCommand.ExecuteAsync(null);

        var call = Assert.Single(host.ShowInEditorCalls);
        Assert.Equal("Template loaded.", call.StatusText);
        Assert.Empty(host.ReportedEditorStatuses);
    }

    [Fact]
    public async Task OpenInEditor_LoadFailure_ReportsEditorStatusAndStays()
    {
        var (vm, service, host) = CreateSut();
        service.LibraryResult = new TemplateLibraryLoadResult { Items = new[] { Item("a", "a.json") } };
        await vm.ReloadLibraryAsync(forceRefresh: true);
        vm.SelectedTemplate = vm.Templates.First();
        service.LoadForEditorException = new InvalidOperationException("nope");

        await vm.OpenInEditorCommand.ExecuteAsync(null);

        Assert.Empty(host.ShowInEditorCalls);
        var status = Assert.Single(host.ReportedEditorStatuses);
        Assert.Equal("Failed to open template. nope", status);
    }

    [Fact]
    public async Task OpenInBuilder_NonV2Template_IsBlockedWithGuidance()
    {
        var (vm, service, host) = CreateSut();
        service.LibraryResult = new TemplateLibraryLoadResult { Items = new[] { Item("a", "a.json", TemplateExecutionEngine.V1Deployment) } };
        await vm.ReloadLibraryAsync(forceRefresh: true);
        vm.SelectedTemplate = vm.Templates.First();

        await vm.OpenInBuilderCommand.ExecuteAsync(null);

        Assert.Empty(host.ShowInBuilderCalls);
        Assert.Equal("Only V2 templates open in Builder. Use the current Editor for V1/simple/legacy templates.", vm.StatusMessage);
    }

    [Fact]
    public async Task OpenInBuilder_V2Template_HandsOffToBuilder()
    {
        var (vm, service, host) = CreateSut();
        service.LibraryResult = new TemplateLibraryLoadResult { Items = new[] { Item("a", "a.json") } };
        await vm.ReloadLibraryAsync(forceRefresh: true);
        vm.SelectedTemplate = vm.Templates.First();

        await vm.OpenInBuilderCommand.ExecuteAsync(null);

        Assert.Single(host.ShowInBuilderCalls);
    }

    [Fact]
    public async Task Delete_Confirmed_DeletesAndReloads()
    {
        var (vm, service, host) = CreateSut();
        service.LibraryResult = new TemplateLibraryLoadResult { Items = new[] { Item("a", "a.json") } };
        await vm.ReloadLibraryAsync(forceRefresh: true);
        vm.SelectedTemplate = vm.Templates.First();
        host.ConfirmDeleteResult = true;

        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.Single(host.DeleteConfirmationRequests);
        Assert.Equal(1, service.DeleteCallCount);
        Assert.Equal("a.json", service.LastDeletedFilePath);
    }

    [Fact]
    public async Task Delete_Declined_DoesNotDelete()
    {
        var (vm, service, host) = CreateSut();
        service.LibraryResult = new TemplateLibraryLoadResult { Items = new[] { Item("a", "a.json") } };
        await vm.ReloadLibraryAsync(forceRefresh: true);
        vm.SelectedTemplate = vm.Templates.First();
        host.ConfirmDeleteResult = false;

        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.Single(host.DeleteConfirmationRequests);
        Assert.Equal(0, service.DeleteCallCount);
    }
}
