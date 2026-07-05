using LabAssistant.Business.Assets;
using LabAssistant.Business.Machines;
using LabAssistant.Business.Templates;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Deployment;
using LabAssistant.Models.Templates;
using LabAssistant.WinUI.Interop;
using LabAssistant.WinUI.Shell;
using LabAssistant.WinUI.ViewModels;
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
    private readonly IAssetsSwitchesCapabilityService _assetsSwitchesCapabilityService;
    private readonly ShellLayoutManager _layoutManager;
    private readonly ShellPanelStateManager _shellPanelStateManager;
    private readonly ShellNavigationCoordinator _navigationCoordinator;
    private readonly ShellThemeManager _themeManager;
    private readonly ShellDialogService _dialogService;
    private readonly ShellKeyboardHandler _keyboardHandler;
    private readonly TemplatesCapabilityRuntime _templatesCapabilityRuntime;
    private readonly DeployCapabilityRuntime _deployCapabilityRuntime;

    public MainWindow()
    {
        InitializeComponent();

        _machinesCapabilityService = App.Services.GetRequiredService<IMachinesCapabilityService>();
        _templatesCapabilityService = App.Services.GetRequiredService<ITemplatesCapabilityService>();
        _assetsSwitchesCapabilityService = App.Services.GetRequiredService<IAssetsSwitchesCapabilityService>();

        ShellNavigationCoordinator? navigationCoordinator = null;
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
            () => IsDeployQuickDeployActive,
            () => IsDeployFromTemplateActive,
            () => _deployCapabilityRuntime,
            () => RootLayout.ActualWidth);
        var panelVisibilityManager = new ShellPanelVisibilityManager(
            NonMachinesPlaceholderTextBlock,
            ApplyRightPanelState,
            () => templatesCapabilityRuntime?.ApplyUiState(),
            ApplyCapabilityShellState);
        var capabilityPageTypes = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["machines"] = typeof(Views.Machines.MachinesPage),
            ["assets"] = typeof(Views.Assets.AssetsPage),
            ["diagnostics"] = typeof(Views.Diagnostics.DiagnosticsPage),
            ["settings"] = typeof(Views.Settings.SettingsPage)
        };
        _navigationCoordinator = navigationCoordinator = new ShellNavigationCoordinator(
            _shellViewModel,
            DispatcherQueue,
            GlobalNavigationView,
            CurrentRouteTextBlock,
            ContentTitleTextBlock,
            ContentDescriptionTextBlock,
            panelVisibilityManager,
            ResetRightPanelForCapabilitySwitch,
            CapabilityFrame,
            capabilityPageTypes,
            typeof(Views.Shell.ShellBlankPage));
        _navigationCoordinator.SetShellHost(new ShellHost(_navigationCoordinator, () => RootLayout.XamlRoot, _dialogService));

        _templatesCapabilityRuntime = templatesCapabilityRuntime = CreateTemplatesCapabilityRuntime();
        _deployCapabilityRuntime = CreateDeployCapabilityRuntime();

        ConfigureShellIcons();
        _navigationCoordinator.ConfigureNavigationView();
        _layoutManager.ApplyShellNavigationMode(1280);

        Title = ShellApplicationTitle;
        SetInitialSize(1280, 800);
        _themeManager.InitializeShellBranding();

        RootLayout.KeyDown += RootLayout_KeyDown;
        RootLayout.SizeChanged += RootLayout_SizeChanged;
        _layoutManager.CompactFallbackChanged += (_, isCompact) => _shellPanelStateManager.SetCompactFallback(isCompact);

        RootLayout.Loaded += async (_, _) =>
        {
            RootLayout.Focus(FocusState.Programmatic);
            try
            {
                await _templatesCapabilityRuntime.EnsureEditorReferenceDataAsync(forceRefresh: true);
                await _templatesCapabilityRuntime.EnsureLibraryAsync(forceRefresh: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Loaded initialization failed: {ex}");
            }
        };

        ApplyState();
    }

    private bool IsDeployOverviewActive => _navigationCoordinator.IsDeployOverviewActive;
    private bool IsDeployFromTemplateActive => _navigationCoordinator.IsDeployFromTemplateActive;
    private bool IsDeployQuickDeployActive => _navigationCoordinator.IsDeployQuickDeployActive;
    private bool IsDeployCapabilityActive => _navigationCoordinator.IsDeployCapabilityActive;
    private bool IsTemplatesLibraryActive => _navigationCoordinator.IsTemplatesLibraryActive;
    private bool IsTemplatesEditorActive => _navigationCoordinator.IsTemplatesEditorActive;
    private bool IsTemplatesBuilderActive => _navigationCoordinator.IsTemplatesBuilderActive;
    private bool IsTemplatesCapabilityActive => _navigationCoordinator.IsTemplatesCapabilityActive;

    private void ApplyState()
    {
        _themeManager.ApplyTheme();
        _navigationCoordinator.ApplyState();
    }

    private void ApplyRightPanelState() => _shellPanelStateManager.ApplyRightPanelState();

    private void ApplyCapabilityShellState()
    {
        _deployCapabilityRuntime.ApplyShellState();
        _templatesCapabilityRuntime.ApplyShellState();
    }

    private void ResetRightPanelForCapabilitySwitch(string incomingCapabilityKey) =>
        _shellPanelStateManager.ResetForCapabilitySwitch(incomingCapabilityKey);

    private void RequestDeployResultsPanelToggle() => _shellPanelStateManager.TogglePanel();

    private void NavigateToRoute(string routeKey) => _navigationCoordinator.NavigateToRoute(routeKey);

    private Task<string?> PickTemplateFileForOpenAsync() => _dialogService.PickTemplateFileForOpenAsync();

    private Task<string?> PickTemplateFileForSaveAsync(string suggestedFileName) =>
        _dialogService.PickTemplateFileForSaveAsync(suggestedFileName);

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
}
