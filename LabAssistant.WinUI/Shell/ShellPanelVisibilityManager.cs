using LabAssistant.WinUI.ViewModels;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.Shell;

internal sealed class ShellPanelVisibilityManager
{
    private readonly FrameworkElement _machinesOverviewPanel;
    private readonly FrameworkElement _assetsLocalNavigationPanel;
    private readonly FrameworkElement _assetsOverviewPanel;
    private readonly FrameworkElement _assetsBaseDisksPanel;
    private readonly FrameworkElement _assetsSwitchesPanel;
    private readonly FrameworkElement _settingsMachinesPanel;
    private readonly FrameworkElement _nonMachinesPlaceholderTextBlock;
    private readonly Action _applyRightPanelState;
    private readonly Action _applyTemplatesUiState;
    private readonly Action _applyCapabilityShellState;

    public ShellPanelVisibilityManager(
        FrameworkElement machinesOverviewPanel,
        FrameworkElement assetsLocalNavigationPanel,
        FrameworkElement assetsOverviewPanel,
        FrameworkElement assetsBaseDisksPanel,
        FrameworkElement assetsSwitchesPanel,
        FrameworkElement settingsMachinesPanel,
        FrameworkElement nonMachinesPlaceholderTextBlock,
        Action applyRightPanelState,
        Action applyTemplatesUiState,
        Action applyCapabilityShellState)
    {
        _machinesOverviewPanel = machinesOverviewPanel;
        _assetsLocalNavigationPanel = assetsLocalNavigationPanel;
        _assetsOverviewPanel = assetsOverviewPanel;
        _assetsBaseDisksPanel = assetsBaseDisksPanel;
        _assetsSwitchesPanel = assetsSwitchesPanel;
        _settingsMachinesPanel = settingsMachinesPanel;
        _nonMachinesPlaceholderTextBlock = nonMachinesPlaceholderTextBlock;
        _applyRightPanelState = applyRightPanelState;
        _applyTemplatesUiState = applyTemplatesUiState;
        _applyCapabilityShellState = applyCapabilityShellState;
    }

    public void ApplyVisibility(ShellCapability capability, ShellSubview subview)
    {
        var routeKey = subview.RouteKey;
        var isMachinesOverviewActive = string.Equals(routeKey, ShellRouteKeys.MachinesOverview, StringComparison.Ordinal);
        var isAssetsOverviewActive = string.Equals(routeKey, ShellRouteKeys.AssetsOverview, StringComparison.Ordinal);
        var isAssetsBaseDisksActive = string.Equals(routeKey, ShellRouteKeys.AssetsBaseDisks, StringComparison.Ordinal);
        var isAssetsSwitchesActive = string.Equals(routeKey, ShellRouteKeys.AssetsSwitches, StringComparison.Ordinal);
        var isAssetsCapabilityActive = isAssetsOverviewActive || isAssetsBaseDisksActive || isAssetsSwitchesActive;
        var isSettingsMachinesActive = string.Equals(routeKey, ShellRouteKeys.SettingsMachines, StringComparison.Ordinal);
        var isKnownCapabilityActive =
            isMachinesOverviewActive ||
            string.Equals(capability.Key, "deploy", StringComparison.Ordinal) ||
            string.Equals(capability.Key, "templates", StringComparison.Ordinal) ||
            isAssetsCapabilityActive ||
            isSettingsMachinesActive ||
            string.Equals(capability.Key, "diagnostics", StringComparison.Ordinal);

        _applyRightPanelState();
        _machinesOverviewPanel.Visibility = isMachinesOverviewActive ? Visibility.Visible : Visibility.Collapsed;
        _assetsLocalNavigationPanel.Visibility = isAssetsCapabilityActive ? Visibility.Visible : Visibility.Collapsed;
        _assetsOverviewPanel.Visibility = isAssetsOverviewActive ? Visibility.Visible : Visibility.Collapsed;
        _assetsBaseDisksPanel.Visibility = isAssetsBaseDisksActive ? Visibility.Visible : Visibility.Collapsed;
        _assetsSwitchesPanel.Visibility = isAssetsSwitchesActive ? Visibility.Visible : Visibility.Collapsed;
        _applyTemplatesUiState();
        _settingsMachinesPanel.Visibility = isSettingsMachinesActive ? Visibility.Visible : Visibility.Collapsed;
        _nonMachinesPlaceholderTextBlock.Visibility = isKnownCapabilityActive ? Visibility.Collapsed : Visibility.Visible;
        _applyCapabilityShellState();
    }
}
