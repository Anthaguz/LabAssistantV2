using LabAssistant.Business.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.ViewModels;
using LabAssistant.WinUI.Views.Deploy;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.ViewModels.Deploy;

internal sealed class DeployWorkspaceComposition
{
    private readonly FrameworkElement _localNavigationHost;
    private readonly FrameworkElement _overviewHost;
    private readonly DeployOnTheFlyWorkspaceComposition _onTheFlyWorkspaceComposition;
    private readonly TabView _subviewTabView;
    private readonly TabViewItem _overviewTabViewItem;
    private readonly TabViewItem _quickDeployTabViewItem;
    private readonly TabViewItem _fromTemplateTabViewItem;
    private readonly DeployOverviewWorkspaceComposition _overviewWorkspaceComposition;
    private readonly DeployFromTemplateWorkspaceComposition _fromTemplateWorkspaceComposition;
    private readonly IDeployWorkspaceShellBridge _shellBridge;
    private readonly Func<IReadOnlyList<VhdxCatalogItem>> _loadCatalogItems;
    private readonly Func<IReadOnlyList<string>> _availableSwitches;
    private readonly Func<Task<IReadOnlyList<string>>> _loadSwitchesAsync;
    private bool _isUpdatingDeploySubviewSelection;

    public DeployWorkspaceComposition(
        FrameworkElement localNavigationHost,
        DeployOverviewView overviewView,
        DeployOnTheFlyWorkspaceComposition onTheFlyWorkspaceComposition,
        TabView subviewTabView,
        TabViewItem overviewTabViewItem,
        TabViewItem quickDeployTabViewItem,
        TabViewItem fromTemplateTabViewItem,
        Func<DeployWorkspaceUiState> getUiState,
        DeployFromTemplateWorkspaceComposition fromTemplateWorkspaceComposition,
        Func<IReadOnlyList<VhdxCatalogItem>> loadCatalogItems,
        Func<IReadOnlyList<string>> availableSwitches,
        Func<Task<IReadOnlyList<string>>> loadSwitchesAsync,
        IDeployWorkspaceShellBridge shellBridge)
    {
        _localNavigationHost = localNavigationHost;
        _overviewHost = overviewView;
        _onTheFlyWorkspaceComposition = onTheFlyWorkspaceComposition;
        _subviewTabView = subviewTabView;
        _overviewTabViewItem = overviewTabViewItem;
        _quickDeployTabViewItem = quickDeployTabViewItem;
        _fromTemplateTabViewItem = fromTemplateTabViewItem;
        _fromTemplateWorkspaceComposition = fromTemplateWorkspaceComposition;
        _loadCatalogItems = loadCatalogItems;
        _availableSwitches = availableSwitches;
        _loadSwitchesAsync = loadSwitchesAsync;
        _shellBridge = shellBridge;
        _overviewWorkspaceComposition = new DeployOverviewWorkspaceComposition(
            overviewView,
            new DeployOverviewWorkspaceHost(
                () => getUiState().QuickDeployDraftCount,
                () => getUiState().IsLoadingTemplates,
                () => getUiState().AvailableTemplateCount),
            new DeployOverviewWorkspaceShellBridge(
                () => _shellBridge.IsDeployOverviewActive,
                _shellBridge.NavigateToRoute));
        WireSharedHandlers();
    }

    public void RefreshSharedUiState() => _overviewWorkspaceComposition.RefreshUiState();

    /// <summary>
    /// Applies shared Deploy-side auto-resolve suggestions so lane workflows do not route that integration back through MainWindow.
    /// </summary>
    public async Task<int> ApplyResolveSuggestionsAsync(LabTemplate template)
    {
        var catalogItems = _loadCatalogItems();
        var cachedSwitches = _availableSwitches();
        var templateSwitches = cachedSwitches.Count > 0
            ? cachedSwitches
            : await _loadSwitchesAsync();
        var availableSwitches = templateSwitches
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var applied = 0;
        foreach (var vm in template.VmTemplates)
        {
            var switchNames = vm.SwitchNames?.Where(name => !string.IsNullOrWhiteSpace(name)).ToList() ?? [];
            if (switchNames.Count == 0 && !string.IsNullOrWhiteSpace(vm.SwitchName))
            {
                switchNames.Add(vm.SwitchName);
            }

            if (switchNames.Count > 0)
            {
                var normalized = switchNames
                    .Where(name => availableSwitches.Contains(name, StringComparer.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (normalized.Count != switchNames.Count)
                {
                    applied++;
                }

                vm.SwitchNames = normalized.Count > 0 ? normalized : null;
                vm.SwitchName = normalized.Count > 0 ? normalized[0] : null;
            }

            if (!string.IsNullOrWhiteSpace(vm.VhdxId))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(vm.VhdxSignature))
            {
                var signatureMatches = VhdxSignature.FindMatches(vm.VhdxSignature, catalogItems);
                if (signatureMatches.Count == 1)
                {
                    var match = signatureMatches[0];
                    vm.VhdxId = match.Id;
                    vm.VhdPath = match.Path;
                    vm.VhdxSignature = match.Signature;
                    applied++;
                    continue;
                }
            }

            if (!string.IsNullOrWhiteSpace(vm.VhdPath))
            {
                var pathMatch = catalogItems.FirstOrDefault(item =>
                    string.Equals(item.Path, vm.VhdPath, StringComparison.OrdinalIgnoreCase));
                if (pathMatch is not null)
                {
                    vm.VhdxId = pathMatch.Id;
                    vm.VhdxSignature = pathMatch.Signature;
                    vm.VhdPath = pathMatch.Path;
                    applied++;
                }
            }
        }

        return applied;
    }

    /// <summary>
    /// Reconciles Deploy From Template selection against the shared Templates inventory without routing that integration back through MainWindow.
    /// </summary>
    public void ReconcileFromTemplateSelection(IReadOnlyList<TemplateLibraryItem> items)
    {
        _fromTemplateWorkspaceComposition.ReconcileSelection(items);
        RefreshSharedUiState();
    }

    /// <summary>
    /// Resets Deploy-local right-panel behavior when the shell changes capability ownership.
    /// </summary>
    public void ResetRightPanelBehavior()
    {
        _fromTemplateWorkspaceComposition.ResetPanelState();
    }

    /// <summary>
    /// Indicates whether Deploy workflow state should force the shell-owned panel infrastructure open.
    /// </summary>
    public bool ShouldAutoOpenRightPanel()
    {
        return _fromTemplateWorkspaceComposition.IsStarting ||
            _onTheFlyWorkspaceComposition.IsStarting ||
            string.Equals(_fromTemplateWorkspaceComposition.LifecycleState, "Running", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(_onTheFlyWorkspaceComposition.LifecycleState, "Running", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Applies the Deploy workflow-local toggle semantics while leaving the shell-owned open/closed flag in the shell.
    /// </summary>
    public bool TryToggleRightPanelFromWorkflow(bool isPanelUnavailable, bool isPanelOpen, out bool nextPanelOpenState)
    {
        nextPanelOpenState = isPanelOpen;
        if (isPanelUnavailable || (!_shellBridge.IsDeployFromTemplateActive && !_shellBridge.IsDeployOnTheFlyActive))
        {
            return false;
        }

        nextPanelOpenState = !isPanelOpen;
        return true;
    }

    /// <summary>
    /// Applies Deploy lane-specific panel visibility and launcher state while the shell keeps container and sizing ownership.
    /// </summary>
    public void ApplyRightPanelState(bool showPanel, bool panelUnavailable)
    {
        _fromTemplateWorkspaceComposition.ApplyResultsPanelState(_shellBridge.IsDeployFromTemplateActive, showPanel, panelUnavailable);
        _onTheFlyWorkspaceComposition.ApplyResultsPanelState(_shellBridge.IsDeployOnTheFlyActive, showPanel, panelUnavailable);
    }

    /// <summary>
    /// Returns the Deploy-owned right-panel title for the active Deploy lane.
    /// </summary>
    public string GetRightPanelTitleText()
    {
        return _shellBridge.IsDeployFromTemplateActive
            ? "From Template Progress / Results"
            : _shellBridge.IsDeployOnTheFlyActive
                ? "Quick Deploy Progress / Results"
                : "Details";
    }

    /// <summary>
    /// Returns whether the shell should show the Deploy overview empty-state content in the shared panel host.
    /// </summary>
    public bool ShouldShowRightPanelEmptyState(bool showPanel)
    {
        return showPanel && !_shellBridge.IsDeployFromTemplateActive && !_shellBridge.IsDeployOnTheFlyActive;
    }

    public void ApplyShellState()
    {
        _localNavigationHost.Visibility = _shellBridge.IsDeployCapabilityActive ? Visibility.Visible : Visibility.Collapsed;
        _overviewHost.Visibility = _shellBridge.IsDeployOverviewActive ? Visibility.Visible : Visibility.Collapsed;
        _onTheFlyWorkspaceComposition.ApplyShellState(_shellBridge.IsDeployOnTheFlyActive);

        SyncDeploySubviewSelection();

        if (_shellBridge.IsDeployOverviewActive)
        {
            _overviewWorkspaceComposition.ApplyShellState();
        }

        _fromTemplateWorkspaceComposition.ApplyShellState(_shellBridge.IsDeployFromTemplateActive);
    }

    private void WireSharedHandlers()
    {
        _subviewTabView.SelectionChanged += DeploySubviewTabView_SelectionChanged;
    }

    private void DeploySubviewTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingDeploySubviewSelection || _subviewTabView.SelectedItem is not TabViewItem selectedTab)
        {
            return;
        }

        if (ReferenceEquals(selectedTab, _overviewTabViewItem))
        {
            _shellBridge.NavigateToRoute(ShellRouteKeys.DeployOverview);
        }
        else if (ReferenceEquals(selectedTab, _quickDeployTabViewItem))
        {
            _shellBridge.NavigateToRoute(ShellRouteKeys.DeployOnTheFly);
        }
        else if (ReferenceEquals(selectedTab, _fromTemplateTabViewItem))
        {
            _shellBridge.NavigateToRoute(ShellRouteKeys.DeployFromTemplate);
        }
    }

    private void SyncDeploySubviewSelection()
    {
        if (!_shellBridge.IsDeployCapabilityActive)
        {
            return;
        }

        var selectedTab = _shellBridge.IsDeployOverviewActive
            ? _overviewTabViewItem
            : _shellBridge.IsDeployOnTheFlyActive
                ? _quickDeployTabViewItem
                : _fromTemplateTabViewItem;

        if (ReferenceEquals(_subviewTabView.SelectedItem, selectedTab))
        {
            return;
        }

        _isUpdatingDeploySubviewSelection = true;
        try
        {
            _subviewTabView.SelectedItem = selectedTab;
        }
        finally
        {
            _isUpdatingDeploySubviewSelection = false;
        }
    }

}
