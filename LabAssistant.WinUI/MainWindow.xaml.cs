using System.Collections.ObjectModel;
using LabAssistant.Business.Machines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using LabAssistant.WinUI.Theming;
using LabAssistant.WinUI.ViewModels;
using WinRT.Interop;

namespace LabAssistant.WinUI;

public sealed partial class MainWindow : Window
{
    private const double DrawerWidth = 280;
    private const int DrawerAnimationDurationMs = 180;

    private readonly ShellViewModel _shellViewModel = new();
    private readonly IMachinesCapabilityService _machinesCapabilityService;
    private readonly ObservableCollection<MachineInventoryItem> _machineInventory = [];
    private ShellCapability _activeCapability;
    private ShellSubview _activeSubview;
    private MachineInventoryItem? _selectedMachine;
    private bool _isMachineActionRunning;
    private bool _isDrawerOpen;
    private bool _isInsightsOpen;
    private ElementTheme _theme = ElementTheme.Light;
    private int _issueCount = 3;

    public MainWindow()
    {
        InitializeComponent();
        _machinesCapabilityService = App.Services.GetRequiredService<IMachinesCapabilityService>();
        _activeCapability = _shellViewModel.GetCapability("Machines");
        _activeSubview = _activeCapability.DefaultSubview;
        MachinesListView.ItemsSource = _machineInventory;
        ConfigureShellIcons();
        InitializeDrawer();
        Title = "LabAssistant.WinUI";
        SetInitialSize(1280, 800);
        RootLayout.KeyDown += RootLayout_KeyDown;
        RootLayout.Loaded += async (_, _) =>
        {
            RootLayout.Focus(FocusState.Programmatic);
            await EnsureMachinesInventoryAsync(forceRefresh: true);
        };
        ApplyState();
    }

    private void SetInitialSize(int width, int height)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow?.Resize(new Windows.Graphics.SizeInt32(width, height));
    }

    private void ConfigureShellIcons()
    {
        HamburgerButton.Content = CreateIconGlyph(ShellIconToken.Menu);
        InsightsToggleButton.Content = CreateIconGlyph(ShellIconToken.Insights);

        MachinesRailButton.Content = CreateIconGlyph(ShellIconToken.Machines);
        DeployRailButton.Content = CreateIconGlyph(ShellIconToken.Deploy);
        TemplatesRailButton.Content = CreateIconGlyph(ShellIconToken.Templates);
        AssetsRailButton.Content = CreateIconGlyph(ShellIconToken.Assets);
        DiagnosticsRailButton.Content = CreateIconGlyph(ShellIconToken.Diagnostics);
        SettingsRailButton.Content = CreateIconGlyph(ShellIconToken.Settings);

        MachinesDrawerButton.Content = CreateDrawerButtonContent(ShellIconToken.Machines, "Machines");
        DeployDrawerButton.Content = CreateDrawerButtonContent(ShellIconToken.Deploy, "Deploy");
        TemplatesDrawerButton.Content = CreateDrawerButtonContent(ShellIconToken.Templates, "Templates");
        AssetsDrawerButton.Content = CreateDrawerButtonContent(ShellIconToken.Assets, "Assets");
        DiagnosticsDrawerButton.Content = CreateDrawerButtonContent(ShellIconToken.Diagnostics, "Diagnostics");
        SettingsDrawerButton.Content = CreateDrawerButtonContent(ShellIconToken.Settings, "Settings");
    }

    private object CreateDrawerButtonContent(string token, string label)
    {
        var container = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        container.Children.Add(CreateIconGlyph(token));
        container.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTopBarForegroundBrush"]
        });
        return container;
    }

    private TextBlock CreateIconGlyph(string token)
    {
        return new TextBlock
        {
            Text = ShellIconCatalog.GetGlyph(token),
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe MDL2 Assets"),
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTopBarForegroundBrush"]
        };
    }

    private void InitializeDrawer()
    {
        DrawerTranslateTransform.X = -DrawerWidth;
        CapabilityDrawer.Visibility = Visibility.Collapsed;
        DrawerScrim.Visibility = Visibility.Collapsed;
    }

    private void SetDrawerOpen(bool isOpen)
    {
        if (_isDrawerOpen == isOpen)
        {
            return;
        }

        _isDrawerOpen = isOpen;

        if (isOpen)
        {
            DrawerScrim.Visibility = Visibility.Visible;
            CapabilityDrawer.Visibility = Visibility.Visible;
            AnimateDrawer(-DrawerWidth, 0, onCompleted: null);
            return;
        }

        AnimateDrawer(DrawerTranslateTransform.X, -DrawerWidth, () =>
        {
            CapabilityDrawer.Visibility = Visibility.Collapsed;
            DrawerScrim.Visibility = Visibility.Collapsed;
        });
    }

    private void AnimateDrawer(double from, double to, Action? onCompleted)
    {
        var storyboard = new Storyboard();
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(DrawerAnimationDurationMs),
            EnableDependentAnimation = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        Storyboard.SetTarget(animation, DrawerTranslateTransform);
        Storyboard.SetTargetProperty(animation, nameof(DrawerTranslateTransform.X));
        storyboard.Children.Add(animation);

        if (onCompleted is not null)
        {
            storyboard.Completed += (_, _) => onCompleted();
        }

        storyboard.Begin();
    }

    private void ApplyState()
    {
        BreadcrumbTextBlock.Text = $"{_activeCapability.DisplayName} > {_activeSubview.DisplayName}";
        ContentTitleTextBlock.Text = _activeCapability.DisplayName;
        ContentDescriptionTextBlock.Text = IsMachinesOverviewActive
            ? "Manage host Hyper-V VMs. Start/stop/restart, open console, or delete with explicit scope."
            : $"Subview: {_activeSubview.DisplayName}. This is scaffold-only placeholder content for AA2b.";
        ThemeToggleButton.Content = _theme == ElementTheme.Light ? "Switch to dark" : "Switch to light";
        RootLayout.RequestedTheme = _theme;
        InsightsPanel.Visibility = _isInsightsOpen ? Visibility.Visible : Visibility.Collapsed;
        IssueBadge.Visibility = _issueCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        IssueBadgeTextBlock.Text = _issueCount.ToString();
        MachinesOverviewPanel.Visibility = IsMachinesOverviewActive ? Visibility.Visible : Visibility.Collapsed;
        NonMachinesPlaceholderTextBlock.Visibility = IsMachinesOverviewActive ? Visibility.Collapsed : Visibility.Visible;
        RenderSubviewSelector();
        RenderSubviewToolbar();
        UpdateMachineActionButtons();
    }

    private void RenderSubviewSelector()
    {
        SubviewSelectorPanel.Children.Clear();

        foreach (var subview in _activeCapability.Subviews)
        {
            var button = new Button
            {
                Content = subview.DisplayName,
                Tag = subview.Key,
                Height = 32,
                Padding = new Thickness(12, 0, 12, 0),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellBorderBrush"],
                Foreground = subview.Key == _activeSubview.Key
                    ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTopBarForegroundBrush"]
                    : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTextPrimaryBrush"],
                Background = subview.Key == _activeSubview.Key
                    ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellAccentBrush"]
                    : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellContentBackgroundBrush"]
            };

            button.Click += SubviewButton_Click;
            SubviewSelectorPanel.Children.Add(button);
        }
    }

    private void RenderSubviewToolbar()
    {
        SubviewToolbarPanel.Children.Clear();

        foreach (var action in _activeSubview.ToolbarActions)
        {
            var button = new Button
            {
                Content = action,
                IsEnabled = false,
                Height = 32,
                Padding = new Thickness(10, 0, 10, 0)
            };

            SubviewToolbarPanel.Children.Add(button);
        }
    }

    private void HamburgerButton_Click(object sender, RoutedEventArgs e)
    {
        SetDrawerOpen(!_isDrawerOpen);
        ApplyState();
    }

    private void CapabilityButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string capability } &&
            _shellViewModel.Capabilities.Any(entry => string.Equals(entry.DisplayName, capability, StringComparison.Ordinal)))
        {
            _activeCapability = _shellViewModel.GetCapability(capability);
            _activeSubview = _activeCapability.DefaultSubview;
            SetDrawerOpen(false);
            ApplyState();
            if (IsMachinesOverviewActive)
            {
                _ = EnsureMachinesInventoryAsync(forceRefresh: false);
            }
        }
    }

    private void SubviewButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string subviewKey })
        {
            return;
        }

        var selectedSubview = _activeCapability.Subviews.FirstOrDefault(
            subview => string.Equals(subview.Key, subviewKey, StringComparison.Ordinal));

        if (selectedSubview is null)
        {
            return;
        }

        _activeSubview = selectedSubview;
        ApplyState();
        if (IsMachinesOverviewActive)
        {
            _ = EnsureMachinesInventoryAsync(forceRefresh: false);
        }
    }

    private void InsightsButton_Click(object sender, RoutedEventArgs e)
    {
        _isInsightsOpen = !_isInsightsOpen;
        ApplyState();
    }

    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        _theme = _theme == ElementTheme.Light ? ElementTheme.Dark : ElementTheme.Light;
        ApplyState();
    }

    private void DrawerScrim_Tapped(object sender, TappedRoutedEventArgs e)
    {
        SetDrawerOpen(false);
        ApplyState();
    }

    private void RootLayout_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape && _isDrawerOpen)
        {
            SetDrawerOpen(false);
            ApplyState();
            e.Handled = true;
        }
    }

    private void EscapeAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (_isDrawerOpen)
        {
            SetDrawerOpen(false);
            ApplyState();
            args.Handled = true;
        }
    }

    private bool IsMachinesOverviewActive =>
        string.Equals(_activeCapability.DisplayName, "Machines", StringComparison.Ordinal) &&
        string.Equals(_activeSubview.Key, "overview", StringComparison.Ordinal);

    private async Task EnsureMachinesInventoryAsync(bool forceRefresh)
    {
        if (!IsMachinesOverviewActive)
        {
            return;
        }

        if (!forceRefresh && _machineInventory.Count > 0)
        {
            return;
        }

        RefreshMachinesButton.IsEnabled = false;
        MachinesStatusTextBlock.Text = "Loading host VM inventory...";

        try
        {
            var inventory = await _machinesCapabilityService.LoadInventoryAsync();
            var selectedVmName = _selectedMachine?.VmName;

            _machineInventory.Clear();
            foreach (var vm in inventory.OrderBy(vm => vm.VmName, StringComparer.OrdinalIgnoreCase))
            {
                _machineInventory.Add(vm);
            }

            _selectedMachine = _machineInventory.FirstOrDefault(vm =>
                string.Equals(vm.VmName, selectedVmName, StringComparison.Ordinal));
            MachinesListView.SelectedItem = _selectedMachine;

            if (_machineInventory.Count == 0)
            {
                MachinesStatusTextBlock.Text = "No Hyper-V VMs found on this host.";
            }
            else
            {
                MachinesStatusTextBlock.Text = $"Loaded {_machineInventory.Count} VM(s).";
            }
        }
        catch (Exception ex)
        {
            MachinesStatusTextBlock.Text = $"Failed to load VM inventory. {ex.Message}";
        }
        finally
        {
            RefreshMachinesButton.IsEnabled = true;
            UpdateMachineDetails();
            UpdateMachineActionButtons();
        }
    }

    private void MachinesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedMachine = MachinesListView.SelectedItem as MachineInventoryItem;
        UpdateMachineDetails();
        UpdateMachineActionButtons();
    }

    private void UpdateMachineDetails()
    {
        if (_selectedMachine is null)
        {
            SelectedVmNameTextBlock.Text = "Name: (none)";
            SelectedVmStateTextBlock.Text = "State: -";
            SelectedVmOriginTextBlock.Text = "Origin: -";
            SelectedVmIdTextBlock.Text = "VM Id: -";
            SelectedVmPathTextBlock.Text = "Path: -";
            return;
        }

        SelectedVmNameTextBlock.Text = $"Name: {_selectedMachine.VmName}";
        SelectedVmStateTextBlock.Text = $"State: {_selectedMachine.State}";
        SelectedVmOriginTextBlock.Text = $"Origin: {_selectedMachine.OriginLabel}";
        SelectedVmIdTextBlock.Text = $"VM Id: {_selectedMachine.VmId}";
        SelectedVmPathTextBlock.Text = $"Path: {(_selectedMachine.VmPath ?? "-")}";
    }

    private void UpdateMachineActionButtons()
    {
        var hasSelection = _selectedMachine is not null;
        var canRunActions = hasSelection && !_isMachineActionRunning;
        StartVmButton.IsEnabled = canRunActions;
        StopVmButton.IsEnabled = canRunActions;
        RestartVmButton.IsEnabled = canRunActions;
        OpenConsoleButton.IsEnabled = canRunActions;
        DeleteVmButton.IsEnabled = canRunActions;
        OpenRdpButton.IsEnabled = false;
    }

    private async void RefreshMachinesButton_Click(object sender, RoutedEventArgs e)
    {
        await EnsureMachinesInventoryAsync(forceRefresh: true);
    }

    private async void StartVmButton_Click(object sender, RoutedEventArgs e)
    {
        await RunMachineOperationAsync(
            "Starting VM...",
            vm => _machinesCapabilityService.StartVmAsync(vm),
            refreshInventory: true);
    }

    private async void StopVmButton_Click(object sender, RoutedEventArgs e)
    {
        await RunMachineOperationAsync(
            "Stopping VM...",
            vm => _machinesCapabilityService.StopVmAsync(vm),
            refreshInventory: true);
    }

    private async void RestartVmButton_Click(object sender, RoutedEventArgs e)
    {
        await RunMachineOperationAsync(
            "Restarting VM...",
            vm => _machinesCapabilityService.RestartVmAsync(vm),
            refreshInventory: true);
    }

    private async void OpenConsoleButton_Click(object sender, RoutedEventArgs e)
    {
        await RunMachineOperationAsync(
            "Opening Hyper-V Console...",
            vm => _machinesCapabilityService.OpenConsoleAsync(vm),
            refreshInventory: false);
    }

    private async void DeleteVmButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMachine is null)
        {
            MachinesStatusTextBlock.Text = "Select a VM before running actions.";
            return;
        }

        var selectedScope = await ShowDeleteScopeDialogAsync(_selectedMachine);
        if (selectedScope is null)
        {
            MachinesStatusTextBlock.Text = "Delete cancelled.";
            return;
        }

        await RunMachineOperationAsync(
            "Deleting VM...",
            vm => _machinesCapabilityService.DeleteVmAsync(vm, selectedScope.Value),
            refreshInventory: true,
            clearSelectionOnSuccess: true);
    }

    private async Task RunMachineOperationAsync(
        string pendingMessage,
        Func<MachineInventoryItem, Task<MachineOperationResult>> operation,
        bool refreshInventory,
        bool clearSelectionOnSuccess = false)
    {
        if (_selectedMachine is null)
        {
            MachinesStatusTextBlock.Text = "Select a VM before running actions.";
            return;
        }

        _isMachineActionRunning = true;
        UpdateMachineActionButtons();
        MachinesStatusTextBlock.Text = pendingMessage;

        var vm = _selectedMachine;
        try
        {
            var result = await operation(vm);
            MachinesStatusTextBlock.Text = $"{result.UserMessage} (operationId: {result.OperationId})";

            if (result.Success && clearSelectionOnSuccess)
            {
                _selectedMachine = null;
                MachinesListView.SelectedItem = null;
            }

            if (refreshInventory)
            {
                await EnsureMachinesInventoryAsync(forceRefresh: true);
            }
            else
            {
                UpdateMachineDetails();
            }
        }
        finally
        {
            _isMachineActionRunning = false;
            UpdateMachineActionButtons();
        }
    }

    private async Task<MachineDeleteScope?> ShowDeleteScopeDialogAsync(MachineInventoryItem vm)
    {
        var vmOnlyRadio = new RadioButton
        {
            Content = "VM registration only",
            IsChecked = true
        };
        var vmAndStorageRadio = new RadioButton
        {
            Content = "VM + associated disks/files"
        };
        var confirmationCheck = new CheckBox
        {
            Content = $"I confirm I want to delete '{vm.VmName}'."
        };

        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = "Choose delete scope. This action is destructive.",
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(vmOnlyRadio);
        content.Children.Add(vmAndStorageRadio);
        content.Children.Add(new TextBlock
        {
            Text = "If deleting with storage, associated disks/files will be removed where possible.",
            TextWrapping = TextWrapping.Wrap
        });
        content.Children.Add(confirmationCheck);

        var dialog = new ContentDialog
        {
            Title = "Delete VM",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false,
            XamlRoot = RootLayout.XamlRoot,
            Content = content
        };

        confirmationCheck.Checked += (_, _) => dialog.IsPrimaryButtonEnabled = true;
        confirmationCheck.Unchecked += (_, _) => dialog.IsPrimaryButtonEnabled = false;

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        return vmAndStorageRadio.IsChecked == true
            ? MachineDeleteScope.VmAndStorage
            : MachineDeleteScope.VmRegistrationOnly;
    }
}
