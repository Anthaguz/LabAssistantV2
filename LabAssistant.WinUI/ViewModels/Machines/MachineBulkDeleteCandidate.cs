using LabAssistant.Business.Machines;

namespace LabAssistant.WinUI.ViewModels.Machines;

/// <summary>
/// One virtual machine in a bulk-delete confirmation, paired with the delete preview the policy
/// produced for it. The preview's <see cref="MachineDeletePreview.SafeForAutomaticStorageDeletion"/>
/// flag lets the batch dialog surface which selected VMs are unsafe for automatic storage deletion.
/// </summary>
public sealed record MachineBulkDeleteCandidate(MachineInventoryItem Vm, MachineDeletePreview Preview);
