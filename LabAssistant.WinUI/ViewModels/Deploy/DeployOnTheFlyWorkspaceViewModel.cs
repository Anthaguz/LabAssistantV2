using System.Collections.ObjectModel;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Models.Deploy;

namespace LabAssistant.WinUI.ViewModels.Deploy;

internal sealed class DeployOnTheFlyWorkspaceViewModel
{
    public ObservableCollection<VmTemplate> VmEntries { get; } = [];

    public ObservableCollection<DeployOnTheFlyVmEntryRow> VmEntryRows { get; } = [];

    public int VmEntryCount => VmEntries.Count;

    public VmTemplate EnsureSeeded(string? selectedVmId)
    {
        if (VmEntries.Count == 0)
        {
            VmEntries.Add(CreateDefaultVmEntry(1));
        }

        RefreshVmEntryRows();
        return ResolveSelection(selectedVmId) ?? VmEntries[0];
    }

    public VmTemplate AddVmEntry()
    {
        var entry = CreateDefaultVmEntry(VmEntries.Count + 1);
        VmEntries.Add(entry);
        RefreshVmEntryRows();
        return entry;
    }

    public VmTemplate? RemoveVmEntry(VmTemplate vmEntry)
    {
        VmEntries.Remove(vmEntry);
        RefreshVmEntryRows();
        return VmEntries.FirstOrDefault();
    }

    public VmTemplate? ReplaceEntriesFromTemplate(LabTemplate template, string? selectedVmId)
    {
        VmEntries.Clear();
        foreach (var vmTemplate in template.VmTemplates)
        {
            VmEntries.Add(CloneVmTemplate(vmTemplate));
        }

        RefreshVmEntryRows();
        return ResolveSelection(selectedVmId) ?? VmEntries.FirstOrDefault();
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

    private VmTemplate? ResolveSelection(string? selectedVmId)
    {
        return string.IsNullOrWhiteSpace(selectedVmId)
            ? null
            : VmEntries.FirstOrDefault(item => string.Equals(item.VmId, selectedVmId, StringComparison.OrdinalIgnoreCase));
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
