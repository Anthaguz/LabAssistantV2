using LabAssistant.Business.Machines;

namespace LabAssistant.WinUI.ViewModels.Machines;

internal interface IMachinesWorkspaceControllerHost
{
    bool IsMachinesOverviewActive { get; }

    void SetMachinesStatus(string message);

    void SetSelectedMachineInView(MachineInventoryItem? selectedMachine);

    void UpdateMachineDetails();

    void UpdateRdpReadinessUi();

    void UpdateMachineActionButtons();

    void ClearMachineEditControls();

    void ApplyMachineEditDraftToControls();

    void UpdateMachineEditDirtyIndicator();

    Task<MachineDeleteScope?> ShowDeleteScopeDialogAsync(MachineInventoryItem vm, MachineDeletePreview preview);

    Task<bool> ShowDeleteConfirmationDialogAsync(MachineInventoryItem vm, MachineDeletePreview preview, MachineDeleteScope effectiveScope);
}

internal sealed class MachinesWorkspaceController
{
    private readonly IMachinesCapabilityService _machinesCapabilityService;
    private readonly MachinesWorkspaceViewModel _workspace;
    private readonly IMachinesWorkspaceControllerHost _host;

    public MachinesWorkspaceController(
        IMachinesCapabilityService machinesCapabilityService,
        MachinesWorkspaceViewModel workspace,
        IMachinesWorkspaceControllerHost host)
    {
        _machinesCapabilityService = machinesCapabilityService;
        _workspace = workspace;
        _host = host;
    }

    public async Task<bool> EnsureInventoryAsync(bool forceRefresh)
    {
        if (!_host.IsMachinesOverviewActive)
        {
            return false;
        }

        if (!forceRefresh && _workspace.Inventory.Count > 0)
        {
            return true;
        }

        _workspace.IsInventoryRefreshing = true;
        _host.UpdateMachineActionButtons();
        _host.SetMachinesStatus("Loading host VM inventory...");

        try
        {
            var inventory = await _machinesCapabilityService.LoadInventoryAsync();
            var selectedVmName = _workspace.SelectedMachine?.VmName;
            var orderedInventory = inventory
                .OrderBy(vm => vm.VmName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _workspace.Inventory.Clear();
            foreach (var vm in orderedInventory)
            {
                _workspace.Inventory.Add(vm);
            }

            _workspace.SelectedMachine = _workspace.Inventory.FirstOrDefault(vm =>
                string.Equals(vm.VmName, selectedVmName, StringComparison.Ordinal));
            _host.SetSelectedMachineInView(_workspace.SelectedMachine);

            _host.SetMachinesStatus(_workspace.Inventory.Count == 0
                ? "No Hyper-V VMs found on this host."
                : $"Loaded {_workspace.Inventory.Count} VM(s).");

            SyncRdpReadinessCache();
            await LoadMachineEditStateAsync();
            return true;
        }
        catch (Exception ex)
        {
            _host.SetMachinesStatus($"Failed to load VM inventory. Showing last known list. {ex.Message}");
            return false;
        }
        finally
        {
            _workspace.IsInventoryRefreshing = false;
            _host.UpdateMachineDetails();
            _host.UpdateMachineActionButtons();
        }
    }

    public async Task HandleSelectionChangedAsync(MachineInventoryItem? selectedMachine)
    {
        _workspace.DiscardEditDraft();
        _host.UpdateMachineEditDirtyIndicator();
        _workspace.SelectedMachine = selectedMachine;
        UpdateSelectedRdpReadinessFromCache();
        _host.UpdateMachineDetails();
        _host.UpdateRdpReadinessUi();
        _host.UpdateMachineActionButtons();
        await LoadMachineEditStateAsync();
    }

    public async Task StartSelectedMachineAsync()
    {
        await RunSelectedMachineOperationAsync(
            "Starting VM...",
            vm => _machinesCapabilityService.StartVmAsync(vm),
            refreshInventory: true);
    }

    public async Task StopSelectedMachineAsync()
    {
        await RunSelectedMachineOperationAsync(
            "Stopping VM...",
            vm => _machinesCapabilityService.StopVmAsync(vm),
            refreshInventory: true);
    }

    public async Task RestartSelectedMachineAsync()
    {
        await RunSelectedMachineOperationAsync(
            "Restarting VM...",
            vm => _machinesCapabilityService.RestartVmAsync(vm),
            refreshInventory: true);
    }

    public async Task OpenSelectedMachineConsoleAsync()
    {
        await RunSelectedMachineOperationAsync(
            "Opening Hyper-V Console...",
            vm => _machinesCapabilityService.OpenConsoleAsync(vm),
            refreshInventory: false);
    }

    public async Task OpenSelectedMachineRdpAsync()
    {
        if (_workspace.SelectedMachine is null)
        {
            _host.SetMachinesStatus("Select a VM before running actions.");
            return;
        }

        if (_workspace.SelectedRdpReadiness.State != MachineRdpReadinessState.Ready ||
            string.IsNullOrWhiteSpace(_workspace.SelectedRdpReadiness.TargetIpv4))
        {
            _host.SetMachinesStatus($"RDP not ready. {_workspace.SelectedRdpReadiness.Message}");
            if (DateTimeOffset.UtcNow - _workspace.LastOnDemandRdpRefreshUtc >= TimeSpan.FromSeconds(2))
            {
                _workspace.LastOnDemandRdpRefreshUtc = DateTimeOffset.UtcNow;
                await RefreshRdpReadinessAsync(selectedOnly: true);
            }

            return;
        }

        var targetIpv4 = _workspace.SelectedRdpReadiness.TargetIpv4!;
        await RunSelectedMachineOperationAsync(
            "Opening RDP...",
            vm => _machinesCapabilityService.OpenRdpAsync(vm, targetIpv4),
            refreshInventory: false);
    }

    public async Task DeleteSelectedMachineAsync()
    {
        if (_workspace.SelectedMachine is null)
        {
            _host.SetMachinesStatus("Select a VM before running actions.");
            return;
        }

        MachineDeletePreview preview;
        try
        {
            preview = await _machinesCapabilityService.GetDeletePreviewAsync(_workspace.SelectedMachine);
        }
        catch (Exception ex)
        {
            _host.SetMachinesStatus($"Failed to evaluate delete policy/classification. {ex.Message}");
            return;
        }

        MachineDeleteScope? selectedScope;
        var policyCanAutoSelectScope = preview.PolicyMode != MachineDeletionPolicyMode.AskEveryTime &&
            preview.DefaultScope == MachineDeleteScope.VmAndStorage &&
            preview.SafeForAutomaticStorageDeletion;

        if (policyCanAutoSelectScope)
        {
            var confirmed = await _host.ShowDeleteConfirmationDialogAsync(_workspace.SelectedMachine, preview, preview.DefaultScope);
            selectedScope = confirmed ? preview.DefaultScope : null;
        }
        else
        {
            selectedScope = await _host.ShowDeleteScopeDialogAsync(_workspace.SelectedMachine, preview);
        }

        if (selectedScope is null)
        {
            _host.SetMachinesStatus("Delete cancelled.");
            return;
        }

        await RunSelectedMachineOperationAsync(
            "Deleting VM...",
            vm => _machinesCapabilityService.DeleteVmAsync(vm, selectedScope.Value),
            refreshInventory: true,
            clearSelectionOnSuccess: true);
    }

    public async Task ApplySelectedMachineEditsAsync()
    {
        if (_workspace.SelectedMachine is null || _workspace.EditDraft is null || _workspace.LoadedEditSnapshot is null)
        {
            _host.SetMachinesStatus("Select a VM and modify values before Apply.");
            return;
        }

        if (!_workspace.HasEditChanges)
        {
            _host.SetMachinesStatus("No pending machine edits.");
            return;
        }

        _workspace.IsMachineEditApplying = true;
        _host.UpdateMachineActionButtons();
        try
        {
            var result = await _machinesCapabilityService.ApplyEditsAsync(_workspace.SelectedMachine, _workspace.EditDraft);
            _host.SetMachinesStatus($"{result.UserMessage} (operationId: {result.OperationId})");
            if (!result.Success)
            {
                return;
            }

            await LoadMachineEditStateAsync();
            await EnsureInventoryAsync(forceRefresh: true);
        }
        finally
        {
            _workspace.IsMachineEditApplying = false;
            _host.UpdateMachineActionButtons();
        }
    }

    public async Task RefreshRdpReadinessAsync(bool selectedOnly)
    {
        if (!_host.IsMachinesOverviewActive || _workspace.Inventory.Count == 0)
        {
            return;
        }

        if (_workspace.IsRdpReadinessRefreshRunning)
        {
            return;
        }

        var candidates = selectedOnly && _workspace.SelectedMachine is not null
            ? [_workspace.SelectedMachine]
            : _workspace.Inventory.ToList();

        _workspace.IsRdpReadinessRefreshRunning = true;
        _workspace.LastRdpReadinessRefreshUtc = DateTimeOffset.UtcNow;

        try
        {
            foreach (var vm in candidates)
            {
                SetRdpReadiness(vm, new MachineRdpReadinessResult
                {
                    State = MachineRdpReadinessState.Checking,
                    ReasonCode = MachineRdpReadinessReasonCodes.CheckFailed,
                    Message = "Checking RDP readiness..."
                });
            }

            foreach (var vm in candidates)
            {
                var readiness = await _machinesCapabilityService.EvaluateRdpReadinessAsync(vm, CancellationToken.None);
                SetRdpReadiness(vm, readiness);
            }
        }
        finally
        {
            _workspace.IsRdpReadinessRefreshRunning = false;
        }
    }

    private async Task RunSelectedMachineOperationAsync(
        string pendingMessage,
        Func<MachineInventoryItem, Task<MachineOperationResult>> operation,
        bool refreshInventory,
        bool clearSelectionOnSuccess = false)
    {
        if (_workspace.SelectedMachine is null)
        {
            _host.SetMachinesStatus("Select a VM before running actions.");
            return;
        }

        _workspace.IsMachineActionRunning = true;
        _host.UpdateMachineActionButtons();
        _host.SetMachinesStatus(pendingMessage);

        var vm = _workspace.SelectedMachine;
        try
        {
            var result = await operation(vm);
            _host.SetMachinesStatus($"{result.UserMessage} (operationId: {result.OperationId})");

            if (result.Success && clearSelectionOnSuccess)
            {
                _workspace.SelectedMachine = null;
                _host.SetSelectedMachineInView(null);
            }

            if (refreshInventory)
            {
                var refreshSucceeded = await EnsureInventoryAsync(forceRefresh: true);
                if (!refreshSucceeded)
                {
                    _host.SetMachinesStatus($"{result.UserMessage} Inventory refresh failed; showing last known list.");
                }
                else if (_workspace.SelectedMachine is not null)
                {
                    await RefreshRdpReadinessAsync(selectedOnly: true);
                }
            }
            else
            {
                _host.UpdateMachineDetails();
            }
        }
        finally
        {
            _workspace.IsMachineActionRunning = false;
            _host.UpdateMachineActionButtons();
        }
    }

    private async Task LoadMachineEditStateAsync()
    {
        if (!_host.IsMachinesOverviewActive || _workspace.SelectedMachine is null)
        {
            _host.ClearMachineEditControls();
            return;
        }

        _workspace.IsMachineEditLoading = true;
        _host.UpdateMachineActionButtons();
        try
        {
            var snapshot = await _machinesCapabilityService.LoadEditSnapshotAsync(_workspace.SelectedMachine);
            _workspace.AvailableSwitches = await _machinesCapabilityService.LoadVirtualSwitchesAsync();
            _workspace.LoadedEditSnapshot = snapshot;
            if (snapshot is null)
            {
                _workspace.EditDraft = null;
                _host.ClearMachineEditControls();
                _host.SetMachinesStatus("Unable to load editable VM settings.");
                return;
            }

            _workspace.EditDraft = CreateDraft(snapshot, Array.Empty<string>());
            _host.ApplyMachineEditDraftToControls();
        }
        catch (Exception ex)
        {
            _workspace.EditDraft = null;
            _workspace.LoadedEditSnapshot = null;
            _host.ClearMachineEditControls();
            _host.SetMachinesStatus($"Failed to load VM edit state. {ex.Message}");
        }
        finally
        {
            _workspace.IsMachineEditLoading = false;
            _host.UpdateMachineActionButtons();
        }
    }

    private void SyncRdpReadinessCache()
    {
        var activeVmKeys = _workspace.Inventory.Select(GetVmReadinessKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var staleKeys = _workspace.RdpReadinessByVmKey.Keys.Where(vmKey => !activeVmKeys.Contains(vmKey)).ToList();
        foreach (var vmKey in staleKeys)
        {
            _workspace.RdpReadinessByVmKey.Remove(vmKey);
        }

        foreach (var vm in _workspace.Inventory)
        {
            var vmKey = GetVmReadinessKey(vm);
            if (!_workspace.RdpReadinessByVmKey.ContainsKey(vmKey))
            {
                _workspace.RdpReadinessByVmKey[vmKey] = CreateUnknownReadiness("RDP readiness has not been checked yet.");
            }
        }

        UpdateSelectedRdpReadinessFromCache();
        _host.UpdateRdpReadinessUi();
    }

    private void SetRdpReadiness(MachineInventoryItem vm, MachineRdpReadinessResult readiness)
    {
        var vmKey = GetVmReadinessKey(vm);
        _workspace.RdpReadinessByVmKey[vmKey] = readiness;

        if (_workspace.SelectedMachine is not null &&
            string.Equals(GetVmReadinessKey(_workspace.SelectedMachine), vmKey, StringComparison.OrdinalIgnoreCase))
        {
            _workspace.SelectedRdpReadiness = readiness;
            _host.UpdateRdpReadinessUi();
            _host.UpdateMachineActionButtons();
        }
    }

    private void UpdateSelectedRdpReadinessFromCache()
    {
        if (_workspace.SelectedMachine is null)
        {
            _workspace.SelectedRdpReadiness = CreateUnknownReadiness("Select a VM to check RDP readiness.");
            return;
        }

        var selectedKey = GetVmReadinessKey(_workspace.SelectedMachine);
        if (_workspace.RdpReadinessByVmKey.TryGetValue(selectedKey, out var readiness))
        {
            _workspace.SelectedRdpReadiness = readiness;
            return;
        }

        _workspace.SelectedRdpReadiness = CreateUnknownReadiness("RDP readiness has not been checked yet.");
    }

    private static MachineEditDraft CreateDraft(MachineEditSnapshot snapshot, IReadOnlyList<string> changedFields)
    {
        return new MachineEditDraft
        {
            CpuCount = snapshot.CpuCount,
            StartupMemoryMb = snapshot.StartupMemoryMb,
            DynamicMemoryEnabled = snapshot.DynamicMemoryEnabled,
            MinimumMemoryMb = snapshot.MinimumMemoryMb,
            MaximumMemoryMb = snapshot.MaximumMemoryMb,
            MemoryBufferPercent = snapshot.MemoryBufferPercent,
            NetworkAdapters = snapshot.NetworkAdapters
                .Select(adapter => new MachineNetworkAdapterConfig
                {
                    AdapterName = adapter.AdapterName,
                    SwitchName = adapter.SwitchName
                })
                .ToList(),
            ChangedFieldKeys = changedFields
        };
    }

    private static MachineRdpReadinessResult CreateUnknownReadiness(string message)
    {
        return new MachineRdpReadinessResult
        {
            State = MachineRdpReadinessState.Unknown,
            ReasonCode = MachineRdpReadinessReasonCodes.CheckFailed,
            Message = message
        };
    }

    private static string GetVmReadinessKey(MachineInventoryItem vm)
    {
        if (!string.IsNullOrWhiteSpace(vm.VmId))
        {
            return vm.VmId;
        }

        return vm.VmName;
    }
}
