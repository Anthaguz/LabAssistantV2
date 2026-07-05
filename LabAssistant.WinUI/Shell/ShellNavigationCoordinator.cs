using LabAssistant.WinUI.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Shell;

internal sealed class ShellNavigationCoordinator
{
    private readonly ShellViewModel _shellViewModel;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly NavigationView _navigationView;
    private readonly TextBlock _currentRouteTextBlock;
    private readonly TextBlock _contentTitleTextBlock;
    private readonly TextBlock _contentDescriptionTextBlock;
    private readonly ShellPanelVisibilityManager _panelVisibilityManager;
    private readonly Func<Task> _loadMachinesDeletionPolicyAsync;
    private readonly Action<string> _resetRightPanelForCapabilitySwitch;
    private readonly Frame _capabilityFrame;
    private readonly IReadOnlyDictionary<string, Type> _capabilityPageTypes;
    private readonly Type _blankPageType;
    private readonly Dictionary<string, NavigationViewItem> _routeToCapabilityNavigationItem = new(StringComparer.Ordinal);

    private IShellHost? _shellHost;
    private ShellCapability _activeCapability;
    private ShellSubview _activeSubview;
    private string _activeRouteKey;
    private bool _isUpdatingNavigationSelection;

    public ShellNavigationCoordinator(
        ShellViewModel shellViewModel,
        DispatcherQueue dispatcherQueue,
        NavigationView navigationView,
        TextBlock currentRouteTextBlock,
        TextBlock contentTitleTextBlock,
        TextBlock contentDescriptionTextBlock,
        ShellPanelVisibilityManager panelVisibilityManager,
        Func<Task> loadMachinesDeletionPolicyAsync,
        Action<string> resetRightPanelForCapabilitySwitch,
        Frame capabilityFrame,
        IReadOnlyDictionary<string, Type> capabilityPageTypes,
        Type blankPageType)
    {
        _shellViewModel = shellViewModel;
        _dispatcherQueue = dispatcherQueue;
        _navigationView = navigationView;
        _currentRouteTextBlock = currentRouteTextBlock;
        _contentTitleTextBlock = contentTitleTextBlock;
        _contentDescriptionTextBlock = contentDescriptionTextBlock;
        _panelVisibilityManager = panelVisibilityManager;
        _loadMachinesDeletionPolicyAsync = loadMachinesDeletionPolicyAsync;
        _resetRightPanelForCapabilitySwitch = resetRightPanelForCapabilitySwitch;
        _capabilityFrame = capabilityFrame;
        _capabilityPageTypes = capabilityPageTypes;
        _blankPageType = blankPageType;

        _activeRouteKey = _shellViewModel.StartupRoute;
        _shellViewModel.TryResolveRoute(_activeRouteKey, out _activeCapability, out _activeSubview);
    }

    /// <summary>
    /// Supplies the shell seam handed to capability pages. Must be set before the first
    /// <see cref="ApplyState"/> so the initial capability frame navigation carries it.
    /// </summary>
    public void SetShellHost(IShellHost shellHost) => _shellHost = shellHost;

    /// <summary>True when the active capability is served by an on-demand capability page.</summary>
    private bool IsActiveCapabilityMigrated => _capabilityPageTypes.ContainsKey(_activeCapability.Key);

    public string ActiveCapabilityKey => _activeCapability.Key;
    public bool IsMachinesOverviewActive => string.Equals(_activeRouteKey, ShellRouteKeys.MachinesOverview, StringComparison.Ordinal);
    public bool IsDeployOverviewActive => string.Equals(_activeRouteKey, ShellRouteKeys.DeployOverview, StringComparison.Ordinal);
    public bool IsDeployFromTemplateActive => string.Equals(_activeRouteKey, ShellRouteKeys.DeployFromTemplate, StringComparison.Ordinal);
    public bool IsDeployOnTheFlyActive => string.Equals(_activeRouteKey, ShellRouteKeys.DeployOnTheFly, StringComparison.Ordinal);
    public bool IsDeployCapabilityActive => IsDeployOverviewActive || IsDeployFromTemplateActive || IsDeployOnTheFlyActive;
    public bool IsTemplatesLibraryActive => string.Equals(_activeRouteKey, ShellRouteKeys.TemplatesLibrary, StringComparison.Ordinal);
    public bool IsTemplatesEditorActive => string.Equals(_activeRouteKey, ShellRouteKeys.TemplatesEditor, StringComparison.Ordinal);
    public bool IsTemplatesBuilderActive => string.Equals(_activeRouteKey, ShellRouteKeys.TemplatesBuilder, StringComparison.Ordinal);
    public bool IsAssetsOverviewActive => string.Equals(_activeRouteKey, ShellRouteKeys.AssetsOverview, StringComparison.Ordinal);
    public bool IsAssetsBaseDisksActive => string.Equals(_activeRouteKey, ShellRouteKeys.AssetsBaseDisks, StringComparison.Ordinal);
    public bool IsAssetsSwitchesActive => string.Equals(_activeRouteKey, ShellRouteKeys.AssetsSwitches, StringComparison.Ordinal);
    public bool IsAssetsCapabilityActive => IsAssetsOverviewActive || IsAssetsBaseDisksActive || IsAssetsSwitchesActive;
    public bool IsTemplatesCapabilityActive => IsTemplatesLibraryActive || IsTemplatesEditorActive || IsTemplatesBuilderActive;
    public bool IsSettingsMachinesActive => string.Equals(_activeRouteKey, ShellRouteKeys.SettingsMachines, StringComparison.Ordinal);
    public bool IsDiagnosticsOverviewActive => string.Equals(_activeRouteKey, ShellRouteKeys.DiagnosticsOverview, StringComparison.Ordinal);
    public bool IsDiagnosticsLogsActive => string.Equals(_activeRouteKey, ShellRouteKeys.DiagnosticsLogs, StringComparison.Ordinal);
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
        if (!_shellViewModel.TryResolveRoute(routeKey, out var capability, out var subview))
        {
            return;
        }

        var changedCapability = !string.Equals(_activeCapability.Key, capability.Key, StringComparison.Ordinal);
        var changedSubview = !string.Equals(_activeSubview.RouteKey, subview.RouteKey, StringComparison.Ordinal);
        if (!changedCapability && !changedSubview)
        {
            return;
        }

        if (changedCapability)
        {
            _resetRightPanelForCapabilitySwitch(capability.Key);
        }

        _activeCapability = capability;
        _activeSubview = subview;
        _activeRouteKey = subview.RouteKey;
        ApplyState();
    }

    public void ApplyState()
    {
        ApplyHeaderAndPanels();
        ApplyFrameState();

        if (IsSettingsMachinesActive)
        {
            _ = SafeFireAndForgetAsync(_loadMachinesDeletionPolicyAsync);
        }
    }

    /// <summary>
    /// Updates shell-owned header context, panel visibility, and navigation selection for the
    /// active route without touching the capability frame. Used both for full state application
    /// and for subview changes a page reports back through <see cref="ReportActiveSubview"/>.
    /// </summary>
    private void ApplyHeaderAndPanels()
    {
        _currentRouteTextBlock.Text = $"{_activeCapability.DisplayName} / {_activeSubview.DisplayName}";
        _contentTitleTextBlock.Text = _activeCapability.DisplayName;
        _contentDescriptionTextBlock.Text = GetContentDescription();
        _panelVisibilityManager.ApplyVisibility(_activeCapability, _activeSubview);
        QueueNavigationSelectionUpdate();
    }

    /// <summary>
    /// Drives the capability frame for the active route. Migrated capabilities are shown in the
    /// frame (navigated on-demand when the capability changes, or forwarded a subview change when
    /// the page is already live); non-migrated capabilities keep the legacy inline content.
    /// </summary>
    private void ApplyFrameState()
    {
        if (!IsActiveCapabilityMigrated)
        {
            // Force a live capability page through its real teardown instead of leaving it
            // loaded-but-hidden. Navigating to the blank page fires the previous page's
            // OnNavigatedFrom/Unloaded so it can cancel timers and in-flight work.
            if (_capabilityFrame.Content is not null && _capabilityFrame.Content.GetType() != _blankPageType)
            {
                _capabilityFrame.Navigate(_blankPageType);
            }

            _capabilityFrame.Visibility = Visibility.Collapsed;
            return;
        }

        _capabilityFrame.Visibility = Visibility.Visible;
        var pageType = _capabilityPageTypes[_activeCapability.Key];

        if (_capabilityFrame.Content?.GetType() != pageType)
        {
            _capabilityFrame.Navigate(pageType, new ShellNavigationRequest(_shellHost!, _activeRouteKey));
        }
        else if (_capabilityFrame.Content is ICapabilityPage page)
        {
            page.ShowSubview(_activeRouteKey);
        }
    }

    /// <summary>
    /// Applies a subview change a live page performed on its own (for example via an internal tab)
    /// to shell header context and navigation selection, without re-navigating the frame.
    /// </summary>
    public void ReportActiveSubview(string routeKey)
    {
        if (string.Equals(routeKey, _activeRouteKey, StringComparison.Ordinal) ||
            !_shellViewModel.TryResolveRoute(routeKey, out var capability, out var subview) ||
            !string.Equals(capability.Key, _activeCapability.Key, StringComparison.Ordinal))
        {
            return;
        }

        _activeSubview = subview;
        _activeRouteKey = routeKey;
        ApplyHeaderAndPanels();
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
        if (!_routeToCapabilityNavigationItem.TryGetValue(_activeRouteKey, out var selectedNavigationItem) ||
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

        return $"Use {_activeCapability.DisplayName} to continue to {_activeSubview.DisplayName}.";
    }

    private async Task SafeFireAndForgetAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShellNavigationCoordinator] Background operation failed: {ex.Message}");
        }
    }
}
