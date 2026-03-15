using LabAssistant.WinUI.Views.Assets;

namespace LabAssistant.WinUI.ViewModels.Assets;

internal interface IAssetsOverviewWorkspaceShellBridge
{
    bool IsAssetsOverviewActive { get; }

    void NavigateToRoute(string routeKey);
}

internal interface IAssetsOverviewWorkspaceHost
{
    bool IsAssetsBaseDisksLoading { get; }

    bool IsAssetsSwitchesLoading { get; }

    int AssetsBaseDiskCount { get; }

    int AssetsSwitchCount { get; }
}

internal sealed class AssetsOverviewWorkspaceShellBridge : IAssetsOverviewWorkspaceShellBridge
{
    private readonly Func<bool> _isAssetsOverviewActive;
    private readonly Action<string> _navigateToRoute;

    public AssetsOverviewWorkspaceShellBridge(
        Func<bool> isAssetsOverviewActive,
        Action<string> navigateToRoute)
    {
        _isAssetsOverviewActive = isAssetsOverviewActive;
        _navigateToRoute = navigateToRoute;
    }

    public bool IsAssetsOverviewActive => _isAssetsOverviewActive();

    public void NavigateToRoute(string routeKey) => _navigateToRoute(routeKey);
}

internal sealed class AssetsOverviewWorkspaceHost : IAssetsOverviewWorkspaceHost
{
    private readonly Func<bool> _isAssetsBaseDisksLoading;
    private readonly Func<bool> _isAssetsSwitchesLoading;
    private readonly Func<int> _getAssetsBaseDiskCount;
    private readonly Func<int> _getAssetsSwitchCount;

    public AssetsOverviewWorkspaceHost(
        Func<bool> isAssetsBaseDisksLoading,
        Func<bool> isAssetsSwitchesLoading,
        Func<int> getAssetsBaseDiskCount,
        Func<int> getAssetsSwitchCount)
    {
        _isAssetsBaseDisksLoading = isAssetsBaseDisksLoading;
        _isAssetsSwitchesLoading = isAssetsSwitchesLoading;
        _getAssetsBaseDiskCount = getAssetsBaseDiskCount;
        _getAssetsSwitchCount = getAssetsSwitchCount;
    }

    public bool IsAssetsBaseDisksLoading => _isAssetsBaseDisksLoading();

    public bool IsAssetsSwitchesLoading => _isAssetsSwitchesLoading();

    public int AssetsBaseDiskCount => _getAssetsBaseDiskCount();

    public int AssetsSwitchCount => _getAssetsSwitchCount();
}

internal sealed class AssetsOverviewWorkspaceComposition
{
    private readonly AssetsOverviewView _view;
    private readonly AssetsOverviewWorkspaceViewModel _workspace = new();
    private readonly IAssetsOverviewWorkspaceHost _host;
    private readonly IAssetsOverviewWorkspaceShellBridge _shellBridge;

    public AssetsOverviewWorkspaceComposition(
        AssetsOverviewView view,
        IAssetsOverviewWorkspaceHost host,
        IAssetsOverviewWorkspaceShellBridge shellBridge)
    {
        _view = view;
        _host = host;
        _shellBridge = shellBridge;
        WireHandlers();
        ApplyWorkspaceState();
    }

    public void ApplyShellState()
    {
        if (!_shellBridge.IsAssetsOverviewActive)
        {
            return;
        }

        RefreshSummary();
        ApplyWorkspaceState();
    }

    private void WireHandlers()
    {
        _view.AssetsOverviewOpenBaseDisksButtonControl.Click += OpenBaseDisksRequested;
        _view.AssetsOverviewOpenSwitchesButtonControl.Click += OpenSwitchesRequested;
    }

    private void OpenBaseDisksRequested(object? sender, object e)
    {
        _shellBridge.NavigateToRoute(ShellRouteKeys.AssetsBaseDisks);
    }

    private void OpenSwitchesRequested(object? sender, object e)
    {
        _shellBridge.NavigateToRoute(ShellRouteKeys.AssetsSwitches);
    }

    private void RefreshSummary()
    {
        _workspace.RefreshSummary(
            _host.IsAssetsBaseDisksLoading,
            _host.IsAssetsSwitchesLoading,
            _host.AssetsBaseDiskCount,
            _host.AssetsSwitchCount);
    }

    private void ApplyWorkspaceState()
    {
        _view.SetBaseDisksSummary(_workspace.BaseDisksSummaryText);
        _view.SetSwitchesSummary(_workspace.SwitchesSummaryText);
    }
}
