using CommunityToolkit.Mvvm.ComponentModel;
using LabAssistant.Business.Assets;

namespace LabAssistant.WinUI.ViewModels.Assets;

public partial class SwitchListItem : ObservableObject
{
    public SwitchListItem(AssetsSwitchRecord record)
    {
        Name = record.Name;
        Type = record.SwitchType;
        AdapterName = record.AdapterName;
    }

    public string Name { get; }

    public string Type { get; }

    public string? AdapterName { get; }

    public string DisplayName => Name;

    public string SecondaryText => string.IsNullOrWhiteSpace(AdapterName)
        ? Type
        : $"{Type} - {AdapterName}";

    [ObservableProperty]
    private string _status = "Validation updates while you edit the selected switch.";

    [ObservableProperty]
    private int _attachedVmCount;

    [ObservableProperty]
    private string _deleteSummary = string.Empty;
}
