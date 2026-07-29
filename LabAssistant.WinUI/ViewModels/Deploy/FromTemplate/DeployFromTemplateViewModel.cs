using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabAssistant.Business.Deployment;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.Services.Logging;
using LabAssistant.WinUI.Infrastructure;
using LabAssistant.WinUI.Models.Deploy;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Full MVVM view model for the Deploy From Template lane. It absorbs the former imperative
/// workspace and composition layers into a single runtime-independent view model that binds through
/// x:Bind. Only V2 (graph-scheduled) templates deploy here; the legacy chain-of-responsibility engine
/// has been retired, so a legacy (V1) template surfaces a blocked notice directing the user to re-create
/// it in the Builder. Workflow orchestration for the V2 review flow is preserved on
/// <see cref="DeployV2ReviewWorkspaceController"/>, for which this view model is the state owner and the
/// <see cref="IDeployFromTemplateV2ReviewHost"/>. UI-thread marshalling, the DI-backed integration host,
/// and the results-panel toggle are injected seams so the type is unit-testable without a WinUI dispatcher.
/// </summary>
internal sealed partial class DeployFromTemplateViewModel : ViewModelBase,
    IDeployFromTemplateLane,
    IDeployFromTemplateV2ReviewHost
{
    private readonly IDeployFromTemplateCompositionHost _host;
    private readonly Action<Action> _marshalToUi;
    private readonly Func<GuestCredentialPromptRequest, CancellationToken, Task<GuestCredentialPromptResponse>>? _requestGuestCredential;

    // Serializes interactive guest-credential dialogs across parallel VM deployments (WinUI permits one open
    // ContentDialog at a time). Static so it also holds across any second lane instance.
    private static readonly SemaphoreSlim _guestCredentialPromptGate = new(1, 1);
    private readonly DeployV2ReviewWorkspaceController _v2ReviewController;

    private readonly List<DeployCompatibilityIssue> _compatibilityIssues = [];
    private readonly Dictionary<string, DeployVmProgressState> _progressByVm = new(StringComparer.OrdinalIgnoreCase);

    private TemplateLibraryItem? _selectedTemplateLibraryItem;
    private string? _selectedTemplateFilePath;
    private bool _showAllVmRows;
    private bool _isLoadingTemplates;
    private bool _isSyncingSelection;
    private bool _isSyncingBaseRemoteAccess;
    private DeploymentReadinessReport? _readinessReport;
    private MultiVmDeploymentContext? _activeDeploymentContext;

    /// <summary>
    /// Creates the From Template lane view model.
    /// </summary>
    /// <param name="host">The DI-backed integration seam for templates, reference data, planning, and runtime.</param>
    /// <param name="marshalToUi">Marshals a callback onto the UI thread; injected so progress callbacks stay testable.</param>
    /// <param name="templateItemsSource">The shared template-library items source bound to the selector.</param>
    /// <param name="requestGuestCredential">
    /// Optional interactive prompt invoked when a running guest rejects its bootstrap credential during deployment.
    /// When supplied, the deploy retries in place with a corrected credential; when null, the runtime fails fast.
    /// </param>
    public DeployFromTemplateViewModel(
        IDeployFromTemplateCompositionHost host,
        Action<Action> marshalToUi,
        object? templateItemsSource,
        Func<GuestCredentialPromptRequest, CancellationToken, Task<GuestCredentialPromptResponse>>? requestGuestCredential = null)
    {
        _host = host;
        _marshalToUi = marshalToUi;
        _requestGuestCredential = requestGuestCredential;
        TemplateItems = templateItemsSource;
        V2Review = new DeployV2ReviewWorkspaceViewModel();
        _v2ReviewController = new DeployV2ReviewWorkspaceController(
            V2Review,
            new DeployV2ReviewProjectionService(),
            this);
        UpdateUi();
    }

    /// <summary>
    /// Raised whenever progress, results, readiness, or credential state changes so the host page can
    /// push the current lane state into the shell right panel and reconcile the results-panel launcher.
    /// </summary>
    public event EventHandler? ResultsPanelStateChanged;

    /// <summary>Gets the shared template-library items source bound to the selector.</summary>
    public object? TemplateItems { get; }

    /// <summary>Gets the observable V2 review sub-state bound directly by the view.</summary>
    public DeployV2ReviewWorkspaceViewModel V2Review { get; }

    // Bound collections.
    public ObservableCollection<DeployIssueRow> IssueRows { get; } = [];

    public ObservableCollection<string> SharedIssueSummaries { get; } = [];

    public ObservableCollection<DeployVmResultRow> ResultRows { get; } = [];

    // Workflow display state.
    [ObservableProperty]
    private string _lifecycleState = "Idle";

    [ObservableProperty]
    private int _progressPercent;

    [ObservableProperty]
    private string _progressSummary = "No deployment started.";

    [ObservableProperty]
    private bool _isEvaluatingReadiness;

    [ObservableProperty]
    private bool _isStarting;

    [ObservableProperty]
    private string _actionStatusText = "No action selected.";

    [ObservableProperty]
    private string _readinessSummaryText = "Select a template to evaluate readiness and run deploy.";

    [ObservableProperty]
    private string _templateSummaryText =
        "Select a template to review what will be deployed, how many VMs it includes, and whether environment fixes are needed.";

    [ObservableProperty]
    private string _templateRemediationText =
        "Use Resolve Suggestions for safe environment remaps, or open Templates Editor for structural fixes.";

    [ObservableProperty]
    private string _sharedIssuesSummaryText =
        "Shared review items appear here when multiple VMs need the same remediation.";

    [ObservableProperty]
    private string _globalIssuesBadgeText = "Issues: 0";

    [ObservableProperty]
    private string _evaluateButtonText = "Review Readiness";

    [ObservableProperty]
    private string _resultsPanelButtonText = "Open Progress / Results";

    [ObservableProperty]
    private bool _canToggleResultsPanel = true;

    [ObservableProperty]
    private string _resultsPanelSummaryText =
        "Use the side panel during or after deploy for progress, timeline, and results.";

    // V2 credential editor (two-way bound).
    [ObservableProperty]
    private string _v2CredentialSlotEditorText = "Select a slot below to create or update its local value.";

    [ObservableProperty]
    private string _v2CredentialSlotUsername = string.Empty;

    // V2 base remote access editor (two-way bound). Defaults mirror the legacy script behavior.
    [ObservableProperty]
    private bool _v2DisableFirewall = true;

    [ObservableProperty]
    private bool _v2DisableRdpNla = true;

    /// <summary>Gets or sets the selected template library item (two-way bound to the selector).</summary>
    public TemplateLibraryItem? SelectedTemplateLibraryItem
    {
        get => _selectedTemplateLibraryItem;
        set
        {
            if (ReferenceEquals(_selectedTemplateLibraryItem, value))
            {
                return;
            }

            _selectedTemplateLibraryItem = value;
            OnPropertyChanged();

            // Programmatic reconciles and library reloads must not re-enter the selection workflow;
            // the retained file path lets a reload remap the selector to the new item reference.
            if (_isSyncingSelection || _isLoadingTemplates || _host.IsTemplatesLoading)
            {
                return;
            }

            _selectedTemplateFilePath = value?.FilePath;
            _ = HandleTemplateSelectionChangedAsync();
        }
    }

    /// <summary>Gets the active loaded template document, if any.</summary>
    public TemplateEditorDocument? ActiveTemplateDocument { get; private set; }

    // IDeployFromTemplateLane.
    public bool IsLoadingTemplates => _isLoadingTemplates;

    public bool ShouldAutoOpenResultsPanel =>
        IsStarting || string.Equals(LifecycleState, "Running", StringComparison.OrdinalIgnoreCase);

    // In-tab surface routing. The three flags below partition the main content area into the
    // configuration, live-progress, and terminal-results surfaces so exactly one shows at a time.
    // They are derived from the lifecycle-state string (compared case-insensitively) and their
    // change notifications are raised from OnLifecycleStateChanged.

    /// <summary>Gets whether the live per-VM progress surface should be shown (lifecycle state Running).</summary>
    public bool ShouldShowProgressView =>
        string.Equals(LifecycleState, "Running", StringComparison.OrdinalIgnoreCase);

    /// <summary>Gets whether the terminal results surface should be shown (Completed/Failed/Cancelled).</summary>
    public bool ShouldShowResultsView =>
        string.Equals(LifecycleState, "Completed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(LifecycleState, "Failed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(LifecycleState, "Cancelled", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether the template configuration surface should be shown. This is the default surface
    /// for every non-running, non-terminal state (Idle/Ready/Blocked/Evaluating/Error and any other
    /// value), so an unexpected lifecycle string never hides all three surfaces.
    /// </summary>
    public bool ShouldShowConfigView => !ShouldShowProgressView && !ShouldShowResultsView;

    public string ResultsPanelTitle => "From Template Progress / Results";

    // Selector / reload enablement (one-way bound).
    public bool IsTemplateSelectorEnabled => !_isLoadingTemplates && !_host.IsTemplatesLoading && !IsStarting;

    public bool IsReloadEnabled => !_isLoadingTemplates && !_host.IsTemplatesLoading && !IsStarting;

    /// <summary>
    /// Activates the lane on tab enter. Deactivation is a no-op because an in-flight deploy keeps
    /// running while another subview is shown; navigate-away cleanup runs through <see cref="CleanupAsync"/>.
    /// </summary>
    public void ApplyShellState(bool isFromTemplateActive)
    {
        if (isFromTemplateActive)
        {
            UpdateUi();
        }
    }

    public void ResetPanelState() => RaiseResultsPanelStateChanged();

    public Task EnsureTemplatesLoadedAsync(bool forceRefresh) => LoadTemplatesAsync(forceRefresh);

    public void RefreshUi() => UpdateUi();

    public void ReconcileSelection(IReadOnlyList<TemplateLibraryItem> items)
    {
        if (string.IsNullOrWhiteSpace(_selectedTemplateFilePath))
        {
            SetSelectedTemplateSilently(null);
            UpdateUi();
            return;
        }

        var match = items.FirstOrDefault(item =>
            string.Equals(item.FilePath, _selectedTemplateFilePath, StringComparison.OrdinalIgnoreCase));
        SetSelectedTemplateSilently(match);

        if (match is null)
        {
            _selectedTemplateFilePath = null;
            ActiveTemplateDocument = null;
            _readinessReport = null;
            _compatibilityIssues.Clear();
            V2Review.Hide();
        }

        UpdateUi();
    }

    public void ApplyResultsPanelState(bool isActive, bool showPanel, bool panelUnavailable)
    {
        var isRunning = IsStarting || string.Equals(LifecycleState, "Running", StringComparison.OrdinalIgnoreCase);
        ResultsPanelButtonText = showPanel && isActive ? "Hide Progress / Results" : "Open Progress / Results";
        CanToggleResultsPanel = isActive && !panelUnavailable;
        ResultsPanelSummaryText = panelUnavailable
            ? "Expand the window to review the progress and results panel."
            : isRunning
                ? "The panel auto-opens while deployment runs and stays available for result review."
                : ResultRows.Count > 0
                    ? $"{ResultRows.Count} VM result row(s) are available for review."
                    : "Use the side panel during or after deploy for progress, timeline, and results.";
    }

    public override Task CleanupAsync()
    {
        // Cleanup/cancellation policy: cancelling the in-flight operation lets the coordinator and V2
        // runtime clean up resources they created so navigate-away never leaves orphaned VMs or disks.
        _activeDeploymentContext?.RequestUserCancellation();
        return base.CleanupAsync();
    }

    // Command handlers.
    [RelayCommand(CanExecute = nameof(CanReloadTemplates))]
    private Task ReloadTemplates() => LoadTemplatesAsync(forceRefresh: true);

    private bool CanReloadTemplates => IsReloadEnabled;

    [RelayCommand(CanExecute = nameof(CanEvaluateReadiness))]
    private Task EvaluateReadiness() => EvaluateReadinessAsync(DeploymentPreflightMode.Quick);

    private bool CanEvaluateReadiness =>
        ActiveTemplateDocument is not null && !IsEvaluatingReadiness && !IsStarting && !V2Review.IsPlanning;

    [RelayCommand(CanExecute = nameof(CanResolveSuggestions))]
    private async Task ResolveSuggestions()
    {
        if (ActiveTemplateDocument is null)
        {
            SetActionStatus("Select a template first.");
            return;
        }

        var applied = await _host.ApplyResolveSuggestionsAsync(ActiveTemplateDocument.Template);
        SetActionStatus(
            applied == 0
                ? "No auto-resolve suggestions available for the current template state."
                : $"Applied {applied} auto-resolve suggestion(s). Re-evaluating readiness...");
        await EvaluateReadinessAsync(DeploymentPreflightMode.Quick);
    }

    private bool CanResolveSuggestions => CanEvaluateReadiness;

    [RelayCommand(CanExecute = nameof(CanOpenTemplateEditor))]
    private async Task OpenTemplateEditor()
    {
        var selectedTemplateLibraryItem = SelectedTemplateLibraryItem;
        if (selectedTemplateLibraryItem is null)
        {
            SetActionStatus("Select a template first.");
            return;
        }

        try
        {
            var document = await _host.LoadTemplateForEditorAsync(selectedTemplateLibraryItem.FilePath);
            await _host.ShowTemplateEditorAsync(document, "Template loaded.");
            SetActionStatus($"Opened '{selectedTemplateLibraryItem.Name}' in Templates editor.");
        }
        catch (Exception ex)
        {
            SetActionStatus($"Failed to open template in editor. {ex.Message}");
        }
    }

    private bool CanOpenTemplateEditor => SelectedTemplateLibraryItem is not null && !IsStarting;

    [RelayCommand(CanExecute = nameof(CanStartDeploy))]
    private Task StartDeploy() => IsActiveTemplateV2() ? StartV2DeployAsync() : ShowLegacyTemplateBlockedAsync();

    private bool CanStartDeploy
    {
        get
        {
            if (ActiveTemplateDocument is null || _isLoadingTemplates || _host.IsTemplatesLoading)
            {
                return false;
            }

            if (IsEvaluatingReadiness || IsStarting || V2Review.IsPlanning)
            {
                return false;
            }

            if (IsActiveTemplateV2())
            {
                return V2Review.CanStartDeploy;
            }

            // Legacy (non-V2) templates can no longer be deployed; they must be re-created in the Builder.
            return false;
        }
    }

    /// <summary>
    /// Message shown when a legacy (pre-V2) template is selected. Legacy templates are no longer deployable;
    /// the user must re-create them in the Builder, which authors V2 templates.
    /// </summary>
    private const string LegacyTemplateNotice =
        "This template uses the legacy deployment format and can no longer be deployed. Open it in the Builder to re-create it.";

    private Task ShowLegacyTemplateBlockedAsync()
    {
        ShowLegacyTemplateBlocked();
        return Task.CompletedTask;
    }

    private void ShowLegacyTemplateBlocked()
    {
        _readinessReport = null;
        _compatibilityIssues.Clear();
        V2Review.Hide();
        SetShowAllVmRows(false);
        ClearResultRows();
        SetWorkflowState(
            isEvaluatingReadiness: false,
            isStarting: false,
            lifecycleState: "Blocked",
            progressPercent: 0,
            progressSummary: LegacyTemplateNotice);
        SetActionStatus(LegacyTemplateNotice);
        UpdateUi();
    }

    [RelayCommand]
    private void ToggleResultsPanel() => _host.OnOpenResultsPanelRequested();

    /// <summary>
    /// Returns the lane to the configuration surface after a run finishes, keeping the current
    /// template selected. Progress state is discarded and the shared <see cref="ResultRows"/>
    /// collection is repopulated from readiness review (the config-mode projection) by clearing the
    /// live-rows flag before <see cref="UpdateUi"/> runs.
    /// </summary>
    [RelayCommand]
    private void BackToConfiguration() => ResetToConfiguration();

    /// <summary>
    /// Resets the lane so the user can start another deployment with the same template. Behaves like
    /// <see cref="BackToConfiguration"/>: it returns to the configuration surface with a fresh state.
    /// </summary>
    [RelayCommand]
    private void DeployAgain() => ResetToConfiguration();

    private void ResetToConfiguration()
    {
        // Drop any completed run's live progress so readiness review owns ResultRows again, then
        // clear the live-rows flag guarding that shared collection before UpdateUi repopulates it.
        _activeDeploymentContext = null;
        _progressByVm.Clear();
        SetShowAllVmRows(false);

        var restoredState = ActiveTemplateDocument is null ? "Idle" : "Ready";
        SetWorkflowState(false, false, restoredState, 0, "No deployment started.");
        SetActionStatus("No action selected.");
        UpdateUi();
    }

    /// <summary>
    /// Applies the selected V2 credential slot value using the current username and the supplied
    /// password (kept off the view model because a PasswordBox value cannot be data-bound).
    /// </summary>
    [RelayCommand]
    private async Task SaveV2CredentialSlot(string? password)
    {
        await _v2ReviewController.SaveCredentialSlotAsync(V2CredentialSlotUsername, password ?? string.Empty);
        SetActionStatus("Saved local credential slot and refreshed the V2 plan.");
        UpdateUi();
    }

    /// <summary>Selects a V2 credential slot; called from the view's list selection interaction.</summary>
    public void SelectV2CredentialSlot(string? slotKey) => _v2ReviewController.SelectCredentialSlot(slotKey);

    // State setters used by the preserved controller.
    public void SetActionStatus(string actionStatusText) => ActionStatusText = actionStatusText;

    public void SetReadinessSummary(string readinessSummaryText) => ReadinessSummaryText = readinessSummaryText;

    public void SetWorkflowState(
        bool isEvaluatingReadiness,
        bool isStarting,
        string lifecycleState,
        int progressPercent,
        string progressSummary)
    {
        IsEvaluatingReadiness = isEvaluatingReadiness;
        IsStarting = isStarting;
        LifecycleState = lifecycleState;
        ProgressPercent = progressPercent;
        ProgressSummary = progressSummary;
        RefreshCommandStates();
    }

    public void SetShowAllVmRows(bool showAllVmRows) => _showAllVmRows = showAllVmRows;

    public void ClearResultRows() => ResultRows.Clear();

    public void InitializeProgressRows(V2PlanBuildResult plan)
    {
        _progressByVm.Clear();

        foreach (var state in DeployV2ProgressPlan.BuildProgressStates(plan))
        {
            _progressByVm[state.VmName] = state;
        }

        RefreshResultRows(compatibilityIssues: [], readinessReport: null);
    }

    public void UpdateProgressMessage(string vmName, string? message)
    {
        if (!_progressByVm.TryGetValue(vmName, out var state))
        {
            return;
        }

        state.UpdateSummaryMessage(message);
        RefreshResultRows(compatibilityIssues: [], readinessReport: null);
    }

    public void ApplyProgressUpdate(string vmName, DeployStepStateUpdate update)
    {
        if (!_progressByVm.TryGetValue(vmName, out var state))
        {
            return;
        }

        state.ApplyStepStateUpdate(update);
        RefreshResultRows(compatibilityIssues: [], readinessReport: null);
    }

    // IDeployFromTemplateV2ReviewHost.
    IStructuredLogger IDeployFromTemplateV2ReviewHost.StructuredLogger => _host.StructuredLogger;

    Task IDeployFromTemplateV2ReviewHost.EnsureReferenceDataAsync(bool forceRefresh) => _host.EnsureReferenceDataAsync(forceRefresh);

    IReadOnlyList<LocalCredentialSlotDefinition> IDeployFromTemplateV2ReviewHost.LoadLocalCredentialSlotDefinitions() =>
        _host.LoadLocalCredentialSlotDefinitions();

    bool IDeployFromTemplateV2ReviewHost.TryGetLocalCredentialSlotValue(string slotKey, out V2RuntimeCredential credential) =>
        _host.TryGetLocalCredentialSlotValue(slotKey, out credential);

    void IDeployFromTemplateV2ReviewHost.UpsertLocalCredentialSlot(string slotKey, string username, string password) =>
        _host.UpsertLocalCredentialSlot(slotKey, username, password);

    Task<V2PlanBuildResult> IDeployFromTemplateV2ReviewHost.BuildV2PlanAsync(
        LabTemplate template,
        IReadOnlyCollection<string> resolvedCredentialSlotKeys,
        IReadOnlyDictionary<string, string> externalSwitchAdapterMappings) =>
        _host.BuildV2PlanAsync(template, resolvedCredentialSlotKeys, externalSwitchAdapterMappings);

    Task<V2RuntimeExecutionResult> IDeployFromTemplateV2ReviewHost.ExecuteV2DeployAsync(
        LabTemplate template,
        V2PlanBuildResult plan,
        IReadOnlyDictionary<string, V2RuntimeCredential> credentialSlotValues,
        V2BaseRemoteAccessOptions baseRemoteAccessOptions,
        MultiVmDeploymentContext deploymentContext) =>
        _host.ExecuteV2DeployAsync(template, plan, credentialSlotValues, baseRemoteAccessOptions, deploymentContext);

    void IDeployFromTemplateV2ReviewHost.ApplyWorkspaceState() => UpdateUi();

    /// <summary>
    /// Projects the credential-slot right panel from the current V2 review state. The host page maps
    /// this onto the credential right-panel view model, keeping the right panel a passive surface.
    /// </summary>
    public DeployFromTemplateCredentialPanelState ProjectCredentialPanel()
    {
        if (ActiveTemplateDocument is null)
        {
            return DeployFromTemplateCredentialPanelState.Reset("Credential slots appear here after V2 plan review.");
        }

        if (!IsActiveTemplateV2())
        {
            return DeployFromTemplateCredentialPanelState.Reset("Credential slots are only used for V2 templates.");
        }

        if (V2Review.IsPlanning)
        {
            return DeployFromTemplateCredentialPanelState.Loading(V2Review.StatusText);
        }

        var credentialSlots = V2Review.CredentialSlotRows
            .Select(row => new CredentialSlotItem(
                row.SlotKey,
                row.PurposeSummary,
                row.AffectedVmSummary,
                row.ExistingUsername,
                row.HasStoredValue))
            .ToList();
        var allSlotsResolved = V2Review.CurrentPlan is not null && credentialSlots.Count == 0;
        var statusMessage = allSlotsResolved
            ? "No unresolved credential slots remain."
            : V2Review.StatusText;

        return DeployFromTemplateCredentialPanelState.WithSlots(credentialSlots, allSlotsResolved, statusMessage);
    }

    partial void OnLifecycleStateChanged(string value)
    {
        OnPropertyChanged(nameof(ShouldAutoOpenResultsPanel));
        OnPropertyChanged(nameof(ShouldShowConfigView));
        OnPropertyChanged(nameof(ShouldShowProgressView));
        OnPropertyChanged(nameof(ShouldShowResultsView));
    }

    partial void OnIsStartingChanged(bool value) => OnPropertyChanged(nameof(ShouldAutoOpenResultsPanel));

    partial void OnV2DisableFirewallChanged(bool value) => ApplyBaseRemoteAccessEdit();

    partial void OnV2DisableRdpNlaChanged(bool value) => ApplyBaseRemoteAccessEdit();

    private void ApplyBaseRemoteAccessEdit()
    {
        if (_isSyncingBaseRemoteAccess)
        {
            return;
        }

        V2Review.UpdateBaseRemoteAccessOptions(V2DisableFirewall, V2DisableRdpNla);
        UpdateUi();
    }

    private Task EvaluateReadinessAsync(DeploymentPreflightMode mode) =>
        IsActiveTemplateV2() ? EvaluateV2PlanAsync() : ShowLegacyTemplateBlockedAsync();

    private async Task HandleTemplateSelectionChangedAsync()
    {
        if (_isLoadingTemplates || _host.IsTemplatesLoading)
        {
            return;
        }

        if (SelectedTemplateLibraryItem is null)
        {
            ClearSelection("No template selected.");
            _readinessReport = null;
            _compatibilityIssues.Clear();
            V2Review.Hide();
            SetWorkflowState(false, false, "Idle", 0, "No template selected.");
            UpdateUi();
            return;
        }

        try
        {
            var selectedTemplate = SelectedTemplateLibraryItem;
            var document = await _host.LoadTemplateForEditorAsync(selectedTemplate.FilePath);
            SetLoadedTemplateDocument(document, $"Loaded '{selectedTemplate.Name}' for deploy review.");
            SetWorkflowState(false, false, "Ready", 0, $"Template '{selectedTemplate.Name}' loaded.");
            _readinessReport = null;
            _compatibilityIssues.Clear();
            UpdateUi();
            await EvaluateReadinessAsync(DeploymentPreflightMode.Quick);
        }
        catch (Exception ex)
        {
            SetSelectionLoadFailed($"Failed to load selected template. {ex.Message}");
            _readinessReport = null;
            _compatibilityIssues.Clear();
            V2Review.Hide();
            SetWorkflowState(false, false, "Error", 0, "Template load failed.");
            UpdateUi();
        }
    }

    private async Task LoadTemplatesAsync(bool forceRefresh)
    {
        if (!forceRefresh && _host.TemplateLibraryItems.Count > 0)
        {
            _host.RefreshSharedUiState();
            UpdateUi();
            return;
        }

        _isLoadingTemplates = true;
        SetWorkflowState(false, false, "Loading", 0, "Loading templates...");
        _host.RefreshSharedUiState();
        UpdateUi();
        SetActionStatus("Loading templates for deploy...");

        var shouldAutoSelectFirst = false;
        try
        {
            await _host.EnsureTemplatesLibraryAsync(forceRefresh);

            var items = _host.TemplateLibraryItems;
            if (items.Count == 0)
            {
                ClearSelection("No templates available for deploy.");
                _readinessReport = null;
                _compatibilityIssues.Clear();
                V2Review.Hide();
                SetWorkflowState(false, false, "Idle", 0, "No templates available.");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_selectedTemplateFilePath))
                {
                    shouldAutoSelectFirst = true;
                }
                else
                {
                    var match = items.FirstOrDefault(item =>
                        string.Equals(item.FilePath, _selectedTemplateFilePath, StringComparison.OrdinalIgnoreCase));
                    SetSelectedTemplateSilently(match);
                    if (match is null)
                    {
                        _selectedTemplateFilePath = null;
                        ActiveTemplateDocument = null;
                        _readinessReport = null;
                        _compatibilityIssues.Clear();
                        V2Review.Hide();
                    }
                }

                SetWorkflowState(false, false, "Idle", 0, "Template list loaded.");
                SetActionStatus($"Loaded {items.Count} template(s) for deploy.");
            }
        }
        catch (Exception ex)
        {
            SetWorkflowState(false, false, "Error", 0, "Template load failed.");
            SetActionStatus($"Failed to load deploy templates. {ex.Message}");
        }
        finally
        {
            _isLoadingTemplates = false;
            _host.RefreshSharedUiState();
            UpdateUi();
        }

        if (shouldAutoSelectFirst)
        {
            var items = _host.TemplateLibraryItems;
            if (items.Count > 0)
            {
                _selectedTemplateFilePath = items[0].FilePath;
                SetSelectedTemplateSilently(items[0]);
                await HandleTemplateSelectionChangedAsync();
            }
        }
    }

    private async Task EvaluateV2PlanAsync()
    {
        if (ActiveTemplateDocument is null)
        {
            SetActionStatus("Select a template first.");
            UpdateUi();
            return;
        }

        SetWorkflowState(false, false, "Evaluating", 20, "Reviewing V2 deployment plan...");
        SetActionStatus("Building V2 plan and review state...");
        await _v2ReviewController.RefreshPlanAsync(ActiveTemplateDocument.Template);
        // Finding 90-A: RefreshPlanAsync has returned, so the "Building..." label is now a lie. Re-set the
        // action status to the review panel's honest outcome (ready / blocked / planning-failed) instead of
        // leaving the frozen in-progress text that made a correctly-blocked plan look like a hang.
        SetActionStatus(V2Review.StatusText);
        SetWorkflowState(
            false,
            false,
            V2Review.CanStartDeploy ? "Ready" : "Blocked",
            V2Review.CanStartDeploy ? 35 : 25,
            V2Review.StatusText);
        UpdateUi();
    }

    private async Task StartV2DeployAsync()
    {
        var activeTemplateDocument = ActiveTemplateDocument;
        if (activeTemplateDocument is null)
        {
            SetActionStatus("Select a template first.");
            return;
        }

        if (V2Review.CurrentPlan is null || !V2Review.CanStartDeploy)
        {
            await EvaluateV2PlanAsync();
            if (V2Review.CurrentPlan is null || !V2Review.CanStartDeploy)
            {
                SetActionStatus("V2 deploy is blocked until review items are resolved.");
                UpdateUi();
                return;
            }
        }

        var plan = V2Review.CurrentPlan;
        var deploymentContext = new MultiVmDeploymentContext();
        SetShowAllVmRows(true);
        ClearResultRows();
        InitializeProgressRows(plan);
        WireProgressCallbacks(
            deploymentContext,
            (vmName, message) =>
            {
                UpdateProgressMessage(vmName, message);
                UpdateUi();
            },
            (vmName, update) =>
            {
                ApplyProgressUpdate(vmName, update);
                UpdateUi();
            });
        SetWorkflowState(false, true, "Running", 60, $"Running V2 deployment for {plan.Context.Vms.Count} VM(s)...");
        SetActionStatus("Starting V2 deployment...");
        UpdateUi();

        _activeDeploymentContext = deploymentContext;
        try
        {
            var result = await _host.ExecuteV2DeployAsync(
                activeTemplateDocument.Template,
                plan,
                V2Review.ResolvedCredentialSlotValues,
                V2Review.CreateBaseRemoteAccessOptions(),
                deploymentContext);

            var issueRows = result.BlockingMessages
                .Select(message => new DeployIssueRow("Global", "Block", message))
                .ToList();
            if (issueRows.Count > 0)
            {
                ReplaceIssueRows(issueRows);
            }

            SetWorkflowState(
                false,
                true,
                result.Success ? "Completed" : deploymentContext.IsCancellationRequested ? "Cancelled" : "Failed",
                100,
                result.Success
                    ? "V2 deployment completed."
                    : result.BlockingMessages.Count > 0
                        ? string.Join(" ", result.BlockingMessages)
                        : "V2 deployment finished with failures.");
            SetActionStatus(
                result.Success
                    ? "V2 deployment finished successfully."
                    : "V2 deployment finished with blocking issues. Review the results panel and blockers.");
        }
        catch (Exception ex)
        {
            SetWorkflowState(false, true, "Failed", 100, "V2 deployment failed.");
            SetActionStatus($"V2 deploy failed. {ex.Message}");
        }
        finally
        {
            _activeDeploymentContext = null;
            SetWorkflowState(false, false, LifecycleState, ProgressPercent, ProgressSummary);
            UpdateUi();
        }
    }

    private void UpdateUi()
    {
        var activeTemplateDocument = ActiveTemplateDocument;
        var isV2Template = IsActiveTemplateV2();
        var hasBlockingFailures = isV2Template
            ? V2Review.HasBlockingItems
            : _compatibilityIssues.Any(issue => issue.IsBlocking) || (_readinessReport?.HasBlockingFailures ?? false);

        RefreshInteractionState(isV2Template);
        RefreshReviewState(hasBlockingFailures);
        SyncV2EditorState();

        if (activeTemplateDocument is null)
        {
            V2Review.Hide();
            SetReadinessSummary("Select a template to evaluate readiness and run deploy.");
            ClearGroupedIssueState();
            RefreshResultRows(_compatibilityIssues, _readinessReport);
            RefreshCommandStates();
            RaiseResultsPanelStateChanged();
            return;
        }

        if (isV2Template)
        {
            SetReadinessSummary(V2Review.StatusText);
            ReplaceIssueRows(V2Review.BlockerRows
                .Select(row => new DeployIssueRow(row.Scope, row.Severity, row.Message))
                .ToList());
            RefreshResultRows([], null);
            RefreshCommandStates();
            RaiseResultsPanelStateChanged();
            return;
        }

        // Legacy (non-V2) templates use the retired deployment format and can no longer be deployed.
        // Surface the re-create-in-Builder notice instead of a readiness summary.
        SetReadinessSummary(LegacyTemplateNotice);
        ReplaceIssueRows([new DeployIssueRow("Global", "Block", LegacyTemplateNotice)]);
        RefreshResultRows([], null);
        RefreshCommandStates();
        RaiseResultsPanelStateChanged();
    }

    private void RefreshInteractionState(bool isV2Template)
    {
        EvaluateButtonText = isV2Template ? "Refresh V2 Plan" : "Review Readiness";
    }

    private void SyncV2EditorState()
    {
        V2CredentialSlotEditorText = V2Review.SelectedCredentialSlotPurpose;
        V2CredentialSlotUsername = V2Review.SelectedCredentialSlotUsername;

        _isSyncingBaseRemoteAccess = true;
        try
        {
            V2DisableFirewall = V2Review.BaseRemoteAccess.DisableFirewall;
            V2DisableRdpNla = V2Review.BaseRemoteAccess.DisableRdpNla;
        }
        finally
        {
            _isSyncingBaseRemoteAccess = false;
        }
    }

    private void UpdateIssueRows()
    {
        var issueRows = new List<DeployIssueRow>();

        foreach (var issue in _compatibilityIssues)
        {
            var scope = string.IsNullOrWhiteSpace(issue.VmName) ? "Global" : issue.VmName;
            issueRows.Add(new DeployIssueRow(
                Scope: scope,
                Severity: issue.IsBlocking ? "Block" : "Warn",
                Message: $"{issue.Message} {issue.Guidance}".Trim()));
        }

        if (_readinessReport is not null)
        {
            foreach (var result in _readinessReport.Results.Where(result => result.Status is DeploymentReadinessStatus.Fail or DeploymentReadinessStatus.Warn))
            {
                var scope = result.AffectedVmNames.Count == 0 ? "Global" : string.Join(", ", result.AffectedVmNames);
                issueRows.Add(new DeployIssueRow(
                    Scope: scope,
                    Severity: result.Status == DeploymentReadinessStatus.Fail ? "Block" : "Warn",
                    Message: $"{result.Message} {result.ActionableGuidance}".Trim()));
            }
        }

        ReplaceIssueRows(issueRows);
    }

    private void ClearGroupedIssueState()
    {
        IssueRows.Clear();
        SharedIssueSummaries.Clear();
        GlobalIssuesBadgeText = "Issues: 0";
        SharedIssuesSummaryText = "Shared review items appear here when multiple VMs need the same remediation.";
    }

    private void ReplaceIssueRows(IReadOnlyList<DeployIssueRow> issueRows)
    {
        IssueRows.Clear();
        foreach (var issueRow in issueRows)
        {
            IssueRows.Add(issueRow);
        }

        GlobalIssuesBadgeText = $"Issues: {IssueRows.Count}";
        RefreshSharedIssueSummaries();
    }

    private void RefreshResultRows(
        IReadOnlyList<DeployCompatibilityIssue> compatibilityIssues,
        DeploymentReadinessReport? readinessReport)
    {
        ReconcileResultRows(BuildResultRows(compatibilityIssues, readinessReport));
    }

    private List<DeployVmResultRow> BuildResultRows(
        IReadOnlyList<DeployCompatibilityIssue> compatibilityIssues,
        DeploymentReadinessReport? readinessReport)
    {
        if (_showAllVmRows && _progressByVm.Count > 0)
        {
            return _progressByVm.Values
                .OrderBy(value => value.VmName, StringComparer.OrdinalIgnoreCase)
                .Select(state => state.ToRow())
                .ToList();
        }

        var rows = new List<DeployVmResultRow>();
        if (ActiveTemplateDocument is null)
        {
            return rows;
        }

        var vmNames = ActiveTemplateDocument.Template.VmTemplates
            .Select(vm => string.IsNullOrWhiteSpace(vm.Name) ? "Unnamed-VM" : vm.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var compatibilityByVm = compatibilityIssues
            .Where(issue => !string.IsNullOrWhiteSpace(issue.VmName))
            .GroupBy(issue => issue.VmName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var readinessByVm = (readinessReport?.Results ?? [])
            .SelectMany(result => result.AffectedVmNames.Select(vmName => (vmName, result)))
            .Where(tuple => !string.IsNullOrWhiteSpace(tuple.vmName))
            .GroupBy(tuple => tuple.vmName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(item => item.result).ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var vmName in vmNames)
        {
            compatibilityByVm.TryGetValue(vmName, out var vmCompatibilityIssues);
            readinessByVm.TryGetValue(vmName, out var vmReadinessResults);

            vmCompatibilityIssues ??= [];
            vmReadinessResults ??= [];

            var hasBlocking = vmCompatibilityIssues.Any(issue => issue.IsBlocking) ||
                              vmReadinessResults.Any(result => result.Status == DeploymentReadinessStatus.Fail);
            var hasWarnings = vmCompatibilityIssues.Any(issue => !issue.IsBlocking) ||
                              vmReadinessResults.Any(result => result.Status == DeploymentReadinessStatus.Warn);
            if (!hasBlocking && !hasWarnings)
            {
                continue;
            }

            var status = hasBlocking ? "Blocked" : hasWarnings ? "Warning" : "Ready";
            var blockingCount = vmCompatibilityIssues.Count(issue => issue.IsBlocking) +
                                vmReadinessResults.Count(result => result.Status == DeploymentReadinessStatus.Fail);
            var warningCount = vmCompatibilityIssues.Count(issue => !issue.IsBlocking) +
                               vmReadinessResults.Count(result => result.Status == DeploymentReadinessStatus.Warn);
            var summary = $"Blocking: {blockingCount} | Warnings: {warningCount}";

            rows.Add(new DeployVmResultRow(
                VmName: vmName,
                Status: status,
                Summary: summary,
                ProgressPercent: hasBlocking ? 100 : 80,
                TimelineSteps: CreateReadinessTimelineSteps(vmCompatibilityIssues, vmReadinessResults, hasBlocking)));
        }

        return rows;
    }

    /// <summary>
    /// Applies <paramref name="desiredRows"/> to <see cref="ResultRows"/> in place. Live per-VM progress streams
    /// many updates per second; a wholesale <c>Clear()</c> then re-add raises a collection Reset that tears down and
    /// recreates every ListView item container, which flickers, resets the user's scroll position, and restarts each
    /// row's progress-bar animation. This replaces only the rows whose rendered content actually changed and grows or
    /// shrinks the collection only when the set of VMs changes, so unchanged rows keep their containers.
    /// </summary>
    private void ReconcileResultRows(IReadOnlyList<DeployVmResultRow> desiredRows)
    {
        for (var index = 0; index < desiredRows.Count; index++)
        {
            var desired = desiredRows[index];
            if (index >= ResultRows.Count)
            {
                ResultRows.Add(desired);
            }
            else if (!RenderedRowEquals(ResultRows[index], desired))
            {
                ResultRows[index] = desired;
            }
        }

        for (var index = ResultRows.Count - 1; index >= desiredRows.Count; index--)
        {
            ResultRows.RemoveAt(index);
        }
    }

    /// <summary>
    /// Compares two rows by only the fields the progress row template renders, so an unchanged VM keeps its
    /// existing item container instead of being needlessly replaced on every progress tick.
    /// </summary>
    private static bool RenderedRowEquals(DeployVmResultRow existing, DeployVmResultRow desired) =>
        string.Equals(existing.VmName, desired.VmName, StringComparison.Ordinal)
        && string.Equals(existing.Status, desired.Status, StringComparison.Ordinal)
        && existing.ProgressPercent == desired.ProgressPercent
        && string.Equals(existing.DisplaySummary, desired.DisplaySummary, StringComparison.Ordinal);

    private void RefreshReviewState(bool hasBlockingFailures)
    {
        if (ActiveTemplateDocument is null)
        {
            TemplateSummaryText = "Select a template to review what will be deployed, how many VMs it includes, and whether environment fixes are needed.";
            TemplateRemediationText = "Use Resolve Suggestions for safe environment remaps, or open Templates Editor for structural fixes.";
            ReadinessSummaryText = "Select a template to evaluate readiness and run deploy.";
            ClearGroupedIssueState();
            return;
        }

        TemplateSummaryText =
            $"Template '{ActiveTemplateDocument.Template.Name}' will deploy {ActiveTemplateDocument.Template.VmTemplates.Count} VM(s). Review shared environment blockers here before deciding whether to remediate or open the template editor.";
        TemplateRemediationText = hasBlockingFailures
            ? "Blocking issues are grouped below when possible. Use Resolve Suggestions for safe shared remaps, or Open in Templates Editor for structural fixes."
            : "This surface is for template review and remediation. Use Open in Templates Editor only when the template itself needs structural changes.";
    }

    private void RefreshSharedIssueSummaries()
    {
        SharedIssueSummaries.Clear();

        var groupedIssues = IssueRows
            .Where(issue => !string.Equals(issue.Scope, "Global", StringComparison.OrdinalIgnoreCase))
            .GroupBy(issue => $"{issue.Severity}|{issue.Message}", StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(issue => issue.Scope).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .OrderByDescending(group => group.Key.StartsWith("Block|", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(group => group.Count())
            .ToList();

        foreach (var group in groupedIssues)
        {
            var scopes = group
                .Select(issue => issue.Scope)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(scope => scope, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var message = group.First().Message;
            var severity = group.First().Severity;
            SharedIssueSummaries.Add($"{severity}: {message} Shared across {scopes.Count} VM(s): {string.Join(", ", scopes)}");
        }

        SharedIssuesSummaryText = SharedIssueSummaries.Count > 0
            ? "Shared environment and compatibility issues detected across multiple VMs. Fix them here when safe, or open Templates Editor for structural changes."
            : "No shared review items are currently grouped. Review the readiness summary, then use the main actions below.";
    }

    private void ClearSelection(string actionStatusText)
    {
        SetSelectedTemplateSilently(null);
        _selectedTemplateFilePath = null;
        ActiveTemplateDocument = null;
        ActionStatusText = actionStatusText;
        RefreshReviewState(hasBlockingFailures: false);
    }

    private void SetLoadedTemplateDocument(TemplateEditorDocument document, string actionStatusText)
    {
        ActiveTemplateDocument = document;
        ActionStatusText = actionStatusText;
        RefreshReviewState(hasBlockingFailures: false);
    }

    private void SetSelectionLoadFailed(string actionStatusText)
    {
        ActiveTemplateDocument = null;
        ActionStatusText = actionStatusText;
        RefreshReviewState(hasBlockingFailures: false);
    }

    private void SetSelectedTemplateSilently(TemplateLibraryItem? item)
    {
        _isSyncingSelection = true;
        try
        {
            SelectedTemplateLibraryItem = item;
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    // Wires per-VM progress callbacks (log + step-state) and the interactive guest-credential re-prompt onto
    // the deployment context. Internal rather than private so runtime-independent tests can exercise the
    // credential-prompt wiring seam directly (the WinUI source is compiled into the test assembly).
    internal void WireProgressCallbacks(
        MultiVmDeploymentContext context,
        Action<string, string?> onLogMessage,
        Action<string, DeployStepStateUpdate> onStepStateUpdated)
    {
        void Wire(VmDeploymentContext vmContext)
        {
            var vmName = string.IsNullOrWhiteSpace(vmContext.VmName) ? "Unnamed-VM" : vmContext.VmName.Trim();
            vmContext.LogCallback = message => _marshalToUi(() => onLogMessage(vmName, message));
            vmContext.StepStateEmitter = update => _marshalToUi(() => onStepStateUpdated(vmName, update));

            // Only wire the interactive credential re-prompt when a prompt is available. Leaving it null when there is
            // no prompt (tests, headless) keeps the runtime on its fail-fast path for a rejected credential.
            if (_requestGuestCredential is not null)
            {
                vmContext.RequestGuestCredential = RequestGuestCredentialOnUiThread;
            }
        }

        // Wire contexts that already exist (the classic path builds its per-VM contexts before wiring).
        foreach (var vmContext in context.VmContexts)
        {
            Wire(vmContext);
        }

        // Wire contexts the runtime builds during execution (the V2 runtime clears and rebuilds VmContexts
        // internally, so without this hook its step-state and log callbacks would be attached to nothing).
        context.VmContextRegistered = Wire;
    }

    /// <summary>
    /// Bridges the runtime's guest-credential prompt request onto the UI thread. The runtime awaits the returned task
    /// from a worker thread, so the dialog is shown via the UI marshaller and the result flows back through a
    /// completion source. A corrected credential is persisted to the credential slot store when the user chose to
    /// remember it, so future deployments reuse it.
    /// </summary>
    private Task<GuestCredentialPromptResponse> RequestGuestCredentialOnUiThread(
        GuestCredentialPromptRequest request,
        CancellationToken cancellationToken)
    {
        var prompt = _requestGuestCredential;
        if (prompt is null)
        {
            return Task.FromResult(GuestCredentialPromptResponse.Cancel());
        }

        var completion = new TaskCompletionSource<GuestCredentialPromptResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        // Link the result to the deploy's cancellation so the awaiting runtime worker (which holds the coordinator's
        // per-slot gate) is always released, even if the marshalled continuation below never runs because the UI
        // dispatcher is tearing down. Disposed once the prompt resolves so the registration does not linger.
        var cancellationRegistration = cancellationToken.Register(
            () => completion.TrySetResult(GuestCredentialPromptResponse.Cancel()));
        completion.Task.ContinueWith(
            _ => cancellationRegistration.Dispose(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        _marshalToUi(async () =>
        {
            // WinUI allows only one ContentDialog open at a time. VMs deploy in parallel and can each reject a
            // credential for a DIFFERENT slot, so the coordinator's per-slot single-flight does not prevent two
            // prompts overlapping. Serialize the actual dialog display here so a second prompt waits for the first
            // to close instead of throwing. WaitAsync yields the UI thread, so this cannot deadlock.
            try
            {
                await _guestCredentialPromptGate.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                completion.TrySetResult(GuestCredentialPromptResponse.Cancel());
                return;
            }

            try
            {
                var response = await prompt(request, cancellationToken);
                if (!response.Cancelled &&
                    response.RememberForSlot &&
                    !string.IsNullOrWhiteSpace(request.CredentialSlotKey) &&
                    !string.IsNullOrWhiteSpace(response.Username))
                {
                    _host.UpsertLocalCredentialSlot(request.CredentialSlotKey, response.Username, response.Password);
                }

                completion.TrySetResult(response);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
            finally
            {
                _guestCredentialPromptGate.Release();
            }
        });

        return completion.Task;
    }

    private bool IsActiveTemplateV2()
    {
        var template = ActiveTemplateDocument?.Template;
        if (template is null)
        {
            return false;
        }

        var engine = template.ExecutionEngine != TemplateExecutionEngine.Unknown
            ? template.ExecutionEngine
            : TemplateSchemaVersionCatalog.Classify(template.SchemaVersion);
        return engine == TemplateExecutionEngine.V2UnifiedPlanning;
    }

    private void RefreshCommandStates()
    {
        ReloadTemplatesCommand.NotifyCanExecuteChanged();
        EvaluateReadinessCommand.NotifyCanExecuteChanged();
        ResolveSuggestionsCommand.NotifyCanExecuteChanged();
        OpenTemplateEditorCommand.NotifyCanExecuteChanged();
        StartDeployCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsTemplateSelectorEnabled));
        OnPropertyChanged(nameof(IsReloadEnabled));
    }

    private void RaiseResultsPanelStateChanged() => ResultsPanelStateChanged?.Invoke(this, EventArgs.Empty);

    private static IReadOnlyList<DeployTimelineStepRow> CreateReadinessTimelineSteps(
        IReadOnlyList<DeployCompatibilityIssue> compatibilityIssues,
        IReadOnlyList<DeploymentReadinessCheckResult> readinessResults,
        bool hasBlocking)
    {
        var state = hasBlocking ? DeployTimelineStepState.Failed : DeployTimelineStepState.Succeeded;
        var rows = new List<DeployTimelineStepRow>
        {
            new("Readiness evaluation", state)
        };

        foreach (var issue in compatibilityIssues)
        {
            var issueState = issue.IsBlocking ? DeployTimelineStepState.Failed : DeployTimelineStepState.Pending;
            rows.Add(new DeployTimelineStepRow($"{issue.Message} {issue.Guidance}".Trim(), issueState));
        }

        foreach (var result in readinessResults.Where(result => result.Status is DeploymentReadinessStatus.Fail or DeploymentReadinessStatus.Warn))
        {
            var issueState = result.Status == DeploymentReadinessStatus.Fail ? DeployTimelineStepState.Failed : DeployTimelineStepState.Pending;
            rows.Add(new DeployTimelineStepRow($"{result.Message} {result.ActionableGuidance}".Trim(), issueState));
        }

        return rows;
    }
}
