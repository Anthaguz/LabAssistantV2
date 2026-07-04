using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Models.Deploy;
using LabAssistant.WinUI.ViewModels.Templates;
using LabAssistant.WinUI.Views.Deploy;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.ViewModels.Deploy;

/// <summary>
/// Applies Quick Deploy workspace state to the long-lived view pair.
/// Workflow orchestration stays in the lane owner and controller; this type is limited to composition and view-state application.
/// </summary>
internal sealed class DeployOnTheFlyWorkspaceComposition
{
    private const string VhdxPlaceholder = "(Select base disk)";
    private readonly DeployOnTheFlyView _view;
    private readonly DeployOnTheFlyRightPanelView _rightPanelView;
    private readonly DeployOnTheFlyWorkspaceViewModel _workspace;
    private IReadOnlyList<string> _availableSwitches = Array.Empty<string>();
    private IReadOnlyList<TemplateVhdxCatalogOption> _availableVhdxCatalogOptions = Array.Empty<TemplateVhdxCatalogOption>();
    private string _statusText = "Ready.";

    public DeployOnTheFlyWorkspaceComposition(
        DeployOnTheFlyView view,
        DeployOnTheFlyRightPanelView rightPanelView,
        DeployOnTheFlyWorkspaceViewModel workspace)
    {
        _view = view;
        _rightPanelView = rightPanelView;
        _workspace = workspace;

        _view.SetVmEntriesSource(_workspace.VmEntryRows);
        _rightPanelView.ViewModel.Reset();
        SetVisibility(isActive: false);
    }

    /// <summary>
    /// Applies the current Quick Deploy workspace state to the bound view while keeping shell panel ownership outside this seam.
    /// </summary>
    public void UpdateUi()
    {
        var hasEntries = _workspace.VmEntryCount > 0;
        var hasBlockingFailures = _workspace.HasBlockingFailures;

        if (!hasEntries)
        {
            _workspace.ClearReadinessState("Add at least one VM entry to evaluate readiness.");
            _workspace.ResetProgressState();
        }

        RefreshResultRows();
        RefreshIssueRows();
        UpdateRightPanelState();
        UpdateVmEntryRows();
        UpdateEditorPanel();

        var blockingIssueCount = _workspace.IssueRows.Count(issue => string.Equals(issue.Severity, "Block", StringComparison.OrdinalIgnoreCase));
        var warningIssueCount = _workspace.IssueRows.Count(issue => !string.Equals(issue.Severity, "Block", StringComparison.OrdinalIgnoreCase));
        var shouldShowInlineGuidance = hasEntries &&
                                       !_workspace.IsStarting &&
                                       _workspace.LiveProgressVmCount == 0;
        _view.ApplyWorkspaceState(new DeployOnTheFlyWorkspaceViewState(
            CanAddVm: !_workspace.IsEvaluatingReadiness && !_workspace.IsStarting,
            CanRemoveVm: _workspace.SelectedVmEntry is not null &&
                         !_workspace.IsEvaluatingReadiness &&
                         !_workspace.IsStarting,
            CanApplyVmChanges: _workspace.SelectedVmEntry is not null &&
                               !_workspace.IsEvaluatingReadiness &&
                               !_workspace.IsStarting,
            CanEvaluate: false,
            CanResolveSuggestions: hasEntries && !_workspace.IsEvaluatingReadiness && !_workspace.IsStarting,
            CanOpenTemplateEditor: hasEntries && !_workspace.IsStarting,
            CanStartDeploy: hasEntries && !hasBlockingFailures && !_workspace.IsEvaluatingReadiness && !_workspace.IsStarting,
            EditorIssueSummaryText: BuildEditorIssueSummaryText(),
            OverallStateText: _workspace.LifecycleState,
            ProgressPercent: _workspace.ProgressPercent,
            ProgressSummaryText: _workspace.ProgressSummary,
            GlobalIssuesBadgeText: $"Blocking: {blockingIssueCount} | Warnings: {warningIssueCount}",
            ReadinessSummaryText: shouldShowInlineGuidance
                ? $"{_workspace.ReadinessSummaryText} Review VM row badges and the selected VM details to fix blockers here before deploy."
                : _workspace.ProgressSummary,
            StatusText: _statusText));
    }

    /// <summary>
    /// Updates the Quick Deploy status text and reapplies the current view state when the view has already been loaded.
    /// </summary>
    public void SetActionStatus(string statusText)
    {
        _statusText = statusText;

        if (_view.Content is not null)
        {
            UpdateUi();
        }
    }

    /// <summary>
    /// Rebuilds the Quick Deploy result rows from the current workspace state for the bound right panel.
    /// </summary>
    public void RefreshResultRows()
    {
        _workspace.RefreshResultRows();
        UpdateRightPanelState();
    }

    /// <summary>
    /// Rebuilds the Quick Deploy issue rows from the current workspace state for the bound right panel.
    /// </summary>
    public void RefreshIssueRows()
    {
        _workspace.RefreshIssueRows();
    }

    /// <summary>
    /// Applies a completed deployment outcome summary to the Quick Deploy workspace-owned result and issue rows.
    /// </summary>
    public void ApplyOutcomeSummary(DeploymentOutcomeSummary summary)
    {
        _workspace.ApplyOutcomeSummary(summary);
        UpdateRightPanelState();
    }

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
    /// Captures editor interaction back into workspace draft state.
    /// </summary>
    public void SyncEditorDraft(DeployOnTheFlyEditorInteractionState interactionState)
    {
        var selectedCatalogOption = interactionState.SelectedVhdxCatalogOption;
        _workspace.UpdateEditorDraft(
            interactionState.VmName,
            interactionState.VmMemoryText,
            interactionState.VmCpuText,
            interactionState.SelectedSwitches,
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
                SetActionStatus("VM name is required.");
            }

            return false;
        }

        if (!int.TryParse(_workspace.EditorVmMemoryDraft, out var memoryMb) || memoryMb <= 0)
        {
            if (showValidationErrors)
            {
                SetActionStatus("Memory must be a positive integer.");
            }

            return false;
        }

        if (!int.TryParse(_workspace.EditorVmCpuDraft, out var cpuCount) || cpuCount <= 0)
        {
            if (showValidationErrors)
            {
                SetActionStatus("CPU count must be a positive integer.");
            }

            return false;
        }

        var previousName = _workspace.ApplyEditorDraftToSelectedVm();
        if (showSuccessStatus)
        {
            SetActionStatus($"Updated '{vmName}'.");
        }

        if (!string.Equals(previousName, vmName, StringComparison.Ordinal))
        {
            _workspace.RefreshVmEntryRows();
            SelectVmEntry(_workspace.SelectedVmEntry);
        }

        return true;
    }

    /// <summary>
    /// Applies route visibility without owning activation-time workflow behavior.
    /// </summary>
    public void SetVisibility(bool isActive)
    {
        _view.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
    }

    public void ApplyResultsPanelState(bool isActive, bool showPanel, bool panelUnavailable)
    {
        _rightPanelView.Visibility = isActive && showPanel ? Visibility.Visible : Visibility.Collapsed;
        _view.SetResultsPanelLauncherState(
            showPanel && isActive ? "Hide Progress / Results" : "Open Progress / Results",
            isActive && !panelUnavailable,
            panelUnavailable
                ? "Expand the window to review the progress and results panel."
                : _workspace.IsStarting || string.Equals(_workspace.LifecycleState, "Running", StringComparison.OrdinalIgnoreCase)
                ? "The panel auto-opens while deployment runs and stays available for result review."
                : _workspace.ResultRows.Count > 0
                    ? $"{_workspace.ResultRows.Count} VM result row(s) are available for review."
                    : "Use the side panel during or after deploy for progress, timeline, and results.");
    }

    private void UpdateRightPanelState()
    {
        _rightPanelView.ViewModel.UpdateState(
            _workspace.LifecycleState,
            _workspace.ProgressPercent,
            _workspace.ProgressSummary,
            _workspace.ResultRows.ToList());
    }

    private DeployOnTheFlyEditorViewState BuildEditorViewState()
    {
        var vhdItems = new List<object> { VhdxPlaceholder };
        vhdItems.AddRange(_availableVhdxCatalogOptions);

        if (_workspace.SelectedVmEntry is null)
        {
            return new DeployOnTheFlyEditorViewState(
                _workspace.EditorVmNameDraft,
                _workspace.EditorVmMemoryDraft,
                _workspace.EditorVmCpuDraft,
                _availableSwitches,
                Array.Empty<string>(),
                "Select a VM entry first.",
                vhdItems,
                VhdxPlaceholder,
                "Select a VM entry first.");
        }

        string switchGuidanceText;
        var selectedSwitches = _workspace.EditorSwitchNamesDraft;
        if (_availableSwitches.Count == 0)
        {
            switchGuidanceText = "No host switches available. Add a switch in Assets first.";
        }
        else if (selectedSwitches.Count == 0)
        {
            switchGuidanceText = "Switch selection is optional.";
        }
        else
        {
            switchGuidanceText = "Switch rows configured.";
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
            _availableSwitches,
            selectedSwitches,
            switchGuidanceText,
            vhdItems,
            selectedVhdxCatalogItem,
            vhdxGuidanceText);
    }

    private void UpdateVmEntryRows()
    {
        _workspace.RefreshVmEntryRows();

        var compatibilityByVm = _workspace.CompatibilityIssues
            .Where(issue => !string.IsNullOrWhiteSpace(issue.VmName))
            .GroupBy(issue => issue.VmName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var readinessByVm = (_workspace.ReadinessReport?.Results ?? [])
            .SelectMany(result => result.AffectedVmNames.Select(vmName => (vmName, result)))
            .Where(tuple => !string.IsNullOrWhiteSpace(tuple.vmName))
            .GroupBy(tuple => tuple.vmName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(item => item.result).ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var row in _workspace.VmEntryRows)
        {
            row.DisplayName = string.IsNullOrWhiteSpace(row.VmEntry.Name) ? "Unnamed VM" : row.VmEntry.Name.Trim();
            row.SecondaryText = BuildVmEntrySecondaryText(row.VmEntry);

            compatibilityByVm.TryGetValue(row.VmEntry.Name ?? string.Empty, out var compatibilityIssues);
            readinessByVm.TryGetValue(row.VmEntry.Name ?? string.Empty, out var readinessIssues);
            compatibilityIssues ??= [];
            readinessIssues ??= [];

            var draftIssues = ReferenceEquals(row.VmEntry, _workspace.SelectedVmEntry)
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
                row.IssueBrush = Application.Current.Resources["ShellCriticalBrush"] as Microsoft.UI.Xaml.Media.Brush;
                row.IssueBadgeVisibility = Visibility.Visible;
                row.IssueSummaryVisibility = Visibility.Visible;
            }
            else if (warningMessages.Count > 0)
            {
                row.IssueBadgeText = "Warning";
                row.IssueSummary = warningMessages[0];
                row.IssueBrush = Application.Current.Resources["ShellWarnBrush"] as Microsoft.UI.Xaml.Media.Brush;
                row.IssueBadgeVisibility = Visibility.Visible;
                row.IssueSummaryVisibility = Visibility.Visible;
            }
            else
            {
                row.IssueBadgeText = string.Empty;
                row.IssueSummary = string.Empty;
                row.IssueBrush = Application.Current.Resources["ShellTextSecondaryBrush"] as Microsoft.UI.Xaml.Media.Brush;
                row.IssueBadgeVisibility = Visibility.Collapsed;
                row.IssueSummaryVisibility = Visibility.Collapsed;
            }
        }
    }

    private string BuildEditorIssueSummaryText()
    {
        if (_workspace.SelectedVmEntry is null)
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

        var selectedRow = _workspace.FindRow(_workspace.SelectedVmEntry);
        if (selectedRow is not null && selectedRow.IssueSummaryVisibility == Visibility.Visible)
        {
            return $"{selectedRow.IssueBadgeText}: {selectedRow.IssueSummary}";
        }

        return "Ready. Changes validate while you edit. Row signals show which VM needs attention.";
    }

    private List<(bool IsBlocking, string Message)> GetDraftIssues()
    {
        if (_workspace.SelectedVmEntry is null)
        {
            return [];
        }

        return GetDraftIssues(
            _workspace.EditorVmNameDraft,
            _workspace.EditorVmMemoryDraft,
            _workspace.EditorVmCpuDraft,
            _workspace.EditorSwitchNamesDraft,
            _availableSwitches,
            string.IsNullOrWhiteSpace(_workspace.EditorVhdxIdDraft) &&
            string.IsNullOrWhiteSpace(_workspace.EditorVhdPathDraft)
                ? null
                : new object(),
            _availableVhdxCatalogOptions.Count);
    }

    private static List<(bool IsBlocking, string Message)> GetVmEntryIssues(VmTemplate vmEntry)
    {
        var memoryText = vmEntry.MemoryMb.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var cpuText = vmEntry.CpuCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
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

    private static string FormatIssueMessage(string message, string? guidance)
    {
        return string.IsNullOrWhiteSpace(guidance) ? message.Trim() : $"{message} {guidance}".Trim();
    }
}
