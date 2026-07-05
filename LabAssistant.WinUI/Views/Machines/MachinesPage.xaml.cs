using LabAssistant.Business.Machines;
using LabAssistant.WinUI.Shell;
using LabAssistant.WinUI.ViewModels.Machines;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace LabAssistant.WinUI.Views.Machines;

/// <summary>
/// Capability page that owns the Machines surface while it is the active shell content. The page
/// is created on-demand by the shell capability frame and torn down on leave, so it owns the
/// readiness-polling timer lifecycle and implements the view model's shell seam directly - there
/// is no separate capability-runtime or delegate-bag bridge layer.
/// </summary>
public sealed partial class MachinesPage : Page, IMachinesCapabilityShellBridge
{
    private readonly MachinesViewModel _viewModel;

    private IShellHost? _shellHost;
    private DispatcherQueueTimer? _rdpReadinessTimer;
    private bool _isActive;

    public MachinesPage()
    {
        InitializeComponent();
        _viewModel = MachinesOverviewViewHost.ViewModel;
    }

    public bool IsMachinesOverviewActive => _isActive;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is ShellNavigationRequest request)
        {
            _shellHost = request.Host;
        }

        _isActive = true;
        _viewModel.AttachShellBridge(this);
        _viewModel.ApplyShellState();
        UpdateReadinessPollingState();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);

        // Stop shell-owned polling before the view unloads and clears the bridge. The view model's
        // own Unloaded -> CleanupAsync detaches the bridge and cancels in-flight inventory work.
        _isActive = false;
        UpdateReadinessPollingState();
    }

    public void UpdateReadinessPollingState()
    {
        EnsureRdpReadinessTimer();
        if (_rdpReadinessTimer is null)
        {
            return;
        }

        if (_isActive && _viewModel.HasInventory)
        {
            if (!_rdpReadinessTimer.IsRunning)
            {
                _rdpReadinessTimer.Start();
                if (DateTimeOffset.UtcNow - _viewModel.LastRdpReadinessRefreshUtc >= _rdpReadinessTimer.Interval)
                {
                    _ = _viewModel.RefreshRdpReadinessAsync(selectedOnly: false);
                }
            }

            return;
        }

        if (_rdpReadinessTimer.IsRunning)
        {
            _rdpReadinessTimer.Stop();
        }
    }

    public Task<MachineDeleteScope?> ShowDeleteScopeDialogAsync(MachineInventoryItem vm, MachineDeletePreview preview) =>
        MachinesDeleteDialogs.ShowDeleteScopeDialogAsync(_shellHost?.XamlRoot ?? XamlRoot, vm, preview);

    public Task<bool> ShowDeleteConfirmationDialogAsync(MachineInventoryItem vm, MachineDeletePreview preview, MachineDeleteScope effectiveScope) =>
        MachinesDeleteDialogs.ShowDeleteConfirmationDialogAsync(_shellHost?.XamlRoot ?? XamlRoot, vm, preview, effectiveScope);

    private void EnsureRdpReadinessTimer()
    {
        if (_rdpReadinessTimer is not null)
        {
            return;
        }

        _rdpReadinessTimer = DispatcherQueue.CreateTimer();
        _rdpReadinessTimer.Interval = TimeSpan.FromMinutes(5);
        _rdpReadinessTimer.Tick += async (_, _) => await _viewModel.RefreshRdpReadinessAsync(selectedOnly: false);
    }
}
