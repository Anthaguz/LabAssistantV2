using CommunityToolkit.Mvvm.ComponentModel;
using LabAssistant.Models.Templates;

namespace LabAssistant.WinUI.ViewModels.Templates;

public partial class TemplateSlotItem : ObservableObject
{
    internal VmTemplate? SourceItem { get; private set; }

    [ObservableProperty] private string _vmId = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _baseDiskId = string.Empty;
    [ObservableProperty] private string _generation = string.Empty;
    [ObservableProperty] private string _switchNames = string.Empty;
    [ObservableProperty] private string _memoryMb = string.Empty;
    [ObservableProperty] private string _cpuCount = string.Empty;
    [ObservableProperty] private string _baseDiskPath = string.Empty;
    [ObservableProperty] private string _vhdxSignature = string.Empty;

    internal TemplateVhdxCatalogOption? SelectedCatalogOption { get; set; }

    public string Description => string.IsNullOrWhiteSpace(SwitchNames)
        ? ConstraintSummary
        : $"{ConstraintSummary} • Switches: {SwitchNames}";

    public string ConstraintSummary => $"Memory {MemoryMb} MB • CPU {CpuCount}";

    public static TemplateSlotItem FromVmTemplate(VmTemplate sourceItem)
    {
        var slot = new TemplateSlotItem
        {
            SourceItem = sourceItem,
            VmId = sourceItem.VmId,
            Name = sourceItem.Name,
            MemoryMb = sourceItem.MemoryMb.ToString(),
            CpuCount = sourceItem.CpuCount.ToString(),
            BaseDiskId = sourceItem.VhdxId ?? string.Empty,
            BaseDiskPath = sourceItem.VhdPath ?? string.Empty,
            VhdxSignature = sourceItem.VhdxSignature ?? string.Empty,
            SwitchNames = string.Join(", ", sourceItem.SwitchNames?.Where(name => !string.IsNullOrWhiteSpace(name)) ?? Enumerable.Empty<string>())
        };

        if (string.IsNullOrWhiteSpace(slot.SwitchNames))
        {
            slot.SwitchNames = sourceItem.SwitchName ?? string.Empty;
        }

        return slot;
    }

    internal void ApplyDraftState(
        string vmIdText,
        string name,
        string memoryMb,
        string cpuCount,
        IReadOnlyList<string> selectedSwitches,
        TemplateVhdxCatalogOption? selectedCatalogOption,
        string baseDiskId,
        string baseDiskPath,
        string vhdxSignature,
        string generation)
    {
        VmId = vmIdText.Replace("VM ID:", string.Empty, StringComparison.Ordinal).Trim();
        Name = name;
        MemoryMb = memoryMb;
        CpuCount = cpuCount;
        SwitchNames = string.Join(", ", selectedSwitches.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()));
        BaseDiskId = baseDiskId;
        BaseDiskPath = baseDiskPath;
        VhdxSignature = vhdxSignature;
        Generation = generation;
        SelectedCatalogOption = selectedCatalogOption;
        RaiseSummaryChanged();
    }

    internal IReadOnlyList<string> CaptureSwitches() => SwitchNames
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    partial void OnNameChanged(string value) => RaiseSummaryChanged();
    partial void OnMemoryMbChanged(string value) => RaiseSummaryChanged();
    partial void OnCpuCountChanged(string value) => RaiseSummaryChanged();
    partial void OnSwitchNamesChanged(string value) => RaiseSummaryChanged();

    private void RaiseSummaryChanged()
    {
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(ConstraintSummary));
    }
}
