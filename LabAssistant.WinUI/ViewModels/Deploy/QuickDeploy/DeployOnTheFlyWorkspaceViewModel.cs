using System.Collections.ObjectModel;
using LabAssistant.Business.Deployment;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Models.Deploy;

namespace LabAssistant.WinUI.ViewModels.Deploy;

internal sealed class DeployOnTheFlyWorkspaceViewModel
{
    public ObservableCollection<VmTemplate> VmEntries { get; } = [];

    public ObservableCollection<DeployOnTheFlyVmEntryRow> VmEntryRows { get; } = [];

    public int VmEntryCount => VmEntries.Count;

    public VmTemplate? SelectedVmEntry { get; private set; }

    public string EditorVmNameDraft { get; private set; } = string.Empty;

    public string EditorVmMemoryDraft { get; private set; } = string.Empty;

    public string EditorVmCpuDraft { get; private set; } = string.Empty;

    public IReadOnlyList<string> EditorSwitchNamesDraft { get; private set; } = Array.Empty<string>();

    public string? EditorVhdxIdDraft { get; private set; }

    public string? EditorVhdPathDraft { get; private set; }

    public string? EditorVhdxSignatureDraft { get; private set; }

    public bool IsSynchronizingEditorDraft { get; private set; }

    public IReadOnlyList<DeployCompatibilityIssue> CompatibilityIssues => _compatibilityIssues;

    public DeploymentReadinessReport? ReadinessReport { get; private set; }

    public bool IsEvaluatingReadiness { get; private set; }

    public bool IsStarting { get; private set; }

    public ObservableCollection<DeployVmResultRow> ResultRows { get; } = [];

    public ObservableCollection<DeployIssueRow> IssueRows { get; } = [];

    public string LifecycleState { get; private set; } = "Idle";

    public int ProgressPercent { get; private set; }

    public string ProgressSummary { get; private set; } = "No deployment started.";

    public bool ShowAllVmRows { get; private set; }

    public int LiveProgressVmCount => _progressByVm.Count;

    public string ReadinessSummaryText { get; private set; } = "Readiness has not been evaluated.";

    public bool HasBlockingFailures =>
        _compatibilityIssues.Any(issue => issue.IsBlocking) || (ReadinessReport?.HasBlockingFailures ?? false);

    private readonly List<DeployCompatibilityIssue> _compatibilityIssues = [];
    private readonly Dictionary<string, DeployVmProgressState> _progressByVm = new(StringComparer.OrdinalIgnoreCase);

    public VmTemplate EnsureSeeded(string? selectedVmId)
    {
        if (VmEntries.Count == 0)
        {
            VmEntries.Add(CreateDefaultVmEntry(1));
        }

        RefreshVmEntryRows();
        SelectedVmEntry = ResolveSelection(selectedVmId) ?? VmEntries[0];
        LoadEditorDraftFromSelection();
        return SelectedVmEntry;
    }

    public VmTemplate AddVmEntry()
    {
        var entry = CreateDefaultVmEntry(VmEntries.Count + 1);
        VmEntries.Add(entry);
        RefreshVmEntryRows();
        SelectedVmEntry = entry;
        LoadEditorDraftFromSelection();
        return entry;
    }

    public VmTemplate? RemoveVmEntry(VmTemplate vmEntry)
    {
        VmEntries.Remove(vmEntry);
        RefreshVmEntryRows();
        SelectedVmEntry = VmEntries.FirstOrDefault();
        LoadEditorDraftFromSelection();
        return SelectedVmEntry;
    }

    public VmTemplate? ReplaceEntriesFromTemplate(LabTemplate template, string? selectedVmId)
    {
        VmEntries.Clear();
        foreach (var vmTemplate in template.VmTemplates)
        {
            VmEntries.Add(CloneVmTemplate(vmTemplate));
        }

        RefreshVmEntryRows();
        SelectedVmEntry = ResolveSelection(selectedVmId) ?? VmEntries.FirstOrDefault();
        LoadEditorDraftFromSelection();
        return SelectedVmEntry;
    }

    public IReadOnlyList<VmTemplate> CreateTemplateSnapshot() => VmEntries.Select(CloneVmTemplate).ToList();

    public void RefreshVmEntryRows()
    {
        var existingByVm = VmEntryRows.ToDictionary(row => row.VmEntry);
        var staleRows = VmEntryRows.Where(row => !VmEntries.Contains(row.VmEntry)).ToList();
        foreach (var staleRow in staleRows)
        {
            VmEntryRows.Remove(staleRow);
        }

        for (var index = 0; index < VmEntries.Count; index++)
        {
            var vmEntry = VmEntries[index];
            if (!existingByVm.TryGetValue(vmEntry, out var row))
            {
                row = new DeployOnTheFlyVmEntryRow(vmEntry);
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

    public DeployOnTheFlyVmEntryRow? FindRow(VmTemplate? vmEntry)
    {
        return vmEntry is null
            ? null
            : VmEntryRows.FirstOrDefault(row => ReferenceEquals(row.VmEntry, vmEntry));
    }

    public void SetSelectedVmEntry(VmTemplate? vmEntry)
    {
        SelectedVmEntry = vmEntry;
        LoadEditorDraftFromSelection();
    }

    public void BeginEditorDraftSync()
    {
        IsSynchronizingEditorDraft = true;
    }

    public void EndEditorDraftSync()
    {
        IsSynchronizingEditorDraft = false;
    }

    public void UpdateEditorDraft(
        string? vmName,
        string? memoryText,
        string? cpuText,
        IReadOnlyList<string> selectedSwitchNames,
        string? selectedVhdxId,
        string? selectedVhdPath,
        string? selectedVhdxSignature)
    {
        EditorVmNameDraft = vmName ?? string.Empty;
        EditorVmMemoryDraft = memoryText ?? string.Empty;
        EditorVmCpuDraft = cpuText ?? string.Empty;
        EditorSwitchNamesDraft = NormalizeSwitchNames(selectedSwitchNames);
        EditorVhdxIdDraft = NormalizeValue(selectedVhdxId);
        EditorVhdPathDraft = NormalizeValue(selectedVhdPath);
        EditorVhdxSignatureDraft = NormalizeValue(selectedVhdxSignature);
    }

    public string? ApplyEditorDraftToSelectedVm()
    {
        if (SelectedVmEntry is null)
        {
            return null;
        }

        var previousName = SelectedVmEntry.Name;
        SelectedVmEntry.Name = EditorVmNameDraft.Trim();
        SelectedVmEntry.MemoryMb = int.Parse(EditorVmMemoryDraft);
        SelectedVmEntry.CpuCount = int.Parse(EditorVmCpuDraft);
        var normalizedSwitchNames = NormalizeSwitchNames(EditorSwitchNamesDraft);
        SelectedVmEntry.SwitchName = normalizedSwitchNames.FirstOrDefault();
        SelectedVmEntry.SwitchNames = normalizedSwitchNames.Count == 0 ? null : normalizedSwitchNames.ToList();
        SelectedVmEntry.VhdxId = EditorVhdxIdDraft;
        SelectedVmEntry.VhdPath = EditorVhdPathDraft;
        SelectedVmEntry.VhdxSignature = EditorVhdxSignatureDraft;
        return previousName;
    }

    public void SetSelectedVhdDraft(string? selectedVhdxId, string? selectedVhdPath, string? selectedVhdxSignature)
    {
        EditorVhdxIdDraft = NormalizeValue(selectedVhdxId);
        EditorVhdPathDraft = NormalizeValue(selectedVhdPath);
        EditorVhdxSignatureDraft = NormalizeValue(selectedVhdxSignature);
    }

    public void BeginReadinessEvaluation()
    {
        IsEvaluatingReadiness = true;
    }

    public void BeginStarting()
    {
        IsStarting = true;
    }

    public void EndStarting()
    {
        IsStarting = false;
    }

    public void SetWorkflowState(
        string lifecycleState,
        int progressPercent,
        string progressSummary)
    {
        LifecycleState = lifecycleState;
        ProgressPercent = progressPercent;
        ProgressSummary = progressSummary;
    }

    public void ResetProgressState()
    {
        LifecycleState = "Idle";
        ProgressPercent = 0;
        ProgressSummary = "No deployment started.";
    }

    public void SetShowAllVmRows(bool showAllVmRows)
    {
        ShowAllVmRows = showAllVmRows;
    }

    public void InitializeProgressRows(
        MultiVmDeploymentContext context,
        Func<VmDeploymentContext, IReadOnlyList<DeployTimelineStepDefinition>> expectedStepsFactory)
    {
        _progressByVm.Clear();

        foreach (var vmContext in context.VmContexts)
        {
            var vmName = string.IsNullOrWhiteSpace(vmContext.VmName) ? "Unnamed-VM" : vmContext.VmName.Trim();
            _progressByVm[vmName] = new DeployVmProgressState(vmName, expectedStepsFactory(vmContext));
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
    }

    public void RefreshResultRows()
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

        var compatibilityByVm = _compatibilityIssues
            .Where(issue => !string.IsNullOrWhiteSpace(issue.VmName))
            .GroupBy(issue => issue.VmName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var readinessByVm = (ReadinessReport?.Results ?? [])
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

            var status = hasBlocking ? "Blocked" : "Warning";
            var blockingCount = vmCompatibilityIssues.Count(issue => issue.IsBlocking) +
                                vmReadinessResults.Count(result => result.Status == DeploymentReadinessStatus.Fail);
            var warningCount = vmCompatibilityIssues.Count(issue => !issue.IsBlocking) +
                               vmReadinessResults.Count(result => result.Status == DeploymentReadinessStatus.Warn);

            ResultRows.Add(new DeployVmResultRow(
                VmName: vmName,
                Status: status,
                Summary: $"Blocking: {blockingCount} | Warnings: {warningCount}",
                ProgressPercent: hasBlocking ? 100 : 80,
                TimelineSteps: CreateReadinessTimelineSteps(vmCompatibilityIssues, vmReadinessResults, hasBlocking)));
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

    public void RefreshIssueRows()
    {
        IssueRows.Clear();

        foreach (var issue in _compatibilityIssues)
        {
            IssueRows.Add(new DeployIssueRow(
                Scope: string.IsNullOrWhiteSpace(issue.VmName) ? "Global" : issue.VmName.Trim(),
                Severity: issue.IsBlocking ? "Block" : "Warn",
                Message: FormatIssueMessage(issue.Message, issue.Guidance)));
        }

        if (ReadinessReport is null)
        {
            return;
        }

        foreach (var result in ReadinessReport.Results.Where(result => result.Status is DeploymentReadinessStatus.Fail or DeploymentReadinessStatus.Warn))
        {
            var scope = result.AffectedVmNames.Count > 0
                ? string.Join(", ", result.AffectedVmNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
                : "Global";
            IssueRows.Add(new DeployIssueRow(
                Scope: scope,
                Severity: result.Status == DeploymentReadinessStatus.Fail ? "Block" : "Warn",
                Message: FormatIssueMessage(result.Message, result.ActionableGuidance)));
        }
    }

    public void ApplyOutcomeSummary(DeploymentOutcomeSummary summary)
    {
        ResultRows.Clear();

        foreach (var vmOutcome in summary.VmOutcomes)
        {
            if (_progressByVm.TryGetValue(vmOutcome.VmName, out var liveState))
            {
                liveState.MarkCompleted(vmOutcome.Status.ToString(), BuildCleanupSummary(vmOutcome));
                ResultRows.Add(liveState.ToRow());
            }
            else
            {
                ResultRows.Add(new DeployVmResultRow(
                    VmName: vmOutcome.VmName,
                    Status: vmOutcome.Status.ToString(),
                    Summary: BuildCleanupSummary(vmOutcome),
                    ProgressPercent: 100,
                    TimelineSteps: CreateOutcomeTimelineSteps(vmOutcome)));
            }
        }

        IssueRows.Clear();
        foreach (var residual in summary.Residuals)
        {
            IssueRows.Add(new DeployIssueRow(
                Scope: residual.VmName,
                Severity: "Warn",
                Message: $"{residual.ResourceType} '{residual.Identifier}' residual. Suggested action: {residual.SuggestedAction}"));
        }
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
        RefreshIssueRows();
        RefreshResultRows();
    }

    public void ClearReadinessState(string readinessSummaryText)
    {
        _compatibilityIssues.Clear();
        ReadinessReport = null;
        ReadinessSummaryText = readinessSummaryText;
        IsEvaluatingReadiness = false;
        RefreshIssueRows();
        RefreshResultRows();
    }

    public void SetReadinessEvaluationFailed(string readinessSummaryText)
    {
        _compatibilityIssues.Clear();
        ReadinessReport = null;
        ReadinessSummaryText = readinessSummaryText;
        IsEvaluatingReadiness = false;
        RefreshIssueRows();
        RefreshResultRows();
    }

    private VmTemplate? ResolveSelection(string? selectedVmId)
    {
        return string.IsNullOrWhiteSpace(selectedVmId)
            ? null
            : VmEntries.FirstOrDefault(item => string.Equals(item.VmId, selectedVmId, StringComparison.OrdinalIgnoreCase));
    }

    private void LoadEditorDraftFromSelection()
    {
        if (SelectedVmEntry is null)
        {
            EditorVmNameDraft = string.Empty;
            EditorVmMemoryDraft = string.Empty;
            EditorVmCpuDraft = string.Empty;
            EditorSwitchNamesDraft = Array.Empty<string>();
            EditorVhdxIdDraft = null;
            EditorVhdPathDraft = null;
            EditorVhdxSignatureDraft = null;
            return;
        }

        EditorVmNameDraft = SelectedVmEntry.Name;
        EditorVmMemoryDraft = SelectedVmEntry.MemoryMb.ToString();
        EditorVmCpuDraft = SelectedVmEntry.CpuCount.ToString();
        EditorSwitchNamesDraft = NormalizeSwitchNames(SelectedVmEntry.SwitchNames?.Count > 0
            ? SelectedVmEntry.SwitchNames
            : string.IsNullOrWhiteSpace(SelectedVmEntry.SwitchName)
                ? Array.Empty<string>()
                : [SelectedVmEntry.SwitchName]);
        EditorVhdxIdDraft = NormalizeValue(SelectedVmEntry.VhdxId);
        EditorVhdPathDraft = NormalizeValue(SelectedVmEntry.VhdPath);
        EditorVhdxSignatureDraft = NormalizeValue(SelectedVmEntry.VhdxSignature);
    }

    private static string? NormalizeValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static IReadOnlyList<string> NormalizeSwitchNames(IEnumerable<string>? switchNames)
    {
        if (switchNames is null)
        {
            return Array.Empty<string>();
        }

        return switchNames
            .Select(name => string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim())
            .ToList();
    }

    private static string FormatIssueMessage(string message, string? guidance)
    {
        return string.IsNullOrWhiteSpace(guidance) ? message.Trim() : $"{message} {guidance}".Trim();
    }

    private static IReadOnlyList<DeployTimelineStepRow> CreateReadinessTimelineSteps(
        IReadOnlyList<DeployCompatibilityIssue> compatibilityIssues,
        IReadOnlyList<DeploymentReadinessCheckResult> readinessResults,
        bool hasBlocking)
    {
        var state = hasBlocking ? DeployTimelineStepState.Failed : DeployTimelineStepState.Succeeded;
        var steps = new List<DeployTimelineStepRow>
        {
            new("Readiness evaluation", state)
        };

        foreach (var issue in compatibilityIssues)
        {
            steps.Add(new DeployTimelineStepRow(
                $"{issue.Message} {issue.Guidance}".Trim(),
                issue.IsBlocking ? DeployTimelineStepState.Failed : DeployTimelineStepState.Pending));
        }

        foreach (var readinessResult in readinessResults.Where(result => result.Status is DeploymentReadinessStatus.Fail or DeploymentReadinessStatus.Warn))
        {
            steps.Add(new DeployTimelineStepRow(
                $"{readinessResult.Message} {readinessResult.ActionableGuidance}".Trim(),
                readinessResult.Status == DeploymentReadinessStatus.Fail
                    ? DeployTimelineStepState.Failed
                    : DeployTimelineStepState.Pending));
        }

        return steps;
    }

    private static IReadOnlyList<DeployTimelineStepRow> CreateOutcomeTimelineSteps(VmDeploymentOutcomeSummary vmOutcome)
    {
        var outcomeState = vmOutcome.Status switch
        {
            VmDeploymentOutcomeStatus.Succeeded => DeployTimelineStepState.Succeeded,
            VmDeploymentOutcomeStatus.Failed => DeployTimelineStepState.Failed,
            VmDeploymentOutcomeStatus.Cancelled => DeployTimelineStepState.Skipped,
            _ => DeployTimelineStepState.Pending
        };

        var rows = new List<DeployTimelineStepRow>
        {
            new("Deploy VM", outcomeState)
        };

        if (vmOutcome.Cleanup.CleanupRan)
        {
            var cleanupState = vmOutcome.Cleanup.Status switch
            {
                VmCleanupOutcomeStatus.Succeeded => DeployTimelineStepState.Succeeded,
                VmCleanupOutcomeStatus.Residuals => DeployTimelineStepState.Failed,
                _ => DeployTimelineStepState.Skipped
            };

            if (cleanupState != DeployTimelineStepState.Skipped)
            {
                rows.Add(new("Cleanup", cleanupState));
            }
        }

        return rows;
    }

    private static string BuildCleanupSummary(VmDeploymentOutcomeSummary vmOutcome)
    {
        var cleanup = vmOutcome.Cleanup;
        if (!cleanup.CleanupRan)
        {
            return "Completed";
        }

        return cleanup.Status switch
        {
            VmCleanupOutcomeStatus.Succeeded => "Cleanup completed",
            VmCleanupOutcomeStatus.Residuals => $"Cleanup completed with residuals ({cleanup.ResidualCount}). Manual cleanup may be required.",
            _ => "Cleanup not needed"
        };
    }

    private static VmTemplate CreateDefaultVmEntry(int sequence)
    {
        return new VmTemplate
        {
            Name = $"Quick VM {sequence}",
            MemoryMb = 2048,
            CpuCount = 2
        };
    }

    private static VmTemplate CloneVmTemplate(VmTemplate source)
    {
        return new VmTemplate
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
}
