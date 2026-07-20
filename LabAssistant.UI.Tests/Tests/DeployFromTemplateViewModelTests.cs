using LabAssistant.Business.Templates;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Models.Deploy;
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
    public void SelectingClassicTemplate_ShowsLegacyBlockedNotice_AndDisablesDeploy()
    {
        var harness = CreateHarness();
        var item = harness.Host.AddTemplate("Classic", ClassicPath, TemplateExecutionEngine.V1Deployment);

        harness.Vm.SelectedTemplateLibraryItem = item;

        // Legacy (V1) templates can no longer be deployed; the lane surfaces a re-create-in-Builder notice
        // and blocks deploy, but the template still loads and can be opened in the editor.
        Assert.NotNull(harness.Vm.ActiveTemplateDocument);
        Assert.Equal("Blocked", harness.Vm.LifecycleState);
        Assert.False(harness.Vm.StartDeployCommand.CanExecute(null));
        Assert.True(harness.Vm.OpenTemplateEditorCommand.CanExecute(null));
        Assert.Contains(harness.Vm.IssueRows, row => row.Message.Contains("legacy deployment format", StringComparison.OrdinalIgnoreCase));
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
        // Re-evaluation of a legacy template lands on the re-create-in-Builder notice.
        Assert.Contains("legacy deployment format", harness.Vm.ActionStatusText, StringComparison.OrdinalIgnoreCase);
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

    [Theory]
    [InlineData("Idle")]
    [InlineData("Ready")]
    [InlineData("Blocked")]
    [InlineData("Evaluating")]
    [InlineData("Warning")]
    [InlineData("Error")]
    public void LifecycleState_NonRunningNonTerminal_ShowsConfigSurface(string state)
    {
        var harness = CreateHarness();

        harness.Vm.LifecycleState = state;

        Assert.True(harness.Vm.ShouldShowConfigView);
        Assert.False(harness.Vm.ShouldShowProgressView);
        Assert.False(harness.Vm.ShouldShowResultsView);
    }

    [Fact]
    public void LifecycleState_Running_ShowsProgressSurface()
    {
        var harness = CreateHarness();

        harness.Vm.LifecycleState = "Running";

        Assert.False(harness.Vm.ShouldShowConfigView);
        Assert.True(harness.Vm.ShouldShowProgressView);
        Assert.False(harness.Vm.ShouldShowResultsView);
    }

    [Theory]
    [InlineData("Completed")]
    [InlineData("Failed")]
    [InlineData("Cancelled")]
    public void LifecycleState_Terminal_ShowsResultsSurface(string state)
    {
        var harness = CreateHarness();

        harness.Vm.LifecycleState = state;

        Assert.False(harness.Vm.ShouldShowConfigView);
        Assert.False(harness.Vm.ShouldShowProgressView);
        Assert.True(harness.Vm.ShouldShowResultsView);
    }

    [Fact]
    public void ViewStateFlags_AreCaseInsensitive()
    {
        var harness = CreateHarness();

        harness.Vm.LifecycleState = "running";
        Assert.True(harness.Vm.ShouldShowProgressView);

        harness.Vm.LifecycleState = "COMPLETED";
        Assert.True(harness.Vm.ShouldShowResultsView);

        harness.Vm.LifecycleState = "ReAdY";
        Assert.True(harness.Vm.ShouldShowConfigView);
    }

    [Fact]
    public void LifecycleStateChange_RaisesAllViewStateFlagNotifications()
    {
        var harness = CreateHarness();
        var configRaised = false;
        var progressRaised = false;
        var resultsRaised = false;
        harness.Vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DeployFromTemplateViewModel.ShouldShowConfigView))
            {
                configRaised = true;
            }
            else if (e.PropertyName == nameof(DeployFromTemplateViewModel.ShouldShowProgressView))
            {
                progressRaised = true;
            }
            else if (e.PropertyName == nameof(DeployFromTemplateViewModel.ShouldShowResultsView))
            {
                resultsRaised = true;
            }
        };

        harness.Vm.LifecycleState = "Running";

        Assert.True(configRaised);
        Assert.True(progressRaised);
        Assert.True(resultsRaised);
    }

    [Fact]
    public void DeployAgain_FromResults_RestoresConfigSurfaceForSelectedTemplate()
    {
        var harness = CreateHarness();
        harness.Host.V2PlanFactory = (_, _, _) => ReadyPlan();
        var item = harness.Host.AddTemplate("V2", V2Path, TemplateExecutionEngine.V2UnifiedPlanning);
        harness.Vm.SelectedTemplateLibraryItem = item;

        Assert.True(harness.Vm.ShouldShowConfigView);
        Assert.True(harness.Vm.V2Review.IsVisible);

        // Simulate a finished run landing on the terminal results surface.
        harness.Vm.SetShowAllVmRows(true);
        harness.Vm.SetWorkflowState(false, false, "Completed", 100, "Deployment completed.");
        Assert.True(harness.Vm.ShouldShowResultsView);

        harness.Vm.DeployAgainCommand.Execute(null);

        Assert.True(harness.Vm.ShouldShowConfigView);
        Assert.Equal("Ready", harness.Vm.LifecycleState);
        Assert.True(harness.Vm.V2Review.IsVisible);
    }

    [Fact]
    public void BackToConfiguration_WithoutTemplate_ResetsToIdleConfigSurface()
    {
        var harness = CreateHarness();
        harness.Vm.SetShowAllVmRows(true);
        harness.Vm.SetWorkflowState(false, false, "Failed", 100, "Deployment failed.");
        Assert.True(harness.Vm.ShouldShowResultsView);

        harness.Vm.BackToConfigurationCommand.Execute(null);

        Assert.True(harness.Vm.ShouldShowConfigView);
        Assert.Equal("Idle", harness.Vm.LifecycleState);
        Assert.Empty(harness.Vm.ResultRows);
    }

    private static V2PlanBuildResult TwoVmReadyPlan() => new()
    {
        Success = true,
        Context = new V2ResolvedPlanningContext
        {
            ResolvedDeploymentProfileName = "Default",
            Vms =
            [
                new V2ResolvedVmPlanningContext { VmId = "vm1", VmName = "VM1" },
                new V2ResolvedVmPlanningContext { VmId = "vm2", VmName = "VM2" }
            ]
        },
        Nodes =
        [
            new V2PlanNode { NodeId = "n1", VmId = "vm1", VmName = "VM1", Kind = V2PlanNodeKind.ProvisionVm, DisplayName = "Provision VM1" },
            new V2PlanNode { NodeId = "n2", VmId = "vm2", VmName = "VM2", Kind = V2PlanNodeKind.ProvisionVm, DisplayName = "Provision VM2" }
        ],
        Waves = [new V2SchedulingWave { WaveNumber = 1, DisplayName = "Wave 1", NodeIds = ["n1", "n2"], Summary = "2 nodes" }]
    };

    [Fact]
    public async Task V2Deploy_RuntimeRegisteredVmContext_StreamsLiveProgressIntoResultRows()
    {
        // The V2 runtime clears and rebuilds VmContexts internally, so callbacks wired before execution must
        // still reach the contexts it creates. Without the VmContextRegistered hook the step-state emitter is
        // never attached and the live surface stays frozen on its seeded "Queued" rows.
        var harness = CreateHarness();
        harness.Host.V2PlanFactory = (_, _, _) => ReadyPlan();
        harness.Host.OnExecuteV2 = context =>
        {
            context.VmContexts.Clear();
            var vmContext = new VmDeploymentContext { VmName = "VM1", OperationId = context.OperationId };
            context.RegisterVmContext(vmContext);
            vmContext.EmitStepState("provision", "Provision VM1", DeployStepState.Running, "Creating VM...");
            return Task.CompletedTask;
        };
        var item = harness.Host.AddTemplate("V2", V2Path, TemplateExecutionEngine.V2UnifiedPlanning);
        harness.Vm.SelectedTemplateLibraryItem = item;

        await harness.Vm.StartDeployCommand.ExecuteAsync(null);

        var row = Assert.Single(harness.Vm.ResultRows, candidate => candidate.VmName == "VM1");
        Assert.Equal("Running", row.Status);
        Assert.Equal("Creating VM...", row.Summary);
        Assert.True(row.ProgressPercent > 0);
    }

    [Fact]
    public async Task V2Deploy_ProgressTick_ReusesUnchangedVmRowInstances()
    {
        // A progress tick for one VM must not rebuild the whole ResultRows collection; unchanged VM rows keep
        // their existing instances so the bound ListView does not tear down and re-animate every row each tick.
        var harness = CreateHarness();
        harness.Host.V2PlanFactory = (_, _, _) => TwoVmReadyPlan();
        DeployVmResultRow? vm2AfterFirstTick = null;
        DeployVmResultRow? vm2AfterSecondTick = null;
        harness.Host.OnExecuteV2 = context =>
        {
            context.VmContexts.Clear();
            var vm1 = new VmDeploymentContext { VmName = "VM1", OperationId = context.OperationId };
            var vm2 = new VmDeploymentContext { VmName = "VM2", OperationId = context.OperationId };
            context.RegisterVmContext(vm1);
            context.RegisterVmContext(vm2);

            vm1.EmitStepState("provision", "Provision VM1", DeployStepState.Running, "VM1 running");
            vm2AfterFirstTick = harness.Vm.ResultRows.Single(candidate => candidate.VmName == "VM2");

            vm1.EmitStepState("configure", "Configure VM1", DeployStepState.Running, "VM1 still running");
            vm2AfterSecondTick = harness.Vm.ResultRows.Single(candidate => candidate.VmName == "VM2");

            return Task.CompletedTask;
        };
        var item = harness.Host.AddTemplate("V2", V2Path, TemplateExecutionEngine.V2UnifiedPlanning);
        harness.Vm.SelectedTemplateLibraryItem = item;

        await harness.Vm.StartDeployCommand.ExecuteAsync(null);

        Assert.NotNull(vm2AfterFirstTick);
        Assert.Same(vm2AfterFirstTick, vm2AfterSecondTick);
    }

    [Fact]
    public async System.Threading.Tasks.Task GuestCredentialPrompt_WhenPromptWiredAndRemembered_PersistsCorrectedSlot()
    {
        var host = new FakeFromTemplateCompositionHost();
        GuestCredentialPromptRequest? seenRequest = null;
        System.Threading.Tasks.Task<GuestCredentialPromptResponse> Prompt(
            GuestCredentialPromptRequest request, System.Threading.CancellationToken _)
        {
            seenRequest = request;
            return System.Threading.Tasks.Task.FromResult(new GuestCredentialPromptResponse
            {
                Cancelled = false,
                Username = "Administrator",
                Password = "Corrected!",
                RememberForSlot = true
            });
        }

        var vm = new DeployFromTemplateViewModel(host, action => action(), host.Templates, Prompt);

        // The runtime builds per-VM contexts during execution and registers them; the VM wires callbacks via this seam.
        var multi = new MultiVmDeploymentContext();
        vm.WireProgressCallbacks(multi, (_, _) => { }, (_, _) => { });

        var context = new VmDeploymentContext { VmName = "dc01" };
        multi.RegisterVmContext(context);

        Assert.NotNull(context.RequestGuestCredential);
        var response = await context.RequestGuestCredential!(
            new GuestCredentialPromptRequest
            {
                VmName = "dc01",
                CredentialSlotKey = "slot-local",
                ExpectedUsername = "Administrator"
            },
            System.Threading.CancellationToken.None);

        Assert.False(response.Cancelled);
        Assert.Equal("Corrected!", response.Password);
        Assert.NotNull(seenRequest);
        Assert.Equal("slot-local", seenRequest!.CredentialSlotKey);
        // "Remember" persists the corrected credential to the slot store so future deploys reuse it.
        Assert.Contains(host.Upserts, u => u.SlotKey == "slot-local" && u.Password == "Corrected!");
    }

    [Fact]
    public void GuestCredentialPrompt_WhenNoPromptWired_LeavesContextUnwiredForFailFast()
    {
        var host = new FakeFromTemplateCompositionHost();
        // No prompt delegate: the runtime must keep its fail-fast behavior (context callback stays null).
        var vm = new DeployFromTemplateViewModel(host, action => action(), host.Templates);

        var multi = new MultiVmDeploymentContext();
        vm.WireProgressCallbacks(multi, (_, _) => { }, (_, _) => { });

        var context = new VmDeploymentContext { VmName = "dc01" };
        multi.RegisterVmContext(context);

        Assert.Null(context.RequestGuestCredential);
    }
}
