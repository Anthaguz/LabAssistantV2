using LabAssistant.Business.Deployment;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Views.Deploy;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Owns the long-lived Quick Deploy lane boundary, including workspace lifetime, controller lifetime,
/// reference-data refresh, template handoff, resolve suggestions, and lane-local panel intent.
/// Shell routing and the shared panel container still remain outside this owner.
/// </summary>
internal sealed class DeployOnTheFlyWorkspaceOwner : IDeployOnTheFlyWorkspaceControllerHost, IDeployQuickDeployLane
{
    private readonly DeployOnTheFlyView _view;
    private readonly DeployOnTheFlyWorkspaceViewModel _workspace = new();
    private readonly DeployOnTheFlyWorkspaceComposition _composition;
    private readonly DeployOnTheFlyWorkspaceController _controller;
    private readonly DeployReferenceDataService _referenceDataService;
    private readonly DeployResolveSuggestionsService _resolveSuggestionsService;
    private readonly DeployTemplateEditorLauncher _templateEditorLauncher;
    private readonly DeployOnTheFlyWorkspaceShellBridge _shellBridge;
    private readonly IDeploymentPreflightService _deploymentPreflightService;
    private readonly IDeploymentCoordinator _deploymentCoordinator;
    private readonly IDeploymentOutcomeSummaryBuilder _deploymentOutcomeSummaryBuilder;

    public DeployOnTheFlyWorkspaceOwner(
        DeployOnTheFlyView view,
        DeployOnTheFlyRightPanelView rightPanelView,
        DeployReferenceDataService referenceDataService,
        DeployResolveSuggestionsService resolveSuggestionsService,
        DeployTemplateEditorLauncher templateEditorLauncher,
        DeployOnTheFlyWorkspaceShellBridge shellBridge,
        IDeploymentPreflightService deploymentPreflightService,
        IDeploymentCoordinator deploymentCoordinator,
        IDeploymentOutcomeSummaryBuilder deploymentOutcomeSummaryBuilder)
    {
        _view = view;
        _referenceDataService = referenceDataService;
        _resolveSuggestionsService = resolveSuggestionsService;
        _templateEditorLauncher = templateEditorLauncher;
        _shellBridge = shellBridge;
        _deploymentPreflightService = deploymentPreflightService;
        _deploymentCoordinator = deploymentCoordinator;
        _deploymentOutcomeSummaryBuilder = deploymentOutcomeSummaryBuilder;
        _composition = new DeployOnTheFlyWorkspaceComposition(view, rightPanelView, _workspace);
        _controller = new DeployOnTheFlyWorkspaceController(_workspace, this);
        _composition.SetVisibility(isActive: false);
        WireHandlers();
    }

    public event EventHandler? SharedUiStateChanged;

    public int DraftCount => _workspace.VmEntryCount;

    public bool ShouldAutoOpenResultsPanel =>
        _workspace.IsStarting || string.Equals(_workspace.LifecycleState, "Running", StringComparison.OrdinalIgnoreCase);

    public string ResultsPanelTitle => "Quick Deploy Progress / Results";

    public void ApplyShellState(bool isActive)
    {
        _composition.SetVisibility(isActive);

        if (!isActive)
        {
            return;
        }

        EnsureSeeded();
        _ = EnsureReferenceDataAsync(forceRefresh: false);
        UpdateUi();

        if (_workspace.VmEntryCount > 0 &&
            _workspace.ReadinessReport is null &&
            !_workspace.IsEvaluatingReadiness &&
            !_workspace.IsStarting)
        {
            _controller.ScheduleAutoEvaluate();
        }
    }

    public void ApplyResultsPanelState(bool isActive, bool showPanel, bool panelUnavailable)
    {
        _composition.ApplyResultsPanelState(isActive, showPanel, panelUnavailable);
    }

    private void WireHandlers()
    {
        _view.VmEntrySelectionChanged += VmEntrySelectionChanged;
        _view.VmRemoveRequested += VmRemoveRequested;
        _view.AddVmRequested += AddVmRequested;
        _view.RemoveSelectedVmRequested += RemoveSelectedVmRequested;
        _view.ApplyVmChangesRequested += ApplyVmChangesRequested;
        _view.VmDraftChanged += VmDraftChanged;
        _view.EvaluateRequested += EvaluateRequested;
        _view.ResolveSuggestionsRequested += ResolveSuggestionsRequested;
        _view.OpenTemplateEditorRequested += OpenTemplateEditorRequested;
        _view.StartDeployRequested += StartDeployRequested;
        _view.OpenResultsPanelRequested += OpenResultsPanelRequested;
    }

    private void EnsureSeeded()
    {
        var previousCount = _workspace.VmEntryCount;
        _workspace.EnsureSeeded(_workspace.SelectedVmEntry?.VmId);
        _composition.SelectVmEntry(_workspace.SelectedVmEntry);

        if (_workspace.VmEntryCount != previousCount)
        {
            NotifySharedUiStateChanged();
        }
    }

    private async Task EnsureReferenceDataAsync(bool forceRefresh)
    {
        await _referenceDataService.EnsureAsync(forceRefresh);
        _composition.SetEditorReferenceData(
            _referenceDataService.AvailableSwitches,
            _referenceDataService.VhdxCatalogOptions);
        UpdateUi();
    }

    private LabTemplate BuildTemplate()
    {
        return new LabTemplate
        {
            Name = "Quick Deploy Draft",
            Description = "Generated quick deploy input.",
            VmTemplates = _workspace.CreateTemplateSnapshot().ToList()
        };
    }

    private void ReplaceVmEntriesFromTemplate(LabTemplate template)
    {
        _workspace.ReplaceEntriesFromTemplate(template, _workspace.SelectedVmEntry?.VmId);
        _composition.SelectVmEntry(_workspace.SelectedVmEntry);
        NotifySharedUiStateChanged();
        UpdateUi();
    }

    private async Task<int> ApplyResolveSuggestionsAsync(LabTemplate template)
    {
        await _referenceDataService.EnsureAsync(forceRefresh: false);
        return _resolveSuggestionsService.Apply(
            template,
            _referenceDataService.CatalogItems,
            _referenceDataService.AvailableSwitches);
    }

    private void UpdateUi()
    {
        _composition.UpdateUi();
        _shellBridge.RefreshResultsPanelState();
    }

    private void NotifySharedUiStateChanged()
    {
        SharedUiStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void VmEntrySelectionChanged(object? sender, EventArgs e)
    {
        _workspace.SetSelectedVmEntry(_view.CaptureVmSelectionInteractionState().SelectedVmEntryRow?.VmEntry);
        UpdateUi();
    }

    private async void VmRemoveRequested(VmTemplate vmEntry)
    {
        await RemoveVmEntryAsync(vmEntry);
    }

    private void AddVmRequested(object? sender, EventArgs e)
    {
        AddVmEntry();
    }

    private async void RemoveSelectedVmRequested(object? sender, EventArgs e)
    {
        await RemoveVmEntryAsync(_workspace.SelectedVmEntry);
    }

    private async void ApplyVmChangesRequested(object? sender, EventArgs e)
    {
        await ApplyVmChangesAsync();
    }

    private void VmDraftChanged(object? sender, EventArgs e)
    {
        if (_workspace.IsSynchronizingEditorDraft)
        {
            return;
        }

        _composition.SyncEditorDraft(_view.CaptureEditorInteractionState());
        UpdateUi();
        _controller.ScheduleAutoEvaluate();
    }

    private async void EvaluateRequested(object? sender, EventArgs e)
    {
        await _controller.EvaluateReadinessAsync(DeploymentPreflightMode.Full);
    }

    private async void ResolveSuggestionsRequested(object? sender, EventArgs e)
    {
        await ResolveSuggestionsAsync();
    }

    private async void OpenTemplateEditorRequested(object? sender, EventArgs e)
    {
        await OpenTemplateEditorAsync();
    }

    private async void StartDeployRequested(object? sender, EventArgs e)
    {
        await _controller.StartDeployAsync();
    }

    private void OpenResultsPanelRequested(object? sender, EventArgs e)
    {
        _shellBridge.RequestResultsPanelToggle();
    }

    private void AddVmEntry()
    {
        _workspace.SetShowAllVmRows(false);
        var entry = _workspace.AddVmEntry();
        _composition.SelectVmEntry(entry);
        _workspace.ClearReadinessState("Readiness has not been evaluated.");
        _workspace.ResetProgressState();
        _composition.SetActionStatus($"Added VM entry '{entry.Name}'.");
        NotifySharedUiStateChanged();
        UpdateUi();
    }

    private async Task RemoveVmEntryAsync(VmTemplate? vmEntry)
    {
        _workspace.SetShowAllVmRows(false);
        if (vmEntry is null)
        {
            _composition.SetActionStatus("Select a VM entry first.");
            return;
        }

        var vmName = vmEntry.Name;
        if (!await _shellBridge.ShowRemoveVmEntryConfirmationDialogAsync(vmName))
        {
            return;
        }

        _workspace.RemoveVmEntry(vmEntry);
        _composition.SelectVmEntry(_workspace.SelectedVmEntry);
        _workspace.ClearReadinessState(
            _workspace.VmEntryCount == 0
                ? "Add at least one VM entry to evaluate readiness."
                : "Readiness has not been evaluated.");
        _workspace.ResetProgressState();
        _composition.SetActionStatus($"Removed VM entry '{vmName}'.");
        NotifySharedUiStateChanged();
        UpdateUi();
    }

    private async Task ApplyVmChangesAsync()
    {
        _workspace.SetShowAllVmRows(false);
        if (_workspace.SelectedVmEntry is null)
        {
            _composition.SetActionStatus("Select a VM entry first.");
            return;
        }

        if (!_composition.TryApplyVmFields(showSuccessStatus: true))
        {
            return;
        }

        _workspace.ClearReadinessState("Readiness has not been evaluated.");
        _workspace.ResetProgressState();
        UpdateUi();
        await _controller.EvaluateReadinessAsync(DeploymentPreflightMode.Full);
    }

    private async Task ResolveSuggestionsAsync()
    {
        if (_workspace.VmEntryCount == 0)
        {
            _composition.SetActionStatus("Add at least one VM entry first.");
            return;
        }

        var template = BuildTemplate();
        var applied = await ApplyResolveSuggestionsAsync(template);
        if (applied > 0)
        {
            ReplaceVmEntriesFromTemplate(template);
        }

        _composition.SetActionStatus(applied == 0
            ? "No auto-resolve suggestions available for the current quick deploy configuration."
            : $"Applied {applied} auto-resolve suggestion(s). Re-evaluating readiness...");
        await _controller.EvaluateReadinessAsync(DeploymentPreflightMode.Full);
    }

    private async Task OpenTemplateEditorAsync()
    {
        if (!_composition.TryApplyVmFields(showSuccessStatus: false) && _workspace.SelectedVmEntry is not null)
        {
            return;
        }

        if (_workspace.VmEntryCount == 0)
        {
            _composition.SetActionStatus("Add at least one VM entry first.");
            return;
        }

        await _templateEditorLauncher.ShowEditorAsync(new TemplateEditorDocument
        {
            Template = BuildTemplate(),
            SourceFilePath = null
        }, "Opened quick deploy configuration in Templates editor.");
        _composition.SetActionStatus("Opened quick deploy configuration in Templates editor.");
    }

    AppSettings IDeployOnTheFlyWorkspaceControllerHost.DeploymentSettings => _referenceDataService.DeploymentSettings;

    IReadOnlyList<string> IDeployOnTheFlyWorkspaceControllerHost.AvailableSwitches => _referenceDataService.AvailableSwitches;

    IReadOnlyList<VhdxCatalogItem> IDeployOnTheFlyWorkspaceControllerHost.LoadCatalogItems() => _referenceDataService.CatalogItems;

    bool IDeployOnTheFlyWorkspaceControllerHost.TryApplyVmFields(bool showSuccessStatus, bool showValidationErrors) =>
        _composition.TryApplyVmFields(showSuccessStatus, showValidationErrors);

    void IDeployOnTheFlyWorkspaceControllerHost.SetActionStatus(string statusText)
    {
        _composition.SetActionStatus(statusText);
        _shellBridge.RefreshResultsPanelState();
    }

    LabTemplate IDeployOnTheFlyWorkspaceControllerHost.BuildTemplate() => BuildTemplate();

    Task IDeployOnTheFlyWorkspaceControllerHost.EnsureReferenceDataAsync(bool forceRefresh) => EnsureReferenceDataAsync(forceRefresh);

    Task<DeploymentReadinessReport> IDeployOnTheFlyWorkspaceControllerHost.RunReadinessChecksAsync(
        MultiVmDeploymentContext context,
        DeploymentPreflightMode mode) =>
        _deploymentPreflightService.RunAsync(context, mode);

    void IDeployOnTheFlyWorkspaceControllerHost.EnqueueUiUpdate(Action updateAction)
    {
        _shellBridge.EnqueueUiUpdate(updateAction);
    }

    void IDeployOnTheFlyWorkspaceControllerHost.UpdateUi() => UpdateUi();

    async Task<DeploymentOutcomeSummary> IDeployOnTheFlyWorkspaceControllerHost.DeployAllAsync(MultiVmDeploymentContext context)
    {
        await _deploymentCoordinator.DeployAllAsync(context);
        return _deploymentOutcomeSummaryBuilder.Build(context);
    }
}
