using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Models.Deploy;
using LabAssistant.WinUI.ViewModels.Templates;
using LabAssistant.WinUI.Views.Deploy;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.ViewModels.Deploy;

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

    void OnVmEntriesSelectionChanged(DeployOnTheFlyVmEntryRow? selectedRow);

    Task OnVmRemoveRequestedAsync(VmTemplate vmEntry);

    void OnAddVmRequested();

    Task OnRemoveSelectedVmRequestedAsync();

    void OnApplyVmChangesRequested();

    void OnEditorInteractionChanged(DeployOnTheFlyEditorInteractionState interactionState);

    Task OnEvaluateRequestedAsync();

    Task OnResolveSuggestionsRequestedAsync();

    Task OnOpenTemplateEditorRequestedAsync();

    Task OnStartRequestedAsync();

    void OnOpenResultsPanelRequested();
}

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

    public void SetEditorReferenceData(
        IReadOnlyList<string> availableSwitches,
        IReadOnlyList<TemplateVhdxCatalogOption> availableVhdxCatalogOptions)
    {
        _availableSwitches = availableSwitches;
        _availableVhdxCatalogOptions = availableVhdxCatalogOptions;
    }

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

    public void UpdateEditorPanel()
    {
        _view.ApplyEditorViewState(BuildEditorViewState());
    }

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
        _view.VmRemoveRequested += async vmEntry => await _host.OnVmRemoveRequestedAsync(vmEntry);
        _view.AddVmRequested += (_, _) => _host.OnAddVmRequested();
        _view.RemoveSelectedVmRequested += async (_, _) => await _host.OnRemoveSelectedVmRequestedAsync();
        _view.ApplyVmChangesRequested += (_, _) => _host.OnApplyVmChangesRequested();
        _view.VmDraftChanged += (_, _) => _host.OnEditorInteractionChanged(_view.CaptureEditorInteractionState());
        _view.EvaluateRequested += async (_, _) => await _host.OnEvaluateRequestedAsync();
        _view.ResolveSuggestionsRequested += async (_, _) => await _host.OnResolveSuggestionsRequestedAsync();
        _view.OpenTemplateEditorRequested += async (_, _) => await _host.OnOpenTemplateEditorRequestedAsync();
        _view.StartDeployRequested += async (_, _) => await _host.OnStartRequestedAsync();
        _view.OpenResultsPanelRequested += (_, _) => _host.OnOpenResultsPanelRequested();
    }
}
