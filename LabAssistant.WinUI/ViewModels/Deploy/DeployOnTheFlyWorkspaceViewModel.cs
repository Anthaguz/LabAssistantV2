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

    public string? EditorSwitchNameDraft { get; private set; }

    public string? EditorVhdxIdDraft { get; private set; }

    public string? EditorVhdPathDraft { get; private set; }

    public string? EditorVhdxSignatureDraft { get; private set; }

    public bool IsSynchronizingEditorDraft { get; private set; }

    public IReadOnlyList<DeployCompatibilityIssue> CompatibilityIssues => _compatibilityIssues;

    public DeploymentReadinessReport? ReadinessReport { get; private set; }

    public bool IsEvaluatingReadiness { get; private set; }

    public bool IsStarting { get; private set; }

    public string ReadinessSummaryText { get; private set; } = "Readiness has not been evaluated.";

    public bool HasBlockingFailures =>
        _compatibilityIssues.Any(issue => issue.IsBlocking) || (ReadinessReport?.HasBlockingFailures ?? false);

    private readonly List<DeployCompatibilityIssue> _compatibilityIssues = [];

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
        string? selectedSwitchName,
        string? selectedVhdxId,
        string? selectedVhdPath,
        string? selectedVhdxSignature)
    {
        EditorVmNameDraft = vmName ?? string.Empty;
        EditorVmMemoryDraft = memoryText ?? string.Empty;
        EditorVmCpuDraft = cpuText ?? string.Empty;
        EditorSwitchNameDraft = NormalizeValue(selectedSwitchName);
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
        SelectedVmEntry.SwitchName = EditorSwitchNameDraft;
        SelectedVmEntry.SwitchNames = string.IsNullOrWhiteSpace(EditorSwitchNameDraft)
            ? null
            : [EditorSwitchNameDraft];
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
    }

    public void ClearReadinessState(string readinessSummaryText)
    {
        _compatibilityIssues.Clear();
        ReadinessReport = null;
        ReadinessSummaryText = readinessSummaryText;
        IsEvaluatingReadiness = false;
    }

    public void SetReadinessEvaluationFailed(string readinessSummaryText)
    {
        _compatibilityIssues.Clear();
        ReadinessReport = null;
        ReadinessSummaryText = readinessSummaryText;
        IsEvaluatingReadiness = false;
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
            EditorSwitchNameDraft = null;
            EditorVhdxIdDraft = null;
            EditorVhdPathDraft = null;
            EditorVhdxSignatureDraft = null;
            return;
        }

        EditorVmNameDraft = SelectedVmEntry.Name;
        EditorVmMemoryDraft = SelectedVmEntry.MemoryMb.ToString();
        EditorVmCpuDraft = SelectedVmEntry.CpuCount.ToString();
        EditorSwitchNameDraft = NormalizeValue(SelectedVmEntry.SwitchNames?.FirstOrDefault() ?? SelectedVmEntry.SwitchName);
        EditorVhdxIdDraft = NormalizeValue(SelectedVmEntry.VhdxId);
        EditorVhdPathDraft = NormalizeValue(SelectedVmEntry.VhdPath);
        EditorVhdxSignatureDraft = NormalizeValue(SelectedVmEntry.VhdxSignature);
    }

    private static string? NormalizeValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
