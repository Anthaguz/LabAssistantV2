using LabAssistant.Business.Machines;
using LabAssistant.WinUI.Views.Machines;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.ViewModels.Machines;

internal interface IMachinesWorkspaceShellBridge
{
    bool IsMachinesOverviewActive { get; }

    void UpdateReadinessPollingState();

    Task<MachineDeleteScope?> ShowDeleteScopeDialogAsync(MachineInventoryItem vm, MachineDeletePreview preview);

    Task<bool> ShowDeleteConfirmationDialogAsync(MachineInventoryItem vm, MachineDeletePreview preview, MachineDeleteScope effectiveScope);
}

internal sealed class MachinesWorkspaceShellBridge : IMachinesWorkspaceShellBridge
{
    private readonly Func<bool> _isMachinesOverviewActive;
    private readonly Action _updateReadinessPollingState;
    private readonly Func<XamlRoot?> _getXamlRoot;

    public MachinesWorkspaceShellBridge(
        Func<bool> isMachinesOverviewActive,
        Action updateReadinessPollingState,
        Func<XamlRoot?> getXamlRoot)
    {
        _isMachinesOverviewActive = isMachinesOverviewActive;
        _updateReadinessPollingState = updateReadinessPollingState;
        _getXamlRoot = getXamlRoot;
    }

    public bool IsMachinesOverviewActive => _isMachinesOverviewActive();

    public void UpdateReadinessPollingState() => _updateReadinessPollingState();

    public async Task<MachineDeleteScope?> ShowDeleteScopeDialogAsync(MachineInventoryItem vm, MachineDeletePreview preview)
    {
        var vmOnlyRadio = new RadioButton
        {
            Content = "VM registration only",
            IsChecked = preview.DefaultScope == MachineDeleteScope.VmRegistrationOnly
        };
        var vmAndStorageRadio = new RadioButton
        {
            IsChecked = preview.DefaultScope == MachineDeleteScope.VmAndStorage,
            Content = "VM + associated disks/files"
        };
        var confirmationCheck = new CheckBox
        {
            Content = $"I confirm I want to delete '{vm.VmName}'."
        };

        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = "Choose delete scope. This action is destructive.",
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(new TextBlock
        {
            Text = $"Policy: {preview.PolicyMode} â€” {preview.PolicyMessage}",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTextSecondaryBrush"]
        });

        foreach (var disk in preview.DiskClassifications)
        {
            content.Children.Add(new TextBlock
            {
                Text = $"Disk: {disk.DiskPath} | {disk.Classification} ({disk.Reason})",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTextSecondaryBrush"]
            });
        }

        content.Children.Add(vmOnlyRadio);
        content.Children.Add(vmAndStorageRadio);
        content.Children.Add(new TextBlock
        {
            Text = "If deleting with storage, associated disks/files will be removed where possible.",
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(confirmationCheck);

        var dialog = new ContentDialog
        {
            Title = "Delete VM",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            XamlRoot = _getXamlRoot(),
            Content = content
        };

        confirmationCheck.Checked += (_, _) => dialog.IsPrimaryButtonEnabled = true;
        confirmationCheck.Unchecked += (_, _) => dialog.IsPrimaryButtonEnabled = false;

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        return vmAndStorageRadio.IsChecked == true
            ? MachineDeleteScope.VmAndStorage
            : MachineDeleteScope.VmRegistrationOnly;
    }

    public async Task<bool> ShowDeleteConfirmationDialogAsync(MachineInventoryItem vm, MachineDeletePreview preview, MachineDeleteScope effectiveScope)
    {
        var scopeText = effectiveScope == MachineDeleteScope.VmAndStorage
            ? "VM + associated disks/files"
            : "VM registration only";
        var confirmationCheck = new CheckBox
        {
            Content = $"I confirm I want to delete '{vm.VmName}'."
        };

        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = $"Effective delete scope: {scopeText}",
            TextWrapping = TextWrapping.Wrap,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        content.Children.Add(new TextBlock
        {
            Text = $"Policy: {preview.PolicyMode} â€” {preview.PolicyMessage}",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTextSecondaryBrush"]
        });
        content.Children.Add(confirmationCheck);

        var dialog = new ContentDialog
        {
            Title = "Delete VM",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            XamlRoot = _getXamlRoot(),
            Content = content
        };

        confirmationCheck.Checked += (_, _) => dialog.IsPrimaryButtonEnabled = true;
        confirmationCheck.Unchecked += (_, _) => dialog.IsPrimaryButtonEnabled = false;

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }
}

internal sealed class MachinesWorkspaceComposition : IMachinesWorkspaceControllerHost
{
    private readonly MachinesOverviewView _view;
    private readonly MachinesWorkspaceViewModel _workspace = new();
    private readonly MachinesWorkspaceController _controller;
    private readonly IMachinesWorkspaceShellBridge _shellBridge;
    private bool _isUpdatingMachineSelection;

    public MachinesWorkspaceComposition(
        IMachinesCapabilityService machinesCapabilityService,
        MachinesOverviewView view,
        IMachinesWorkspaceShellBridge shellBridge)
    {
        _view = view;
        _shellBridge = shellBridge;
        _controller = new MachinesWorkspaceController(machinesCapabilityService, _workspace, this);

        _view.SetInventorySource(_workspace.Inventory);
        _view.SetStatusText(_workspace.StatusText);
        WireViewHandlers();
    }

    public bool HasInventory => _workspace.Inventory.Count > 0;

    public DateTimeOffset LastRdpReadinessRefreshUtc => _workspace.LastRdpReadinessRefreshUtc;

    public void ApplyShellState()
    {
        UpdateRdpReadinessUi();
        UpdateMachineActionButtons();
    }

    public void DiscardEditDraft()
    {
        _workspace.DiscardEditDraft();
        UpdateMachineEditDirtyIndicator();
    }

    public Task EnsureInventoryAsync(bool forceRefresh)
    {
        return _controller.EnsureInventoryAsync(forceRefresh);
    }

    public Task RefreshRdpReadinessAsync(bool selectedOnly)
    {
        return _controller.RefreshRdpReadinessAsync(selectedOnly);
    }

    private void WireViewHandlers()
    {
        _view.RefreshRequested += RefreshRequested;
        _view.SelectedMachineChanged += SelectedMachineChanged;
        _view.MachineEditChanged += MachineEditChanged;
        _view.ApplyMachineEditsRequested += ApplyMachineEditsRequested;
        _view.StartMachineRequested += StartMachineRequested;
        _view.StopMachineRequested += StopMachineRequested;
        _view.RestartMachineRequested += RestartMachineRequested;
        _view.OpenConsoleRequested += OpenConsoleRequested;
        _view.DeleteMachineRequested += DeleteMachineRequested;
        _view.OpenRdpRequested += OpenRdpRequested;
    }

    private async void RefreshRequested(object? sender, EventArgs e)
    {
        await _controller.EnsureInventoryAsync(forceRefresh: true);
    }

    private void SelectedMachineChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingMachineSelection)
        {
            return;
        }

        _ = _controller.HandleSelectionChangedAsync(_view.SelectedMachine);
    }

    private void MachineEditChanged(object? sender, EventArgs e)
    {
        UpdateMachineEditDraftFromControls();
    }

    private async void ApplyMachineEditsRequested(object? sender, EventArgs e)
    {
        await _controller.ApplySelectedMachineEditsAsync();
    }

    private async void StartMachineRequested(object? sender, EventArgs e)
    {
        await _controller.StartSelectedMachineAsync();
    }

    private async void StopMachineRequested(object? sender, EventArgs e)
    {
        await _controller.StopSelectedMachineAsync();
    }

    private async void RestartMachineRequested(object? sender, EventArgs e)
    {
        await _controller.RestartSelectedMachineAsync();
    }

    private async void OpenConsoleRequested(object? sender, EventArgs e)
    {
        await _controller.OpenSelectedMachineConsoleAsync();
    }

    private async void DeleteMachineRequested(object? sender, EventArgs e)
    {
        await _controller.DeleteSelectedMachineAsync();
    }

    private async void OpenRdpRequested(object? sender, EventArgs e)
    {
        await _controller.OpenSelectedMachineRdpAsync();
    }

    private void UpdateMachineDetails()
    {
        _view.UpdateMachineDetails(_workspace.SelectedMachine);
    }

    private void UpdateMachineActionButtons()
    {
        var hasSelection = _workspace.SelectedMachine is not null;
        var canRunActions = hasSelection && !_workspace.IsMachineActionRunning;
        _workspace.CanRunSelectedMachineActions = canRunActions;
        _workspace.CanOpenRdp = canRunActions && _workspace.SelectedRdpReadiness.State == MachineRdpReadinessState.Ready;
        _workspace.CanApplyEdits = _workspace.SelectedMachine is not null &&
            !_workspace.IsMachineActionRunning &&
            !_workspace.IsMachineEditLoading &&
            !_workspace.IsMachineEditApplying &&
            HasMachineEditChanges();
        _workspace.RdpTooltipText = _workspace.SelectedRdpReadiness.Message;

        _shellBridge.UpdateReadinessPollingState();
        _view.UpdateActionState(
            _workspace.IsInventoryRefreshing,
            canRunActions,
            _workspace.CanOpenRdp,
            _workspace.RdpTooltipText,
            _workspace.CanApplyEdits);
    }

    private void SetMachinesStatus(string message)
    {
        _workspace.StatusText = message;
        _view.SetStatusText(message);
    }

    private void ClearMachineEditControls()
    {
        _workspace.IsUpdatingMachineEditControls = true;
        _view.ClearEditControls();
        _workspace.IsUpdatingMachineEditControls = false;
        UpdateMachineEditDirtyIndicator();
    }

    private void ApplyMachineEditDraftToControls()
    {
        if (_workspace.EditDraft is null)
        {
            ClearMachineEditControls();
            return;
        }

        _workspace.IsUpdatingMachineEditControls = true;
        _view.ApplyEditDraft(_workspace.EditDraft, _workspace.AvailableSwitches);
        _workspace.IsUpdatingMachineEditControls = false;
        UpdateMachineEditDirtyIndicator();
    }

    private void UpdateMachineEditDraftFromControls()
    {
        if (_workspace.IsUpdatingMachineEditControls ||
            _workspace.EditDraft is null ||
            _workspace.LoadedEditSnapshot is null)
        {
            return;
        }

        var formValues = _view.CaptureEditFormValues();
        if (!TryParseLong(formValues.CpuCountText, out var cpu) ||
            !TryParseLong(formValues.StartupMemoryText, out var startupMb) ||
            !TryParseLong(formValues.MinimumMemoryText, out var minMb) ||
            !TryParseLong(formValues.MaximumMemoryText, out var maxMb) ||
            !TryParseInt(formValues.MemoryBufferText, out var buffer))
        {
            UpdateMachineEditDirtyIndicator();
            return;
        }

        var draft = new MachineEditDraft
        {
            CpuCount = (int)cpu,
            StartupMemoryMb = startupMb,
            DynamicMemoryEnabled = formValues.DynamicMemoryEnabled,
            MinimumMemoryMb = minMb,
            MaximumMemoryMb = maxMb,
            MemoryBufferPercent = buffer,
            NetworkAdapters = formValues.NetworkAdapters
                .Select(adapter => new MachineNetworkAdapterConfig
                {
                    AdapterName = adapter.AdapterName,
                    SwitchName = adapter.SwitchName
                })
                .ToList()
        };

        _workspace.EditDraft = CreateDraft(draft, ComputeChangedFields(_workspace.LoadedEditSnapshot, draft));
        UpdateMachineEditDirtyIndicator();
    }

    private bool HasMachineEditChanges()
    {
        return _workspace.HasEditChanges;
    }

    private void UpdateMachineEditDirtyIndicator()
    {
        _view.SetEditDirtyIndicator(HasMachineEditChanges());
        UpdateMachineActionButtons();
    }

    private void UpdateRdpReadinessUi()
    {
        _view.UpdateRdpReadiness(_workspace.SelectedRdpReadiness);
    }

    bool IMachinesWorkspaceControllerHost.IsMachinesOverviewActive => _shellBridge.IsMachinesOverviewActive;

    void IMachinesWorkspaceControllerHost.SetMachinesStatus(string message) => SetMachinesStatus(message);

    void IMachinesWorkspaceControllerHost.SetSelectedMachineInView(MachineInventoryItem? selectedMachine)
    {
        _isUpdatingMachineSelection = true;
        try
        {
            _view.SetSelectedMachine(selectedMachine);
        }
        finally
        {
            _isUpdatingMachineSelection = false;
        }
    }

    void IMachinesWorkspaceControllerHost.UpdateMachineDetails() => UpdateMachineDetails();

    void IMachinesWorkspaceControllerHost.UpdateRdpReadinessUi() => UpdateRdpReadinessUi();

    void IMachinesWorkspaceControllerHost.UpdateMachineActionButtons() => UpdateMachineActionButtons();

    void IMachinesWorkspaceControllerHost.ClearMachineEditControls() => ClearMachineEditControls();

    void IMachinesWorkspaceControllerHost.ApplyMachineEditDraftToControls() => ApplyMachineEditDraftToControls();

    void IMachinesWorkspaceControllerHost.UpdateMachineEditDirtyIndicator() => UpdateMachineEditDirtyIndicator();

    Task<MachineDeleteScope?> IMachinesWorkspaceControllerHost.ShowDeleteScopeDialogAsync(MachineInventoryItem vm, MachineDeletePreview preview) =>
        _shellBridge.ShowDeleteScopeDialogAsync(vm, preview);

    Task<bool> IMachinesWorkspaceControllerHost.ShowDeleteConfirmationDialogAsync(
        MachineInventoryItem vm,
        MachineDeletePreview preview,
        MachineDeleteScope effectiveScope) =>
        _shellBridge.ShowDeleteConfirmationDialogAsync(vm, preview, effectiveScope);

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
            var baselineAdapter = baseline.NetworkAdapters.FirstOrDefault(a =>
                string.Equals(a.AdapterName, adapter.AdapterName, StringComparison.OrdinalIgnoreCase));
            if (!string.Equals(baselineAdapter?.SwitchName ?? string.Empty, adapter.SwitchName ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                changed.Add($"switch:{adapter.AdapterName}");
            }
        }

        return changed;
    }

    private static bool TryParseLong(string? text, out long value)
    {
        return long.TryParse(text, out value);
    }

    private static bool TryParseInt(string? text, out int value)
    {
        return int.TryParse(text, out value);
    }
}
