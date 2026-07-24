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

    /// <summary>
    /// Confirms a bulk delete for several selected virtual machines at once (F07). A single scope
    /// choice applies to every candidate: <see cref="MachineDeleteScope.VmRegistrationOnly"/> never
    /// removes storage, while <see cref="MachineDeleteScope.VmAndStorage"/> is an explicit override
    /// that removes storage for all selected VMs even where the policy flagged them unsafe for
    /// automatic storage deletion. Returns the chosen scope, or null when the user cancels.
    /// </summary>
    Task<MachineDeleteScope?> ShowBulkDeleteScopeDialogAsync(IReadOnlyList<MachineBulkDeleteCandidate> candidates);

    /// <summary>
    /// Prompts for a new virtual machine name (F08). Returns the entered name, or null when the
    /// user cancels. The returned value is validated by the business layer before it is applied.
    /// </summary>
    Task<string?> ShowRenameDialogAsync(MachineInventoryItem vm);
}
