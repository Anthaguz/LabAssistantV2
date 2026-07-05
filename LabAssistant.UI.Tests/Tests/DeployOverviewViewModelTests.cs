using LabAssistant.WinUI.ViewModels.Deploy;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Runtime-independent unit tests for <see cref="DeployOverviewViewModel"/>, the route-entry
/// chooser of the frame-hosted Deploy capability. These establish the Deploy view-model test seam
/// that prior shell migrations deferred: they exercise computed summary state, command wiring, and
/// the navigation-intent events the capability page subscribes to, without a WinUI dispatcher.
/// </summary>
public sealed class DeployOverviewViewModelTests
{
    [Fact]
    public void RefreshSummary_NoDraftAndNoTemplates_UsesEmptyStatePrompts()
    {
        var vm = new DeployOverviewViewModel();

        vm.RefreshSummary(quickDeployDraftCount: 0, isLoadingTemplates: false, availableTemplateCount: 0);

        Assert.Equal("Open Quick Deploy to configure VM entries and run deployment.", vm.QuickDeploySummaryText);
        Assert.Equal(
            "Open From Template to review a template and resolve readiness blockers before deploy.",
            vm.FromTemplateSummaryText);
    }

    [Fact]
    public void RefreshSummary_SingleDraft_UsesSingularWording()
    {
        var vm = new DeployOverviewViewModel();

        vm.RefreshSummary(quickDeployDraftCount: 1, isLoadingTemplates: false, availableTemplateCount: 1);

        Assert.Equal("1 VM entry currently staged in the Quick Deploy draft.", vm.QuickDeploySummaryText);
        Assert.Equal("1 template currently available for From Template.", vm.FromTemplateSummaryText);
    }

    [Fact]
    public void RefreshSummary_MultipleDrafts_UsesPluralWording()
    {
        var vm = new DeployOverviewViewModel();

        vm.RefreshSummary(quickDeployDraftCount: 3, isLoadingTemplates: false, availableTemplateCount: 4);

        Assert.Equal("3 VM entries currently staged in the Quick Deploy draft.", vm.QuickDeploySummaryText);
        Assert.Equal("4 templates currently available for From Template.", vm.FromTemplateSummaryText);
    }

    [Fact]
    public void RefreshSummary_TemplatesLoading_TakesPrecedenceOverCount()
    {
        var vm = new DeployOverviewViewModel();

        vm.RefreshSummary(quickDeployDraftCount: 0, isLoadingTemplates: true, availableTemplateCount: 5);

        Assert.Equal("Template inventory is loading.", vm.FromTemplateSummaryText);
    }

    [Fact]
    public void RefreshSummary_RaisesPropertyChangedForBoundText()
    {
        var vm = new DeployOverviewViewModel();
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.RefreshSummary(quickDeployDraftCount: 2, isLoadingTemplates: false, availableTemplateCount: 3);

        Assert.Contains(nameof(DeployOverviewViewModel.QuickDeploySummaryText), changed);
        Assert.Contains(nameof(DeployOverviewViewModel.FromTemplateSummaryText), changed);
    }

    [Fact]
    public void OpenQuickDeployCommand_RaisesOpenQuickDeployRequested()
    {
        var vm = new DeployOverviewViewModel();
        var raised = 0;
        vm.OpenQuickDeployRequested += (_, _) => raised++;

        Assert.True(vm.OpenQuickDeployAction.CanExecute(null));
        vm.OpenQuickDeployAction.Execute(null);

        Assert.Equal(1, raised);
    }

    [Fact]
    public void OpenFromTemplateCommand_RaisesOpenFromTemplateRequested()
    {
        var vm = new DeployOverviewViewModel();
        var raised = 0;
        vm.OpenFromTemplateRequested += (_, _) => raised++;

        Assert.True(vm.OpenFromTemplateAction.CanExecute(null));
        vm.OpenFromTemplateAction.Execute(null);

        Assert.Equal(1, raised);
    }
}
