using System.Collections.ObjectModel;
using LabAssistant.Business.Machines;

namespace LabAssistant.WinUI.ViewModels.Machines;

internal sealed class MachinesWorkspaceViewModel
{
    public ObservableCollection<MachineInventoryItem> Inventory { get; } = [];

    public Dictionary<string, MachineRdpReadinessResult> RdpReadinessByVmKey { get; } = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> AvailableSwitches { get; set; } = Array.Empty<string>();

    public MachineInventoryItem? SelectedMachine { get; set; }

    public MachineRdpReadinessResult SelectedRdpReadiness { get; set; } = new()
    {
        State = MachineRdpReadinessState.Unknown,
        ReasonCode = MachineRdpReadinessReasonCodes.CheckFailed,
        Message = "Select a VM to check RDP readiness."
    };

    public MachineEditSnapshot? LoadedEditSnapshot { get; set; }

    public MachineEditDraft? EditDraft { get; set; }

    public bool IsMachineActionRunning { get; set; }

    public bool IsInventoryRefreshing { get; set; }

    public bool IsRdpReadinessRefreshRunning { get; set; }

    public bool IsMachineEditLoading { get; set; }

    public bool IsMachineEditApplying { get; set; }

    public bool IsUpdatingMachineEditControls { get; set; }

    public bool CanRunSelectedMachineActions { get; set; }

    public bool CanOpenRdp { get; set; }

    public bool CanApplyEdits { get; set; }

    public string RdpTooltipText { get; set; } = "Select a VM to check RDP readiness.";

    public string StatusText { get; set; } = "Select a VM to run actions.";

    public DateTimeOffset LastRdpReadinessRefreshUtc { get; set; } = DateTimeOffset.MinValue;

    public DateTimeOffset LastOnDemandRdpRefreshUtc { get; set; } = DateTimeOffset.MinValue;

    public bool HasEditChanges => EditDraft is not null && EditDraft.ChangedFieldKeys.Count > 0;

    public void DiscardEditDraft()
    {
        LoadedEditSnapshot = null;
        EditDraft = null;
        IsUpdatingMachineEditControls = false;
    }

    public void ClearInventoryState()
    {
        Inventory.Clear();
        RdpReadinessByVmKey.Clear();
        SelectedMachine = null;
        SelectedRdpReadiness = new MachineRdpReadinessResult
        {
            State = MachineRdpReadinessState.Unknown,
            ReasonCode = MachineRdpReadinessReasonCodes.CheckFailed,
            Message = "Select a VM to check RDP readiness."
        };
        AvailableSwitches = Array.Empty<string>();
        StatusText = "No Hyper-V VMs found on this host.";
        DiscardEditDraft();
    }
}
