using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabAssistant.WinUI.Infrastructure;
using LabAssistant.WinUI.ViewModels;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.ViewModels.Assets;

public partial class AssetsOverviewViewModel : ViewModelBase
{
    private Action<string>? _navigateToRoute;

    [ObservableProperty]
    private int _baseDiskCount;

    [ObservableProperty]
    private int _switchCount;

    [ObservableProperty]
    private string _catalogStatus = "Use Assets to inspect shared disk and switch inventory without leaving the capability workspace.";

    public string BaseDisksSummaryText => IsLoading
        ? "Base disk inventory is loading."
        : BaseDiskCount > 0
            ? $"{BaseDiskCount} base disks currently loaded."
            : "Open Base Disks to inspect imported VHDX inventory.";

    public string SwitchesSummaryText => IsLoading
        ? "Switch inventory is loading."
        : SwitchCount > 0
            ? $"{SwitchCount} virtual switches currently loaded."
            : "Open Switches to inspect host virtual switch inventory.";

    public Visibility LoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;

    public void ConfigureNavigation(Action<string> navigateToRoute)
    {
        _navigateToRoute = navigateToRoute;
    }

    public void RefreshSummary(bool isBaseDisksLoading, bool isSwitchesLoading, int baseDiskCount, int switchCount)
    {
        BaseDiskCount = baseDiskCount;
        SwitchCount = switchCount;
        IsLoading = isBaseDisksLoading || isSwitchesLoading;
        CatalogStatus = IsLoading
            ? "Assets inventory is loading."
            : baseDiskCount == 0 && switchCount == 0
                ? "No shared assets are loaded yet."
                : "Assets inventory is ready.";
        NotifySummaryChanged();
    }

    partial void OnBaseDiskCountChanged(int value) => NotifySummaryChanged();

    partial void OnSwitchCountChanged(int value) => NotifySummaryChanged();

    partial void OnCatalogStatusChanged(string value) => NotifySummaryChanged();

    [RelayCommand]
    private void OpenBaseDisks()
    {
        _navigateToRoute?.Invoke(ShellRouteKeys.AssetsBaseDisks);
    }

    [RelayCommand]
    private void OpenSwitches()
    {
        _navigateToRoute?.Invoke(ShellRouteKeys.AssetsSwitches);
    }

    private void NotifySummaryChanged()
    {
        OnPropertyChanged(nameof(BaseDisksSummaryText));
        OnPropertyChanged(nameof(SwitchesSummaryText));
        OnPropertyChanged(nameof(LoadingVisibility));
    }
}
