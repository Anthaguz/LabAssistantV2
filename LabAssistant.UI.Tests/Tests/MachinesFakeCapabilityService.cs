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

    public List<(string VmName, MachineDeleteScope Scope)> DeleteCalls { get; } = new();

    public List<(string VmName, string NewName)> RenameCalls { get; } = new();

    /// <summary>Per-VM exceptions thrown by power/delete ops, keyed by VM name, to exercise
    /// per-VM failure isolation in the bulk runner.</summary>
    public Dictionary<string, string> ActionExceptionsByVmName { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Per-VM value for <see cref="MachineDeletePreview.SafeForAutomaticStorageDeletion"/>
    /// returned by <see cref="GetDeletePreviewAsync"/>; VMs absent from the map default to false.</summary>
    public Dictionary<string, bool> SafeForAutomaticStorageDeletionByVmName { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Invoked at the start of every power/delete op, before any configured failure, so a
    /// test can request cancellation at a deterministic VM boundary.</summary>
    public Action<MachineInventoryItem, string>? OnActionInvoked { get; set; }

    public MachineOperationResult RenameResult { get; set; } = new() { Success = true, OperationId = "op", UserMessage = "renamed" };

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
        => Task.FromResult(new MachineDeletePreview
        {
            SafeForAutomaticStorageDeletion =
                SafeForAutomaticStorageDeletionByVmName.TryGetValue(vm.VmName, out var safe) && safe
        });

    public Task<MachineOperationResult> StartVmAsync(MachineInventoryItem vm) => RecordAction(vm, "start");

    public Task<MachineOperationResult> StopVmAsync(MachineInventoryItem vm) => RecordAction(vm, "stop");

    public Task<MachineOperationResult> TurnOffVmAsync(MachineInventoryItem vm) => RecordAction(vm, "turn_off");

    public Task<MachineOperationResult> RestartVmAsync(MachineInventoryItem vm) => RecordAction(vm, "restart");

    public Task<MachineOperationResult> RenameVmAsync(MachineInventoryItem vm, string newName)
    {
        RenameCalls.Add((vm.VmName, newName));
        return Task.FromResult(RenameResult);
    }

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

    public Task<MachineOperationResult> DeleteVmAsync(MachineInventoryItem vm, MachineDeleteScope scope)
    {
        OnActionInvoked?.Invoke(vm, "delete");
        DeleteCalls.Add((vm.VmName, scope));
        if (ActionExceptionsByVmName.TryGetValue(vm.VmName, out var reason))
        {
            throw new InvalidOperationException(reason);
        }

        ActionCalls.Add((vm.VmName, "delete"));
        return Task.FromResult(new MachineOperationResult { Success = true, OperationId = "op", UserMessage = "delete" });
    }

    private Task<MachineOperationResult> RecordAction(MachineInventoryItem vm, string action)
    {
        OnActionInvoked?.Invoke(vm, action);
        if (ActionExceptionsByVmName.TryGetValue(vm.VmName, out var reason))
        {
            throw new InvalidOperationException(reason);
        }

        ActionCalls.Add((vm.VmName, action));
        return Task.FromResult(new MachineOperationResult { Success = true, OperationId = "op", UserMessage = action });
    }
}
