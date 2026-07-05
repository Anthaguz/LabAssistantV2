using LabAssistant.Business.Templates;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels.Deploy;
using Xunit;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Runtime-independent unit tests for <see cref="DeployFromTemplateViewModel"/>, the full-MVVM From
/// Template lane that replaced the imperative composition/workspace pair. These exercise template
/// selection and reconcile, readiness/plan gating, command CanExecute wiring, V2 plan review
/// (blockers, credential-slot resolution, switch-adapter mapping, base remote access), and
/// navigate-away cancellation through injected seams (a synchronous UI marshaller and a fake
/// composition host) with no WinUI dispatcher, Hyper-V, or disk access present.
/// </summary>
public sealed class DeployFromTemplateViewModelTests
{
    private const string ClassicPath = @"C:\Templates\classic.labtemplate";
    private const string V2Path = @"C:\Templates\v2.labtemplate";

    private sealed class Harness
    {
        public required DeployFromTemplateViewModel Vm { get; init; }

        public required FakeFromTemplateCompositionHost Host { get; init; }

        public int ResultsPanelStateChangedCount { get; set; }
    }

    private static Harness CreateHarness()
    {
        var host = new FakeFromTemplateCompositionHost();
        var vm = new DeployFromTemplateViewModel(host, action => action(), host.Templates);
        var harness = new Harness { Vm = vm, Host = host };
        vm.ResultsPanelStateChanged += (_, _) => harness.ResultsPanelStateChangedCount++;
        return harness;
    }

    private static V2PlanBuildResult ReadyPlan() => new()
    {
        Success = true,
        Context = new V2ResolvedPlanningContext
        {
            ResolvedDeploymentProfileName = "Default",
            Vms = [new V2ResolvedVmPlanningContext { VmId = "vm1", VmName = "VM1" }]
        },
        Nodes = [new V2PlanNode { NodeId = "n1", VmId = "vm1", VmName = "VM1", Kind = V2PlanNodeKind.ProvisionVm, DisplayName = "Provision VM1" }],
        Waves = [new V2SchedulingWave { WaveNumber = 1, DisplayName = "Wave 1", NodeIds = ["n1"], Summary = "1 node" }]
    };

    private static V2PlanBuildResult BlockedPlan() => new()
    {
        Success = false,
        Context = new V2ResolvedPlanningContext
        {
            ResolvedDeploymentProfileName = "Default",
            Vms = [new V2ResolvedVmPlanningContext { VmId = "vm1", VmName = "VM1" }]
        },
        Issues =
        [
            new V2PlanIssue
            {
                Severity = V2PlanIssueSeverity.Blocking,
                Code = "PLAN_BLOCK",
                VmId = "vm1",
                VmName = "VM1",
                Message = "Router egress cannot be validated.",
                SuggestedAction = "Add a router-capable VM."
            }
        ]
    };

    [Fact]
    public void FreshViewModel_HasNoTemplate_DisablesActionCommands()
    {
        var harness = CreateHarness();

        Assert.Null(harness.Vm.ActiveTemplateDocument);
        Assert.False(harness.Vm.EvaluateReadinessCommand.CanExecute(null));
        Assert.False(harness.Vm.ResolveSuggestionsCommand.CanExecute(null));
        Assert.False(harness.Vm.OpenTemplateEditorCommand.CanExecute(null));
        Assert.False(harness.Vm.StartDeployCommand.CanExecute(null));
    }

    [Fact]
    public void SelectingClassicTemplate_LoadsDocumentAndEnablesReadyDeploy()
    {
        var harness = CreateHarness();
        var item = harness.Host.AddTemplate("Classic", ClassicPath, TemplateExecutionEngine.V1Deployment);

        harness.Vm.SelectedTemplateLibraryItem = item;

        Assert.NotNull(harness.Vm.ActiveTemplateDocument);
        Assert.Equal("Review Readiness", harness.Vm.EvaluateButtonText);
        Assert.True(harness.Vm.StartDeployCommand.CanExecute(null));
        Assert.True(harness.Vm.OpenTemplateEditorCommand.CanExecute(null));
    }

    [Fact]
    public void ClassicReadinessWithBlockingFailure_DisablesStartDeploy()
    {
        var harness = CreateHarness();
        harness.Host.ReadinessReport = new DeploymentReadinessReport
        {
            Mode = DeploymentPreflightMode.Quick,
            Results = [new DeploymentReadinessCheckResult { Status = DeploymentReadinessStatus.Fail, Message = "Host memory insufficient." }]
        };
        var item = harness.Host.AddTemplate("Classic", ClassicPath, TemplateExecutionEngine.V1Deployment);

        harness.Vm.SelectedTemplateLibraryItem = item;

        Assert.Equal("Blocked", harness.Vm.LifecycleState);
        Assert.False(harness.Vm.StartDeployCommand.CanExecute(null));
    }

    [Fact]
    public void ReconcileSelection_RemapsToNewInstance_ThenClearsWhenMissing()
    {
        var harness = CreateHarness();
        var item = harness.Host.AddTemplate("Classic", ClassicPath, TemplateExecutionEngine.V1Deployment);
        harness.Vm.SelectedTemplateLibraryItem = item;

        var remapped = new TemplateLibraryItem { Name = "Classic", FilePath = ClassicPath, ExecutionEngine = TemplateExecutionEngine.V1Deployment };
        harness.Vm.ReconcileSelection([remapped]);
        Assert.Same(remapped, harness.Vm.SelectedTemplateLibraryItem);

        harness.Vm.ReconcileSelection([]);
        Assert.Null(harness.Vm.SelectedTemplateLibraryItem);
        Assert.Null(harness.Vm.ActiveTemplateDocument);
    }

    [Fact]
    public async Task ResolveSuggestions_AppliesCountAndReevaluates()
    {
        var harness = CreateHarness();
        harness.Host.ResolveSuggestionsResult = 2;
        var item = harness.Host.AddTemplate("Classic", ClassicPath, TemplateExecutionEngine.V1Deployment);
        harness.Vm.SelectedTemplateLibraryItem = item;

        await harness.Vm.ResolveSuggestionsCommand.ExecuteAsync(null);

        Assert.Equal(1, harness.Host.ResolveSuggestionsCallCount);
        Assert.Equal("Readiness passed with no issues.", harness.Vm.ActionStatusText);
    }

    [Fact]
    public async Task OpenTemplateEditor_InvokesHostEditorSeam()
    {
        var harness = CreateHarness();
        var item = harness.Host.AddTemplate("Classic", ClassicPath, TemplateExecutionEngine.V1Deployment);
        harness.Vm.SelectedTemplateLibraryItem = item;

        await harness.Vm.OpenTemplateEditorCommand.ExecuteAsync(null);

        Assert.Equal(1, harness.Host.ShowEditorCount);
        Assert.Contains("Opened", harness.Vm.ActionStatusText);
    }

    [Fact]
    public void SelectingV2Template_BuildsReadyPlan_EnablesStartAndShowsReview()
    {
        var harness = CreateHarness();
        harness.Host.V2PlanFactory = (_, _, _) => ReadyPlan();
        var item = harness.Host.AddTemplate("V2", V2Path, TemplateExecutionEngine.V2UnifiedPlanning);

        harness.Vm.SelectedTemplateLibraryItem = item;

        Assert.True(harness.Vm.V2Review.IsVisible);
        Assert.True(harness.Vm.V2Review.CanStartDeploy);
        Assert.Equal("Refresh V2 Plan", harness.Vm.EvaluateButtonText);
        Assert.True(harness.Vm.StartDeployCommand.CanExecute(null));
        Assert.Single(harness.Vm.V2Review.WaveRows);
    }

    [Fact]
    public void V2PlanWithBlockingIssue_DisablesStartAndPopulatesBlockers()
    {
        var harness = CreateHarness();
        harness.Host.V2PlanFactory = (_, _, _) => BlockedPlan();
        var item = harness.Host.AddTemplate("V2", V2Path, TemplateExecutionEngine.V2UnifiedPlanning);

        harness.Vm.SelectedTemplateLibraryItem = item;

        Assert.True(harness.Vm.V2Review.HasBlockingItems);
        Assert.False(harness.Vm.V2Review.CanStartDeploy);
        Assert.False(harness.Vm.StartDeployCommand.CanExecute(null));
        Assert.NotEmpty(harness.Vm.V2Review.BlockerRows);
    }

    [Fact]
    public async Task V2UnresolvedCredentialSlot_SaveResolvesPlanAndEnablesStart()
    {
        var harness = CreateHarness();
        harness.Host.SlotDefinitions.Add(new LocalCredentialSlotDefinition { SlotKey = "bootstrap" });
        harness.Host.V2PlanFactory = (_, resolvedKeys, _) =>
            resolvedKeys.Contains("bootstrap")
                ? ReadyPlan()
                : new V2PlanBuildResult
                {
                    Success = true,
                    Context = new V2ResolvedPlanningContext
                    {
                        ResolvedDeploymentProfileName = "Default",
                        Vms = [new V2ResolvedVmPlanningContext { VmId = "vm1", VmName = "VM1" }]
                    },
                    UnresolvedRequirements =
                    [
                        new V2UnresolvedRequirement
                        {
                            Kind = V2UnresolvedRequirementKind.CredentialSlot,
                            Key = "bootstrap",
                            AffectedVmIds = ["vm1"],
                            Description = "Bootstrap admin credential."
                        }
                    ]
                };
        var item = harness.Host.AddTemplate("V2", V2Path, TemplateExecutionEngine.V2UnifiedPlanning);

        harness.Vm.SelectedTemplateLibraryItem = item;
        Assert.False(harness.Vm.V2Review.CanStartDeploy);
        Assert.Contains(harness.Vm.V2Review.CredentialSlotRows, row => row.SlotKey == "bootstrap");

        harness.Vm.SelectV2CredentialSlot("bootstrap");
        harness.Vm.V2CredentialSlotUsername = "labadmin";
        await harness.Vm.SaveV2CredentialSlotCommand.ExecuteAsync("Pa55word!");

        Assert.Contains(harness.Host.Upserts, upsert => upsert.SlotKey == "bootstrap" && upsert.Username == "labadmin");
        Assert.True(harness.Vm.V2Review.CanStartDeploy);
        Assert.True(harness.Vm.StartDeployCommand.CanExecute(null));
    }

    [Fact]
    public async Task ExternalSwitchAdapterMapping_FlowsIntoPlanBuild()
    {
        var harness = CreateHarness();
        harness.Host.V2PlanFactory = (_, _, _) => ReadyPlan();
        var item = harness.Host.AddTemplate("V2", V2Path, TemplateExecutionEngine.V2UnifiedPlanning);
        harness.Vm.SelectedTemplateLibraryItem = item;

        harness.Vm.V2Review.SetExternalSwitchAdapterMapping("Lab-External", "Ethernet 2");
        await harness.Vm.EvaluateReadinessCommand.ExecuteAsync(null);

        Assert.NotNull(harness.Host.LastExternalSwitchAdapterMappings);
        Assert.Equal("Ethernet 2", harness.Host.LastExternalSwitchAdapterMappings!["Lab-External"]);
    }

    [Fact]
    public void BaseRemoteAccessToggle_UpdatesReviewOptions()
    {
        var harness = CreateHarness();
        harness.Host.V2PlanFactory = (_, _, _) => ReadyPlan();
        var item = harness.Host.AddTemplate("V2", V2Path, TemplateExecutionEngine.V2UnifiedPlanning);
        harness.Vm.SelectedTemplateLibraryItem = item;

        harness.Vm.V2DisableFirewall = false;

        Assert.False(harness.Vm.V2Review.BaseRemoteAccess.DisableFirewall);
        Assert.True(harness.Vm.V2Review.BaseRemoteAccess.DisableRdpNla);
    }

    [Fact]
    public async Task CleanupAsync_CancelsInFlightClassicDeploy()
    {
        var harness = CreateHarness();
        var gate = new TaskCompletionSource();
        harness.Host.OnDeployAll = _ => gate.Task;
        var item = harness.Host.AddTemplate("Classic", ClassicPath, TemplateExecutionEngine.V1Deployment);
        harness.Vm.SelectedTemplateLibraryItem = item;

        var deployTask = harness.Vm.StartDeployCommand.ExecuteAsync(null);

        Assert.NotNull(harness.Host.LastDeployContext);
        await harness.Vm.CleanupAsync();
        Assert.True(harness.Host.LastDeployContext!.IsCancellationRequested);

        gate.SetResult();
        await deployTask;
    }

    [Fact]
    public async Task CleanupAsync_CancelsInFlightV2Deploy()
    {
        var harness = CreateHarness();
        harness.Host.V2PlanFactory = (_, _, _) => ReadyPlan();
        var gate = new TaskCompletionSource();
        harness.Host.OnExecuteV2 = _ => gate.Task;
        var item = harness.Host.AddTemplate("V2", V2Path, TemplateExecutionEngine.V2UnifiedPlanning);
        harness.Vm.SelectedTemplateLibraryItem = item;

        var deployTask = harness.Vm.StartDeployCommand.ExecuteAsync(null);

        Assert.NotNull(harness.Host.LastV2DeployContext);
        await harness.Vm.CleanupAsync();
        Assert.True(harness.Host.LastV2DeployContext!.IsCancellationRequested);

        gate.SetResult();
        await deployTask;
    }

    [Fact]
    public void ProjectCredentialPanel_ResetsForNonV2Template()
    {
        var harness = CreateHarness();
        var item = harness.Host.AddTemplate("Classic", ClassicPath, TemplateExecutionEngine.V1Deployment);
        harness.Vm.SelectedTemplateLibraryItem = item;

        var projection = harness.Vm.ProjectCredentialPanel();

        Assert.Equal(DeployFromTemplateCredentialPanelKind.Reset, projection.Kind);
    }

    [Fact]
    public void SelectingTemplate_RaisesResultsPanelStateChanged()
    {
        var harness = CreateHarness();
        var item = harness.Host.AddTemplate("Classic", ClassicPath, TemplateExecutionEngine.V1Deployment);

        harness.Vm.SelectedTemplateLibraryItem = item;

        Assert.True(harness.ResultsPanelStateChangedCount > 0);
    }
}
