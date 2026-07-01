using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabAssistant.Business.Machines;
using LabAssistant.WinUI.Infrastructure;

namespace LabAssistant.WinUI.ViewModels.Machines;

public sealed class MachineDetailState
{
    public MachineDetailState(string vmName, string state, string originLabel, string vmId, string vmPath)
    {
        VmName = vmName;
        State = state;
        OriginLabel = originLabel;
        VmId = vmId;
        VmPath = vmPath;
    }

    public string VmName { get; }

    public string State { get; }

    public string OriginLabel { get; }

    public string VmId { get; }

    public string VmPath { get; }
}

public partial class MachineNetworkAdapterEditorItem : ObservableObject
{
    public MachineNetworkAdapterEditorItem(string adapterName, IEnumerable<string> switchOptions, string? selectedSwitchName)
    {
        _adapterName = adapterName;
        _switchOptions = new ObservableCollection<string>(switchOptions);
        _selectedSwitchName = string.IsNullOrWhiteSpace(selectedSwitchName)
            ? MachinesViewModel.DisconnectedSwitchLabel
            : selectedSwitchName;
    }

    [ObservableProperty]
    private string _adapterName;

    [ObservableProperty]
    private ObservableCollection<string> _switchOptions;

    [ObservableProperty]
    private string _selectedSwitchName;
}

public partial class MachinesViewModel : ViewModelBase
{
    internal const string DisconnectedSwitchLabel = "(Disconnected)";
    private const string DefaultStatusMessage = "Select a VM to run actions.";
    private const string UnknownRdpMessage = "RDP readiness unknown.";

    private readonly IMachinesCapabilityService _machinesService;
    private readonly Dictionary<string, MachineInventoryItem> _inventoryByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MachineRdpReadinessResult> _rdpReadinessByVmKey = new(StringComparer.OrdinalIgnoreCase);

    private IMachinesCapabilityShellBridge? _shellBridge;
    private MachineEditSnapshot? _loadedEditSnapshot;
    private MachineEditDraft? _editDraft;
    private IReadOnlyList<string> _availableSwitches = Array.Empty<string>();
    private MachineRdpReadinessResult _selectedRdpReadiness = CreateUnknownReadiness(UnknownRdpMessage);
    private bool _isSyncingEditFields;
    private bool _suppressSelectionChanged;
    private bool _isMachineEditLoading;
    private bool _isRdpRefreshRunning;
    private bool _isPropertyChangedHooked;
    private int _selectionRevision;

    public MachinesViewModel(IMachinesCapabilityService machinesService)
    {
        _machinesService = machinesService;
        EnsurePropertyChangedSubscription();
    }

    [ObservableProperty]
    private ObservableCollection<MachineListItem> _machines = new();

    [ObservableProperty]
    private MachineListItem? _selectedMachine;

    [ObservableProperty]
    private MachineDetailState? _selectedDetail;

    [ObservableProperty]
    private bool _hasInventory;

    [ObservableProperty]
    private string _statusMessage = DefaultStatusMessage;

    [ObservableProperty]
    private string _cpuCount = string.Empty;

    [ObservableProperty]
    private string _startupMemory = string.Empty;

    [ObservableProperty]
    private bool _isDynamicMemory;

    [ObservableProperty]
    private string _minMemory = string.Empty;

    [ObservableProperty]
    private string _maxMemory = string.Empty;

    [ObservableProperty]
    private string _memoryBuffer = string.Empty;

    [ObservableProperty]
    private bool _hasDirtyEdits;

    [ObservableProperty]
    private string _rdpReadinessText = UnknownRdpMessage;

    public ObservableCollection<MachineNetworkAdapterEditorItem> NetworkAdapters { get; } = new();

    public bool HasSelection => SelectedMachine is not null && SelectedDetail is not null;

    public bool ShowSelectionHint => !HasSelection;

    public bool ShowEmptyState => !IsLoading && !HasInventory;

    public bool CanRefresh => !IsLoading;

    public bool CanRunMachineActions => SelectedMachine is not null && !IsLoading && !_isMachineEditLoading;

    public bool CanOpenRdp => CanRunMachineActions && _selectedRdpReadiness.State == MachineRdpReadinessState.Ready;

    public bool CanSaveChanges => SelectedMachine is not null && HasDirtyEdits && !IsLoading && !_isMachineEditLoading;

    public bool CanEditMachine => SelectedMachine is not null && !IsLoading && !_isMachineEditLoading;

    public bool IsDynamicMemoryEditorEnabled => IsDynamicMemory;

    public double DynamicMemoryPanelOpacity => IsDynamicMemory ? 1.0 : 0.65;

    public string SelectedVmNameText => SelectedDetail is null ? "Name: (none)" : $"Name: {SelectedDetail.VmName}";

    public string SelectedVmStateText => SelectedDetail is null ? "State: -" : $"State: {SelectedDetail.State}";

    public string SelectedVmOriginText => SelectedDetail is null ? "Origin: -" : $"Origin: {SelectedDetail.OriginLabel}";

    public string SelectedVmIdText => SelectedDetail is null ? "VM Id: -" : $"VM Id: {SelectedDetail.VmId}";

    public string SelectedVmPathText => SelectedDetail is null ? "Path: -" : $"Path: {SelectedDetail.VmPath}";

    public string MinimumMemoryText
    {
        get => MinMemory;
        set => MinMemory = value;
    }

    public string MaximumMemoryText
    {
        get => MaxMemory;
        set => MaxMemory = value;
    }

    public string MemoryBufferText
    {
        get => MemoryBuffer;
        set => MemoryBuffer = value;
    }

    public DateTimeOffset LastRdpReadinessRefreshUtc { get; private set; } = DateTimeOffset.MinValue;

    public DateTimeOffset LastOnDemandRdpRefreshUtc { get; private set; } = DateTimeOffset.MinValue;

    public override async Task InitializeAsync(object? parameter = null, CancellationToken cancellationToken = default)
    {
        if (IsInitialized)
        {
            return;
        }

        EnsurePropertyChangedSubscription();
        await EnsureInventoryAsync(forceRefresh: true, cancellationToken);
        IsInitialized = true;
    }

    public override async Task CleanupAsync()
    {
        await base.CleanupAsync();
        _shellBridge = null;
        _isMachineEditLoading = false;
        _isRdpRefreshRunning = false;
        _selectionRevision++;
        ClearWorkspaceState(DefaultStatusMessage);
        ReleasePropertyChangedSubscription();
        IsInitialized = false;
    }

    internal void AttachShellBridge(IMachinesCapabilityShellBridge shellBridge)
    {
        _shellBridge = shellBridge;
        UpdateShellPollingState();
    }

    public void ApplyShellState()
    {
        RaiseComputedStateChanged();
        UpdateShellPollingState();
    }

    public void DiscardEditDraft()
    {
        if (_loadedEditSnapshot is null)
        {
            ClearEditFields();
            return;
        }

        ApplySnapshotToEditors(_loadedEditSnapshot, _availableSwitches);
    }

    public Task EnsureInventoryAsync(bool forceRefresh)
    {
        return EnsureInventoryAsync(forceRefresh, LifecycleToken);
    }

    private Task EnsureInventoryAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        return RefreshInventoryCoreAsync(forceRefresh, cancellationToken);
    }

    public Task RefreshRdpReadinessAsync(bool selectedOnly)
    {
        return RefreshRdpReadinessAsync(selectedOnly, LifecycleToken);
    }

    private async Task RefreshRdpReadinessAsync(bool selectedOnly, CancellationToken cancellationToken)
    {
        if (!HasInventory || _isRdpRefreshRunning)
        {
            return;
        }

        var candidates = selectedOnly && SelectedMachine is not null
            ? TryGetInventoryItem(SelectedMachine, out var selectedVm) ? [selectedVm] : []
            : _inventoryByKey.Values.OrderBy(vm => vm.VmName, StringComparer.OrdinalIgnoreCase).ToList();

        if (candidates.Count == 0)
        {
            return;
        }

        _isRdpRefreshRunning = true;
        LastRdpReadinessRefreshUtc = DateTimeOffset.UtcNow;
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(LifecycleToken, cancellationToken);
        try
        {
            foreach (var vm in candidates)
            {
                linkedCts.Token.ThrowIfCancellationRequested();
                SetRdpReadiness(vm, new MachineRdpReadinessResult
                {
                    State = MachineRdpReadinessState.Checking,
                    ReasonCode = MachineRdpReadinessReasonCodes.CheckFailed,
                    Message = "Checking RDP readiness..."
                });
            }

            foreach (var vm in candidates)
            {
                var readiness = await _machinesService.EvaluateRdpReadinessAsync(vm, linkedCts.Token);
                SetRdpReadiness(vm, readiness);
            }
        }
        catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
        {
            return;
        }
        finally
        {
            _isRdpRefreshRunning = false;
            RaiseComputedStateChanged();
        }
    }

    [RelayCommand]
    private Task RefreshAsync()
    {
        return EnsureInventoryAsync(forceRefresh: true, LifecycleToken);
    }

    [RelayCommand]
    private Task StartVmAsync()
    {
        return RunSelectedMachineOperationAsync(
            "Starting VM...",
            (vm, _) => _machinesService.StartVmAsync(vm),
            refreshInventory: true);
    }

    [RelayCommand]
    private Task StopVmAsync()
    {
        return RunSelectedMachineOperationAsync(
            "Stopping VM...",
            (vm, _) => _machinesService.StopVmAsync(vm),
            refreshInventory: true);
    }

    [RelayCommand]
    private Task RestartVmAsync()
    {
        return RunSelectedMachineOperationAsync(
            "Restarting VM...",
            (vm, _) => _machinesService.RestartVmAsync(vm),
            refreshInventory: true);
    }

    [RelayCommand]
    private Task OpenConsoleAsync()
    {
        return RunSelectedMachineOperationAsync(
            "Opening Hyper-V Console...",
            (vm, _) => _machinesService.OpenConsoleAsync(vm),
            refreshInventory: false);
    }

    [RelayCommand]
    private async Task OpenRdpAsync()
    {
        if (!TryGetSelectedInventoryItem(out var vm))
        {
            StatusMessage = DefaultStatusMessage;
            return;
        }

        if (_selectedRdpReadiness.State != MachineRdpReadinessState.Ready ||
            string.IsNullOrWhiteSpace(_selectedRdpReadiness.TargetIpv4))
        {
            StatusMessage = $"RDP not ready. {_selectedRdpReadiness.Message}";
            if (DateTimeOffset.UtcNow - LastOnDemandRdpRefreshUtc >= TimeSpan.FromSeconds(2))
            {
                LastOnDemandRdpRefreshUtc = DateTimeOffset.UtcNow;
                await RefreshRdpReadinessAsync(selectedOnly: true, LifecycleToken);
            }

            return;
        }

        await RunSelectedMachineOperationAsync(
            "Opening RDP...",
            (selectedVm, _) => _machinesService.OpenRdpAsync(selectedVm, _selectedRdpReadiness.TargetIpv4!),
            refreshInventory: false);
    }

    [RelayCommand]
    private async Task DeleteVmAsync()
    {
        if (!TryGetSelectedInventoryItem(out var vm))
        {
            StatusMessage = DefaultStatusMessage;
            return;
        }

        MachineDeletePreview preview;
        try
        {
            preview = await _machinesService.GetDeletePreviewAsync(vm);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusMessage = $"Failed to evaluate delete policy/classification. {ex.Message}";
            return;
        }

        if (_shellBridge is null)
        {
            StatusMessage = "Delete UI is unavailable.";
            return;
        }

        MachineDeleteScope? selectedScope;
        var policyCanAutoSelectScope = preview.PolicyMode != MachineDeletionPolicyMode.AskEveryTime &&
            preview.DefaultScope == MachineDeleteScope.VmAndStorage &&
            preview.SafeForAutomaticStorageDeletion;

        if (policyCanAutoSelectScope)
        {
            var confirmed = await _shellBridge.ShowDeleteConfirmationDialogAsync(vm, preview, preview.DefaultScope);
            selectedScope = confirmed ? preview.DefaultScope : null;
        }
        else
        {
            selectedScope = await _shellBridge.ShowDeleteScopeDialogAsync(vm, preview);
        }

        if (selectedScope is null)
        {
            StatusMessage = "Delete cancelled.";
            return;
        }

        await RunSelectedMachineOperationAsync(
            "Deleting VM...",
            (selectedVm, _) => _machinesService.DeleteVmAsync(selectedVm, selectedScope.Value),
            refreshInventory: true,
            clearSelectionOnSuccess: true);
    }

    [RelayCommand]
    private async Task SaveChangesAsync()
    {
        if (!TryGetSelectedInventoryItem(out var vm) || _loadedEditSnapshot is null)
        {
            StatusMessage = "Select a VM and modify values before Apply.";
            return;
        }

        if (!TryBuildCurrentDraft(out var draft))
        {
            StatusMessage = "Enter valid machine settings before saving.";
            return;
        }

        _editDraft = CreateDraft(draft, ComputeChangedFields(_loadedEditSnapshot, draft));
        HasDirtyEdits = _editDraft.ChangedFieldKeys.Count > 0;
        if (!HasDirtyEdits)
        {
            StatusMessage = "No pending machine edits.";
            return;
        }

        await ExecuteWithLoadingAsync(async (ct) =>
        {
            StatusMessage = "Applying machine changes...";
            var result = await _machinesService.ApplyEditsAsync(vm, _editDraft);
            StatusMessage = $"{result.UserMessage} (operationId: {result.OperationId})";
            if (!result.Success)
            {
                return;
            }

            await RefreshInventoryCoreAsync(forceRefresh: true, ct);
            await LoadSelectedMachineStateAsync(SelectedMachine, ++_selectionRevision, ct);
        }, LifecycleToken);

        if (!string.IsNullOrWhiteSpace(ErrorMessage))
        {
            StatusMessage = $"Failed to apply machine changes. {ErrorMessage}";
        }
    }

    partial void OnSelectedMachineChanged(MachineListItem? value)
    {
        if (_suppressSelectionChanged)
        {
            return;
        }

        ApplySelectionState(value, updateNoSelectionStatus: true);
        _ = LoadSelectedMachineStateAsync(value, ++_selectionRevision, LifecycleToken);
    }

    partial void OnCpuCountChanged(string value) => RefreshDraftFromEditors();

    partial void OnSelectedDetailChanged(MachineDetailState? value)
    {
        OnPropertyChanged(nameof(SelectedVmNameText));
        OnPropertyChanged(nameof(SelectedVmStateText));
        OnPropertyChanged(nameof(SelectedVmOriginText));
        OnPropertyChanged(nameof(SelectedVmIdText));
        OnPropertyChanged(nameof(SelectedVmPathText));
    }

    partial void OnStartupMemoryChanged(string value) => RefreshDraftFromEditors();

    partial void OnIsDynamicMemoryChanged(bool value)
    {
        OnPropertyChanged(nameof(IsDynamicMemoryEditorEnabled));
        OnPropertyChanged(nameof(DynamicMemoryPanelOpacity));
        RefreshDraftFromEditors();
    }

    partial void OnMinMemoryChanged(string value)
    {
        OnPropertyChanged(nameof(MinimumMemoryText));
        RefreshDraftFromEditors();
    }

    partial void OnMaxMemoryChanged(string value)
    {
        OnPropertyChanged(nameof(MaximumMemoryText));
        RefreshDraftFromEditors();
    }

    partial void OnMemoryBufferChanged(string value)
    {
        OnPropertyChanged(nameof(MemoryBufferText));
        RefreshDraftFromEditors();
    }

    partial void OnHasDirtyEditsChanged(bool value) => RaiseComputedStateChanged();

    partial void OnHasInventoryChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowEmptyState));
        UpdateShellPollingState();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IsLoading))
        {
            RaiseComputedStateChanged();
        }
    }

    private void EnsurePropertyChangedSubscription()
    {
        if (_isPropertyChangedHooked)
        {
            return;
        }

        PropertyChanged += OnViewModelPropertyChanged;
        _isPropertyChangedHooked = true;
    }

    private void ReleasePropertyChangedSubscription()
    {
        if (!_isPropertyChangedHooked)
        {
            return;
        }

        PropertyChanged -= OnViewModelPropertyChanged;
        _isPropertyChangedHooked = false;
    }

    private async Task RefreshInventoryCoreAsync(bool forceRefresh, CancellationToken cancellationToken = default)
    {
        if (!forceRefresh && HasInventory)
        {
            return;
        }

        var selectedVmKey = SelectedMachine is null ? null : GetVmKey(SelectedMachine);
        await ExecuteWithLoadingAsync(async (ct) =>
        {
            StatusMessage = "Loading host VM inventory...";
            var inventory = await _machinesService.LoadInventoryAsync();
            var orderedInventory = inventory
                .OrderBy(vm => vm.VmName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _inventoryByKey.Clear();
            foreach (var vm in orderedInventory)
            {
                _inventoryByKey[GetVmKey(vm)] = vm;
            }

            Machines = new ObservableCollection<MachineListItem>(
                orderedInventory.Select(vm => new MachineListItem(vm.VmName, vm.State, vm.OriginLabel, vm.VmId)));
            HasInventory = Machines.Count > 0;

            SyncRdpReadinessCache();

            var nextSelection = selectedVmKey is null
                ? null
                : Machines.FirstOrDefault(machine => string.Equals(GetVmKey(machine), selectedVmKey, StringComparison.OrdinalIgnoreCase));

            SetSelectedMachineSilently(nextSelection);
            ApplySelectionState(nextSelection, updateNoSelectionStatus: false);

            StatusMessage = HasInventory
                ? $"Loaded {Machines.Count} VM(s)."
                : "No Hyper-V VMs found on this host.";

            await LoadSelectedMachineStateAsync(nextSelection, ++_selectionRevision, ct);
        }, cancellationToken);

        if (!string.IsNullOrWhiteSpace(ErrorMessage))
        {
            StatusMessage = $"Failed to load VM inventory. Showing last known list. {ErrorMessage}";
        }
    }

    private async Task RunSelectedMachineOperationAsync(
        string pendingMessage,
        Func<MachineInventoryItem, CancellationToken, Task<MachineOperationResult>> operation,
        bool refreshInventory,
        bool clearSelectionOnSuccess = false)
    {
        if (!TryGetSelectedInventoryItem(out var vm))
        {
            StatusMessage = DefaultStatusMessage;
            return;
        }

        await ExecuteWithLoadingAsync(async (ct) =>
        {
            StatusMessage = pendingMessage;
            var result = await operation(vm, ct);
            StatusMessage = $"{result.UserMessage} (operationId: {result.OperationId})";
            if (!result.Success)
            {
                return;
            }

            if (clearSelectionOnSuccess)
            {
                SetSelectedMachineSilently(null);
                ApplySelectionState(null, updateNoSelectionStatus: false);
            }

            if (refreshInventory)
            {
                await RefreshInventoryCoreAsync(forceRefresh: true, ct);
                if (SelectedMachine is not null)
                {
                    await RefreshRdpReadinessAsync(selectedOnly: true, ct);
                }
            }
        }, LifecycleToken);

        if (!string.IsNullOrWhiteSpace(ErrorMessage))
        {
            StatusMessage = $"{pendingMessage.TrimEnd('.')} failed. {ErrorMessage}";
        }
    }

    private async Task LoadSelectedMachineStateAsync(MachineListItem? selectedMachine, int revision, CancellationToken cancellationToken = default)
    {
        if (selectedMachine is null || !TryGetInventoryItem(selectedMachine, out var vm))
        {
            _loadedEditSnapshot = null;
            _editDraft = null;
            ClearEditFields();
            return;
        }

        _isMachineEditLoading = true;
        RaiseComputedStateChanged();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await _machinesService.LoadEditSnapshotAsync(vm);
            cancellationToken.ThrowIfCancellationRequested();
            var availableSwitches = await _machinesService.LoadVirtualSwitchesAsync();
            cancellationToken.ThrowIfCancellationRequested();
            if (revision != _selectionRevision || !IsCurrentSelection(selectedMachine))
            {
                return;
            }

            _loadedEditSnapshot = snapshot;
            _availableSwitches = availableSwitches;
            if (snapshot is null)
            {
                _editDraft = null;
                ClearEditFields();
                StatusMessage = "Unable to load editable VM settings.";
                return;
            }

            ApplySnapshotToEditors(snapshot, availableSwitches);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            if (revision != _selectionRevision || !IsCurrentSelection(selectedMachine))
            {
                return;
            }

            ErrorMessage = ex.Message;
            _loadedEditSnapshot = null;
            _editDraft = null;
            ClearEditFields();
            StatusMessage = $"Failed to load VM edit state. {ex.Message}";
        }
        finally
        {
            if (revision == _selectionRevision)
            {
                _isMachineEditLoading = false;
                RaiseComputedStateChanged();
            }
        }
    }

    private void ApplySelectionState(MachineListItem? selection, bool updateNoSelectionStatus)
    {
        SelectedDetail = selection is not null && TryGetInventoryItem(selection, out var vm)
            ? new MachineDetailState(
                vm.VmName,
                vm.State,
                vm.OriginLabel,
                vm.VmId,
                string.IsNullOrWhiteSpace(vm.VmPath) ? "-" : vm.VmPath)
            : null;

        UpdateSelectedReadinessFromCache(selection);
        ClearEditFields();

        if (selection is null && updateNoSelectionStatus)
        {
            StatusMessage = HasInventory ? DefaultStatusMessage : "No Hyper-V VMs found on this host.";
        }

        RaiseComputedStateChanged();
    }

    private void ApplySnapshotToEditors(MachineEditSnapshot snapshot, IReadOnlyList<string> availableSwitches)
    {
        _isSyncingEditFields = true;
        try
        {
            _availableSwitches = availableSwitches;
            CpuCount = snapshot.CpuCount.ToString();
            StartupMemory = snapshot.StartupMemoryMb.ToString();
            IsDynamicMemory = snapshot.DynamicMemoryEnabled;
            MinMemory = snapshot.MinimumMemoryMb.ToString();
            MaxMemory = snapshot.MaximumMemoryMb.ToString();
            MemoryBuffer = snapshot.MemoryBufferPercent.ToString();

            foreach (var adapter in NetworkAdapters)
            {
                adapter.PropertyChanged -= OnNetworkAdapterChanged;
            }

            NetworkAdapters.Clear();
            foreach (var adapter in snapshot.NetworkAdapters)
            {
                var editor = new MachineNetworkAdapterEditorItem(
                    adapter.AdapterName,
                    [DisconnectedSwitchLabel, .. availableSwitches],
                    adapter.SwitchName);
                editor.PropertyChanged += OnNetworkAdapterChanged;
                NetworkAdapters.Add(editor);
            }

            _editDraft = CreateDraft(snapshot, Array.Empty<string>());
            HasDirtyEdits = false;
        }
        finally
        {
            _isSyncingEditFields = false;
            RaiseComputedStateChanged();
        }
    }

    private void ClearEditFields()
    {
        _isSyncingEditFields = true;
        try
        {
            CpuCount = string.Empty;
            StartupMemory = string.Empty;
            IsDynamicMemory = false;
            MinMemory = string.Empty;
            MaxMemory = string.Empty;
            MemoryBuffer = string.Empty;

            foreach (var adapter in NetworkAdapters)
            {
                adapter.PropertyChanged -= OnNetworkAdapterChanged;
            }

            NetworkAdapters.Clear();
            HasDirtyEdits = false;
        }
        finally
        {
            _isSyncingEditFields = false;
            RaiseComputedStateChanged();
        }
    }

    private void RefreshDraftFromEditors()
    {
        if (_isSyncingEditFields || _loadedEditSnapshot is null)
        {
            return;
        }

        if (!TryBuildCurrentDraft(out var draft))
        {
            RaiseComputedStateChanged();
            return;
        }

        _editDraft = CreateDraft(draft, ComputeChangedFields(_loadedEditSnapshot, draft));
        HasDirtyEdits = _editDraft.ChangedFieldKeys.Count > 0;
    }

    private bool TryBuildCurrentDraft(out MachineEditDraft draft)
    {
        draft = new MachineEditDraft();
        if (!int.TryParse(CpuCount, out var cpuCount) ||
            !long.TryParse(StartupMemory, out var startupMemoryMb) ||
            !long.TryParse(MinMemory, out var minMemoryMb) ||
            !long.TryParse(MaxMemory, out var maxMemoryMb) ||
            !int.TryParse(MemoryBuffer, out var memoryBufferPercent))
        {
            return false;
        }

        draft = new MachineEditDraft
        {
            CpuCount = cpuCount,
            StartupMemoryMb = startupMemoryMb,
            DynamicMemoryEnabled = IsDynamicMemory,
            MinimumMemoryMb = minMemoryMb,
            MaximumMemoryMb = maxMemoryMb,
            MemoryBufferPercent = memoryBufferPercent,
            NetworkAdapters = NetworkAdapters
                .Select(adapter => new MachineNetworkAdapterConfig
                {
                    AdapterName = adapter.AdapterName,
                    SwitchName = NormalizeSwitchSelection(adapter.SelectedSwitchName)
                })
                .ToList()
        };
        return true;
    }

    private void SyncRdpReadinessCache()
    {
        var activeKeys = _inventoryByKey.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var staleKeys = _rdpReadinessByVmKey.Keys.Where(key => !activeKeys.Contains(key)).ToList();
        foreach (var staleKey in staleKeys)
        {
            _rdpReadinessByVmKey.Remove(staleKey);
        }

        foreach (var vm in _inventoryByKey.Values)
        {
            var key = GetVmKey(vm);
            if (!_rdpReadinessByVmKey.ContainsKey(key))
            {
                _rdpReadinessByVmKey[key] = CreateUnknownReadiness("RDP readiness has not been checked yet.");
            }
        }

        UpdateSelectedReadinessFromCache(SelectedMachine);
    }

    private void UpdateSelectedReadinessFromCache(MachineListItem? machine)
    {
        if (machine is null || !TryGetInventoryItem(machine, out var vm))
        {
            SetSelectedRdpReadiness(CreateUnknownReadiness("Select a VM to check RDP readiness."));
            return;
        }

        var key = GetVmKey(vm);
        if (_rdpReadinessByVmKey.TryGetValue(key, out var readiness))
        {
            SetSelectedRdpReadiness(readiness);
            return;
        }

        SetSelectedRdpReadiness(CreateUnknownReadiness("RDP readiness has not been checked yet."));
    }

    private void SetRdpReadiness(MachineInventoryItem vm, MachineRdpReadinessResult readiness)
    {
        var key = GetVmKey(vm);
        _rdpReadinessByVmKey[key] = readiness;
        if (SelectedMachine is not null && string.Equals(GetVmKey(SelectedMachine), key, StringComparison.OrdinalIgnoreCase))
        {
            SetSelectedRdpReadiness(readiness);
        }
    }

    private void SetSelectedRdpReadiness(MachineRdpReadinessResult readiness)
    {
        _selectedRdpReadiness = readiness;
        RdpReadinessText = readiness.State == MachineRdpReadinessState.Ready || readiness.State == MachineRdpReadinessState.Unknown
            ? readiness.Message
            : $"{readiness.Message} ({readiness.ReasonCode})";
        RaiseComputedStateChanged();
    }

    private void RaiseComputedStateChanged()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(ShowSelectionHint));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(CanRefresh));
        OnPropertyChanged(nameof(CanRunMachineActions));
        OnPropertyChanged(nameof(CanOpenRdp));
        OnPropertyChanged(nameof(CanSaveChanges));
        OnPropertyChanged(nameof(CanEditMachine));
        UpdateShellPollingState();
    }

    private void UpdateShellPollingState()
    {
        _shellBridge?.UpdateReadinessPollingState();
    }

    private void ClearWorkspaceState(string statusMessage)
    {
        _inventoryByKey.Clear();
        _rdpReadinessByVmKey.Clear();
        _availableSwitches = Array.Empty<string>();
        LastRdpReadinessRefreshUtc = DateTimeOffset.MinValue;
        LastOnDemandRdpRefreshUtc = DateTimeOffset.MinValue;
        SetSelectedMachineSilently(null);
        SelectedDetail = null;
        Machines = new ObservableCollection<MachineListItem>();
        HasInventory = false;
        _loadedEditSnapshot = null;
        _editDraft = null;
        ErrorMessage = null;
        StatusMessage = statusMessage;
        SetSelectedRdpReadiness(CreateUnknownReadiness(UnknownRdpMessage));
        ClearEditFields();
    }

    private void SetSelectedMachineSilently(MachineListItem? machine)
    {
        _suppressSelectionChanged = true;
        try
        {
            SelectedMachine = machine;
        }
        finally
        {
            _suppressSelectionChanged = false;
        }
    }

    private void OnNetworkAdapterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MachineNetworkAdapterEditorItem.SelectedSwitchName))
        {
            RefreshDraftFromEditors();
        }
    }

    private bool TryGetSelectedInventoryItem(out MachineInventoryItem vm)
    {
        vm = null!;
        return SelectedMachine is not null && TryGetInventoryItem(SelectedMachine, out vm);
    }

    private bool TryGetInventoryItem(MachineListItem machine, out MachineInventoryItem vm)
    {
        return _inventoryByKey.TryGetValue(GetVmKey(machine), out vm!);
    }

    private bool IsCurrentSelection(MachineListItem machine)
    {
        return SelectedMachine is not null &&
            string.Equals(GetVmKey(SelectedMachine), GetVmKey(machine), StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeSwitchSelection(string? switchName)
    {
        return string.Equals(switchName, DisconnectedSwitchLabel, StringComparison.Ordinal)
            ? null
            : switchName;
    }

    private static string GetVmKey(MachineInventoryItem vm)
    {
        return !string.IsNullOrWhiteSpace(vm.VmId) ? vm.VmId : vm.VmName;
    }

    private static string GetVmKey(MachineListItem vm)
    {
        return !string.IsNullOrWhiteSpace(vm.VmId) ? vm.VmId : vm.VmName;
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

    private static MachineEditDraft CreateDraft(MachineEditDraft draft, IReadOnlyList<string> changedFields)
    {
        return new MachineEditDraft
        {
            CpuCount = draft.CpuCount,
            StartupMemoryMb = draft.StartupMemoryMb,
            DynamicMemoryEnabled = draft.DynamicMemoryEnabled,
            MinimumMemoryMb = draft.MinimumMemoryMb,
            MaximumMemoryMb = draft.MaximumMemoryMb,
            MemoryBufferPercent = draft.MemoryBufferPercent,
            NetworkAdapters = draft.NetworkAdapters,
            ChangedFieldKeys = changedFields
        };
    }

    private static IReadOnlyList<string> ComputeChangedFields(MachineEditSnapshot baseline, MachineEditDraft draft)
    {
        var changed = new List<string>();
        if (baseline.CpuCount != draft.CpuCount)
        {
            changed.Add("cpuCount");
        }

        if (baseline.StartupMemoryMb != draft.StartupMemoryMb)
        {
            changed.Add("startupMemoryMb");
        }

        if (baseline.DynamicMemoryEnabled != draft.DynamicMemoryEnabled)
        {
            changed.Add("dynamicMemoryEnabled");
        }

        if (baseline.MinimumMemoryMb != draft.MinimumMemoryMb)
        {
            changed.Add("minimumMemoryMb");
        }

        if (baseline.MaximumMemoryMb != draft.MaximumMemoryMb)
        {
            changed.Add("maximumMemoryMb");
        }

        if (baseline.MemoryBufferPercent != draft.MemoryBufferPercent)
        {
            changed.Add("memoryBufferPercent");
        }

        foreach (var adapter in draft.NetworkAdapters)
        {
            var baselineAdapter = baseline.NetworkAdapters.FirstOrDefault(item =>
                string.Equals(item.AdapterName, adapter.AdapterName, StringComparison.OrdinalIgnoreCase));
            if (!string.Equals(
                    baselineAdapter?.SwitchName ?? string.Empty,
                    adapter.SwitchName ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase))
            {
                changed.Add($"switch:{adapter.AdapterName}");
            }
        }

        return changed;
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
}
