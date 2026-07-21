using LabAssistant.Business.Machines;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Configurable in-memory fake of <see cref="IMachinesCapabilityService"/> so the Machines view
/// model can be exercised headlessly without Hyper-V. Every task completes synchronously, which
/// lets tests drive the selection-triggered edit-snapshot load deterministically.
/// </summary>
internal sealed class MachinesFakeCapabilityService : IMachinesCapabilityService
{
    public List<MachineInventoryItem> Inventory { get; } = new();

    public Dictionary<string, MachineEditSnapshot> SnapshotsByVmName { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> VirtualSwitches { get; } = new();

    public List<(string VmName, MachineEditDraft Draft)> ApplyEditCalls { get; } = new();

    public List<(string VmName, string Action)> ActionCalls { get; } = new();

    public MachineOperationResult ApplyEditResult { get; set; } = new() { Success = true, OperationId = "op", UserMessage = "applied" };

    public Task<IReadOnlyList<MachineInventoryItem>> LoadInventoryAsync()
        => Task.FromResult<IReadOnlyList<MachineInventoryItem>>(Inventory.ToList());

    public Task<MachineEditSnapshot?> LoadEditSnapshotAsync(MachineInventoryItem vm)
        => Task.FromResult(SnapshotsByVmName.TryGetValue(vm.VmName, out var snapshot) ? snapshot : null);

    public Task<IReadOnlyList<string>> LoadVirtualSwitchesAsync()
        => Task.FromResult<IReadOnlyList<string>>(VirtualSwitches.ToList());

    public Task<MachineDeletionPolicyMode> GetDeletionPolicyAsync()
        => Task.FromResult(MachineDeletionPolicyMode.AskEveryTime);

    public Task SetDeletionPolicyAsync(MachineDeletionPolicyMode mode) => Task.CompletedTask;

    public Task<MachineDeletePreview> GetDeletePreviewAsync(MachineInventoryItem vm)
        => Task.FromResult(new MachineDeletePreview());

    public Task<MachineOperationResult> StartVmAsync(MachineInventoryItem vm) => RecordAction(vm, "start");

    public Task<MachineOperationResult> StopVmAsync(MachineInventoryItem vm) => RecordAction(vm, "stop");

    public Task<MachineOperationResult> RestartVmAsync(MachineInventoryItem vm) => RecordAction(vm, "restart");

    public Task<MachineOperationResult> OpenConsoleAsync(MachineInventoryItem vm) => RecordAction(vm, "open_console");

    public Task<MachineRdpReadinessResult> EvaluateRdpReadinessAsync(MachineInventoryItem vm, CancellationToken cancellationToken = default)
        => Task.FromResult(new MachineRdpReadinessResult
        {
            State = string.Equals(vm.State, "Running", StringComparison.OrdinalIgnoreCase)
                ? MachineRdpReadinessState.Ready
                : MachineRdpReadinessState.NotReady,
            ReasonCode = string.Equals(vm.State, "Running", StringComparison.OrdinalIgnoreCase)
                ? MachineRdpReadinessReasonCodes.Ready
                : MachineRdpReadinessReasonCodes.VmNotRunning,
            Message = "readiness",
            TargetIpv4 = "10.0.0.5"
        });

    public Task<MachineOperationResult> OpenRdpAsync(MachineInventoryItem vm, string targetIpv4) => RecordAction(vm, "open_rdp");

    public Task<MachineOperationResult> ApplyEditsAsync(MachineInventoryItem vm, MachineEditDraft draft)
    {
        ApplyEditCalls.Add((vm.VmName, draft));
        return Task.FromResult(ApplyEditResult);
    }

    public Task<MachineOperationResult> DeleteVmAsync(MachineInventoryItem vm, MachineDeleteScope scope) => RecordAction(vm, "delete");

    private Task<MachineOperationResult> RecordAction(MachineInventoryItem vm, string action)
    {
        ActionCalls.Add((vm.VmName, action));
        return Task.FromResult(new MachineOperationResult { Success = true, OperationId = "op", UserMessage = action });
    }
}
