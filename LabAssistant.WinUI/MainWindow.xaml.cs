using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.Logging;
using LabAssistant.WinUI.Shell;
using LabAssistant.WinUI.ViewModels;
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
    private readonly ShellLayoutManager _layoutManager;
    private readonly ShellPanelStateManager _shellPanelStateManager;
    private readonly ShellNavigationCoordinator _navigationCoordinator;
    private readonly ShellThemeManager _themeManager;
    private readonly ShellDialogService _dialogService;
    private readonly ShellKeyboardHandler _keyboardHandler;
    private readonly IStructuredLogger _structuredLogger = App.Services.GetRequiredService<IStructuredLogger>();

    public MainWindow()
    {
        InitializeComponent();

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
            RightPanelTitleTextBlock,
            RightPanelContentHost,
            () => RootLayout.ActualWidth);
        var capabilityPageTypes = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["machines"] = typeof(Views.Machines.MachinesPage),
            ["assets"] = typeof(Views.Assets.AssetsPage),
            ["diagnostics"] = typeof(Views.Diagnostics.DiagnosticsPage),
            ["deploy"] = typeof(Views.Deploy.DeployPage),
            ["templates"] = typeof(Views.Templates.TemplatesPage),
            ["settings"] = typeof(Views.Settings.SettingsPage)
        };
        _navigationCoordinator = new ShellNavigationCoordinator(
            _shellViewModel,
            DispatcherQueue,
            GlobalNavigationView,
            CurrentRouteTextBlock,
            ContentTitleTextBlock,
            ContentDescriptionTextBlock,
            ApplyRightPanelState,
            ResetRightPanelForCapabilitySwitch,
            CapabilityFrame,
            capabilityPageTypes);
        _navigationCoordinator.SetShellHost(new ShellHost(
            _navigationCoordinator,
            () => RootLayout.XamlRoot,
            _dialogService,
            _shellPanelStateManager));

        // Shell chrome only: register the navigator the Templates editor hand-off uses to route to
        // the Templates capability. The transient TemplatesPage drains the pending document on entry.
        App.Services.GetRequiredService<ViewModels.Templates.ITemplateEditorHandoff>()
            .SetNavigator(() => NavigateToRoute(ShellRouteKeys.TemplatesEditor));

        ConfigureShellIcons();
        _navigationCoordinator.ConfigureNavigationView();
        _layoutManager.ApplyShellNavigationMode(1280);

        Title = ShellApplicationTitle;
        SetInitialSize(1280, 800);
        _themeManager.InitializeShellBranding();

        RootLayout.KeyDown += RootLayout_KeyDown;
        RootLayout.SizeChanged += RootLayout_SizeChanged;
        _layoutManager.CompactFallbackChanged += (_, isCompact) => _shellPanelStateManager.SetCompactFallback(isCompact);

        RootLayout.Loaded += (_, _) => RootLayout.Focus(FocusState.Programmatic);
        Closed += MainWindow_Closed;

        ApplyState();
    }

    private void ApplyState()
    {
        _themeManager.ApplyTheme();
        _navigationCoordinator.ApplyState();
    }

    /// <summary>
    /// Forces any live capability page through its real <c>OnNavigatedFrom</c>/<c>Unloaded</c>
    /// teardown (stopping timers, cancelling in-flight work) before the window is destroyed.
    /// Process exit alone would skip that lifecycle step and leak page-owned resources. Must
    /// stay synchronous and cannot throw: the window is already closing and there is no safe
    /// point to defer or retry cleanup.
    /// </summary>
    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        var operationId = Guid.NewGuid().ToString("N");
        var activeCapabilityKey = _navigationCoordinator.ActiveCapabilityKey;
        var context = new Dictionary<string, object?> { ["capability"] = activeCapabilityKey };

        try
        {
            if (CapabilityFrame.Content is not null && CapabilityFrame.Content.GetType() != typeof(Views.Shell.ShellBlankPage))
            {
                CapabilityFrame.Navigate(typeof(Views.Shell.ShellBlankPage));
            }

            _structuredLogger.Log(LaStatus.ShellUi_WindowTeardownComplete, operationId, "ok", context);
        }
        catch (Exception ex)
        {
            context["error"] = ex.Message;
            _structuredLogger.Log(LaStatus.ShellUi_WindowTeardownError, operationId, "failed", context);
        }
    }

    private void ApplyRightPanelState() => _shellPanelStateManager.ApplyRightPanelState();

    private void ResetRightPanelForCapabilitySwitch(string incomingCapabilityKey) =>
        _shellPanelStateManager.ResetForCapabilitySwitch(incomingCapabilityKey);

    private void NavigateToRoute(string routeKey) => _navigationCoordinator.NavigateToRoute(routeKey);

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
