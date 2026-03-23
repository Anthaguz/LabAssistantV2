using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.Business.Templates;
using LabAssistant.WinUI.Models.Deploy;
using LabAssistant.WinUI.ViewModels.Templates;
using LabAssistant.WinUI.Views.Deploy;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Shell-facing contract for the Quick Deploy composition.
/// Keeps shell-owned route, panel, and cross-capability actions explicit while editor and workspace coordination stay inside capability-local seams.
/// </summary>
internal interface IDeployOnTheFlyCompositionHost
{
    int VmEntryCount { get; }

    DeploymentReadinessReport? ReadinessReport { get; }

    bool IsEvaluatingReadiness { get; }

    bool IsStarting { get; }

    string LifecycleState { get; }

    void EnsureSeeded();

    Task EnsureReferenceDataAsync(bool forceRefresh);

    void UpdateUi();

    void SetActionStatus(string statusText);

    void ScheduleAutoEvaluate();

    /// <summary>
    /// Builds the current Quick Deploy draft as a template snapshot for downstream workflow steps.
    /// </summary>
    LabTemplate BuildTemplate();

    /// <summary>
    /// Replaces the current Quick Deploy VM entries from a template snapshot after host-owned suggestion resolution mutates it.
    /// </summary>
    void ReplaceVmEntriesFromTemplate(LabTemplate template);

    /// <summary>
    /// Applies host-owned auto-resolve logic to a template snapshot without moving that shared helper into the Quick Deploy seam.
    /// </summary>
    Task<int> ApplyResolveSuggestionsAsync(LabTemplate template);

    /// <summary>
    /// Opens a template snapshot in the shared Templates editor workflow.
    /// </summary>
    Task ShowTemplateEditorAsync(TemplateEditorDocument document, string statusText);

    /// <summary>
    /// Refreshes shared Deploy shell state after Quick Deploy entry count changes.
    /// </summary>
    void RefreshSharedUiState();

    /// <summary>
    /// Shows the shell-owned confirmation dialog for removing a Quick Deploy VM entry.
    /// </summary>
    Task<bool> ShowRemoveVmEntryConfirmationDialogAsync(string vmName);

    void OnVmEntriesSelectionChanged(DeployOnTheFlyVmEntryRow? selectedRow);

    void OnEditorInteractionChanged(DeployOnTheFlyEditorInteractionState interactionState);

    Task OnEvaluateRequestedAsync(DeploymentPreflightMode mode);

    Task OnStartRequestedAsync();

    void OnOpenResultsPanelRequested();
}

/// <summary>
/// Coordinates the long-lived Quick Deploy views with workspace state and delegates shell-owned actions through <see cref="IDeployOnTheFlyCompositionHost"/>.
/// This composition owns capability-local editor synchronization and view application, but not shell chrome or routing.
/// </summary>
internal sealed class DeployOnTheFlyWorkspaceComposition
{
    private const string SwitchPlaceholder = "(No switch)";
    private const string VhdxPlaceholder = "(Select base disk)";
    private readonly DeployOnTheFlyView _view;
    private readonly DeployOnTheFlyRightPanelView _rightPanelView;
    private readonly DeployOnTheFlyWorkspaceViewModel _workspace;
    private readonly IDeployOnTheFlyCompositionHost _host;
    private IReadOnlyList<string> _availableSwitches = Array.Empty<string>();
    private IReadOnlyList<TemplateVhdxCatalogOption> _availableVhdxCatalogOptions = Array.Empty<TemplateVhdxCatalogOption>();

    public DeployOnTheFlyWorkspaceComposition(
        DeployOnTheFlyView view,
        DeployOnTheFlyRightPanelView rightPanelView,
        DeployOnTheFlyWorkspaceViewModel workspace,
        IDeployOnTheFlyCompositionHost host)
    {
        _view = view;
        _rightPanelView = rightPanelView;
        _workspace = workspace;
        _host = host;

        _view.SetVmEntriesSource(_workspace.VmEntryRows);
        _rightPanelView.SetResultRowsItemsSource(_workspace.ResultRows);
        WireHandlers();
    }

    public int ResultRowCount => _workspace.ResultRows.Count;

    public bool IsStarting => _workspace.IsStarting;

    public string LifecycleState => _workspace.LifecycleState;

    /// <summary>
    /// Updates the reference data used to render the selected VM editor without recreating the workspace.
    /// </summary>
    public void SetEditorReferenceData(
        IReadOnlyList<string> availableSwitches,
        IReadOnlyList<TemplateVhdxCatalogOption> availableVhdxCatalogOptions)
    {
        _availableSwitches = availableSwitches;
        _availableVhdxCatalogOptions = availableVhdxCatalogOptions;
    }

    /// <summary>
    /// Reconciles the editor selection with the workspace-selected VM entry.
    /// </summary>
    public void SelectVmEntry(VmTemplate? vmEntry)
    {
        if (vmEntry is null)
        {
            _view.SetVmEntrySelection(null);
            return;
        }

        var selectedRow = _workspace.FindRow(vmEntry);
        _view.SetVmEntrySelection(selectedRow);
    }

    /// <summary>
    /// Applies the current editor view state for the selected VM entry.
    /// </summary>
    public void UpdateEditorPanel()
    {
        _view.ApplyEditorViewState(BuildEditorViewState());
    }

    /// <summary>
    /// Captures editor interaction back into workspace draft state while leaving shell refresh and delayed readiness triggering outside this method.
    /// </summary>
    public void SyncEditorDraft(DeployOnTheFlyEditorInteractionState interactionState)
    {
        var selectedSwitch = interactionState.SelectedSwitchItem is string switchName &&
                             !string.Equals(switchName, SwitchPlaceholder, StringComparison.Ordinal)
            ? switchName
            : null;

        var selectedCatalogOption = interactionState.SelectedVhdxCatalogOption;
        _workspace.UpdateEditorDraft(
            interactionState.VmName,
            interactionState.VmMemoryText,
            interactionState.VmCpuText,
            selectedSwitch,
            selectedCatalogOption?.Id,
            selectedCatalogOption?.Path,
            selectedCatalogOption?.Signature);
    }

    /// <summary>
    /// Validates and applies the current editor draft to the selected VM entry, including row refresh when a rename changes list presentation.
    /// </summary>
    public bool TryApplyVmFields(bool showSuccessStatus, bool showValidationErrors = true)
    {
        if (_workspace.SelectedVmEntry is null || _workspace.IsSynchronizingEditorDraft)
        {
            return false;
        }

        var vmName = _workspace.EditorVmNameDraft.Trim();
        if (string.IsNullOrWhiteSpace(vmName))
        {
            if (showValidationErrors)
            {
                _host.SetActionStatus("VM name is required.");
            }

            return false;
        }

        if (!int.TryParse(_workspace.EditorVmMemoryDraft, out var memoryMb) || memoryMb <= 0)
        {
            if (showValidationErrors)
            {
                _host.SetActionStatus("Memory must be a positive integer.");
            }

            return false;
        }

        if (!int.TryParse(_workspace.EditorVmCpuDraft, out var cpuCount) || cpuCount <= 0)
        {
            if (showValidationErrors)
            {
                _host.SetActionStatus("CPU count must be a positive integer.");
            }

            return false;
        }

        var previousName = _workspace.ApplyEditorDraftToSelectedVm();
        if (showSuccessStatus)
        {
            _host.SetActionStatus($"Updated '{vmName}'.");
        }

        if (!string.Equals(previousName, vmName, StringComparison.Ordinal))
        {
            RefreshVmEntryRows();
        }

        return true;
    }

    /// <summary>
    /// Applies the current VM draft and immediately routes into the existing readiness boundary for the updated workspace snapshot.
    /// </summary>
    public async Task ApplyVmChangesAsync()
    {
        _workspace.SetShowAllVmRows(false);
        if (_workspace.SelectedVmEntry is null)
        {
            _host.SetActionStatus("Select a VM entry first.");
            return;
        }

        if (!TryApplyVmFields(showSuccessStatus: true))
        {
            return;
        }

        _workspace.ClearReadinessState("Readiness has not been evaluated.");
        _workspace.ResetProgressState();
        _host.UpdateUi();
        await EvaluateReadinessAsync(DeploymentPreflightMode.Full);
    }

    /// <summary>
    /// Forwards explicit readiness requests to the local readiness owner without pulling that workflow back into the shell.
    /// </summary>
    public Task EvaluateReadinessAsync(DeploymentPreflightMode mode) =>
        _host.OnEvaluateRequestedAsync(mode);

    /// <summary>
    /// Applies auto-resolve suggestions against the current Quick Deploy snapshot and keeps the long-lived workspace in sync with any changes.
    /// </summary>
    public async Task ResolveSuggestionsAsync()
    {
        if (_workspace.VmEntryCount == 0)
        {
            _host.SetActionStatus("Add at least one VM entry first.");
            return;
        }

        var template = _host.BuildTemplate();
        var applied = await _host.ApplyResolveSuggestionsAsync(template);
        if (applied > 0)
        {
            _host.ReplaceVmEntriesFromTemplate(template);
        }

        _host.SetActionStatus(applied == 0
            ? "No auto-resolve suggestions available for the current quick deploy configuration."
            : $"Applied {applied} auto-resolve suggestion(s). Re-evaluating readiness...");
        await EvaluateReadinessAsync(DeploymentPreflightMode.Full);
    }

    /// <summary>
    /// Opens the current Quick Deploy draft in the Templates editor while keeping editor validation local to the Quick Deploy seam first.
    /// </summary>
    public async Task OpenTemplateEditorAsync()
    {
        if (!TryApplyVmFields(showSuccessStatus: false) && _workspace.SelectedVmEntry is not null)
        {
            return;
        }

        if (_workspace.VmEntryCount == 0)
        {
            _host.SetActionStatus("Add at least one VM entry first.");
            return;
        }

        await _host.ShowTemplateEditorAsync(new TemplateEditorDocument
        {
            Template = _host.BuildTemplate(),
            SourceFilePath = null
        }, "Opened quick deploy configuration in Templates editor.");
        _host.SetActionStatus("Opened quick deploy configuration in Templates editor.");
    }

    /// <summary>
    /// Adds a VM entry to the long-lived Quick Deploy workspace and resets readiness/progress state for the updated draft.
    /// </summary>
    public void AddVmEntry()
    {
        _workspace.SetShowAllVmRows(false);
        var entry = _workspace.AddVmEntry();
        _host.RefreshSharedUiState();
        SelectVmEntry(entry);
        _workspace.ClearReadinessState("Readiness has not been evaluated.");
        _workspace.ResetProgressState();
        _host.SetActionStatus($"Added VM entry '{entry.Name}'.");
        UpdateEditorPanel();
        _host.UpdateUi();
    }

    /// <summary>
    /// Removes a VM entry from the long-lived Quick Deploy workspace after shell confirmation and reconciles the selection/editor state that remains.
    /// </summary>
    public async Task RemoveVmEntryAsync(VmTemplate? vmEntry)
    {
        _workspace.SetShowAllVmRows(false);
        if (vmEntry is null)
        {
            _host.SetActionStatus("Select a VM entry first.");
            return;
        }

        var vmName = vmEntry.Name;
        if (!await _host.ShowRemoveVmEntryConfirmationDialogAsync(vmName))
        {
            return;
        }

        _workspace.RemoveVmEntry(vmEntry);
        _host.RefreshSharedUiState();
        SelectVmEntry(_workspace.SelectedVmEntry);
        _workspace.ClearReadinessState(
            _workspace.VmEntryCount == 0
                ? "Add at least one VM entry to evaluate readiness."
                : "Readiness has not been evaluated.");
        _workspace.ResetProgressState();
        _host.SetActionStatus($"Removed VM entry '{vmName}'.");
        UpdateEditorPanel();
        _host.UpdateUi();
    }

    /// <summary>
    /// Applies shell activation state while preserving the long-lived Quick Deploy workspace and triggering initial readiness only when the active route first needs it.
    /// </summary>
    public void ApplyShellState(bool isActive)
    {
        _view.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;

        if (!isActive)
        {
            return;
        }

        _host.EnsureSeeded();
        _ = _host.EnsureReferenceDataAsync(forceRefresh: false);
        _host.UpdateUi();

        if (_host.VmEntryCount > 0 &&
            _host.ReadinessReport is null &&
            !_host.IsEvaluatingReadiness &&
            !_host.IsStarting)
        {
            _host.ScheduleAutoEvaluate();
        }
    }

    public void ApplyResultsPanelState(bool isActive, bool showPanel, bool panelUnavailable)
    {
        _rightPanelView.Visibility = isActive && showPanel ? Visibility.Visible : Visibility.Collapsed;
        _view.SetResultsPanelLauncherState(
            showPanel && isActive ? "Hide Progress / Results" : "Open Progress / Results",
            isActive && !panelUnavailable,
            panelUnavailable
                ? "Expand the window to review the progress and results panel."
                : _host.IsStarting || string.Equals(_host.LifecycleState, "Running", StringComparison.OrdinalIgnoreCase)
                ? "The panel auto-opens while deployment runs and stays available for result review."
                : ResultRowCount > 0
                    ? $"{ResultRowCount} VM result row(s) are available for review."
                    : "Use the side panel during or after deploy for progress, timeline, and results.");
    }

    private DeployOnTheFlyEditorViewState BuildEditorViewState()
    {
        var switchItems = new List<object> { SwitchPlaceholder };
        switchItems.AddRange(_availableSwitches);

        var vhdItems = new List<object> { VhdxPlaceholder };
        vhdItems.AddRange(_availableVhdxCatalogOptions);

        if (_workspace.SelectedVmEntry is null)
        {
            return new DeployOnTheFlyEditorViewState(
                _workspace.EditorVmNameDraft,
                _workspace.EditorVmMemoryDraft,
                _workspace.EditorVmCpuDraft,
                switchItems,
                SwitchPlaceholder,
                "Select a VM entry first.",
                vhdItems,
                VhdxPlaceholder,
                "Select a VM entry first.");
        }

        object selectedSwitchItem;
        string switchGuidanceText;
        var selectedSwitch = _workspace.EditorSwitchNameDraft;
        if (!string.IsNullOrWhiteSpace(selectedSwitch) &&
            _availableSwitches.Contains(selectedSwitch, StringComparer.OrdinalIgnoreCase))
        {
            selectedSwitchItem = _availableSwitches.First(name =>
                string.Equals(name, selectedSwitch, StringComparison.OrdinalIgnoreCase));
            switchGuidanceText = "Switch selected from host inventory.";
        }
        else
        {
            selectedSwitchItem = SwitchPlaceholder;
            switchGuidanceText = _availableSwitches.Count == 0
                ? "No host switches available. Add a switch in Assets first."
                : "Switch selection is optional.";
        }

        var vhdSelection = string.IsNullOrWhiteSpace(_workspace.EditorVhdxIdDraft)
            ? null
            : _availableVhdxCatalogOptions.FirstOrDefault(option =>
                string.Equals(option.Id, _workspace.EditorVhdxIdDraft, StringComparison.OrdinalIgnoreCase));
        if (vhdSelection is null && !string.IsNullOrWhiteSpace(_workspace.EditorVhdPathDraft))
        {
            vhdSelection = _availableVhdxCatalogOptions.FirstOrDefault(option =>
                string.Equals(option.Path, _workspace.EditorVhdPathDraft, StringComparison.OrdinalIgnoreCase));
        }

        object selectedVhdxCatalogItem;
        string vhdxGuidanceText;
        if (vhdSelection is not null)
        {
            selectedVhdxCatalogItem = vhdSelection;
            vhdxGuidanceText = $"Selected: {vhdSelection.DisplayLabel} ({vhdSelection.Id}).";
        }
        else
        {
            selectedVhdxCatalogItem = VhdxPlaceholder;
            vhdxGuidanceText = _availableVhdxCatalogOptions.Count == 0
                ? "No VHDX catalog entries available. Import base disks in Assets first."
                : "Select a base disk from catalog.";
        }

        return new DeployOnTheFlyEditorViewState(
            _workspace.EditorVmNameDraft,
            _workspace.EditorVmMemoryDraft,
            _workspace.EditorVmCpuDraft,
            switchItems,
            selectedSwitchItem,
            switchGuidanceText,
            vhdItems,
            selectedVhdxCatalogItem,
            vhdxGuidanceText);
    }

    private void RefreshVmEntryRows()
    {
        _workspace.RefreshVmEntryRows();
        SelectVmEntry(_workspace.SelectedVmEntry);
    }

    private void WireHandlers()
    {
        _view.VmEntrySelectionChanged += (_, _) =>
            _host.OnVmEntriesSelectionChanged(_view.CaptureVmSelectionInteractionState().SelectedVmEntryRow);
        _view.VmRemoveRequested += async vmEntry => await RemoveVmEntryAsync(vmEntry);
        _view.AddVmRequested += (_, _) => AddVmEntry();
        _view.RemoveSelectedVmRequested += async (_, _) => await RemoveVmEntryAsync(_workspace.SelectedVmEntry);
        _view.ApplyVmChangesRequested += async (_, _) => await ApplyVmChangesAsync();
        _view.VmDraftChanged += (_, _) => _host.OnEditorInteractionChanged(_view.CaptureEditorInteractionState());
        _view.EvaluateRequested += async (_, _) => await EvaluateReadinessAsync(DeploymentPreflightMode.Full);
        _view.ResolveSuggestionsRequested += async (_, _) => await ResolveSuggestionsAsync();
        _view.OpenTemplateEditorRequested += async (_, _) => await OpenTemplateEditorAsync();
        _view.StartDeployRequested += async (_, _) => await _host.OnStartRequestedAsync();
        _view.OpenResultsPanelRequested += (_, _) => _host.OnOpenResultsPanelRequested();
    }
}
