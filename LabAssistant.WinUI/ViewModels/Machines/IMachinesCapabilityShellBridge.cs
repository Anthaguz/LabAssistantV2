using LabAssistant.Business.Machines;

namespace LabAssistant.WinUI.ViewModels.Machines;

/// <summary>
/// Shell seam the <see cref="MachinesViewModel"/> uses for shell-owned concerns: readiness
/// polling coordination and destructive delete confirmation dialogs. Implemented directly by the
/// Machines capability page, which owns the capability while it is the active shell surface.
/// </summary>
internal interface IMachinesCapabilityShellBridge
{
    bool IsMachinesOverviewActive { get; }

    void UpdateReadinessPollingState();

    Task<MachineDeleteScope?> ShowDeleteScopeDialogAsync(MachineInventoryItem vm, MachineDeletePreview preview);

    Task<bool> ShowDeleteConfirmationDialogAsync(MachineInventoryItem vm, MachineDeletePreview preview, MachineDeleteScope effectiveScope);
}
