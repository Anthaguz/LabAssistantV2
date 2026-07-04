using CommunityToolkit.Mvvm.ComponentModel;

namespace LabAssistant.WinUI.ViewModels.Machines;

public partial class MachineListItem : ObservableObject
{
    public MachineListItem(string vmName, string state, string originLabel, string vmId)
    {
        _vmName = vmName;
        _state = state;
        _originLabel = originLabel;
        _vmId = vmId;
    }

    [ObservableProperty]
    private string _vmName;

    [ObservableProperty]
    private string _state;

    [ObservableProperty]
    private string _originLabel;

    [ObservableProperty]
    private string _vmId;
}
