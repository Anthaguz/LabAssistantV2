using LabAssistant.Business.Deployment;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Models.Deploy;
using LabAssistant.WinUI.Views.Deploy;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.ViewModels.Deploy;

internal sealed class DeployFromTemplateWorkspaceComposition : IDeployFromTemplateWorkspaceControllerHost, IDeployFromTemplateLane,
    IDeployFromTemplateV2ReviewHost
{
    private readonly DeployFromTemplateView _view;
    private readonly DeployFromTemplateRightPanelView _rightPanelView;
    private readonly IDeployFromTemplateCompositionHost _host;
    private readonly DeployFromTemplateWorkspaceViewModel _workspace = new();
    private readonly DeployFromTemplateWorkspaceController _controller;
    private readonly DeployV2ReviewWorkspaceViewModel _v2ReviewWorkspace = new();
    private readonly DeployV2ReviewWorkspaceController _v2ReviewController;
    private readonly List<DeployCompatibilityIssue> _compatibilityIssues = [];
    private DeploymentReadinessReport? _readinessReport;
    private bool _isLoadingTemplates;

    public DeployFromTemplateWorkspaceComposition(
        DeployFromTemplateView view,
        DeployFromTemplateRightPanelView rightPanelView,
        object? templateItemsSource,
        IDeployFromTemplateCompositionHost host)
    {
        _view = view;
        _rightPanelView = rightPanelView;
        _host = host;
        _controller = new DeployFromTemplateWorkspaceController(_workspace, this);
        _v2ReviewController = new DeployV2ReviewWorkspaceController(
            _v2ReviewWorkspace,
            new DeployV2ReviewProjectionService(),
            this);
        _view.SetTemplateItemsSource(templateItemsSource);
        _view.SetTemplateSelectorDisplayMemberPath(nameof(TemplateLibraryItem.Name));
        _view.SetSharedIssueSummariesItemsSource(_workspace.SharedIssueSummaries);
        _view.SetV2BlockersItemsSource(_v2ReviewWorkspace.BlockerRows);
        _view.SetV2CredentialSlotsItemsSource(_v2ReviewWorkspace.CredentialSlotRows);
        _view.SetV2WavesItemsSource(_v2ReviewWorkspace.WaveRows);
        _view.SetV2DiagnosticsItemsSource(_v2ReviewWorkspace.DiagnosticRows);
        _rightPanelView.ViewModel.Reset();
        WireHandlers();
        UpdateUi();
    }

    public TemplateLibraryItem? SelectedTemplateLibraryItem => _workspace.SelectedTemplateLibraryItem;

    public TemplateEditorDocument? ActiveTemplateDocument => _workspace.ActiveTemplateDocument;

    public int IssueRowCount => _workspace.IssueRows.Count;

    public int ResultRowCount => _workspace.ResultRows.Count;

    public bool IsLoadingTemplates => _isLoadingTemplates;

    public bool IsEvaluatingReadiness => _workspace.IsEvaluatingReadiness || _v2ReviewWorkspace.IsPlanning;

    public bool IsStarting => _workspace.IsStarting;

    public string LifecycleState => _workspace.LifecycleState;

    public int ProgressPercent => _workspace.ProgressPercent;

    public string ProgressSummary => _workspace.ProgressSummary;

    public bool ShouldAutoOpenResultsPanel =>
        _workspace.IsStarting || string.Equals(_workspace.LifecycleState, "Running", StringComparison.OrdinalIgnoreCase);

    public string ResultsPanelTitle => "From Template Progress / Results";

    public void ApplyShellState(bool isFromTemplateActive)
    {
        _view.Visibility = isFromTemplateActive ? Visibility.Visible : Visibility.Collapsed;
        if (isFromTemplateActive)
        {
            UpdateUi();
        }
    }

    public void ResetPanelState()
    {
        _rightPanelView.ViewModel.Reset();
    }

    public Task EvaluateReadinessAsync(DeploymentPreflightMode mode)
    {
        return IsActiveTemplateV2()
            ? EvaluateV2PlanAsync()
            : _controller.EvaluateReadinessAsync(mode);
    }

    public Task EnsureTemplatesLoadedAsync(bool forceRefresh) => LoadTemplatesAsync(forceRefresh);

    public void RefreshUi() => UpdateUi();

    public Task StartDeployAsync()
    {
        return IsActiveTemplateV2()
            ? StartV2DeployAsync()
            : _controller.StartDeployAsync();
    }

    public void SetSelectedTemplateLibraryItem(TemplateLibraryItem? selectedTemplateLibraryItem)
    {
        _workspace.SetSelectedTemplateLibraryItem(selectedTemplateLibraryItem);
        UpdateUi();
    }

    public void ClearSelection(string actionStatusText)
    {
        _workspace.ClearSelection(actionStatusText);
        _v2ReviewWorkspace.Hide();
        UpdateUi();
    }

    public void SetLoadedTemplateDocument(TemplateEditorDocument document, string actionStatusText)
    {
        _workspace.SetLoadedTemplateDocument(document, actionStatusText);
        UpdateUi();
    }

    public void SetSelectionLoadFailed(string actionStatusText)
    {
        _workspace.SetSelectionLoadFailed(actionStatusText);
        _v2ReviewWorkspace.Hide();
        UpdateUi();
    }

    public void SetActionStatus(string actionStatusText)
    {
        _workspace.SetActionStatus(actionStatusText);
        UpdateUi();
    }

    public void SetReadinessSummary(string readinessSummaryText)
    {
        _workspace.SetReadinessSummary(readinessSummaryText);
        UpdateUi();
    }

    public void SetWorkflowState(
        bool isEvaluatingReadiness,
        bool isStarting,
        string lifecycleState,
        int progressPercent,
        string progressSummary)
    {
        _workspace.SetWorkflowState(isEvaluatingReadiness, isStarting, lifecycleState, progressPercent, progressSummary);
        UpdateUi();
    }

    public void ClearGroupedIssueState()
    {
        _workspace.ClearGroupedIssueState();
        UpdateUi();
    }

    public void ReplaceIssueRows(IReadOnlyList<DeployIssueRow> issueRows)
    {
        _workspace.ReplaceIssueRows(issueRows);
        UpdateUi();
    }

    public void RefreshResultRows(
        IReadOnlyList<DeployCompatibilityIssue> compatibilityIssues,
        DeploymentReadinessReport? readinessReport)
    {
        _workspace.RefreshResultRows(compatibilityIssues, readinessReport);
        UpdateUi();
    }

    public void RefreshReviewState(bool hasBlockingFailures)
    {
        _workspace.RefreshReviewState(hasBlockingFailures);
        UpdateUi();
    }

    public void ReconcileSelection(IReadOnlyList<TemplateLibraryItem> items)
    {
        _workspace.ReconcileSelection(items);
        UpdateUi();
    }

    public void SetInteractionState(bool isLoadingTemplates, bool hasBlockingFailures)
    {
        var hasTemplate = _workspace.ActiveTemplateDocument is not null;
        var isV2Template = IsActiveTemplateV2();
        _view.SetEvaluateButtonText(isV2Template ? "Refresh V2 Plan" : "Review Readiness");
        _view.SetInteractionState(
            isTemplateSelectorEnabled: !isLoadingTemplates && !_workspace.IsStarting,
            isReloadEnabled: !isLoadingTemplates && !_workspace.IsStarting,
            isEvaluateReadinessEnabled: hasTemplate && !_workspace.IsEvaluatingReadiness && !_workspace.IsStarting && !_v2ReviewWorkspace.IsPlanning,
            isResolveSuggestionsEnabled: hasTemplate && !_workspace.IsEvaluatingReadiness && !_workspace.IsStarting && !_v2ReviewWorkspace.IsPlanning,
            isOpenTemplateEditorEnabled: _workspace.SelectedTemplateLibraryItem is not null && !_workspace.IsStarting,
            isStartDeployEnabled: hasTemplate &&
                                  !isLoadingTemplates &&
                                  !_workspace.IsEvaluatingReadiness &&
                                  !_workspace.IsStarting &&
                                  !_v2ReviewWorkspace.IsPlanning &&
                                  (isV2Template ? _v2ReviewWorkspace.CanStartDeploy : !hasBlockingFailures));
    }

    public void ApplyResultsPanelState(bool isActive, bool showPanel, bool panelUnavailable)
    {
        _rightPanelView.Visibility = isActive && showPanel ? Visibility.Visible : Visibility.Collapsed;
        var isRunning = _workspace.IsStarting || string.Equals(_workspace.LifecycleState, "Running", StringComparison.OrdinalIgnoreCase);
        _view.SetResultsPanelLauncherState(
            showPanel && isActive ? "Hide Progress / Results" : "Open Progress / Results",
            isActive && !panelUnavailable,
            panelUnavailable
                ? "Expand the window to review the progress and results panel."
                : isRunning
                    ? "The panel auto-opens while deployment runs and stays available for result review."
                    : _workspace.ResultRows.Count > 0
                        ? $"{_workspace.ResultRows.Count} VM result row(s) are available for review."
                        : "Use the side panel during or after deploy for progress, timeline, and results.");
    }

    AppSettings IDeployFromTemplateWorkspaceControllerHost.DeploymentSettings => _host.DeploymentSettings;

    IReadOnlyList<string> IDeployFromTemplateWorkspaceControllerHost.AvailableSwitches => _host.AvailableSwitches;

    IReadOnlyList<V2AvailableSwitchInfo> IDeployFromTemplateWorkspaceControllerHost.AvailableSwitchInfo => _host.AvailableSwitchInfo;

    TemplateEditorDocument? IDeployFromTemplateWorkspaceControllerHost.ActiveTemplateDocument => _workspace.ActiveTemplateDocument;

    IReadOnlyList<VhdxCatalogItem> IDeployFromTemplateWorkspaceControllerHost.LoadCatalogItems() => _host.LoadCatalogItems();

    Task IDeployFromTemplateWorkspaceControllerHost.EnsureReferenceDataAsync(bool forceRefresh) => _host.EnsureReferenceDataAsync(forceRefresh);

    Task<DeploymentReadinessReport> IDeployFromTemplateWorkspaceControllerHost.RunReadinessAsync(
        MultiVmDeploymentContext context,
        DeploymentPreflightMode mode) => _host.RunReadinessAsync(context, mode);

    void IDeployFromTemplateWorkspaceControllerHost.ReplaceCompatibilityIssues(IReadOnlyList<DeployCompatibilityIssue> issues)
    {
        _compatibilityIssues.Clear();
        _compatibilityIssues.AddRange(issues);
        UpdateUi();
    }

    DeploymentReadinessReport? IDeployFromTemplateWorkspaceControllerHost.CurrentReadinessReport
    {
        get => _readinessReport;
        set
        {
            _readinessReport = value;
            UpdateUi();
        }
    }

    Task<DeploymentOutcomeSummary> IDeployFromTemplateWorkspaceControllerHost.DeployAllAsync(MultiVmDeploymentContext context) => _host.DeployAllAsync(context);

    void IDeployFromTemplateWorkspaceControllerHost.AttachProgressCallbacks(
        MultiVmDeploymentContext context,
        Action<string, string?> onLogMessage,
        Action<string, DeployStepStateUpdate> onStepStateUpdated) => _host.AttachProgressCallbacks(context, onLogMessage, onStepStateUpdated);

    void IDeployFromTemplateWorkspaceControllerHost.ApplyWorkspaceState() => UpdateUi();

    TemplateEditorDocument? IDeployFromTemplateV2ReviewHost.ActiveTemplateDocument => _workspace.ActiveTemplateDocument;

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

    private void WireHandlers()
    {
        _view.ReloadTemplatesRequested += async (_, _) => await EnsureTemplatesLoadedAsync(forceRefresh: true);
        _view.EvaluateReadinessRequested += async (_, _) => await EvaluateReadinessAsync(DeploymentPreflightMode.Quick);
        _view.ResolveSuggestionsRequested += async (_, _) => await ResolveSuggestionsAsync();
        _view.OpenTemplateEditorRequested += async (_, _) => await OpenTemplateEditorAsync();
        _view.StartDeployRequested += async (_, _) => await StartDeployAsync();
        _view.TemplateSelectionChanged += async (_, _) => await HandleTemplateSelectionChangedAsync();
        _view.OpenResultsPanelRequested += (_, _) => _host.OnOpenResultsPanelRequested();
        _view.V2CredentialSlotSelectionChanged += (_, _) => _v2ReviewController.SelectCredentialSlot(_view.SelectedV2CredentialSlotRow?.SlotKey);
        _view.SaveV2CredentialSlotRequested += async (_, _) => await SaveSelectedCredentialSlotAsync();
        _view.V2BaseRemoteAccessOptionsChanged += (_, _) =>
        {
            _v2ReviewWorkspace.UpdateBaseRemoteAccessOptions(_view.V2DisableFirewall, _view.V2DisableRdpNla);
            UpdateUi();
        };
    }

    private async Task ResolveSuggestionsAsync()
    {
        if (_workspace.ActiveTemplateDocument is null)
        {
            _workspace.SetActionStatus("Select a template first.");
            return;
        }

        var applied = await _host.ApplyResolveSuggestionsAsync(_workspace.ActiveTemplateDocument.Template);
        _workspace.SetActionStatus(
            applied == 0
                ? "No auto-resolve suggestions available for the current template state."
                : $"Applied {applied} auto-resolve suggestion(s). Re-evaluating readiness...");
        await EvaluateReadinessAsync(DeploymentPreflightMode.Quick);
    }

    private async Task OpenTemplateEditorAsync()
    {
        var selectedTemplateLibraryItem = _workspace.SelectedTemplateLibraryItem;
        if (selectedTemplateLibraryItem is null)
        {
            _workspace.SetActionStatus("Select a template first.");
            return;
        }

        try
        {
            var document = await _host.LoadTemplateForEditorAsync(selectedTemplateLibraryItem.FilePath);
            await _host.ShowTemplateEditorAsync(document, "Template loaded.");
            _workspace.SetActionStatus($"Opened '{selectedTemplateLibraryItem.Name}' in Templates editor.");
        }
        catch (Exception ex)
        {
            _workspace.SetActionStatus($"Failed to open template in editor. {ex.Message}");
        }
    }

    private async Task HandleTemplateSelectionChangedAsync()
    {
        if (_isLoadingTemplates || _host.IsTemplatesLoading)
        {
            return;
        }

        _workspace.SetSelectedTemplateLibraryItem(_view.SelectedTemplateLibraryItem);
        if (_workspace.SelectedTemplateLibraryItem is null)
        {
            _workspace.ClearSelection("No template selected.");
            _readinessReport = null;
            _compatibilityIssues.Clear();
            _v2ReviewWorkspace.Hide();
            _workspace.SetWorkflowState(
                isEvaluatingReadiness: false,
                isStarting: false,
                lifecycleState: "Idle",
                progressPercent: 0,
                progressSummary: "No template selected.");
            UpdateUi();
            return;
        }

        try
        {
            var selectedTemplate = _workspace.SelectedTemplateLibraryItem;
            var document = await _host.LoadTemplateForEditorAsync(selectedTemplate.FilePath);
            _workspace.SetLoadedTemplateDocument(
                document,
                $"Loaded '{selectedTemplate.Name}' for deploy review.");
            _workspace.SetWorkflowState(
                isEvaluatingReadiness: false,
                isStarting: false,
                lifecycleState: "Ready",
                progressPercent: 0,
                progressSummary: $"Template '{selectedTemplate.Name}' loaded.");
            _readinessReport = null;
            _compatibilityIssues.Clear();
            UpdateUi();
            await EvaluateReadinessAsync(DeploymentPreflightMode.Quick);
        }
        catch (Exception ex)
        {
            _workspace.SetSelectionLoadFailed($"Failed to load selected template. {ex.Message}");
            _readinessReport = null;
            _compatibilityIssues.Clear();
            _v2ReviewWorkspace.Hide();
            _workspace.SetWorkflowState(
                isEvaluatingReadiness: false,
                isStarting: false,
                lifecycleState: "Error",
                progressPercent: 0,
                progressSummary: "Template load failed.");
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
        _workspace.SetWorkflowState(
            isEvaluatingReadiness: false,
            isStarting: false,
            lifecycleState: "Loading",
            progressPercent: 0,
            progressSummary: "Loading templates...");
        _host.RefreshSharedUiState();
        UpdateUi();
        _workspace.SetActionStatus("Loading templates for deploy...");

        try
        {
            await _host.EnsureTemplatesLibraryAsync(forceRefresh);

            if (_host.TemplateLibraryItems.Count == 0)
            {
                _workspace.ClearSelection("No templates available for deploy.");
                _readinessReport = null;
                _compatibilityIssues.Clear();
                _v2ReviewWorkspace.Hide();
                _workspace.SetWorkflowState(
                    isEvaluatingReadiness: false,
                    isStarting: false,
                    lifecycleState: "Idle",
                    progressPercent: 0,
                    progressSummary: "No templates available.");
            }
            else
            {
                if (_workspace.SelectedTemplateLibraryItem is null)
                {
                    _workspace.SetSelectedTemplateLibraryItem(_host.TemplateLibraryItems[0]);
                }

                _workspace.SetWorkflowState(
                    isEvaluatingReadiness: false,
                    isStarting: false,
                    lifecycleState: "Idle",
                    progressPercent: 0,
                    progressSummary: "Template list loaded.");
                _workspace.SetActionStatus($"Loaded {_host.TemplateLibraryItems.Count} template(s) for deploy.");
            }
        }
        catch (Exception ex)
        {
            _workspace.SetWorkflowState(
                isEvaluatingReadiness: false,
                isStarting: false,
                lifecycleState: "Error",
                progressPercent: 0,
                progressSummary: "Template load failed.");
            _workspace.SetActionStatus($"Failed to load deploy templates. {ex.Message}");
        }
        finally
        {
            _isLoadingTemplates = false;
            _host.RefreshSharedUiState();
            UpdateUi();
        }
    }

    private async Task EvaluateV2PlanAsync()
    {
        if (_workspace.ActiveTemplateDocument is null)
        {
            _workspace.SetActionStatus("Select a template first.");
            UpdateUi();
            return;
        }

        _workspace.SetWorkflowState(
            isEvaluatingReadiness: false,
            isStarting: false,
            lifecycleState: "Evaluating",
            progressPercent: 20,
            progressSummary: "Reviewing V2 deployment plan...");
        _workspace.SetActionStatus("Building V2 plan and review state...");
        await _v2ReviewController.RefreshPlanAsync(_workspace.ActiveTemplateDocument.Template);
        _workspace.SetWorkflowState(
            isEvaluatingReadiness: false,
            isStarting: false,
            lifecycleState: _v2ReviewWorkspace.CanStartDeploy ? "Ready" : "Blocked",
            progressPercent: _v2ReviewWorkspace.CanStartDeploy ? 35 : 25,
            progressSummary: _v2ReviewWorkspace.StatusText);
        UpdateUi();
    }

    private async Task SaveSelectedCredentialSlotAsync()
    {
        await _v2ReviewController.SaveCredentialSlotAsync(_view.V2CredentialSlotUsername, _view.V2CredentialSlotPassword);
        _workspace.SetActionStatus("Saved local credential slot and refreshed the V2 plan.");
        UpdateUi();
    }

    private async Task StartV2DeployAsync()
    {
        var activeTemplateDocument = _workspace.ActiveTemplateDocument;
        if (activeTemplateDocument is null)
        {
            _workspace.SetActionStatus("Select a template first.");
            return;
        }

        if (_v2ReviewWorkspace.CurrentPlan is null || !_v2ReviewWorkspace.CanStartDeploy)
        {
            await EvaluateV2PlanAsync();
            if (_v2ReviewWorkspace.CurrentPlan is null || !_v2ReviewWorkspace.CanStartDeploy)
            {
                _workspace.SetActionStatus("V2 deploy is blocked until review items are resolved.");
                UpdateUi();
                return;
            }
        }

        var plan = _v2ReviewWorkspace.CurrentPlan;
        var deploymentContext = new MultiVmDeploymentContext();
        _workspace.SetShowAllVmRows(true);
        _workspace.ClearResultRows();
        _workspace.InitializeProgressRows(plan);
        _host.AttachProgressCallbacks(
            deploymentContext,
            (vmName, message) =>
            {
                _workspace.UpdateProgressMessage(vmName, message);
                UpdateUi();
            },
            (vmName, update) =>
            {
                _workspace.ApplyProgressUpdate(vmName, update);
                UpdateUi();
            });
        _workspace.SetWorkflowState(
            isEvaluatingReadiness: false,
            isStarting: true,
            lifecycleState: "Running",
            progressPercent: 60,
            progressSummary: $"Running V2 deployment for {plan.Context.Vms.Count} VM(s)...");
        _workspace.SetActionStatus("Starting V2 deployment...");
        UpdateUi();

        try
        {
            var result = await _host.ExecuteV2DeployAsync(
                activeTemplateDocument.Template,
                plan,
                _v2ReviewWorkspace.ResolvedCredentialSlotValues,
                _v2ReviewWorkspace.CreateBaseRemoteAccessOptions(),
                deploymentContext);

            var issueRows = result.BlockingMessages
                .Select(message => new DeployIssueRow("Global", "Block", message))
                .ToList();
            if (issueRows.Count > 0)
            {
                _workspace.ReplaceIssueRows(issueRows);
            }

            _workspace.SetWorkflowState(
                isEvaluatingReadiness: false,
                isStarting: true,
                lifecycleState: result.Success ? "Completed" : deploymentContext.IsCancellationRequested ? "Cancelled" : "Failed",
                progressPercent: 100,
                progressSummary: result.Success
                    ? "V2 deployment completed."
                    : result.BlockingMessages.Count > 0
                        ? string.Join(" ", result.BlockingMessages)
                        : "V2 deployment finished with failures.");
            _workspace.SetActionStatus(
                result.Success
                    ? "V2 deployment finished successfully."
                    : "V2 deployment finished with blocking issues. Review the results panel and blockers.");
        }
        catch (Exception ex)
        {
            _workspace.SetWorkflowState(
                isEvaluatingReadiness: false,
                isStarting: true,
                lifecycleState: "Failed",
                progressPercent: 100,
                progressSummary: "V2 deployment failed.");
            _workspace.SetActionStatus($"V2 deploy failed. {ex.Message}");
        }
        finally
        {
            _workspace.SetWorkflowState(
                isEvaluatingReadiness: false,
                isStarting: false,
                lifecycleState: _workspace.LifecycleState,
                progressPercent: _workspace.ProgressPercent,
                progressSummary: _workspace.ProgressSummary);
            UpdateUi();
        }
    }

    private void UpdateUi()
    {
        var activeTemplateDocument = _workspace.ActiveTemplateDocument;
        var isV2Template = IsActiveTemplateV2();
        var hasBlockingFailures = isV2Template
            ? _v2ReviewWorkspace.HasBlockingItems
            : _compatibilityIssues.Any(issue => issue.IsBlocking) || (_readinessReport?.HasBlockingFailures ?? false);

        SetInteractionState(_isLoadingTemplates || _host.IsTemplatesLoading, hasBlockingFailures);
        _workspace.RefreshReviewState(hasBlockingFailures);

        if (activeTemplateDocument is null)
        {
            _v2ReviewWorkspace.Hide();
            _workspace.SetReadinessSummary("Select a template to evaluate readiness and run deploy.");
            _workspace.ClearGroupedIssueState();
            _workspace.RefreshResultRows(_compatibilityIssues, _readinessReport);
            UpdateCredentialsPanelState();
            ApplyWorkspaceState();
            _host.RefreshResultsPanelState();
            return;
        }

        if (isV2Template)
        {
            _workspace.SetReadinessSummary(_v2ReviewWorkspace.StatusText);
            _workspace.ReplaceIssueRows(_v2ReviewWorkspace.BlockerRows
                .Select(row => new DeployIssueRow(row.Scope, row.Severity, row.Message))
                .ToList());
            _workspace.RefreshResultRows([], null);
            UpdateCredentialsPanelState();
            ApplyWorkspaceState();
            _host.RefreshResultsPanelState();
            return;
        }

        var failCount = _compatibilityIssues.Count(issue => issue.IsBlocking) +
                        (_readinessReport?.Results.Count(result => result.Status == DeploymentReadinessStatus.Fail) ?? 0);
        var warnCount = _compatibilityIssues.Count(issue => !issue.IsBlocking) +
                        (_readinessReport?.Results.Count(result => result.Status == DeploymentReadinessStatus.Warn) ?? 0);
        var passCount = _readinessReport?.Results.Count(result => result.Status == DeploymentReadinessStatus.Pass) ?? 0;
        var deployState = hasBlockingFailures ? "Blocked" : "Ready";
        _workspace.SetReadinessSummary(
            $"{deployState}. Pass={passCount}, Warn={warnCount}, Fail={failCount}. " +
            $"Template: {activeTemplateDocument.Template.Name} ({activeTemplateDocument.Template.VmTemplates.Count} VMs).");

        _workspace.RefreshResultRows(_compatibilityIssues, _readinessReport);
        UpdateIssueRows();
        UpdateCredentialsPanelState();
        ApplyWorkspaceState();
        _host.RefreshResultsPanelState();
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

        _workspace.ReplaceIssueRows(issueRows);
    }

    private void ApplyWorkspaceState()
    {
        if (!ReferenceEquals(_view.SelectedTemplateLibraryItem, _workspace.SelectedTemplateLibraryItem))
        {
            _view.SelectedTemplateLibraryItem = _workspace.SelectedTemplateLibraryItem;
        }

        _view.ApplyWorkspaceState(new DeployFromTemplateViewState(
            _workspace.TemplateSummaryText,
            _workspace.TemplateRemediationText,
            _workspace.ActionStatusText,
            _workspace.ReadinessSummaryText,
            _workspace.SharedIssuesSummaryText,
            _workspace.GlobalIssuesBadgeText,
            _workspace.LifecycleState,
            _workspace.ProgressPercent,
            _workspace.ProgressSummary));
        _view.ApplyV2ReviewState(_v2ReviewWorkspace.IsVisible, _v2ReviewWorkspace.StatusText, _v2ReviewWorkspace.PlanSummary);
        _view.ApplyV2CredentialEditorState(_v2ReviewWorkspace.SelectedCredentialSlotPurpose, _v2ReviewWorkspace.SelectedCredentialSlotUsername);
        _view.ApplyV2BaseRemoteAccessState(_v2ReviewWorkspace.BaseRemoteAccess);
    }

    private void UpdateCredentialsPanelState()
    {
        if (_workspace.ActiveTemplateDocument is null)
        {
            _rightPanelView.ViewModel.Reset();
            return;
        }

        if (!IsActiveTemplateV2())
        {
            _rightPanelView.ViewModel.Reset("Credential slots are only used for V2 templates.");
            return;
        }

        if (_v2ReviewWorkspace.IsPlanning)
        {
            _rightPanelView.ViewModel.ShowLoading(_v2ReviewWorkspace.StatusText);
            return;
        }

        var credentialSlots = _v2ReviewWorkspace.CredentialSlotRows
            .Select(row => new CredentialSlotItem(
                row.SlotKey,
                row.PurposeSummary,
                row.AffectedVmSummary,
                row.ExistingUsername,
                row.HasStoredValue))
            .ToList();
        var allSlotsResolved = _v2ReviewWorkspace.CurrentPlan is not null && credentialSlots.Count == 0;
        var statusMessage = allSlotsResolved
            ? "No unresolved credential slots remain."
            : _v2ReviewWorkspace.StatusText;

        _rightPanelView.ViewModel.UpdateSlots(credentialSlots, allSlotsResolved, statusMessage);
    }

    private bool IsActiveTemplateV2()
    {
        var template = _workspace.ActiveTemplateDocument?.Template;
        if (template is null)
        {
            return false;
        }

        var engine = template.ExecutionEngine != TemplateExecutionEngine.Unknown
            ? template.ExecutionEngine
            : TemplateSchemaVersionCatalog.Classify(template.SchemaVersion);
        return engine == TemplateExecutionEngine.V2UnifiedPlanning;
    }
}
