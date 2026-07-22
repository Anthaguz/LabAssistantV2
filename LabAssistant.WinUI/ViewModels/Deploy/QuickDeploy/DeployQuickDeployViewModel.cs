using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabAssistant.Business.Deployment;
using LabAssistant.Business.Planning;
using LabAssistant.Business.Runtime;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Infrastructure;
using LabAssistant.WinUI.Models.Deploy;
using LabAssistant.WinUI.ViewModels.Templates;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Full MVVM view model for the Quick Deploy lane. It absorbs the former imperative workspace,
/// owner, and composition layers into a single runtime-independent view model that binds through
/// x:Bind. Workflow orchestration (readiness sequencing, debounced auto-evaluate, deploy start,
/// per-VM progress) is preserved on <see cref="DeployQuickDeployWorkspaceController"/>, for which
/// this view model is both the state owner and the <see cref="IDeployQuickDeployWorkspaceControllerHost"/>.
/// UI-thread marshalling, the remove-confirmation dialog, and the results-panel toggle are injected
/// seams so the type is unit-testable without a WinUI dispatcher.
/// </summary>
internal sealed partial class DeployQuickDeployViewModel : ViewModelBase, IDeployQuickDeployWorkspaceControllerHost, IDeployQuickDeployLane
{
    private const string VhdxPlaceholder = "(Select base disk)";

    private readonly DeployReferenceDataService _referenceDataService;
    private readonly DeployResolveSuggestionsService _resolveSuggestionsService;
    private readonly Func<TemplateEditorDocument, string, Task> _showTemplateEditorAsync;
    private readonly IDeploymentPreflightService _deploymentPreflightService;
    private readonly IV2PlanningCapabilityService _v2PlanningCapabilityService;
    private readonly IV2RuntimeCapabilityService _v2RuntimeCapabilityService;
    private readonly Action<Action> _marshalToUi;
    private readonly Func<string, Task<bool>> _confirmRemoveVmAsync;
    private readonly Action _requestResultsPanelToggle;
    private readonly DeployQuickDeployWorkspaceController _controller;

    private readonly List<DeployCompatibilityIssue> _compatibilityIssues = [];
    private readonly Dictionary<string, DeployVmProgressState> _progressByVm = new(StringComparer.OrdinalIgnoreCase);

    // Blocking rows produced by the V2 planner or runtime for the current deploy attempt. They live outside the
    // readiness-derived issue set so a terminal plan/runtime block stays visible on the results surface, and are
    // re-appended by RefreshIssueRows rather than being rebuilt from readiness state.
    private readonly List<DeployIssueRow> _deployBlockerRows = [];

    private IReadOnlyList<string> _availableSwitches = Array.Empty<string>();
    private IReadOnlyList<TemplateVhdxCatalogOption> _availableVhdxCatalogOptions = Array.Empty<TemplateVhdxCatalogOption>();
    private string? _editorVhdxIdDraft;
    private string? _editorVhdPathDraft;
    private string? _editorVhdxSignatureDraft;
    private MultiVmDeploymentContext? _activeDeploymentContext;

    public DeployQuickDeployViewModel(
        DeployReferenceDataService referenceDataService,
        DeployResolveSuggestionsService resolveSuggestionsService,
        Func<TemplateEditorDocument, string, Task> showTemplateEditorAsync,
        IDeploymentPreflightService deploymentPreflightService,
        IV2PlanningCapabilityService v2PlanningCapabilityService,
        IV2RuntimeCapabilityService v2RuntimeCapabilityService,
        Action<Action> marshalToUi,
        Func<string, Task<bool>> confirmRemoveVmAsync,
        Action requestResultsPanelToggle,
        int autoEvaluateDelayMs = 350)
    {
        _referenceDataService = referenceDataService;
        _resolveSuggestionsService = resolveSuggestionsService;
        _showTemplateEditorAsync = showTemplateEditorAsync;
        _deploymentPreflightService = deploymentPreflightService;
        _v2PlanningCapabilityService = v2PlanningCapabilityService;
        _v2RuntimeCapabilityService = v2RuntimeCapabilityService;
        _marshalToUi = marshalToUi;
        _confirmRemoveVmAsync = confirmRemoveVmAsync;
        _requestResultsPanelToggle = requestResultsPanelToggle;
        _controller = new DeployQuickDeployWorkspaceController(this, this, autoEvaluateDelayMs);
    }

    /// <inheritdoc />
    public event EventHandler? SharedUiStateChanged;

    /// <summary>
    /// Raised whenever progress, results, or readiness state changes so the host page can push the
    /// current lane state into the shell right panel and reconcile the results-panel launcher.
    /// </summary>
    public event EventHandler? ResultsPanelStateChanged;

    // Bound collections.
    public ObservableCollection<VmTemplate> VmEntries { get; } = [];

    public ObservableCollection<DeployQuickDeployVmEntryRow> VmEntryRows { get; } = [];

    public ObservableCollection<DeployVmResultRow> ResultRows { get; } = [];

    public ObservableCollection<DeployIssueRow> IssueRows { get; } = [];

    public ObservableCollection<DeployQuickDeploySwitchRowItem> SwitchRows { get; } = [];

    public ObservableCollection<object> VhdxCatalogItems { get; } = [];

    // Editor draft (two-way bound).
    [ObservableProperty]
    private string _editorVmNameDraft = string.Empty;

    [ObservableProperty]
    private string _editorVmMemoryDraft = string.Empty;

    [ObservableProperty]
    private string _editorVmCpuDraft = string.Empty;

    [ObservableProperty]
    private object? _selectedVhdxCatalogItem;

    [ObservableProperty]
    private DeployQuickDeployVmEntryRow? _selectedVmEntryRow;

    // Workflow display state.
    [ObservableProperty]
    private string _lifecycleState = "Idle";

    [ObservableProperty]
    private int _progressPercent;

    [ObservableProperty]
    private string _progressSummary = "No deployment started.";

    [ObservableProperty]
    private string _statusText = "Ready.";

    [ObservableProperty]
    private string _readinessDisplayText = "Readiness has not been evaluated.";

    [ObservableProperty]
    private string _globalIssuesBadgeText = "Blocking: 0 | Warnings: 0";

    [ObservableProperty]
    private string _editorIssueSummaryText =
        "Changes validate while you edit. Row signals show which VM needs attention.";

    [ObservableProperty]
    private string _switchGuidanceText = "Switch selection is optional.";

    // F26: a clear inline hint that points the user at the base-disk selector when a VM still has no
    // base disk chosen. Deliberately separate from the removed low-signal "helper label".
    [ObservableProperty]
    private bool _showBaseDiskHint;

    [ObservableProperty]
    private string _baseDiskHintText = "Select a base disk to continue.";

    // F27: inline validation state for the Name field so a name/folder collision (or an empty/duplicate
    // name) is surfaced right on the textbox instead of only in the readiness list.
    [ObservableProperty]
    private bool _hasEditorNameError;

    [ObservableProperty]
    private string _editorNameValidationText = string.Empty;

    [ObservableProperty]
    private string _resultsPanelButtonText = "Open Progress / Results";

    [ObservableProperty]
    private string _resultsPanelSummaryText =
        "Use the side panel during or after deploy for progress and results.";

    [ObservableProperty]
    private bool _canToggleResultsPanel = true;

    // Non-bound workflow flags used by the controller.
    public bool IsSynchronizingEditorDraft { get; private set; }

    public bool IsEvaluatingReadiness { get; private set; }

    public bool IsStarting { get; private set; }

    public bool ShowAllVmRows { get; private set; }

    public string ReadinessSummaryText { get; private set; } = "Readiness has not been evaluated.";

    public DeploymentReadinessReport? ReadinessReport { get; private set; }

    public IReadOnlyList<DeployCompatibilityIssue> CompatibilityIssues => _compatibilityIssues;

    public VmTemplate? SelectedVmEntry => SelectedVmEntryRow?.VmEntry;

    public int VmEntryCount => VmEntries.Count;

    public int LiveProgressVmCount => _progressByVm.Count;

    public bool HasBlockingFailures =>
        _compatibilityIssues.Any(issue => issue.IsBlocking) || (ReadinessReport?.HasBlockingFailures ?? false);

    // Command CanExecute gates.
    public bool CanAddVm => !IsEvaluatingReadiness && !IsStarting;

    public bool CanRemoveVm => SelectedVmEntry is not null && !IsEvaluatingReadiness && !IsStarting;

    public bool CanApplyVmChanges => CanRemoveVm;

    public bool CanResolveSuggestions => VmEntries.Count > 0 && !IsEvaluatingReadiness && !IsStarting;

    public bool CanOpenTemplateEditor => VmEntries.Count > 0 && !IsStarting;

    public bool CanStartDeploy =>
        VmEntries.Count > 0 && !HasBlockingFailures && !IsEvaluatingReadiness && !IsStarting;

    // IDeployQuickDeployLane.
    public int DraftCount => VmEntries.Count;

    public bool ShouldAutoOpenResultsPanel =>
        IsStarting || string.Equals(LifecycleState, "Running", StringComparison.OrdinalIgnoreCase);

    public string ResultsPanelTitle => "Quick Deploy Progress / Results";

    /// <summary>
    /// Activates the lane on tab enter: seeds the default entry, loads reference data, and schedules
    /// the first readiness pass. Deactivation is a no-op because an in-flight deploy keeps running
    /// while another subview is shown; navigate-away cleanup is handled by <see cref="CleanupAsync"/>.
    /// </summary>
    public void ApplyShellState(bool isActive)
    {
        if (!isActive)
        {
            return;
        }

        EnsureSeeded();
        _ = ((IDeployQuickDeployWorkspaceControllerHost)this).EnsureReferenceDataAsync(forceRefresh: false);
        UpdateUi();

        if (VmEntries.Count > 0 &&
            ReadinessReport is null &&
            !IsEvaluatingReadiness &&
            !IsStarting)
        {
            _controller.ScheduleAutoEvaluate();
        }
    }

    public void ApplyResultsPanelState(bool isActive, bool showPanel, bool panelUnavailable)
    {
        ResultsPanelButtonText = showPanel && isActive ? "Hide Progress / Results" : "Open Progress / Results";
        CanToggleResultsPanel = isActive && !panelUnavailable;
        ResultsPanelSummaryText = panelUnavailable
            ? "Expand the window to review the progress and results panel."
            : ShouldAutoOpenResultsPanel
                ? "The panel auto-opens while deployment runs and stays available for result review."
                : ResultRows.Count > 0
                    ? $"{ResultRows.Count} VM result row(s) are available for review."
                    : "Use the side panel during or after deploy for progress, timeline, and results.";
    }

    public override Task CleanupAsync()
    {
        // Cleanup/cancellation policy: cancelling the in-flight operation lets the coordinator
        // clean up resources it created so navigate-away never leaves orphaned VMs or disks.
        _activeDeploymentContext?.RequestUserCancellation();
        return base.CleanupAsync();
    }

    // Command handlers.
    [RelayCommand(CanExecute = nameof(CanAddVm))]
    private void AddVm()
    {
        ShowAllVmRows = false;
        var entry = AddVmEntry();
        ClearReadinessState("Readiness has not been evaluated.");
        ResetProgressState();
        StatusText = $"Added VM entry '{entry.Name}'.";
        NotifySharedUiStateChanged();
        UpdateUi();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveVm))]
    private Task RemoveVm() => RemoveVmEntryAsync(SelectedVmEntry);

    [RelayCommand]
    private Task RemoveVmRow(DeployQuickDeployVmEntryRow? row) => RemoveVmEntryAsync(row?.VmEntry);

    [RelayCommand(CanExecute = nameof(CanApplyVmChanges))]
    private async Task ApplyVmChanges()
    {
        ShowAllVmRows = false;
        if (SelectedVmEntry is null)
        {
            StatusText = "Select a VM entry first.";
            return;
        }

        if (!TryApplyVmFields(showSuccessStatus: true))
        {
            return;
        }

        ClearReadinessState("Readiness has not been evaluated.");
        ResetProgressState();
        UpdateUi();
        await _controller.EvaluateReadinessAsync(DeploymentPreflightMode.Full);
    }

    [RelayCommand(CanExecute = nameof(CanResolveSuggestions))]
    private async Task ResolveSuggestions()
    {
        if (VmEntries.Count == 0)
        {
            StatusText = "Add at least one VM entry first.";
            return;
        }

        var template = BuildTemplate();
        await ((IDeployQuickDeployWorkspaceControllerHost)this).EnsureReferenceDataAsync(forceRefresh: false);
        var applied = _resolveSuggestionsService.Apply(
            template,
            _referenceDataService.CatalogItems,
            _referenceDataService.AvailableSwitches);

        // Nothing was stale, so skip the readiness re-run and leave the outcome on the status line. The
        // enablement gate cannot cheaply pre-compute this (the switch/catalog reference data is loaded
        // asynchronously and is empty/stale at CanExecute time), so the button stays enabled and the
        // result is reported here instead.
        if (applied == 0)
        {
            StatusText = "No stale references found.";
            return;
        }

        ReplaceVmEntriesFromTemplate(template);

        // Re-evaluate against the repaired entries, then restore the auto-fix outcome as the action
        // status: EvaluateReadinessAsync overwrites StatusText with its own readiness summary, which would
        // otherwise hide what the button just did. The readiness verdict still surfaces via the lifecycle
        // badges and readiness panel.
        await _controller.EvaluateReadinessAsync(DeploymentPreflightMode.Full);
        StatusText = $"Auto-fixed {applied} reference(s).";
    }

    [RelayCommand(CanExecute = nameof(CanOpenTemplateEditor))]
    private async Task OpenTemplateEditor()
    {
        if (!TryApplyVmFields(showSuccessStatus: false) && SelectedVmEntry is not null)
        {
            return;
        }

        if (VmEntries.Count == 0)
        {
            StatusText = "Add at least one VM entry first.";
            return;
        }

        await _showTemplateEditorAsync(
            new TemplateEditorDocument
            {
                Template = BuildTemplate(),
                SourceFilePath = null
            },
            "Opened quick deploy configuration in Templates editor.");
        StatusText = "Opened quick deploy configuration in Templates editor.";
    }

    [RelayCommand(CanExecute = nameof(CanStartDeploy))]
    private Task StartDeploy() => _controller.StartDeployAsync();

    [RelayCommand]
    private Task Evaluate() => _controller.EvaluateReadinessAsync(DeploymentPreflightMode.Full);

    [RelayCommand(CanExecute = nameof(CanAddVm))]
    private void AddSwitchRow()
    {
        var row = CreateSwitchRow(null);
        SwitchRows.Add(row);
        OnEditorDraftChanged();
    }

    [RelayCommand]
    private void RemoveSwitchRow(DeployQuickDeploySwitchRowItem? row)
    {
        if (row is null)
        {
            return;
        }

        row.PropertyChanged -= SwitchRow_PropertyChanged;
        SwitchRows.Remove(row);
        OnEditorDraftChanged();
    }

    [RelayCommand]
    private void ToggleResultsPanel() => _requestResultsPanelToggle();

    // Editor draft change reactions.
    partial void OnEditorVmNameDraftChanged(string value) => OnEditorDraftChanged();

    partial void OnEditorVmMemoryDraftChanged(string value) => OnEditorDraftChanged();

    partial void OnEditorVmCpuDraftChanged(string value) => OnEditorDraftChanged();

    partial void OnSelectedVhdxCatalogItemChanged(object? value)
    {
        var option = value as TemplateVhdxCatalogOption;
        _editorVhdxIdDraft = NormalizeValue(option?.Id);
        _editorVhdPathDraft = NormalizeValue(option?.Path);
        _editorVhdxSignatureDraft = NormalizeValue(option?.Signature);
        OnEditorDraftChanged();
    }

    partial void OnSelectedVmEntryRowChanged(DeployQuickDeployVmEntryRow? value)
    {
        if (IsSynchronizingEditorDraft)
        {
            return;
        }

        LoadEditorDraftFromSelection();
        UpdateUi();
    }

    partial void OnLifecycleStateChanged(string value) => OnPropertyChanged(nameof(ShouldAutoOpenResultsPanel));

    private void OnEditorDraftChanged()
    {
        if (IsSynchronizingEditorDraft)
        {
            return;
        }

        UpdateUi();
        _controller.ScheduleAutoEvaluate();
    }

    private void SwitchRow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DeployQuickDeploySwitchRowItem.SelectedSwitch))
        {
            OnEditorDraftChanged();
        }
    }

    // Workspace state operations invoked by the controller.
    public void SetShowAllVmRows(bool showAllVmRows) => ShowAllVmRows = showAllVmRows;

    public void BeginStarting()
    {
        IsStarting = true;
        RefreshCommandStates();
    }

    public void EndStarting()
    {
        IsStarting = false;
        RefreshCommandStates();
    }

    public void BeginReadinessEvaluation()
    {
        IsEvaluatingReadiness = true;
        RefreshCommandStates();
    }

    public void SetWorkflowState(string lifecycleState, int progressPercent, string progressSummary)
    {
        LifecycleState = lifecycleState;
        ProgressPercent = ResolveWorkflowProgress(lifecycleState, progressPercent);
        ProgressSummary = progressSummary;
    }

    /// <summary>
    /// Resolves the overall progress value shown by both the readiness bar and the right-panel overall
    /// deployment bar. While VMs are actively deploying the bar reflects the real per-VM aggregate so it
    /// moves as VMs complete instead of resting on a fixed stage constant (the previous behavior left it
    /// stuck near the middle for the entire run). Pre-deploy and terminal states keep the caller's value.
    /// </summary>
    private int ResolveWorkflowProgress(string lifecycleState, int fallbackPercent) =>
        IsLiveDeployState(lifecycleState) && _progressByVm.Count > 0
            ? ComputeVmAggregateProgress()
            : fallbackPercent;

    private void RefreshLiveDeployProgress()
    {
        if (_progressByVm.Count == 0 || !IsLiveDeployState(LifecycleState))
        {
            return;
        }

        ProgressPercent = ComputeVmAggregateProgress();
    }

    private int ComputeVmAggregateProgress()
    {
        if (_progressByVm.Count == 0)
        {
            return 0;
        }

        var average = _progressByVm.Values.Average(state => state.ProgressPercent);
        return (int)Math.Round(average, MidpointRounding.AwayFromZero);
    }

    private static bool IsLiveDeployState(string lifecycleState) =>
        string.Equals(lifecycleState, "Running", StringComparison.OrdinalIgnoreCase);

    public void ResetProgressState()
    {
        LifecycleState = "Idle";
        ProgressPercent = 0;
        ProgressSummary = "No deployment started.";
    }

    public void ApplyReadinessResult(
        IReadOnlyList<DeployCompatibilityIssue> compatibilityIssues,
        DeploymentReadinessReport readinessReport,
        string readinessSummaryText)
    {
        _compatibilityIssues.Clear();
        _compatibilityIssues.AddRange(compatibilityIssues);
        ReadinessReport = readinessReport;
        ReadinessSummaryText = readinessSummaryText;
        IsEvaluatingReadiness = false;
        _deployBlockerRows.Clear();
        RefreshIssueRows();
        RefreshResultRows();
        RefreshCommandStates();
    }

    public void ClearReadinessState(string readinessSummaryText)
    {
        _compatibilityIssues.Clear();
        ReadinessReport = null;
        ReadinessSummaryText = readinessSummaryText;
        IsEvaluatingReadiness = false;
        _deployBlockerRows.Clear();
        RefreshIssueRows();
        RefreshResultRows();
        RefreshCommandStates();
    }

    public void SetReadinessEvaluationFailed(string readinessSummaryText) =>
        ClearReadinessState(readinessSummaryText);

    /// <summary>
    /// Seeds per-VM live-progress rows from a built V2 plan. The V2 runtime rebuilds its per-VM contexts during
    /// execution, so unlike the classic path the timeline must come from the plan nodes rather than pre-built
    /// contexts. Shared with the From-Template lane through <see cref="DeployV2ProgressPlan"/>.
    /// </summary>
    public void InitializeProgressRows(V2PlanBuildResult plan)
    {
        _progressByVm.Clear();
        foreach (var state in DeployV2ProgressPlan.BuildProgressStates(plan))
        {
            _progressByVm[state.VmName] = state;
        }

        RefreshResultRows();
    }

    public void UpdateProgressMessage(string vmName, string? message)
    {
        if (!_progressByVm.TryGetValue(vmName, out var state))
        {
            return;
        }

        state.UpdateSummaryMessage(message);
        RefreshResultRows();
    }

    public void ApplyProgressUpdate(string vmName, DeployStepStateUpdate update)
    {
        if (!_progressByVm.TryGetValue(vmName, out var state))
        {
            return;
        }

        state.ApplyStepStateUpdate(update);
        RefreshResultRows();
        RefreshLiveDeployProgress();
    }

    /// <summary>
    /// Projects the V2 planner's blocking issues into the deploy blocker rows so a plan that fails to build surfaces
    /// its blockers to the user instead of silently doing nothing. These persist across UI refreshes on the terminal
    /// blocked surface.
    /// </summary>
    public void ApplyPlanBlockers(IReadOnlyList<V2PlanIssue> blockers)
    {
        _deployBlockerRows.Clear();
        foreach (var blocker in blockers)
        {
            _deployBlockerRows.Add(new DeployIssueRow(
                Scope: string.IsNullOrWhiteSpace(blocker.VmName) ? "Global" : blocker.VmName!,
                Severity: "Block",
                Message: string.IsNullOrWhiteSpace(blocker.SuggestedAction)
                    ? blocker.Message
                    : $"{blocker.Message} {blocker.SuggestedAction}".Trim()));
        }

        RefreshIssueRows();
    }

    /// <summary>
    /// Projects the terminal V2 runtime result. Per-VM rows are already driven by the live step-state stream during
    /// execution, so this only surfaces any blocking messages the runtime returned.
    /// </summary>
    public void ApplyV2ExecutionResult(V2RuntimeExecutionResult result)
    {
        _deployBlockerRows.Clear();
        foreach (var message in result.BlockingMessages)
        {
            _deployBlockerRows.Add(new DeployIssueRow(
                Scope: "Global",
                Severity: "Block",
                Message: message));
        }

        RefreshIssueRows();
    }

    // IDeployQuickDeployWorkspaceControllerHost.
    AppSettings IDeployQuickDeployWorkspaceControllerHost.DeploymentSettings => _referenceDataService.DeploymentSettings;

    IReadOnlyList<string> IDeployQuickDeployWorkspaceControllerHost.AvailableSwitches => _referenceDataService.AvailableSwitches;

    IReadOnlyList<VhdxCatalogItem> IDeployQuickDeployWorkspaceControllerHost.LoadCatalogItems() => _referenceDataService.CatalogItems;

    bool IDeployQuickDeployWorkspaceControllerHost.TryApplyVmFields(bool showSuccessStatus, bool showValidationErrors) =>
        TryApplyVmFields(showSuccessStatus, showValidationErrors);

    void IDeployQuickDeployWorkspaceControllerHost.SetActionStatus(string statusText) => SetActionStatus(statusText);

    LabTemplate IDeployQuickDeployWorkspaceControllerHost.BuildTemplate() => BuildTemplate();

    LabTemplate IDeployQuickDeployWorkspaceControllerHost.BuildV2Template() => BuildV2Template();

    async Task IDeployQuickDeployWorkspaceControllerHost.EnsureReferenceDataAsync(bool forceRefresh)
    {
        await _referenceDataService.EnsureAsync(forceRefresh);
        _availableSwitches = _referenceDataService.AvailableSwitches;
        _availableVhdxCatalogOptions = _referenceDataService.VhdxCatalogOptions;
        RebuildVhdxCatalogItems();
        LoadEditorDraftFromSelection();
        TryAutoSelectSingleBaseDisk();
        UpdateUi();
    }

    /// <summary>
    /// F26: when the catalog holds exactly one base disk and the selected VM has none chosen yet, pick it
    /// automatically. This is trivially safe because there is no ambiguity, and it removes the most common
    /// first-run blocker (the default VM starting with no base disk).
    /// </summary>
    private void TryAutoSelectSingleBaseDisk()
    {
        if (_availableVhdxCatalogOptions.Count != 1)
        {
            return;
        }

        var entry = SelectedVmEntry;
        if (entry is null)
        {
            return;
        }

        var entryHasDisk = !string.IsNullOrWhiteSpace(entry.VhdxId) || !string.IsNullOrWhiteSpace(entry.VhdPath);
        if (entryHasDisk || SelectedVhdxCatalogItem is TemplateVhdxCatalogOption)
        {
            return;
        }

        var single = VhdxCatalogItems.OfType<TemplateVhdxCatalogOption>().FirstOrDefault();
        if (single is not null)
        {
            SelectedVhdxCatalogItem = single;
        }
    }

    Task<DeploymentReadinessReport> IDeployQuickDeployWorkspaceControllerHost.RunReadinessChecksAsync(
        MultiVmDeploymentContext context,
        DeploymentPreflightMode mode) =>
        _deploymentPreflightService.RunAsync(context, mode);

    void IDeployQuickDeployWorkspaceControllerHost.EnqueueUiUpdate(Action updateAction) => _marshalToUi(updateAction);

    void IDeployQuickDeployWorkspaceControllerHost.UpdateUi() => UpdateUi();

    async Task<V2PlanBuildResult> IDeployQuickDeployWorkspaceControllerHost.BuildV2PlanAsync(LabTemplate template)
    {
        await _referenceDataService.EnsureAsync(forceRefresh: false);
        return await _v2PlanningCapabilityService.BuildPlanAsync(new V2PlanBuildRequest
        {
            Template = template,
            CatalogItems = _referenceDataService.CatalogItems,
            AvailableSwitchNames = _referenceDataService.AvailableSwitches,
            AvailableSwitches = _referenceDataService.AvailableSwitchInfo,
            // Quick Deploy authors bare standalone VMs with no credential slots, so nothing is pre-resolved.
            ResolvedCredentialSlotKeys = Array.Empty<string>(),
            ExternalSwitchAdapterMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            DefaultDeploymentProfile = "Balanced"
        });
    }

    async Task<V2RuntimeExecutionResult> IDeployQuickDeployWorkspaceControllerHost.ExecuteV2DeployAsync(
        LabTemplate template,
        V2PlanBuildResult plan,
        MultiVmDeploymentContext context)
    {
        _activeDeploymentContext = context;
        try
        {
            return await _v2RuntimeCapabilityService.ExecuteAsync(new V2RuntimeExecutionRequest
            {
                Template = template,
                Plan = plan,
                Settings = _referenceDataService.DeploymentSettings,
                // Standalone VMs perform no guest work, so no credential slot values are required.
                CredentialSlotValues = new Dictionary<string, V2RuntimeCredential>(StringComparer.OrdinalIgnoreCase),
                BaseRemoteAccessOptions = new V2BaseRemoteAccessOptions(),
                DeploymentContext = context
            });
        }
        finally
        {
            _activeDeploymentContext = null;
        }
    }

    // Internal helpers ported from the former workspace view model, owner, and composition.
    private void SetActionStatus(string statusText)
    {
        StatusText = statusText;
        RaiseResultsPanelStateChanged();
    }

    private LabTemplate BuildTemplate() => new()
    {
        Name = "Quick Deploy Draft",
        Description = "Generated quick deploy input.",
        VmTemplates = VmEntries.Select(CloneVmTemplate).ToList()
    };

    /// <summary>
    /// Builds the V2-engine variant of the Quick Deploy template for planning and execution. Quick Deploy runs on
    /// the unified V2 graph engine, so this stamps the engine + schema and marks every VM Standalone: the planner
    /// then emits provisioning-only nodes (no domain-join progression, no guest work, no credential slots) that
    /// preserve the fast single-VM UX. The classic <see cref="BuildTemplate"/> shape is retained for the
    /// engine-agnostic readiness preflight, which is routed through the CoR-shaped context builder.
    /// </summary>
    private LabTemplate BuildV2Template()
    {
        var template = BuildTemplate();
        template.ExecutionEngine = TemplateExecutionEngine.V2UnifiedPlanning;
        template.SchemaVersion = "2.0.0";
        foreach (var vm in template.VmTemplates)
        {
            vm.MembershipMode = V2MembershipModeCatalog.Standalone;
        }

        return template;
    }

    private void EnsureSeeded()
    {
        var previousCount = VmEntries.Count;
        if (VmEntries.Count == 0)
        {
            VmEntries.Add(CreateDefaultVmEntry(1));
        }

        RefreshVmEntryRows();
        SetSelectedRow(FindRow(SelectedVmEntry) ?? VmEntryRows.FirstOrDefault());
        LoadEditorDraftFromSelection();

        if (VmEntries.Count != previousCount)
        {
            NotifySharedUiStateChanged();
        }
    }

    private VmTemplate AddVmEntry()
    {
        var entry = CreateDefaultVmEntry(VmEntries.Count + 1);
        VmEntries.Add(entry);
        RefreshVmEntryRows();
        SetSelectedRow(FindRow(entry));
        LoadEditorDraftFromSelection();
        return entry;
    }

    private async Task RemoveVmEntryAsync(VmTemplate? vmEntry)
    {
        ShowAllVmRows = false;
        if (vmEntry is null)
        {
            SetActionStatus("Select a VM entry first.");
            return;
        }

        var vmName = vmEntry.Name;
        if (!await _confirmRemoveVmAsync(vmName))
        {
            return;
        }

        VmEntries.Remove(vmEntry);
        RefreshVmEntryRows();
        SetSelectedRow(VmEntryRows.FirstOrDefault());
        LoadEditorDraftFromSelection();
        ClearReadinessState(
            VmEntries.Count == 0
                ? "Add at least one VM entry to evaluate readiness."
                : "Readiness has not been evaluated.");
        ResetProgressState();
        SetActionStatus($"Removed VM entry '{vmName}'.");
        NotifySharedUiStateChanged();
        UpdateUi();
    }

    private void ReplaceVmEntriesFromTemplate(LabTemplate template)
    {
        VmEntries.Clear();
        foreach (var vmTemplate in template.VmTemplates)
        {
            VmEntries.Add(CloneVmTemplate(vmTemplate));
        }

        RefreshVmEntryRows();
        SetSelectedRow(FindRow(SelectedVmEntry) ?? VmEntryRows.FirstOrDefault());
        LoadEditorDraftFromSelection();
        NotifySharedUiStateChanged();
        UpdateUi();
    }

    private void SetSelectedRow(DeployQuickDeployVmEntryRow? row)
    {
        IsSynchronizingEditorDraft = true;
        try
        {
            SelectedVmEntryRow = row;
        }
        finally
        {
            IsSynchronizingEditorDraft = false;
        }
    }

    private DeployQuickDeployVmEntryRow? FindRow(VmTemplate? vmEntry) =>
        vmEntry is null ? null : VmEntryRows.FirstOrDefault(row => ReferenceEquals(row.VmEntry, vmEntry));

    private void RefreshVmEntryRows()
    {
        var existingByVm = VmEntryRows.ToDictionary(row => row.VmEntry);
        foreach (var staleRow in VmEntryRows.Where(row => !VmEntries.Contains(row.VmEntry)).ToList())
        {
            VmEntryRows.Remove(staleRow);
        }

        for (var index = 0; index < VmEntries.Count; index++)
        {
            var vmEntry = VmEntries[index];
            if (!existingByVm.TryGetValue(vmEntry, out var row))
            {
                row = new DeployQuickDeployVmEntryRow(vmEntry);
                VmEntryRows.Insert(index, row);
                existingByVm[vmEntry] = row;
            }
            else
            {
                var currentIndex = VmEntryRows.IndexOf(row);
                if (currentIndex != index)
                {
                    VmEntryRows.Move(currentIndex, index);
                }
            }
        }
    }

    private void LoadEditorDraftFromSelection()
    {
        IsSynchronizingEditorDraft = true;
        try
        {
            var vmEntry = SelectedVmEntry;
            if (vmEntry is null)
            {
                EditorVmNameDraft = string.Empty;
                EditorVmMemoryDraft = string.Empty;
                EditorVmCpuDraft = string.Empty;
                RebuildSwitchRows(Array.Empty<string>());
                _editorVhdxIdDraft = null;
                _editorVhdPathDraft = null;
                _editorVhdxSignatureDraft = null;
                SelectedVhdxCatalogItem = VhdxCatalogItems.Count > 0 ? VhdxCatalogItems[0] : null;
                return;
            }

            EditorVmNameDraft = vmEntry.Name;
            EditorVmMemoryDraft = vmEntry.MemoryMb.ToString(CultureInfo.InvariantCulture);
            EditorVmCpuDraft = vmEntry.CpuCount.ToString(CultureInfo.InvariantCulture);
            RebuildSwitchRows(ResolveEntrySwitchNames(vmEntry));
            _editorVhdxIdDraft = NormalizeValue(vmEntry.VhdxId);
            _editorVhdPathDraft = NormalizeValue(vmEntry.VhdPath);
            _editorVhdxSignatureDraft = NormalizeValue(vmEntry.VhdxSignature);
            SelectedVhdxCatalogItem = ResolveSelectedVhdxItem();
        }
        finally
        {
            IsSynchronizingEditorDraft = false;
        }
    }

    private void RebuildSwitchRows(IReadOnlyList<string> selectedSwitches)
    {
        // Keep the switch-row collection stable across readiness passes. This runs on every evaluate
        // (via LoadEditorDraftFromSelection), and rebuilding tears down the bound ItemsControl, which
        // closes any open switch dropdown mid-selection. Skip when the rows already reflect the
        // requested selection against the current available switches.
        if (SwitchRowsMatchCurrentState(selectedSwitches))
        {
            return;
        }

        foreach (var existing in SwitchRows)
        {
            existing.PropertyChanged -= SwitchRow_PropertyChanged;
        }

        SwitchRows.Clear();
        foreach (var selectedSwitch in selectedSwitches)
        {
            SwitchRows.Add(CreateSwitchRow(selectedSwitch));
        }
    }

    /// <summary>
    /// Returns true when <see cref="SwitchRows"/> already mirrors what a rebuild for
    /// <paramref name="selectedSwitches"/> would produce (same effective selection per row and the
    /// same option set), so the destructive rebuild can be skipped and the dropdown stays open.
    /// </summary>
    private bool SwitchRowsMatchCurrentState(IReadOnlyList<string> selectedSwitches)
    {
        if (SwitchRows.Count != selectedSwitches.Count)
        {
            return false;
        }

        for (var index = 0; index < selectedSwitches.Count; index++)
        {
            var row = SwitchRows[index];
            if (!string.Equals(row.EffectiveSwitchName, ResolveEffectiveSwitchName(selectedSwitches[index]), StringComparison.Ordinal))
            {
                return false;
            }

            if (!SwitchRowOptionsMatchAvailable(row))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Mirrors <see cref="CreateSwitchRow"/> resolution: the effective host-switch name for a requested
    /// value, or an empty string when it is blank or not among the current available switches.
    /// </summary>
    private string ResolveEffectiveSwitchName(string? requested) =>
        !string.IsNullOrWhiteSpace(requested) &&
        _availableSwitches.Contains(requested, StringComparer.OrdinalIgnoreCase)
            ? _availableSwitches.First(name => string.Equals(name, requested, StringComparison.OrdinalIgnoreCase))
            : string.Empty;

    private bool SwitchRowOptionsMatchAvailable(DeployQuickDeploySwitchRowItem row)
    {
        if (row.Options.Count != _availableSwitches.Count + 1)
        {
            return false;
        }

        if (!string.Equals(row.Options[0], DeployQuickDeploySwitchRowItem.Placeholder, StringComparison.Ordinal))
        {
            return false;
        }

        for (var index = 0; index < _availableSwitches.Count; index++)
        {
            if (!string.Equals(row.Options[index + 1], _availableSwitches[index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private DeployQuickDeploySwitchRowItem CreateSwitchRow(string? selectedSwitch)
    {
        var options = new List<string> { DeployQuickDeploySwitchRowItem.Placeholder };
        options.AddRange(_availableSwitches);
        var row = new DeployQuickDeploySwitchRowItem(options, selectedSwitch);
        row.PropertyChanged += SwitchRow_PropertyChanged;
        return row;
    }

    private void RebuildVhdxCatalogItems()
    {
        // The catalog collection is the base-disk ComboBox ItemsSource. Clearing it resets the
        // control's selection to null, which writes back through the TwoWay binding and would
        // re-arm auto-evaluate - spinning the workflow and closing any open dropdown. The
        // reference-data service reuses its option instances, so skip the rebuild entirely when
        // the option set is unchanged. Any genuine rebuild runs under the editor-sync guard so the
        // transient selection reset never schedules a readiness pass, and the prior selection is
        // preserved across the rebuild.
        if (VhdxCatalogItemsMatchCurrentOptions())
        {
            return;
        }

        var wasSynchronizing = IsSynchronizingEditorDraft;
        IsSynchronizingEditorDraft = true;
        try
        {
            var previousSelection = SelectedVhdxCatalogItem;
            VhdxCatalogItems.Clear();
            VhdxCatalogItems.Add(VhdxPlaceholder);
            foreach (var option in _availableVhdxCatalogOptions)
            {
                VhdxCatalogItems.Add(option);
            }

            SelectedVhdxCatalogItem = previousSelection is not null && VhdxCatalogItems.Contains(previousSelection)
                ? previousSelection
                : VhdxCatalogItems[0];
        }
        finally
        {
            IsSynchronizingEditorDraft = wasSynchronizing;
        }
    }

    /// <summary>
    /// Returns true when <see cref="VhdxCatalogItems"/> already mirrors the current catalog options
    /// (placeholder followed by the same option instances), so a destructive rebuild can be skipped.
    /// </summary>
    private bool VhdxCatalogItemsMatchCurrentOptions()
    {
        if (VhdxCatalogItems.Count != _availableVhdxCatalogOptions.Count + 1)
        {
            return false;
        }

        if (!Equals(VhdxCatalogItems[0], VhdxPlaceholder))
        {
            return false;
        }

        for (var index = 0; index < _availableVhdxCatalogOptions.Count; index++)
        {
            if (!ReferenceEquals(VhdxCatalogItems[index + 1], _availableVhdxCatalogOptions[index]))
            {
                return false;
            }
        }

        return true;
    }

    private object ResolveSelectedVhdxItem()
    {
        var match = string.IsNullOrWhiteSpace(_editorVhdxIdDraft)
            ? null
            : _availableVhdxCatalogOptions.FirstOrDefault(option =>
                string.Equals(option.Id, _editorVhdxIdDraft, StringComparison.OrdinalIgnoreCase));
        if (match is null && !string.IsNullOrWhiteSpace(_editorVhdPathDraft))
        {
            match = _availableVhdxCatalogOptions.FirstOrDefault(option =>
                string.Equals(option.Path, _editorVhdPathDraft, StringComparison.OrdinalIgnoreCase));
        }

        return match is not null
            ? VhdxCatalogItems.FirstOrDefault(item => ReferenceEquals(item, match)) ?? (object)VhdxPlaceholder
            : VhdxPlaceholder;
    }

    private bool TryApplyVmFields(bool showSuccessStatus, bool showValidationErrors = true)
    {
        if (SelectedVmEntry is null || IsSynchronizingEditorDraft)
        {
            return false;
        }

        var vmName = EditorVmNameDraft.Trim();
        if (string.IsNullOrWhiteSpace(vmName))
        {
            if (showValidationErrors)
            {
                SetActionStatus("VM name is required.");
            }

            return false;
        }

        if (!int.TryParse(EditorVmMemoryDraft, out var memoryMb) || memoryMb <= 0)
        {
            if (showValidationErrors)
            {
                SetActionStatus("Memory must be a positive integer.");
            }

            return false;
        }

        if (!int.TryParse(EditorVmCpuDraft, out var cpuCount) || cpuCount <= 0)
        {
            if (showValidationErrors)
            {
                SetActionStatus("CPU count must be a positive integer.");
            }

            return false;
        }

        var selectedEntry = SelectedVmEntry;
        var previousName = selectedEntry.Name;
        var switchNames = CurrentSwitchDraft();
        selectedEntry.Name = vmName;
        selectedEntry.MemoryMb = memoryMb;
        selectedEntry.CpuCount = cpuCount;
        selectedEntry.SwitchName = switchNames.FirstOrDefault();
        selectedEntry.SwitchNames = switchNames.Count == 0 ? null : switchNames.ToList();
        selectedEntry.VhdxId = _editorVhdxIdDraft;
        selectedEntry.VhdPath = _editorVhdPathDraft;
        selectedEntry.VhdxSignature = _editorVhdxSignatureDraft;

        if (showSuccessStatus)
        {
            SetActionStatus($"Updated '{vmName}'.");
        }

        if (!string.Equals(previousName, vmName, StringComparison.Ordinal))
        {
            RefreshVmEntryRows();
            SetSelectedRow(FindRow(selectedEntry));
        }

        return true;
    }

    private List<string> CurrentSwitchDraft() =>
        SwitchRows.Select(row => row.EffectiveSwitchName).ToList();

    private void UpdateUi()
    {
        if (VmEntries.Count == 0)
        {
            ClearReadinessState("Add at least one VM entry to evaluate readiness.");
            ResetProgressState();
        }

        RefreshResultRows();
        RefreshIssueRows();
        UpdateVmEntryRowBadges();
        UpdateEditorGuidanceAndSummary();
        RefreshCommandStates();
        RaiseResultsPanelStateChanged();
    }

    private void RefreshCommandStates()
    {
        AddVmCommand.NotifyCanExecuteChanged();
        RemoveVmCommand.NotifyCanExecuteChanged();
        ApplyVmChangesCommand.NotifyCanExecuteChanged();
        ResolveSuggestionsCommand.NotifyCanExecuteChanged();
        OpenTemplateEditorCommand.NotifyCanExecuteChanged();
        StartDeployCommand.NotifyCanExecuteChanged();
        AddSwitchRowCommand.NotifyCanExecuteChanged();
    }

    private void RaiseResultsPanelStateChanged() => ResultsPanelStateChanged?.Invoke(this, EventArgs.Empty);

    private void NotifySharedUiStateChanged() => SharedUiStateChanged?.Invoke(this, EventArgs.Empty);

    private void UpdateEditorGuidanceAndSummary()
    {
        var blockingIssueCount = IssueRows.Count(issue => string.Equals(issue.Severity, "Block", StringComparison.OrdinalIgnoreCase));
        var warningIssueCount = IssueRows.Count - blockingIssueCount;
        GlobalIssuesBadgeText = $"Blocking: {blockingIssueCount} | Warnings: {warningIssueCount}";

        var hasEntries = VmEntries.Count > 0;
        var shouldShowInlineGuidance = hasEntries && !IsStarting && LiveProgressVmCount == 0;
        ReadinessDisplayText = shouldShowInlineGuidance
            ? $"{ReadinessSummaryText} Review VM row badges and the selected VM details to fix blockers here before deploy."
            : ProgressSummary;

        SwitchGuidanceText = _availableSwitches.Count == 0
            ? "No host switches available. Add a switch in Assets first."
            : CurrentSwitchDraft().Any(name => !string.IsNullOrWhiteSpace(name))
                ? "Switch rows configured."
                : "Switch selection is optional.";

        var selectedOption = SelectedVhdxCatalogItem as TemplateVhdxCatalogOption;
        var hasSelectedEntry = SelectedVmEntry is not null;

        // F26: nudge the user to pick a base disk while one is still missing on the selected VM.
        ShowBaseDiskHint = hasSelectedEntry && selectedOption is null;
        BaseDiskHintText = _availableVhdxCatalogOptions.Count == 0
            ? "No base disks in the catalog. Import one in Assets first."
            : "Select a base disk to continue.";

        UpdateEditorNameValidation();

        EditorIssueSummaryText = BuildEditorIssueSummaryText();
    }

    /// <summary>
    /// F27: computes inline validation for the Name field. Surfaces an empty name, a duplicate name that
    /// collides with another VM entry, and a destination-folder collision reported by readiness for the
    /// selected VM (the case where a folder already matches the VM name and blocks the deploy).
    /// </summary>
    private void UpdateEditorNameValidation()
    {
        var selectedEntry = SelectedVmEntry;
        if (selectedEntry is null)
        {
            HasEditorNameError = false;
            EditorNameValidationText = string.Empty;
            return;
        }

        var name = EditorVmNameDraft.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            HasEditorNameError = true;
            EditorNameValidationText = "VM name is required.";
            return;
        }

        var duplicateName = VmEntries
            .Where(entry => !ReferenceEquals(entry, selectedEntry))
            .Any(entry => string.Equals(entry.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase));
        if (duplicateName)
        {
            HasEditorNameError = true;
            EditorNameValidationText = "Another VM entry already uses this name.";
            return;
        }

        var folderCollision = FindDestinationFolderCollision(name);
        if (folderCollision is not null)
        {
            HasEditorNameError = true;
            EditorNameValidationText = folderCollision;
            return;
        }

        HasEditorNameError = false;
        EditorNameValidationText = string.Empty;
    }

    /// <summary>
    /// Returns the readiness message for a destination-folder collision scoped to <paramref name="vmName"/>,
    /// or null when there is none. A collision is a blocking destination-path readiness failure whose code
    /// marks an already-existing folder for that VM.
    /// </summary>
    private string? FindDestinationFolderCollision(string vmName)
    {
        var results = ReadinessReport?.Results;
        if (results is null)
        {
            return null;
        }

        foreach (var result in results)
        {
            if (result.Status != DeploymentReadinessStatus.Fail ||
                result.Category != DeploymentReadinessCategory.DestinationPathStorage)
            {
                continue;
            }

            if (!(result.Code?.Contains("CONFLICT_DIR_EXISTS", StringComparison.OrdinalIgnoreCase) ?? false))
            {
                continue;
            }

            if (result.AffectedVmNames.Any(affected => string.Equals(affected?.Trim(), vmName, StringComparison.OrdinalIgnoreCase)))
            {
                return FormatIssueMessage(result.Message, result.ActionableGuidance);
            }
        }

        return null;
    }

    private string BuildEditorIssueSummaryText()
    {
        if (SelectedVmEntry is null)
        {
            return "Select a VM entry to review its properties and resolve any issues inline.";
        }

        var draftIssues = GetDraftIssues();
        if (draftIssues.Count > 0)
        {
            var blockingCount = draftIssues.Count(issue => issue.IsBlocking);
            return blockingCount > 0
                ? $"Blocking issues in this VM: {string.Join(" ", draftIssues.Where(issue => issue.IsBlocking).Select(issue => issue.Message))}"
                : $"Warnings in this VM: {string.Join(" ", draftIssues.Select(issue => issue.Message))}";
        }

        var selectedRow = FindRow(SelectedVmEntry);
        if (selectedRow is not null && selectedRow.HasIssueSummary)
        {
            return $"{selectedRow.IssueBadgeText}: {selectedRow.IssueSummary}";
        }

        return "Ready. Changes validate while you edit. Row signals show which VM needs attention.";
    }

    private void UpdateVmEntryRowBadges()
    {
        RefreshVmEntryRows();

        var compatibilityByVm = _compatibilityIssues
            .Where(issue => !string.IsNullOrWhiteSpace(issue.VmName))
            .GroupBy(issue => issue.VmName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var readinessByVm = (ReadinessReport?.Results ?? [])
            .SelectMany(result => result.AffectedVmNames.Select(vmName => (vmName, result)))
            .Where(tuple => !string.IsNullOrWhiteSpace(tuple.vmName))
            .GroupBy(tuple => tuple.vmName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(item => item.result).ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var row in VmEntryRows)
        {
            row.DisplayName = string.IsNullOrWhiteSpace(row.VmEntry.Name) ? "Unnamed VM" : row.VmEntry.Name.Trim();
            row.SecondaryText = BuildVmEntrySecondaryText(row.VmEntry);

            compatibilityByVm.TryGetValue(row.VmEntry.Name ?? string.Empty, out var compatibilityIssues);
            readinessByVm.TryGetValue(row.VmEntry.Name ?? string.Empty, out var readinessIssues);
            compatibilityIssues ??= [];
            readinessIssues ??= [];

            var draftIssues = ReferenceEquals(row.VmEntry, SelectedVmEntry)
                ? GetDraftIssues()
                : GetVmEntryIssues(row.VmEntry);

            var blockingMessages = new List<string>();
            var warningMessages = new List<string>();

            blockingMessages.AddRange(draftIssues.Where(issue => issue.IsBlocking).Select(issue => issue.Message));
            warningMessages.AddRange(draftIssues.Where(issue => !issue.IsBlocking).Select(issue => issue.Message));

            blockingMessages.AddRange(compatibilityIssues.Where(issue => issue.IsBlocking).Select(issue => FormatIssueMessage(issue.Message, issue.Guidance)));
            warningMessages.AddRange(compatibilityIssues.Where(issue => !issue.IsBlocking).Select(issue => FormatIssueMessage(issue.Message, issue.Guidance)));

            blockingMessages.AddRange(readinessIssues.Where(issue => issue.Status == DeploymentReadinessStatus.Fail).Select(issue => FormatIssueMessage(issue.Message, issue.ActionableGuidance)));
            warningMessages.AddRange(readinessIssues.Where(issue => issue.Status == DeploymentReadinessStatus.Warn).Select(issue => FormatIssueMessage(issue.Message, issue.ActionableGuidance)));

            if (blockingMessages.Count > 0)
            {
                row.IssueBadgeText = "Blocked";
                row.IssueSummary = blockingMessages[0];
                row.IssueSeverity = "Critical";
                row.HasIssueBadge = true;
                row.HasIssueSummary = true;
            }
            else if (warningMessages.Count > 0)
            {
                row.IssueBadgeText = "Warning";
                row.IssueSummary = warningMessages[0];
                row.IssueSeverity = "Warning";
                row.HasIssueBadge = true;
                row.HasIssueSummary = true;
            }
            else
            {
                row.IssueBadgeText = string.Empty;
                row.IssueSummary = string.Empty;
                row.IssueSeverity = "None";
                row.HasIssueBadge = false;
                row.HasIssueSummary = false;
            }
        }
    }

    private void RefreshResultRows()
    {
        ResultRows.Clear();

        if (ShowAllVmRows && _progressByVm.Count > 0)
        {
            foreach (var state in _progressByVm.Values.OrderBy(value => value.VmName, StringComparer.OrdinalIgnoreCase))
            {
                ResultRows.Add(state.ToRow());
            }

            return;
        }

        var vmNames = VmEntries
            .Select(vm => string.IsNullOrWhiteSpace(vm.Name) ? "Unnamed-VM" : vm.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var mergedIssues = DeployReadinessProjection.Merge(_compatibilityIssues, ReadinessReport);

        foreach (var vmName in vmNames)
        {
            var vmIssues = mergedIssues
                .Where(issue => DeployReadinessProjection.ScopeMatches(issue, vmName))
                .ToList();
            var blocking = vmIssues.Where(issue => issue.IsBlocking).ToList();
            var warnings = vmIssues.Where(issue => !issue.IsBlocking).ToList();
            if (blocking.Count == 0 && warnings.Count == 0)
            {
                continue;
            }

            var hasBlocking = blocking.Count > 0;
            ResultRows.Add(new DeployVmResultRow(
                VmName: vmName,
                Status: hasBlocking ? "Blocked" : "Warning",
                Summary: FormatIssueReasonSummary(blocking, warnings),
                ProgressPercent: hasBlocking ? 100 : 80,
                TimelineSteps: CreateReadinessTimelineSteps(blocking, warnings)));
        }

        var hasReadinessData = ReadinessReport is not null || _compatibilityIssues.Count > 0;
        if (ResultRows.Count == 0 && vmNames.Count > 0 && hasReadinessData)
        {
            ResultRows.Add(new DeployVmResultRow(
                VmName: "Quick Deploy",
                Status: "Ready",
                Summary: "Readiness data is available. Review VM details to continue.",
                ProgressPercent: 100,
                TimelineSteps: []));
        }
    }

    /// <summary>
    /// Builds a per-VM result-row summary that names the actual blocking or warning reason(s) instead of
    /// a bare count. Blocking reasons take precedence; the reasons come from the de-duplicated merged
    /// issue list so the text matches the real number of problems.
    /// </summary>
    private static string FormatIssueReasonSummary(
        IReadOnlyList<DeployMergedIssue> blocking,
        IReadOnlyList<DeployMergedIssue> warnings)
    {
        if (blocking.Count > 0)
        {
            return $"Blocked: {string.Join(" ", blocking.Select(issue => issue.Message))}";
        }

        return $"Warning: {string.Join(" ", warnings.Select(issue => issue.Message))}";
    }

    private void RefreshIssueRows()
    {
        IssueRows.Clear();

        // Compatibility issues and the readiness report describe the same reality; merge them so a single
        // problem is not listed (or counted) twice.
        foreach (var issue in DeployReadinessProjection.Merge(_compatibilityIssues, ReadinessReport))
        {
            IssueRows.Add(new DeployIssueRow(
                Scope: issue.Scope,
                Severity: issue.IsBlocking ? "Block" : "Warn",
                Message: FormatIssueMessage(issue.Message, issue.Guidance)));
        }

        // Deploy-time blockers (V2 plan build failures or runtime blocking messages) are surfaced last so they stay
        // visible on the terminal blocked/failed surface, independent of the readiness-derived rows above.
        foreach (var blockerRow in _deployBlockerRows)
        {
            IssueRows.Add(blockerRow);
        }
    }
    private List<(bool IsBlocking, string Message)> GetDraftIssues()
    {
        if (SelectedVmEntry is null)
        {
            return [];
        }

        return GetDraftIssues(
            EditorVmNameDraft,
            EditorVmMemoryDraft,
            EditorVmCpuDraft,
            CurrentSwitchDraft(),
            _availableSwitches,
            string.IsNullOrWhiteSpace(_editorVhdxIdDraft) && string.IsNullOrWhiteSpace(_editorVhdPathDraft)
                ? null
                : new object(),
            _availableVhdxCatalogOptions.Count);
    }

    private IReadOnlyList<string> ResolveEntrySwitchNames(VmTemplate vmEntry) =>
        vmEntry.SwitchNames?.Count > 0
            ? vmEntry.SwitchNames
            : string.IsNullOrWhiteSpace(vmEntry.SwitchName)
                ? Array.Empty<string>()
                : [vmEntry.SwitchName];

    private static List<(bool IsBlocking, string Message)> GetVmEntryIssues(VmTemplate vmEntry)
    {
        var memoryText = vmEntry.MemoryMb.ToString(CultureInfo.InvariantCulture);
        var cpuText = vmEntry.CpuCount.ToString(CultureInfo.InvariantCulture);
        var selectedCatalog = string.IsNullOrWhiteSpace(vmEntry.VhdxId) && string.IsNullOrWhiteSpace(vmEntry.VhdPath)
            ? null
            : new object();
        IReadOnlyList<string> switchNames = vmEntry.SwitchNames?.Count > 0
            ? vmEntry.SwitchNames
            : string.IsNullOrWhiteSpace(vmEntry.SwitchName)
                ? Array.Empty<string>()
                : [vmEntry.SwitchName];

        return GetDraftIssues(
            vmEntry.Name,
            memoryText,
            cpuText,
            switchNames,
            switchNames,
            selectedCatalog,
            availableCatalogCount: 1);
    }

    private static List<(bool IsBlocking, string Message)> GetDraftIssues(
        string? vmName,
        string? memoryText,
        string? cpuText,
        IReadOnlyList<string>? selectedSwitches,
        IReadOnlyList<string>? availableSwitches,
        object? selectedCatalogItem,
        int availableCatalogCount)
    {
        var issues = new List<(bool IsBlocking, string Message)>();
        if (string.IsNullOrWhiteSpace(vmName))
        {
            issues.Add((true, "VM name is required."));
        }

        if (!int.TryParse(memoryText, out var memoryMb) || memoryMb <= 0)
        {
            issues.Add((true, "Memory must be a positive integer."));
        }

        if (!int.TryParse(cpuText, out var cpuCount) || cpuCount <= 0)
        {
            issues.Add((true, "CPU count must be a positive integer."));
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var selectedSwitch in selectedSwitches ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(selectedSwitch))
            {
                issues.Add((true, "Each switch row must have a selected host switch or be removed."));
                continue;
            }

            if (!(availableSwitches ?? Array.Empty<string>()).Contains(selectedSwitch, StringComparer.OrdinalIgnoreCase))
            {
                issues.Add((true, $"Switch '{selectedSwitch}' is not available on this host."));
                continue;
            }

            if (!seen.Add(selectedSwitch))
            {
                issues.Add((true, $"Duplicate switch '{selectedSwitch}' is not allowed."));
            }
        }

        if (selectedCatalogItem is null)
        {
            issues.Add((true, availableCatalogCount == 0
                ? "Import a base disk in Assets before deploy."
                : "Select a base disk in VM Properties."));
        }

        return issues;
    }

    private static string BuildVmEntrySecondaryText(VmTemplate vmEntry)
    {
        var diskText = string.IsNullOrWhiteSpace(vmEntry.VhdxId) && string.IsNullOrWhiteSpace(vmEntry.VhdPath)
            ? "No base disk"
            : string.IsNullOrWhiteSpace(vmEntry.VhdxId)
                ? "Catalog disk selected"
                : $"Disk: {vmEntry.VhdxId}";
        var switches = vmEntry.SwitchNames?.Where(name => !string.IsNullOrWhiteSpace(name)).ToList() ?? [];
        if (switches.Count == 0 && !string.IsNullOrWhiteSpace(vmEntry.SwitchName))
        {
            switches.Add(vmEntry.SwitchName);
        }

        var switchText = switches.Count == 0 ? "No switch" : string.Join(", ", switches);
        return $"{vmEntry.MemoryMb} MB | {vmEntry.CpuCount} vCPU | {diskText} | Switches: {switchText}";
    }

    private static string FormatIssueMessage(string message, string? guidance) =>
        string.IsNullOrWhiteSpace(guidance) ? message.Trim() : $"{message} {guidance}".Trim();

    private static IReadOnlyList<DeployTimelineStepRow> CreateReadinessTimelineSteps(
        IReadOnlyList<DeployMergedIssue> blocking,
        IReadOnlyList<DeployMergedIssue> warnings)
    {
        var hasBlocking = blocking.Count > 0;
        var steps = new List<DeployTimelineStepRow>
        {
            new("Readiness evaluation", hasBlocking ? DeployTimelineStepState.Failed : DeployTimelineStepState.Succeeded)
        };

        foreach (var issue in blocking.Concat(warnings))
        {
            steps.Add(new DeployTimelineStepRow(
                FormatIssueMessage(issue.Message, issue.Guidance),
                issue.IsBlocking ? DeployTimelineStepState.Failed : DeployTimelineStepState.Pending));
        }

        return steps;
    }

    private static string? NormalizeValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static VmTemplate CreateDefaultVmEntry(int sequence) => new()
    {
        Name = $"Quick VM {sequence}",
        MemoryMb = 2048,
        CpuCount = 2
    };

    private static VmTemplate CloneVmTemplate(VmTemplate source) => new()
    {
        VmId = source.VmId,
        Name = source.Name,
        MemoryMb = source.MemoryMb,
        CpuCount = source.CpuCount,
        VhdxId = source.VhdxId,
        VhdPath = source.VhdPath,
        VhdxSignature = source.VhdxSignature,
        SwitchName = source.SwitchName,
        SwitchNames = source.SwitchNames?.ToList()
    };
}
