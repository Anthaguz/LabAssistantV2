using System.Linq;
using System.Threading.Tasks;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels.Templates;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Regression coverage for the Templates subview command-gating / lifecycle deadlock reported from the
/// combined smoke-test: action commands got stuck after first use and buttons went permanently dead.
/// The root cause was a lifecycle mismatch - the cross-subview host was attached once at page entry but
/// the subview view models were cleaned up (host detached, state wiped) on every hosted-tab switch, so
/// after the first switch every host-routed command silently no-oped. These tests pin the view-model
/// command contract the page-owned lifecycle relies on: actions complete and re-enable, invoking one
/// action does not disable its sibling, and editor-open followed by builder-open both route. All are
/// dispatcher-free.
/// </summary>
public sealed class TemplatesSubviewCommandLifecycleTests
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

    private static (TemplatesLibraryViewModel Vm, RecordingTemplatesCapabilityService Service, RecordingTemplatesLibraryHost Host) CreateLibrary()
    {
        var service = new RecordingTemplatesCapabilityService();
        var host = new RecordingTemplatesLibraryHost();
        var vm = new TemplatesLibraryViewModel(service);
        vm.Attach(host);
        return (vm, service, host);
    }

    [Fact]
    public async Task LibraryCreate_IsRepeatable_ReachesHostEachTime_AndStaysEnabled()
    {
        var (vm, _, host) = CreateLibrary();

        await vm.CreateCommand.ExecuteAsync(null);
        await vm.CreateCommand.ExecuteAsync(null);

        // The action reaches the host on every invocation and leaves the button re-enabled - no stuck
        // IsLoading that would keep CanCreate / the async command's CanExecute false forever.
        Assert.Equal(2, host.ShowInEditorCalls.Count);
        Assert.False(vm.IsLoading);
        Assert.True(vm.CanCreate);
        Assert.True(vm.CreateCommand.CanExecute(null));
    }

    [Fact]
    public async Task LibraryCreate_DoesNotDisableSibling()
    {
        var (vm, _, host) = CreateLibrary();

        await vm.CreateCommand.ExecuteAsync(null);

        // Repro B: after clicking one create action the sibling must remain clickable.
        Assert.True(vm.CanCreateBuilder);
        Assert.True(vm.CreateBuilderCommand.CanExecute(null));

        await vm.CreateBuilderCommand.ExecuteAsync(null);

        Assert.Equal(1, host.CreateBuilderDraftCallCount);
        Assert.True(vm.CanCreate);
        Assert.True(vm.CreateCommand.CanExecute(null));
    }

    [Fact]
    public async Task LibraryOpenInEditorThenBuilder_BothRoute_NoSingleShotLock()
    {
        var (vm, service, host) = CreateLibrary();
        service.LibraryResult = new TemplateLibraryLoadResult { Items = new[] { Item("v2", @"C:\templates\v2.json") } };
        await vm.ReloadLibraryAsync(forceRefresh: true);
        vm.SelectedTemplate = vm.Templates.Single();

        // Repro A: open in editor, then open in builder - both must route, one after the other.
        await vm.OpenInEditorCommand.ExecuteAsync(null);
        await vm.OpenInBuilderCommand.ExecuteAsync(null);

        Assert.Single(host.ShowInEditorCalls);
        Assert.Single(host.ShowInBuilderCalls);
        Assert.False(vm.IsLoading);
        Assert.True(vm.CanOpenInEditor);
        Assert.True(vm.CanOpenInBuilder);
    }

    [Fact]
    public async Task LibraryCleanup_DetachesHost_WhichIsWhyThePageKeepsItAttachedAcrossTabs()
    {
        var (vm, _, host) = CreateLibrary();

        // CleanupAsync detaches the host - this is exactly what a tab-unload used to trigger. It is the
        // failure the page-owned lifecycle avoids by NOT tearing the view model down on tab switches.
        await vm.CleanupAsync();
        await vm.CreateCommand.ExecuteAsync(null);

        // Detached, the action is a safe no-op (not a throw) and the button still re-enables, but it no
        // longer routes anywhere - matching the observed "dead button that does nothing".
        Assert.Empty(host.ShowInEditorCalls);
        Assert.False(vm.IsLoading);
        Assert.True(vm.CreateCommand.CanExecute(null));

        // Re-attaching (as the page does on entry) restores routing end to end.
        vm.Attach(host);
        await vm.CreateCommand.ExecuteAsync(null);
        Assert.Single(host.ShowInEditorCalls);
    }
}
