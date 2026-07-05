using LabAssistant.WinUI.ViewModels;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.Shell;

internal sealed class ShellPanelVisibilityManager
{
    private readonly FrameworkElement _settingsMachinesPanel;
    private readonly FrameworkElement _nonMachinesPlaceholderTextBlock;
    private readonly Action _applyRightPanelState;
    private readonly Action _applyTemplatesUiState;
    private readonly Action _applyCapabilityShellState;

    public ShellPanelVisibilityManager(
        FrameworkElement settingsMachinesPanel,
        FrameworkElement nonMachinesPlaceholderTextBlock,
        Action applyRightPanelState,
        Action applyTemplatesUiState,
        Action applyCapabilityShellState)
    {
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
        var isAssetsCapabilityActive = string.Equals(capability.Key, "assets", StringComparison.Ordinal);
        var isSettingsMachinesActive = string.Equals(routeKey, ShellRouteKeys.SettingsMachines, StringComparison.Ordinal);
        var isKnownCapabilityActive =
            isMachinesOverviewActive ||
            string.Equals(capability.Key, "deploy", StringComparison.Ordinal) ||
            string.Equals(capability.Key, "templates", StringComparison.Ordinal) ||
            isAssetsCapabilityActive ||
            isSettingsMachinesActive ||
            string.Equals(capability.Key, "diagnostics", StringComparison.Ordinal);

        _applyRightPanelState();
        _applyTemplatesUiState();
        _settingsMachinesPanel.Visibility = isSettingsMachinesActive ? Visibility.Visible : Visibility.Collapsed;
        _nonMachinesPlaceholderTextBlock.Visibility = isKnownCapabilityActive ? Visibility.Collapsed : Visibility.Visible;
        _applyCapabilityShellState();
    }
}
