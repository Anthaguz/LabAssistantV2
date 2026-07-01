using System.Diagnostics;
using LabAssistant.Business.Assets;
using LabAssistant.Business.Machines;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.Services.Logging;
using LabAssistant.WinUI.Interop;
using LabAssistant.WinUI.Models.Assets;
using LabAssistant.WinUI.Shell;
using LabAssistant.WinUI.ViewModels;
using LabAssistant.WinUI.ViewModels.Assets;
using LabAssistant.WinUI.ViewModels.Diagnostics;
using LabAssistant.WinUI.ViewModels.Deploy;
using LabAssistant.WinUI.ViewModels.Machines;
using LabAssistant.WinUI.ViewModels.Templates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WinRT.Interop;

namespace LabAssistant.WinUI;

public sealed partial class MainWindow : Window
{
    private const string ShellApplicationTitle = "LabAssistant";

    private readonly ShellViewModel _shellViewModel = new();
    private readonly IMachinesCapabilityService _machinesCapabilityService;
    private readonly ITemplatesCapabilityService _templatesCapabilityService;
    private readonly IAssetsBaseDisksCapabilityService _assetsBaseDisksCapabilityService;
    private readonly IAssetsSwitchesCapabilityService _assetsSwitchesCapabilityService;
    private readonly ShellLayoutManager _layoutManager;
    private readonly ShellPanelStateManager _shellPanelStateManager;
    private readonly ShellNavigationCoordinator _navigationCoordinator;
    private readonly ShellThemeManager _themeManager;
    private readonly ShellDialogService _dialogService;
    private readonly ShellKeyboardHandler _keyboardHandler;
    private readonly MachinesCapabilityRuntime _machinesCapabilityRuntime;
    private readonly AssetsCapabilityRuntime _assetsCapabilityRuntime;
    private readonly AssetsBaseDisksWorkspaceComposition _assetsBaseDisksWorkspaceComposition;
    private readonly AssetsSwitchesWorkspaceComposition _assetsSwitchesWorkspaceComposition;
    private readonly TemplatesCapabilityRuntime _templatesCapabilityRuntime;
    private readonly DeployCapabilityRuntime _deployCapabilityRuntime;
    private readonly DiagnosticsCapabilityRuntime _diagnosticsCapabilityRuntime;

    private bool _isSavingDeletionPolicy;
    private DispatcherQueueTimer? _rdpReadinessTimer;

    public MainWindow()
    {
        InitializeComponent();

        _machinesCapabilityService = App.Services.GetRequiredService<IMachinesCapabilityService>();
        _templatesCapabilityService = App.Services.GetRequiredService<ITemplatesCapabilityService>();
        _assetsBaseDisksCapabilityService = App.Services.GetRequiredService<IAssetsBaseDisksCapabilityService>();
        _assetsSwitchesCapabilityService = App.Services.GetRequiredService<IAssetsSwitchesCapabilityService>();

        ShellNavigationCoordinator? navigationCoordinator = null;
        MachinesCapabilityRuntime? machinesCapabilityRuntime = null;
        TemplatesCapabilityRuntime? templatesCapabilityRuntime = null;

        _layoutManager = new ShellLayoutManager(GlobalNavigationView);
        _themeManager = new ShellThemeManager(
            RootLayout,
            ThemeToggleButton,
            ShellBrandingImage,
            () => WindowNative.GetWindowHandle(this));
        _dialogService = new ShellDialogService(
            () => WindowNative.GetWindowHandle(this),
            () => RootLayout.XamlRoot);
        _keyboardHandler = new ShellKeyboardHandler(GlobalNavigationView);
        _shellPanelStateManager = new ShellPanelStateManager(
            InsightsPanel,
            ShellRightPanelColumn,
            InsightsToggleButton,
            IssueBadge,
            IssueBadgeTextBlock,
            RightPanelTitleTextBlock,
            RightPanelEmptyStateBorder,
            () => navigationCoordinator?.ActiveCapabilityKey ?? string.Empty,
            () => IsDeployOnTheFlyActive,
            () => IsDeployFromTemplateActive,
            () => _deployCapabilityRuntime,
            () => RootLayout.ActualWidth);
        _navigationCoordinator = navigationCoordinator = new ShellNavigationCoordinator(
            _shellViewModel,
            DispatcherQueue,
            GlobalNavigationView,
            CurrentRouteTextBlock,
            ContentTitleTextBlock,
            ContentDescriptionTextBlock,
            MachinesOverviewViewHost,
            AssetsLocalNavigationPanel,
            AssetsOverviewViewHost,
            AssetsBaseDisksViewHost,
            AssetsSwitchesViewHost,
            SettingsMachinesPanel,
            NonMachinesPlaceholderTextBlock,
            ApplyRightPanelState,
            () => templatesCapabilityRuntime?.ApplyUiState(),
            ApplyCapabilityShellState,
            LoadMachinesDeletionPolicyAsync,
            () => machinesCapabilityRuntime?.DiscardEditDraft(),
            ResetRightPanelForCapabilitySwitch,
            () => machinesCapabilityRuntime?.EnsureInventoryAsync(forceRefresh: false) ?? Task.CompletedTask);

        _machinesCapabilityRuntime = machinesCapabilityRuntime = CreateMachinesCapabilityRuntime();
        _assetsCapabilityRuntime = CreateAssetsCapabilityRuntime(
            out var assetsBaseDisksWorkspaceComposition,
            out var assetsSwitchesWorkspaceComposition);
        _assetsBaseDisksWorkspaceComposition = assetsBaseDisksWorkspaceComposition;
        _assetsSwitchesWorkspaceComposition = assetsSwitchesWorkspaceComposition;
        _templatesCapabilityRuntime = templatesCapabilityRuntime = CreateTemplatesCapabilityRuntime();
        _deployCapabilityRuntime = CreateDeployCapabilityRuntime();
        _diagnosticsCapabilityRuntime = CreateDiagnosticsCapabilityRuntime();

        ConfigureShellIcons();
        _navigationCoordinator.ConfigureNavigationView();
        _layoutManager.ApplyShellNavigationMode(1280);

        Title = ShellApplicationTitle;
        SetInitialSize(1280, 800);
        _themeManager.InitializeShellBranding();

        RootLayout.KeyDown += RootLayout_KeyDown;
        RootLayout.SizeChanged += RootLayout_SizeChanged;
        _layoutManager.CompactFallbackChanged += (_, isCompact) => _shellPanelStateManager.SetCompactFallback(isCompact);

        InitializeRdpReadinessTimer();
        RootLayout.Loaded += async (_, _) =>
        {
            RootLayout.Focus(FocusState.Programmatic);
            await _machinesCapabilityRuntime.EnsureInventoryAsync(forceRefresh: true);
            await _templatesCapabilityRuntime.EnsureEditorReferenceDataAsync(forceRefresh: true);
            await _templatesCapabilityRuntime.EnsureLibraryAsync(forceRefresh: true);
            await _assetsBaseDisksWorkspaceComposition.EnsureInventoryAsync(forceRefresh: true);
            await LoadMachinesDeletionPolicyAsync();
        };

        ApplyState();
    }

    private bool IsMachinesOverviewActive => _navigationCoordinator.IsMachinesOverviewActive;
    private bool IsDeployOverviewActive => _navigationCoordinator.IsDeployOverviewActive;
    private bool IsDeployFromTemplateActive => _navigationCoordinator.IsDeployFromTemplateActive;
    private bool IsDeployOnTheFlyActive => _navigationCoordinator.IsDeployOnTheFlyActive;
    private bool IsDeployCapabilityActive => _navigationCoordinator.IsDeployCapabilityActive;
    private bool IsTemplatesLibraryActive => _navigationCoordinator.IsTemplatesLibraryActive;
    private bool IsTemplatesEditorActive => _navigationCoordinator.IsTemplatesEditorActive;
    private bool IsTemplatesBuilderActive => _navigationCoordinator.IsTemplatesBuilderActive;
    private bool IsAssetsOverviewActive => _navigationCoordinator.IsAssetsOverviewActive;
    private bool IsAssetsBaseDisksActive => _navigationCoordinator.IsAssetsBaseDisksActive;
    private bool IsAssetsSwitchesActive => _navigationCoordinator.IsAssetsSwitchesActive;
    private bool IsAssetsCapabilityActive => _navigationCoordinator.IsAssetsCapabilityActive;
    private bool IsTemplatesCapabilityActive => _navigationCoordinator.IsTemplatesCapabilityActive;
    private bool IsSettingsMachinesActive => _navigationCoordinator.IsSettingsMachinesActive;
    private bool IsDiagnosticsOverviewActive => _navigationCoordinator.IsDiagnosticsOverviewActive;
    private bool IsDiagnosticsLogsActive => _navigationCoordinator.IsDiagnosticsLogsActive;
    private bool IsDiagnosticsCapabilityActive => _navigationCoordinator.IsDiagnosticsCapabilityActive;

    private void ApplyState()
    {
        _themeManager.ApplyTheme();
        _navigationCoordinator.ApplyState();
    }

    private void ApplyRightPanelState() => _shellPanelStateManager.ApplyRightPanelState();

    private void ApplyCapabilityShellState()
    {
        _machinesCapabilityRuntime.ApplyShellState();
        _deployCapabilityRuntime.ApplyShellState();
        _assetsCapabilityRuntime.ApplyShellState();
        _templatesCapabilityRuntime.ApplyShellState();
        _diagnosticsCapabilityRuntime.ApplyShellState();
    }

    private void ResetRightPanelForCapabilitySwitch(string incomingCapabilityKey) =>
        _shellPanelStateManager.ResetForCapabilitySwitch(incomingCapabilityKey);

    private void RequestDeployResultsPanelToggle() => _shellPanelStateManager.TogglePanel();

    private void NavigateToRoute(string routeKey) => _navigationCoordinator.NavigateToRoute(routeKey);

    private string? PickBaseDiskFilePath() => _dialogService.PickBaseDiskFilePath();

    private Task<string?> PickTemplateFileForOpenAsync() => _dialogService.PickTemplateFileForOpenAsync();

    private Task<string?> PickTemplateFileForSaveAsync(string suggestedFileName) =>
        _dialogService.PickTemplateFileForSaveAsync(suggestedFileName);

    private Task<bool> ShowAssetsBaseDiskRemoveConfirmationDialogAsync(
        AssetsBaseDiskListRow row,
        AssetsBaseDiskRemovalAssessment assessment) =>
        _dialogService.ShowAssetsBaseDiskRemoveConfirmationDialogAsync(row, assessment);

    private Task<bool> ShowAssetsSwitchDeleteConfirmationDialogAsync(
        AssetsSwitchListRow row,
        AssetsSwitchDeleteAssessment assessment) =>
        _dialogService.ShowAssetsSwitchDeleteConfirmationDialogAsync(row, assessment);

    private Task<bool> ShowDeleteTemplateConfirmationDialogAsync(TemplateLibraryItem selectedTemplate) =>
        _dialogService.ShowDeleteTemplateConfirmationDialogAsync(selectedTemplate);

    private Task<bool> ShowRemoveTemplateVmConfirmationDialogAsync(string vmName) =>
        _dialogService.ShowRemoveTemplateVmConfirmationDialogAsync(vmName);

    private void HamburgerButton_Click(object sender, RoutedEventArgs e) =>
        GlobalNavigationView.IsPaneOpen = !GlobalNavigationView.IsPaneOpen;

    private void GlobalNavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args) =>
        _navigationCoordinator.HandleNavigationItemInvoked(args);

    private void InsightsButton_Click(object sender, RoutedEventArgs e) => _shellPanelStateManager.TogglePanel();

    private void CloseRightPanelButton_Click(object sender, RoutedEventArgs e) => _shellPanelStateManager.ClosePanel();

    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e) => _themeManager.ToggleTheme();

    private void RootLayout_KeyDown(object sender, KeyRoutedEventArgs e) => _keyboardHandler.HandleRootLayoutKeyDown(e);

    private void EscapeAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args) =>
        _keyboardHandler.HandleEscapeAccelerator(args);

    private void RootLayout_SizeChanged(object sender, SizeChangedEventArgs e) =>
        _layoutManager.HandleRootLayoutSizeChanged(e.NewSize.Width);

    private async void SaveMachinesDeletionPolicyButton_Click(object sender, RoutedEventArgs e)
    {
        if (MachinesDeletionPolicyComboBox.SelectedItem is not ComboBoxItem selectedItem ||
            selectedItem.Tag is not string modeRaw ||
            !Enum.TryParse<MachineDeletionPolicyMode>(modeRaw, ignoreCase: true, out var mode))
        {
            MachinesDeletionPolicyStatusTextBlock.Text = "Select a deletion policy mode first.";
            return;
        }

        _isSavingDeletionPolicy = true;
        SaveMachinesDeletionPolicyButton.IsEnabled = false;
        try
        {
            await _machinesCapabilityService.SetDeletionPolicyAsync(mode);
            MachinesDeletionPolicyStatusTextBlock.Text = $"Saved: {selectedItem.Content}";
        }
        catch (Exception ex)
        {
            MachinesDeletionPolicyStatusTextBlock.Text = $"Failed to save policy. {ex.Message}";
        }
        finally
        {
            _isSavingDeletionPolicy = false;
            SaveMachinesDeletionPolicyButton.IsEnabled = true;
        }
    }

    private void InitializeRdpReadinessTimer()
    {
        _rdpReadinessTimer = DispatcherQueue.CreateTimer();
        _rdpReadinessTimer.Interval = TimeSpan.FromMinutes(5);
        _rdpReadinessTimer.Tick += async (_, _) => await _machinesCapabilityRuntime.RefreshRdpReadinessAsync(selectedOnly: false);
    }

    private void UpdateReadinessPollingState()
    {
        if (_rdpReadinessTimer is null)
        {
            return;
        }

        if (IsMachinesOverviewActive && _machinesCapabilityRuntime.HasInventory)
        {
            if (!_rdpReadinessTimer.IsRunning)
            {
                _rdpReadinessTimer.Start();
                if (DateTimeOffset.UtcNow - _machinesCapabilityRuntime.LastRdpReadinessRefreshUtc >= _rdpReadinessTimer.Interval)
                {
                    _ = _machinesCapabilityRuntime.RefreshRdpReadinessAsync(selectedOnly: false);
                }
            }

            return;
        }

        if (_rdpReadinessTimer.IsRunning)
        {
            _rdpReadinessTimer.Stop();
        }
    }

    private string? TryOpenStructuredLogLocation(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"")
                {
                    UseShellExecute = true
                });
                return null;
            }

            var folderPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folderPath}\"")
                {
                    UseShellExecute = true
                });
                return $"Active structured log file not found. Opened log folder: {folderPath}";
            }

            return $"Structured log path does not exist yet: {filePath}";
        }
        catch (Exception ex)
        {
            return $"Failed to open structured log location. {ex.Message}";
        }
    }

    private async Task LoadMachinesDeletionPolicyAsync()
    {
        if (!IsSettingsMachinesActive || _isSavingDeletionPolicy)
        {
            return;
        }

        try
        {
            var mode = await _machinesCapabilityService.GetDeletionPolicyAsync();
            var item = MachinesDeletionPolicyComboBox.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(candidate => string.Equals(candidate.Tag?.ToString(), mode.ToString(), StringComparison.Ordinal));
            MachinesDeletionPolicyComboBox.SelectedItem = item;
            MachinesDeletionPolicyStatusTextBlock.Text = $"Current: {item?.Content ?? mode.ToString()}";
        }
        catch (Exception ex)
        {
            MachinesDeletionPolicyStatusTextBlock.Text = $"Failed to load policy. {ex.Message}";
        }
    }
}
