using LabAssistant.WinUI.ViewModels.Deploy;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Shell;

internal sealed class ShellPanelStateManager
{
    public const double ShellRightPanelExpandedWidth = 380;
    private const string DeployCapabilityKey = "deploy";

    private readonly FrameworkElement _insightsPanel;
    private readonly ColumnDefinition _shellRightPanelColumn;
    private readonly Button _insightsToggleButton;
    private readonly Border _issueBadge;
    private readonly TextBlock _issueBadgeTextBlock;
    private readonly TextBlock _rightPanelTitleTextBlock;
    private readonly Border _rightPanelEmptyStateBorder;
    private readonly Func<string> _getActiveCapabilityKey;
    private readonly Func<bool> _isDeployOnTheFlyActive;
    private readonly Func<bool> _isDeployFromTemplateActive;
    private readonly Func<DeployCapabilityRuntime?> _getDeployCapabilityRuntime;
    private readonly Func<double> _getRootLayoutWidth;

    private bool _isShellRightPanelOpen;
    private bool _isShellRightPanelInCompactFallback;
    private bool _isDeployRightPanelAutoOpenSuppressed;
    private string _shellRightPanelOwnerCapabilityKey = string.Empty;

    public ShellPanelStateManager(
        FrameworkElement insightsPanel,
        ColumnDefinition shellRightPanelColumn,
        Button insightsToggleButton,
        Border issueBadge,
        TextBlock issueBadgeTextBlock,
        TextBlock rightPanelTitleTextBlock,
        Border rightPanelEmptyStateBorder,
        Func<string> getActiveCapabilityKey,
        Func<bool> isDeployOnTheFlyActive,
        Func<bool> isDeployFromTemplateActive,
        Func<DeployCapabilityRuntime?> getDeployCapabilityRuntime,
        Func<double> getRootLayoutWidth)
    {
        _insightsPanel = insightsPanel;
        _shellRightPanelColumn = shellRightPanelColumn;
        _insightsToggleButton = insightsToggleButton;
        _issueBadge = issueBadge;
        _issueBadgeTextBlock = issueBadgeTextBlock;
        _rightPanelTitleTextBlock = rightPanelTitleTextBlock;
        _rightPanelEmptyStateBorder = rightPanelEmptyStateBorder;
        _getActiveCapabilityKey = getActiveCapabilityKey;
        _isDeployOnTheFlyActive = isDeployOnTheFlyActive;
        _isDeployFromTemplateActive = isDeployFromTemplateActive;
        _getDeployCapabilityRuntime = getDeployCapabilityRuntime;
        _getRootLayoutWidth = getRootLayoutWidth;
    }

    public void ApplyRightPanelState()
    {
        if (_getRootLayoutWidth() > 0)
        {
            _isShellRightPanelInCompactFallback = _getRootLayoutWidth() < ShellLayoutManager.ShellRightPanelCompactThreshold;
        }

        _shellRightPanelOwnerCapabilityKey = ResolveRightPanelOwnerCapabilityKey(_getActiveCapabilityKey());
        if (_isShellRightPanelInCompactFallback && _isShellRightPanelOpen)
        {
            _isShellRightPanelOpen = false;
        }

        var deployCapabilityRuntime = _getDeployCapabilityRuntime();
        if (deployCapabilityRuntime is null)
        {
            _insightsPanel.Visibility = Visibility.Collapsed;
            _shellRightPanelColumn.Width = new GridLength(0);
            _insightsToggleButton.IsEnabled = false;
            _insightsToggleButton.Opacity = 0.45;
            ToolTipService.SetToolTip(_insightsToggleButton, "Toggle progress and results panel");
            _rightPanelTitleTextBlock.Text = "Deploy Progress / Results";
            _rightPanelEmptyStateBorder.Visibility = Visibility.Collapsed;
            _issueBadge.Visibility = Visibility.Collapsed;
            _issueBadgeTextBlock.Text = string.Empty;
            return;
        }

        var shouldAutoOpenDeployRightPanel = CanActiveCapabilityOwnRightPanel() && deployCapabilityRuntime.ShouldAutoOpenRightPanel();
        if (!shouldAutoOpenDeployRightPanel)
        {
            _isDeployRightPanelAutoOpenSuppressed = false;
        }

        if (shouldAutoOpenDeployRightPanel && !_isDeployRightPanelAutoOpenSuppressed)
        {
            _isShellRightPanelOpen = true;
        }

        var hasOwner = CanActiveCapabilityOwnRightPanel();
        var showPanel = hasOwner && _isShellRightPanelOpen && !_isShellRightPanelInCompactFallback;
        _insightsPanel.Visibility = showPanel ? Visibility.Visible : Visibility.Collapsed;
        _shellRightPanelColumn.Width = showPanel ? new GridLength(ShellRightPanelExpandedWidth) : new GridLength(0);
        _insightsToggleButton.IsEnabled = hasOwner && !_isShellRightPanelInCompactFallback;
        _insightsToggleButton.Opacity = _insightsToggleButton.IsEnabled ? 1.0 : 0.45;
        ToolTipService.SetToolTip(_insightsToggleButton, "Toggle progress and results panel");
        _rightPanelTitleTextBlock.Text = deployCapabilityRuntime.GetRightPanelTitleText();
        _rightPanelEmptyStateBorder.Visibility = deployCapabilityRuntime.ShouldShowRightPanelEmptyState(showPanel)
            ? Visibility.Visible
            : Visibility.Collapsed;
        deployCapabilityRuntime.ApplyRightPanelState(showPanel, _isShellRightPanelInCompactFallback);
        _issueBadge.Visibility = Visibility.Collapsed;
        _issueBadgeTextBlock.Text = string.Empty;
    }

    public void TogglePanel()
    {
        _shellRightPanelOwnerCapabilityKey = ResolveRightPanelOwnerCapabilityKey(_getActiveCapabilityKey());
        if (_isShellRightPanelInCompactFallback ||
            !CanActiveCapabilityOwnRightPanel() ||
            (!_isDeployOnTheFlyActive() && !_isDeployFromTemplateActive()))
        {
            return;
        }

        SetRightPanelOpenFromUserToggle(!_isShellRightPanelOpen);
    }

    public void ClosePanel()
    {
        if (_isShellRightPanelOpen)
        {
            SetRightPanelOpenFromUserToggle(false);
        }
    }

    public void SetCompactFallback(bool isCompact)
    {
        if (_isShellRightPanelInCompactFallback == isCompact)
        {
            return;
        }

        _isShellRightPanelInCompactFallback = isCompact;
        if (isCompact)
        {
            _isShellRightPanelOpen = false;
        }

        ApplyRightPanelState();
    }

    public void ResetForCapabilitySwitch(string incomingCapabilityKey)
    {
        _shellRightPanelOwnerCapabilityKey = ResolveRightPanelOwnerCapabilityKey(incomingCapabilityKey);
        _isShellRightPanelOpen = false;
        _isDeployRightPanelAutoOpenSuppressed = false;
        _getDeployCapabilityRuntime()?.ResetRightPanelBehavior();
    }

    private bool CanActiveCapabilityOwnRightPanel()
    {
        return string.Equals(_shellRightPanelOwnerCapabilityKey, DeployCapabilityKey, StringComparison.Ordinal);
    }

    private string ResolveRightPanelOwnerCapabilityKey(string capabilityKey)
    {
        return string.Equals(capabilityKey, DeployCapabilityKey, StringComparison.Ordinal)
            ? DeployCapabilityKey
            : string.Empty;
    }

    private void SetRightPanelOpenFromUserToggle(bool isOpen)
    {
        _isShellRightPanelOpen = isOpen;
        _isDeployRightPanelAutoOpenSuppressed = !isOpen &&
            CanActiveCapabilityOwnRightPanel() &&
            _getDeployCapabilityRuntime()?.ShouldAutoOpenRightPanel() == true;
        ApplyRightPanelState();
    }
}
