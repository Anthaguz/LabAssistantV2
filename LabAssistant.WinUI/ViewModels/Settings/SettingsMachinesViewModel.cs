using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabAssistant.Business.Machines;
using LabAssistant.WinUI.Infrastructure;

namespace LabAssistant.WinUI.ViewModels.Settings;

/// <summary>
/// View model for the Settings > Machines subview. Owns the Machines deletion-policy selection and
/// persists it through <see cref="IMachinesCapabilityService"/>. Loads the current policy on
/// initialization and disables saving while a save is in flight (the async command's own run state).
/// This is the only Settings surface today; the broader Settings design is tracked separately.
/// </summary>
public sealed partial class SettingsMachinesViewModel : ViewModelBase
{
    private readonly IMachinesCapabilityService _machinesCapabilityService;

    [ObservableProperty]
    private SettingsDeletionPolicyOption? _selectedPolicy;

    [ObservableProperty]
    private string _statusText = "Policy not loaded.";

    public SettingsMachinesViewModel(IMachinesCapabilityService machinesCapabilityService)
    {
        _machinesCapabilityService = machinesCapabilityService;
    }

    /// <summary>
    /// The deletion-policy options offered in the selector, in the same order and with the same
    /// wording as the prior inline Settings panel so behavior is unchanged.
    /// </summary>
    public IReadOnlyList<SettingsDeletionPolicyOption> PolicyOptions { get; } =
    [
        new(MachineDeletionPolicyMode.AskEveryTime, "Ask every time (default)"),
        new(MachineDeletionPolicyMode.AlwaysDeleteDisks, "Always delete disks"),
        new(MachineDeletionPolicyMode.AlwaysDeleteDisksForLabAssistantProvisioned, "Always delete disks for LabAssistant-provisioned VMs"),
        new(MachineDeletionPolicyMode.AlwaysDeleteDisksForDifferencingOnly, "Always delete disks for differencing disks only"),
    ];

    public override async Task InitializeAsync(object? parameter = null, CancellationToken cancellationToken = default)
    {
        await base.InitializeAsync(parameter, cancellationToken);
        await LoadPolicyAsync();
    }

    private async Task LoadPolicyAsync()
    {
        try
        {
            var mode = await _machinesCapabilityService.GetDeletionPolicyAsync();
            SelectedPolicy = PolicyOptions.FirstOrDefault(option => option.Mode == mode) ?? PolicyOptions[0];
            StatusText = $"Current: {SelectedPolicy.Label}";
        }
        catch (System.Exception ex)
        {
            StatusText = $"Failed to load policy. {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedPolicy is null)
        {
            StatusText = "Select a deletion policy mode first.";
            return;
        }

        var option = SelectedPolicy;
        try
        {
            await _machinesCapabilityService.SetDeletionPolicyAsync(option.Mode);
            StatusText = $"Saved: {option.Label}";
        }
        catch (System.Exception ex)
        {
            StatusText = $"Failed to save policy. {ex.Message}";
        }
    }
}
