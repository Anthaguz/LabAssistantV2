using LabAssistant.WinUI.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Shell;

/// <summary>
/// Thin adapter between the pure <see cref="ShellNavigationState"/> routing logic and the real
/// shell chrome. Asks the state for a transition, then applies it to the capability
/// <see cref="Frame"/>, the <see cref="NavigationView"/> selection, and header text.
/// </summary>
internal sealed class ShellNavigationCoordinator
{
    private readonly ShellViewModel _shellViewModel;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly NavigationView _navigationView;
    private readonly TextBlock _currentRouteTextBlock;
    private readonly TextBlock _contentTitleTextBlock;
    private readonly TextBlock _contentDescriptionTextBlock;
    private readonly Action _applyRightPanelState;
    private readonly Action<string> _resetRightPanelForCapabilitySwitch;
    private readonly Frame _capabilityFrame;
    private readonly ShellNavigationState _state;
    private readonly Dictionary<string, NavigationViewItem> _routeToCapabilityNavigationItem = new(StringComparer.Ordinal);

    private IShellHost? _shellHost;
    private bool _isUpdatingNavigationSelection;

    public ShellNavigationCoordinator(
        ShellViewModel shellViewModel,
        DispatcherQueue dispatcherQueue,
        NavigationView navigationView,
        TextBlock currentRouteTextBlock,
        TextBlock contentTitleTextBlock,
        TextBlock contentDescriptionTextBlock,
        Action applyRightPanelState,
        Action<string> resetRightPanelForCapabilitySwitch,
        Frame capabilityFrame,
        IReadOnlyDictionary<string, Type> capabilityPageTypes)
    {
        _shellViewModel = shellViewModel;
        _dispatcherQueue = dispatcherQueue;
        _navigationView = navigationView;
        _currentRouteTextBlock = currentRouteTextBlock;
        _contentTitleTextBlock = contentTitleTextBlock;
        _contentDescriptionTextBlock = contentDescriptionTextBlock;
        _applyRightPanelState = applyRightPanelState;
        _resetRightPanelForCapabilitySwitch = resetRightPanelForCapabilitySwitch;
        _capabilityFrame = capabilityFrame;
        _state = new ShellNavigationState(shellViewModel, capabilityPageTypes);
    }

    /// <summary>
    /// Supplies the shell seam handed to capability pages. Must be set before the first
    /// <see cref="ApplyState"/> so the initial capability frame navigation carries it.
    /// </summary>
    public void SetShellHost(IShellHost shellHost) => _shellHost = shellHost;

    public string ActiveCapabilityKey => _state.ActiveCapability.Key;
    public bool IsMachinesOverviewActive => string.Equals(_state.ActiveRouteKey, ShellRouteKeys.MachinesOverview, StringComparison.Ordinal);
    public bool IsDeployOverviewActive => string.Equals(_state.ActiveRouteKey, ShellRouteKeys.DeployOverview, StringComparison.Ordinal);
    public bool IsDeployFromTemplateActive => string.Equals(_state.ActiveRouteKey, ShellRouteKeys.DeployFromTemplate, StringComparison.Ordinal);
    public bool IsDeployQuickDeployActive => string.Equals(_state.ActiveRouteKey, ShellRouteKeys.DeployQuickDeploy, StringComparison.Ordinal);
    public bool IsDeployCapabilityActive => IsDeployOverviewActive || IsDeployFromTemplateActive || IsDeployQuickDeployActive;
    public bool IsTemplatesLibraryActive => string.Equals(_state.ActiveRouteKey, ShellRouteKeys.TemplatesLibrary, StringComparison.Ordinal);
    public bool IsTemplatesEditorActive => string.Equals(_state.ActiveRouteKey, ShellRouteKeys.TemplatesEditor, StringComparison.Ordinal);
    public bool IsTemplatesBuilderActive => string.Equals(_state.ActiveRouteKey, ShellRouteKeys.TemplatesBuilder, StringComparison.Ordinal);
    public bool IsAssetsOverviewActive => string.Equals(_state.ActiveRouteKey, ShellRouteKeys.AssetsOverview, StringComparison.Ordinal);
    public bool IsAssetsBaseDisksActive => string.Equals(_state.ActiveRouteKey, ShellRouteKeys.AssetsBaseDisks, StringComparison.Ordinal);
    public bool IsAssetsSwitchesActive => string.Equals(_state.ActiveRouteKey, ShellRouteKeys.AssetsSwitches, StringComparison.Ordinal);
    public bool IsAssetsCapabilityActive => IsAssetsOverviewActive || IsAssetsBaseDisksActive || IsAssetsSwitchesActive;
    public bool IsTemplatesCapabilityActive => IsTemplatesLibraryActive || IsTemplatesEditorActive || IsTemplatesBuilderActive;
    public bool IsSettingsMachinesActive => string.Equals(_state.ActiveRouteKey, ShellRouteKeys.SettingsMachines, StringComparison.Ordinal);
    public bool IsDiagnosticsOverviewActive => string.Equals(_state.ActiveRouteKey, ShellRouteKeys.DiagnosticsOverview, StringComparison.Ordinal);
    public bool IsDiagnosticsLogsActive => string.Equals(_state.ActiveRouteKey, ShellRouteKeys.DiagnosticsLogs, StringComparison.Ordinal);
    public bool IsDiagnosticsCapabilityActive => IsDiagnosticsOverviewActive || IsDiagnosticsLogsActive;

    public void ConfigureNavigationView()
    {
        _routeToCapabilityNavigationItem.Clear();
        _navigationView.MenuItems.Clear();
        _navigationView.FooterMenuItems.Clear();

        foreach (var capability in _shellViewModel.Capabilities)
        {
            var parentItem = new NavigationViewItem
            {
                Content = capability.DisplayName,
                Tag = capability.Key,
                Icon = new FontIcon { Glyph = capability.Glyph }
            };

            foreach (var subview in capability.Subviews)
            {
                _routeToCapabilityNavigationItem[subview.RouteKey] = parentItem;
            }

            if (capability.IsFooter)
            {
                _navigationView.FooterMenuItems.Add(parentItem);
            }
            else
            {
                _navigationView.MenuItems.Add(parentItem);
            }
        }
    }

    public void NavigateToRoute(string routeKey)
    {
        var transition = _state.TryChangeRoute(routeKey);
        if (transition is null)
        {
            return;
        }

        if (transition.CapabilityChanged)
        {
            _resetRightPanelForCapabilitySwitch(transition.Capability.Key);
        }

        ApplyState();
    }

    public void ApplyState()
    {
        ApplyHeaderAndPanels();
        ApplyFrameState();
    }

    /// <summary>
    /// Updates shell-owned header context, right panel, and navigation selection for the active
    /// route without touching the capability frame. Used both for full state application and for
    /// subview changes a page reports back through <see cref="ReportActiveSubview"/>.
    /// </summary>
    private void ApplyHeaderAndPanels()
    {
        _currentRouteTextBlock.Text = _state.BreadcrumbText;
        _contentTitleTextBlock.Text = _state.ActiveCapability.DisplayName;
        _contentDescriptionTextBlock.Text = GetContentDescription();
        _applyRightPanelState();
        QueueNavigationSelectionUpdate();
    }

    /// <summary>
    /// Drives the capability frame for the active route: navigates on-demand when the capability
    /// changes (which also fires the outgoing page's real <c>OnNavigatedFrom</c>/<c>Unloaded</c>
    /// teardown, so cleanup of page-owned resources like timers and in-flight work happens for
    /// free), or forwards a subview change to the page already live in the frame.
    /// </summary>
    private void ApplyFrameState()
    {
        var pageType = _state.ResolvePageType(_state.ActiveCapability.Key);

        if (_capabilityFrame.Content?.GetType() != pageType)
        {
            _capabilityFrame.Navigate(pageType, new ShellNavigationRequest(_shellHost!, _state.ActiveRouteKey));
        }
        else if (_capabilityFrame.Content is ICapabilityPage page)
        {
            page.ShowSubview(_state.ActiveRouteKey);
        }
    }

    /// <summary>
    /// Applies a subview change a live page performed on its own (for example via an internal tab)
    /// to shell header context and navigation selection, without re-navigating the frame.
    /// </summary>
    public void ReportActiveSubview(string routeKey)
    {
        if (_state.ReportActiveSubview(routeKey))
        {
            ApplyHeaderAndPanels();
        }
    }

    public void HandleNavigationItemInvoked(NavigationViewItemInvokedEventArgs args)
    {
        if (_isUpdatingNavigationSelection || args.InvokedItemContainer is not NavigationViewItem invokedItem || invokedItem.Tag is not string key)
        {
            return;
        }

        if (_shellViewModel.TryResolveCapability(key, out var capability))
        {
            NavigateToRoute(capability.DefaultSubview.RouteKey);
            return;
        }

        NavigateToRoute(key);
    }

    public void QueueNavigationSelectionUpdate()
    {
        if (!_routeToCapabilityNavigationItem.TryGetValue(_state.ActiveRouteKey, out var selectedNavigationItem) ||
            ReferenceEquals(_navigationView.SelectedItem, selectedNavigationItem))
        {
            return;
        }

        _isUpdatingNavigationSelection = true;
        _dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                _navigationView.SelectedItem = selectedNavigationItem;
            }
            finally
            {
                _isUpdatingNavigationSelection = false;
            }
        });
    }

    private string GetContentDescription()
    {
        if (IsMachinesOverviewActive)
        {
            return "Manage host Hyper-V VMs. Start/stop/restart, open console, or delete with explicit scope.";
        }

        if (IsDeployCapabilityActive)
        {
            return "Configure and run deployment workflows from one capability surface with readiness, remediation, and results context.";
        }

        if (IsAssetsCapabilityActive)
        {
            return "Manage shared Hyper-V assets, inventory, and compatibility state from one capability surface.";
        }

        if (IsTemplatesCapabilityActive)
        {
            return "Browse templates and enter the editor through explicit create or edit workflows.";
        }

        if (IsSettingsMachinesActive)
        {
            return "Configure Machines policy defaults.";
        }

        if (IsDiagnosticsCapabilityActive)
        {
            return "Inspect support-oriented diagnostics and structured log context from one capability surface.";
        }

        return $"Use {_state.ActiveCapability.DisplayName} to continue to {_state.ActiveSubview.DisplayName}.";
    }
}
