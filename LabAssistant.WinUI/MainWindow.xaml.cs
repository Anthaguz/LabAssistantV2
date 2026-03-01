using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using LabAssistant.Business.Machines;
using LabAssistant.Services.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using LabAssistant.WinUI.Theming;
using LabAssistant.WinUI.ViewModels;
using Microsoft.UI.Dispatching;
using WinRT.Interop;

namespace LabAssistant.WinUI;

public sealed partial class MainWindow : Window
{
    private const double DrawerWidth = 280;
    private const int DrawerAnimationDurationMs = 180;

    private readonly ShellViewModel _shellViewModel = new();
    private readonly IMachinesCapabilityService _machinesCapabilityService;
    private readonly IStructuredLogViewerService _structuredLogViewerService;
    private readonly ObservableCollection<MachineInventoryItem> _machineInventory = [];
    private readonly ObservableCollection<StructuredLogViewerEntry> _structuredLogEntries = [];
    private readonly Dictionary<string, MachineRdpReadinessResult> _rdpReadinessByVmKey = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<string> _availableSwitches = Array.Empty<string>();
    private ShellCapability _activeCapability;
    private ShellSubview _activeSubview;
    private MachineInventoryItem? _selectedMachine;
    private MachineRdpReadinessResult _selectedRdpReadiness = CreateUnknownReadiness("Select a VM to check RDP readiness.");
    private MachineEditSnapshot? _loadedEditSnapshot;
    private MachineEditDraft? _editDraft;
    private StructuredLogViewerEntry? _selectedStructuredLogEntry;
    private bool _isMachineActionRunning;
    private bool _isRdpReadinessRefreshRunning;
    private bool _isMachineEditLoading;
    private bool _isMachineEditApplying;
    private bool _isUpdatingMachineEditControls;
    private bool _isSavingDeletionPolicy;
    private bool _isStructuredLogsLoading;
    private bool _isDrawerOpen;
    private bool _isInsightsOpen;
    private ElementTheme _theme = ElementTheme.Light;
    private int _issueCount = 3;
    private DispatcherQueueTimer? _rdpReadinessTimer;
    private DateTimeOffset _lastRdpReadinessRefreshUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _lastOnDemandRdpRefreshUtc = DateTimeOffset.MinValue;

    public MainWindow()
    {
        InitializeComponent();
        _machinesCapabilityService = App.Services.GetRequiredService<IMachinesCapabilityService>();
        _structuredLogViewerService = App.Services.GetRequiredService<IStructuredLogViewerService>();
        _activeCapability = _shellViewModel.GetCapability("Machines");
        _activeSubview = _activeCapability.DefaultSubview;
        MachinesListView.ItemsSource = _machineInventory;
        StructuredLogsListView.ItemsSource = _structuredLogEntries;
        ConfigureShellIcons();
        InitializeDrawer();
        Title = "LabAssistant.WinUI";
        SetInitialSize(1280, 800);
        RootLayout.KeyDown += RootLayout_KeyDown;
        InitializeRdpReadinessTimer();
        RootLayout.Loaded += async (_, _) =>
        {
            RootLayout.Focus(FocusState.Programmatic);
            await EnsureMachinesInventoryAsync(forceRefresh: true);
            await LoadMachinesDeletionPolicyAsync();
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
            : IsSettingsMachinesActive
                ? "Configure Machines policy defaults."
                : IsDiagnosticsLogsActive
                    ? "Inspect canonical structured logs with envelope fields and dynamic context."
                : $"Subview: {_activeSubview.DisplayName}. This is scaffold-only placeholder content for AA2b.";
        ThemeToggleButton.Content = _theme == ElementTheme.Light ? "Switch to dark" : "Switch to light";
        RootLayout.RequestedTheme = _theme;
        InsightsPanel.Visibility = _isInsightsOpen ? Visibility.Visible : Visibility.Collapsed;
        IssueBadge.Visibility = _issueCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        IssueBadgeTextBlock.Text = _issueCount.ToString();
        MachinesOverviewPanel.Visibility = IsMachinesOverviewActive ? Visibility.Visible : Visibility.Collapsed;
        SettingsMachinesPanel.Visibility = IsSettingsMachinesActive ? Visibility.Visible : Visibility.Collapsed;
        DiagnosticsLogsPanel.Visibility = IsDiagnosticsLogsActive ? Visibility.Visible : Visibility.Collapsed;
        NonMachinesPlaceholderTextBlock.Visibility = (IsMachinesOverviewActive || IsSettingsMachinesActive || IsDiagnosticsLogsActive) ? Visibility.Collapsed : Visibility.Visible;
        RenderSubviewSelector();
        RenderSubviewToolbar();
        UpdateReadinessPollingState();
        UpdateRdpReadinessUi();
        UpdateMachineActionButtons();
        if (IsSettingsMachinesActive)
        {
            _ = LoadMachinesDeletionPolicyAsync();
        }

        if (IsDiagnosticsLogsActive)
        {
            _ = EnsureStructuredLogsLoadedAsync(forceReload: false);
        }
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
            DiscardMachineEditDraft();
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

        DiscardMachineEditDraft();
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

    private bool IsSettingsMachinesActive =>
        string.Equals(_activeCapability.DisplayName, "Settings", StringComparison.Ordinal) &&
        string.Equals(_activeSubview.Key, "machines", StringComparison.Ordinal);

    private bool IsDiagnosticsLogsActive =>
        string.Equals(_activeCapability.DisplayName, "Diagnostics", StringComparison.Ordinal) &&
        string.Equals(_activeSubview.Key, "logs", StringComparison.Ordinal);

    private void InitializeRdpReadinessTimer()
    {
        _rdpReadinessTimer = DispatcherQueue.CreateTimer();
        _rdpReadinessTimer.Interval = TimeSpan.FromMinutes(5);
        _rdpReadinessTimer.Tick += async (_, _) => await RefreshRdpReadinessAsync(selectedOnly: false);
    }

    private void UpdateReadinessPollingState()
    {
        if (_rdpReadinessTimer is null)
        {
            return;
        }

        if (IsMachinesOverviewActive && _machineInventory.Count > 0)
        {
            if (!_rdpReadinessTimer.IsRunning)
            {
                _rdpReadinessTimer.Start();

                // Run one pass when Machines becomes active, then fall back to periodic checks.
                if (DateTimeOffset.UtcNow - _lastRdpReadinessRefreshUtc >= _rdpReadinessTimer.Interval)
                {
                    _ = RefreshRdpReadinessAsync(selectedOnly: false);
                }
            }

            return;
        }

        if (_rdpReadinessTimer.IsRunning)
        {
            _rdpReadinessTimer.Stop();
        }

    }

    private async Task<bool> EnsureMachinesInventoryAsync(bool forceRefresh)
    {
        if (!IsMachinesOverviewActive)
        {
            return false;
        }

        if (!forceRefresh && _machineInventory.Count > 0)
        {
            return true;
        }

        RefreshMachinesButton.IsEnabled = false;
        MachinesStatusTextBlock.Text = "Loading host VM inventory...";

        try
        {
            var inventory = await _machinesCapabilityService.LoadInventoryAsync();
            var selectedVmName = _selectedMachine?.VmName;
            var orderedInventory = inventory
                .OrderBy(vm => vm.VmName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Keep the currently displayed list until a new fetch succeeds.
            _machineInventory.Clear();
            foreach (var vm in orderedInventory)
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

            SyncRdpReadinessCache();
            _ = LoadMachineEditStateAsync();

            return true;
        }
        catch (Exception ex)
        {
            MachinesStatusTextBlock.Text = $"Failed to load VM inventory. Showing last known list. {ex.Message}";
            return false;
        }
        finally
        {
            RefreshMachinesButton.IsEnabled = true;
            UpdateMachineDetails();
            UpdateMachineActionButtons();
        }
    }

    private async Task EnsureStructuredLogsLoadedAsync(bool forceReload)
    {
        if (!IsDiagnosticsLogsActive || _isStructuredLogsLoading)
        {
            return;
        }

        if (!forceReload && _structuredLogEntries.Count > 0)
        {
            return;
        }

        _isStructuredLogsLoading = true;
        ApplyLogFiltersButton.IsEnabled = false;
        ClearLogFiltersButton.IsEnabled = false;
        ReloadLogsButton.IsEnabled = false;
        OpenRawJsonlButton.IsEnabled = false;
        LogsStatusTextBlock.Text = "Loading structured logs...";

        try
        {
            var filter = BuildStructuredLogFilter();
            var result = await _structuredLogViewerService.LoadAsync(filter);

            _structuredLogEntries.Clear();
            foreach (var entry in result.Entries)
            {
                _structuredLogEntries.Add(entry);
            }

            StructuredLogsListView.SelectedItem = null;
            _selectedStructuredLogEntry = null;
            UpdateStructuredLogSelectionDetails();

            var filePath = _structuredLogViewerService.GetStructuredLogFilePath();
            var parseErrorSuffix = result.ParseErrorCount > 0
                ? $" Skipped malformed lines: {result.ParseErrorCount}."
                : string.Empty;
            LogsStatusTextBlock.Text = File.Exists(filePath)
                ? $"Loaded {_structuredLogEntries.Count} events from {result.TotalLineCount} lines.{parseErrorSuffix}"
                : $"Structured log file not found yet: {filePath}";
        }
        catch (Exception ex)
        {
            LogsStatusTextBlock.Text = $"Failed to load structured logs. {ex.Message}";
        }
        finally
        {
            _isStructuredLogsLoading = false;
            ApplyLogFiltersButton.IsEnabled = true;
            ClearLogFiltersButton.IsEnabled = true;
            ReloadLogsButton.IsEnabled = true;
            OpenRawJsonlButton.IsEnabled = true;
        }
    }

    private StructuredLogViewerFilter BuildStructuredLogFilter()
    {
        return new StructuredLogViewerFilter
        {
            OperationId = NormalizeFilterText(LogFilterOperationIdTextBox.Text),
            Level = NormalizeFilterText(LogFilterLevelTextBox.Text),
            Event = NormalizeFilterText(LogFilterEventTextBox.Text),
            TextSearch = NormalizeFilterText(LogFilterTextSearchTextBox.Text),
            StartUtc = LogFilterUseStartDateCheckBox.IsChecked == true
                ? ToDateBoundaryUtc(LogFilterStartDatePicker.Date, isEndBoundary: false)
                : null,
            EndUtc = LogFilterUseEndDateCheckBox.IsChecked == true
                ? ToDateBoundaryUtc(LogFilterEndDatePicker.Date, isEndBoundary: true)
                : null
        };
    }

    private void UpdateStructuredLogSelectionDetails()
    {
        if (_selectedStructuredLogEntry is null)
        {
            SelectedLogEnvelopeTextBlock.Text = "Select a log entry.";
            SelectedLogContextTextBox.Text = string.Empty;
            return;
        }

        SelectedLogEnvelopeTextBlock.Text =
            $"ts={_selectedStructuredLogEntry.TimestampText} | level={_selectedStructuredLogEntry.Level} | event={_selectedStructuredLogEntry.Event} | operationId={_selectedStructuredLogEntry.OperationId} | result={_selectedStructuredLogEntry.Result}";
        SelectedLogContextTextBox.Text = FormatJsonForDetails(_selectedStructuredLogEntry.ContextJson);
    }

    private static string NormalizeFilterText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim();
    }

    private static DateTimeOffset ToDateBoundaryUtc(DateTimeOffset date, bool isEndBoundary)
    {
        var selectedDate = date.Date;
        var localBoundary = isEndBoundary
            ? selectedDate.AddDays(1).AddTicks(-1)
            : selectedDate;
        return localBoundary.ToUniversalTime();
    }

    private static string FormatJsonForDetails(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "{}";
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch
        {
            return json;
        }
    }

    private void MachinesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        DiscardMachineEditDraft();
        _selectedMachine = MachinesListView.SelectedItem as MachineInventoryItem;
        UpdateSelectedRdpReadinessFromCache();
        UpdateMachineDetails();
        UpdateRdpReadinessUi();
        UpdateMachineActionButtons();
        _ = LoadMachineEditStateAsync();
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
            ClearMachineEditControls();
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
        OpenRdpButton.IsEnabled = canRunActions && _selectedRdpReadiness.State == MachineRdpReadinessState.Ready;
        ToolTipService.SetToolTip(OpenRdpButton, _selectedRdpReadiness.Message);
        ApplyMachineEditsButton.IsEnabled = _selectedMachine is not null &&
            !_isMachineActionRunning &&
            !_isMachineEditLoading &&
            !_isMachineEditApplying &&
            HasMachineEditChanges();
    }

    private async void RefreshMachinesButton_Click(object sender, RoutedEventArgs e)
    {
        await EnsureMachinesInventoryAsync(forceRefresh: true);
    }

    private async void ReloadLogsButton_Click(object sender, RoutedEventArgs e)
    {
        await EnsureStructuredLogsLoadedAsync(forceReload: true);
    }

    private async void ApplyLogFiltersButton_Click(object sender, RoutedEventArgs e)
    {
        await EnsureStructuredLogsLoadedAsync(forceReload: true);
    }

    private async void ClearLogFiltersButton_Click(object sender, RoutedEventArgs e)
    {
        LogFilterOperationIdTextBox.Text = string.Empty;
        LogFilterLevelTextBox.Text = string.Empty;
        LogFilterEventTextBox.Text = string.Empty;
        LogFilterTextSearchTextBox.Text = string.Empty;
        LogFilterUseStartDateCheckBox.IsChecked = false;
        LogFilterUseEndDateCheckBox.IsChecked = false;
        LogFilterStartDatePicker.Date = DateTimeOffset.Now;
        LogFilterEndDatePicker.Date = DateTimeOffset.Now;
        await EnsureStructuredLogsLoadedAsync(forceReload: true);
    }

    private void StructuredLogsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedStructuredLogEntry = StructuredLogsListView.SelectedItem as StructuredLogViewerEntry;
        UpdateStructuredLogSelectionDetails();
    }

    private void OpenRawJsonlButton_Click(object sender, RoutedEventArgs e)
    {
        var filePath = _structuredLogViewerService.GetStructuredLogFilePath();
        try
        {
            if (File.Exists(filePath))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"")
                {
                    UseShellExecute = true
                });
                return;
            }

            var folderPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folderPath}\"")
                {
                    UseShellExecute = true
                });
                LogsStatusTextBlock.Text = $"Active structured log file not found. Opened log folder: {folderPath}";
                return;
            }

            LogsStatusTextBlock.Text = $"Structured log path does not exist yet: {filePath}";
        }
        catch (Exception ex)
        {
            LogsStatusTextBlock.Text = $"Failed to open structured log location. {ex.Message}";
        }
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

    private async void OpenRdpButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMachine is null)
        {
            MachinesStatusTextBlock.Text = "Select a VM before running actions.";
            return;
        }

        if (_selectedRdpReadiness.State != MachineRdpReadinessState.Ready ||
            string.IsNullOrWhiteSpace(_selectedRdpReadiness.TargetIpv4))
        {
            MachinesStatusTextBlock.Text = $"RDP not ready. {_selectedRdpReadiness.Message}";
            if (DateTimeOffset.UtcNow - _lastOnDemandRdpRefreshUtc >= TimeSpan.FromSeconds(2))
            {
                _lastOnDemandRdpRefreshUtc = DateTimeOffset.UtcNow;
                await RefreshRdpReadinessAsync(selectedOnly: true);
            }
            return;
        }

        var targetIpv4 = _selectedRdpReadiness.TargetIpv4;

        await RunMachineOperationAsync(
            "Opening RDP...",
            vm => _machinesCapabilityService.OpenRdpAsync(vm, targetIpv4!),
            refreshInventory: false);
    }

    private async void DeleteVmButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMachine is null)
        {
            MachinesStatusTextBlock.Text = "Select a VM before running actions.";
            return;
        }

        MachineDeletePreview preview;
        try
        {
            preview = await _machinesCapabilityService.GetDeletePreviewAsync(_selectedMachine);
        }
        catch (Exception ex)
        {
            MachinesStatusTextBlock.Text = $"Failed to evaluate delete policy/classification. {ex.Message}";
            return;
        }

        MachineDeleteScope? selectedScope;
        var policyCanAutoSelectScope = preview.PolicyMode != MachineDeletionPolicyMode.AskEveryTime &&
            preview.DefaultScope == MachineDeleteScope.VmAndStorage &&
            preview.SafeForAutomaticStorageDeletion;

        if (policyCanAutoSelectScope)
        {
            var confirmed = await ShowDeleteConfirmationDialogAsync(_selectedMachine, preview, preview.DefaultScope);
            selectedScope = confirmed ? preview.DefaultScope : null;
        }
        else
        {
            selectedScope = await ShowDeleteScopeDialogAsync(_selectedMachine, preview);
        }

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
                var refreshSucceeded = await EnsureMachinesInventoryAsync(forceRefresh: true);
                if (!refreshSucceeded)
                {
                    MachinesStatusTextBlock.Text = $"{result.UserMessage} Inventory refresh failed; showing last known list.";
                }
                else if (_selectedMachine is not null)
                {
                    await RefreshRdpReadinessAsync(selectedOnly: true);
                }
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

    private async Task LoadMachineEditStateAsync()
    {
        if (!IsMachinesOverviewActive || _selectedMachine is null)
        {
            ClearMachineEditControls();
            return;
        }

        _isMachineEditLoading = true;
        UpdateMachineActionButtons();
        try
        {
            var snapshot = await _machinesCapabilityService.LoadEditSnapshotAsync(_selectedMachine);
            _availableSwitches = await _machinesCapabilityService.LoadVirtualSwitchesAsync();
            _loadedEditSnapshot = snapshot;
            if (snapshot is null)
            {
                _editDraft = null;
                ClearMachineEditControls();
                MachinesStatusTextBlock.Text = "Unable to load editable VM settings.";
                return;
            }

            _editDraft = CreateDraft(snapshot, Array.Empty<string>());
            ApplyMachineEditDraftToControls();
        }
        catch (Exception ex)
        {
            _editDraft = null;
            _loadedEditSnapshot = null;
            ClearMachineEditControls();
            MachinesStatusTextBlock.Text = $"Failed to load VM edit state. {ex.Message}";
        }
        finally
        {
            _isMachineEditLoading = false;
            UpdateMachineActionButtons();
        }
    }

    private void DiscardMachineEditDraft()
    {
        _loadedEditSnapshot = null;
        _editDraft = null;
        _isUpdatingMachineEditControls = false;
        UpdateMachineEditDirtyIndicator();
    }

    private void ClearMachineEditControls()
    {
        _isUpdatingMachineEditControls = true;
        CpuCountTextBox.Text = string.Empty;
        StartupMemoryTextBox.Text = string.Empty;
        DynamicMemoryToggle.IsOn = false;
        MinimumMemoryTextBox.Text = string.Empty;
        MaximumMemoryTextBox.Text = string.Empty;
        MemoryBufferTextBox.Text = string.Empty;
        NetworkAdapterEditorPanel.Children.Clear();
        DynamicMemoryPanel.IsHitTestVisible = false;
        DynamicMemoryPanel.Opacity = 0.65;
        _isUpdatingMachineEditControls = false;
        UpdateMachineEditDirtyIndicator();
    }

    private void ApplyMachineEditDraftToControls()
    {
        if (_editDraft is null)
        {
            ClearMachineEditControls();
            return;
        }

        _isUpdatingMachineEditControls = true;
        CpuCountTextBox.Text = _editDraft.CpuCount.ToString();
        StartupMemoryTextBox.Text = _editDraft.StartupMemoryMb.ToString();
        DynamicMemoryToggle.IsOn = _editDraft.DynamicMemoryEnabled;
        MinimumMemoryTextBox.Text = _editDraft.MinimumMemoryMb.ToString();
        MaximumMemoryTextBox.Text = _editDraft.MaximumMemoryMb.ToString();
        MemoryBufferTextBox.Text = _editDraft.MemoryBufferPercent.ToString();
        DynamicMemoryPanel.IsHitTestVisible = _editDraft.DynamicMemoryEnabled;
        DynamicMemoryPanel.Opacity = _editDraft.DynamicMemoryEnabled ? 1.0 : 0.65;
        RenderNetworkAdapterEditors();
        _isUpdatingMachineEditControls = false;
        UpdateMachineEditDirtyIndicator();
    }

    private void RenderNetworkAdapterEditors()
    {
        NetworkAdapterEditorPanel.Children.Clear();
        if (_editDraft is null)
        {
            return;
        }

        foreach (var adapter in _editDraft.NetworkAdapters)
        {
            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var name = new TextBlock
            {
                Text = adapter.AdapterName,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTextSecondaryBrush"]
            };
            Grid.SetColumn(name, 0);
            row.Children.Add(name);

            var combo = new ComboBox
            {
                Width = 200,
                Tag = adapter.AdapterName
            };
            combo.Items.Add("(Disconnected)");
            foreach (var switchName in _availableSwitches)
            {
                combo.Items.Add(switchName);
            }

            var selectedSwitch = string.IsNullOrWhiteSpace(adapter.SwitchName) ? "(Disconnected)" : adapter.SwitchName;
            combo.SelectedItem = selectedSwitch;
            combo.SelectionChanged += AdapterSwitchCombo_SelectionChanged;
            Grid.SetColumn(combo, 1);
            row.Children.Add(combo);

            NetworkAdapterEditorPanel.Children.Add(row);
        }
    }

    private void UpdateMachineEditDraftFromControls()
    {
        if (_isUpdatingMachineEditControls || _editDraft is null || _loadedEditSnapshot is null)
        {
            return;
        }

        if (!TryParseLong(CpuCountTextBox.Text, out var cpu) ||
            !TryParseLong(StartupMemoryTextBox.Text, out var startupMb) ||
            !TryParseLong(MinimumMemoryTextBox.Text, out var minMb) ||
            !TryParseLong(MaximumMemoryTextBox.Text, out var maxMb) ||
            !TryParseInt(MemoryBufferTextBox.Text, out var buffer))
        {
            UpdateMachineEditDirtyIndicator();
            return;
        }

        var normalizedAdapters = _editDraft.NetworkAdapters
            .Select(adapter => new MachineNetworkAdapterConfig
            {
                AdapterName = adapter.AdapterName,
                SwitchName = adapter.SwitchName
            })
            .ToList();

        var draft = new MachineEditDraft
        {
            CpuCount = (int)cpu,
            StartupMemoryMb = startupMb,
            DynamicMemoryEnabled = DynamicMemoryToggle.IsOn,
            MinimumMemoryMb = minMb,
            MaximumMemoryMb = maxMb,
            MemoryBufferPercent = buffer,
            NetworkAdapters = normalizedAdapters
        };

        _editDraft = CreateDraft(draft, ComputeChangedFields(_loadedEditSnapshot, draft));
        UpdateMachineEditDirtyIndicator();
    }

    private static MachineEditDraft CreateDraft(MachineEditSnapshot snapshot, IReadOnlyList<string> changedFields)
    {
        return new MachineEditDraft
        {
            CpuCount = snapshot.CpuCount,
            StartupMemoryMb = snapshot.StartupMemoryMb,
            DynamicMemoryEnabled = snapshot.DynamicMemoryEnabled,
            MinimumMemoryMb = snapshot.MinimumMemoryMb,
            MaximumMemoryMb = snapshot.MaximumMemoryMb,
            MemoryBufferPercent = snapshot.MemoryBufferPercent,
            NetworkAdapters = snapshot.NetworkAdapters
                .Select(adapter => new MachineNetworkAdapterConfig
                {
                    AdapterName = adapter.AdapterName,
                    SwitchName = adapter.SwitchName
                })
                .ToList(),
            ChangedFieldKeys = changedFields
        };
    }

    private static MachineEditDraft CreateDraft(MachineEditDraft draft, IReadOnlyList<string> changedFields)
    {
        return new MachineEditDraft
        {
            CpuCount = draft.CpuCount,
            StartupMemoryMb = draft.StartupMemoryMb,
            DynamicMemoryEnabled = draft.DynamicMemoryEnabled,
            MinimumMemoryMb = draft.MinimumMemoryMb,
            MaximumMemoryMb = draft.MaximumMemoryMb,
            MemoryBufferPercent = draft.MemoryBufferPercent,
            NetworkAdapters = draft.NetworkAdapters,
            ChangedFieldKeys = changedFields
        };
    }

    private static IReadOnlyList<string> ComputeChangedFields(MachineEditSnapshot baseline, MachineEditDraft draft)
    {
        var changed = new List<string>();
        if (baseline.CpuCount != draft.CpuCount)
        {
            changed.Add("cpuCount");
        }

        if (baseline.StartupMemoryMb != draft.StartupMemoryMb)
        {
            changed.Add("startupMemoryMb");
        }

        if (baseline.DynamicMemoryEnabled != draft.DynamicMemoryEnabled)
        {
            changed.Add("dynamicMemoryEnabled");
        }

        if (baseline.MinimumMemoryMb != draft.MinimumMemoryMb)
        {
            changed.Add("minimumMemoryMb");
        }

        if (baseline.MaximumMemoryMb != draft.MaximumMemoryMb)
        {
            changed.Add("maximumMemoryMb");
        }

        if (baseline.MemoryBufferPercent != draft.MemoryBufferPercent)
        {
            changed.Add("memoryBufferPercent");
        }

        foreach (var adapter in draft.NetworkAdapters)
        {
            var baselineAdapter = baseline.NetworkAdapters.FirstOrDefault(a =>
                string.Equals(a.AdapterName, adapter.AdapterName, StringComparison.OrdinalIgnoreCase));
            if (!string.Equals(baselineAdapter?.SwitchName ?? string.Empty, adapter.SwitchName ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                changed.Add($"switch:{adapter.AdapterName}");
            }
        }

        return changed;
    }

    private bool HasMachineEditChanges()
    {
        return _editDraft is not null && _editDraft.ChangedFieldKeys.Count > 0;
    }

    private void UpdateMachineEditDirtyIndicator()
    {
        MachineEditDirtyTextBlock.Visibility = HasMachineEditChanges() ? Visibility.Visible : Visibility.Collapsed;
        UpdateMachineActionButtons();
    }

    private async void ApplyMachineEditsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMachine is null || _editDraft is null || _loadedEditSnapshot is null)
        {
            MachinesStatusTextBlock.Text = "Select a VM and modify values before Apply.";
            return;
        }

        if (!HasMachineEditChanges())
        {
            MachinesStatusTextBlock.Text = "No pending machine edits.";
            return;
        }

        _isMachineEditApplying = true;
        UpdateMachineActionButtons();
        try
        {
            var result = await _machinesCapabilityService.ApplyEditsAsync(_selectedMachine, _editDraft);
            MachinesStatusTextBlock.Text = $"{result.UserMessage} (operationId: {result.OperationId})";
            if (!result.Success)
            {
                return;
            }

            await LoadMachineEditStateAsync();
            await EnsureMachinesInventoryAsync(forceRefresh: true);
        }
        finally
        {
            _isMachineEditApplying = false;
            UpdateMachineActionButtons();
        }
    }

    private void CpuCountTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateMachineEditDraftFromControls();
    private void StartupMemoryTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateMachineEditDraftFromControls();
    private void MinimumMemoryTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateMachineEditDraftFromControls();
    private void MaximumMemoryTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateMachineEditDraftFromControls();
    private void MemoryBufferTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateMachineEditDraftFromControls();

    private void DynamicMemoryToggle_Toggled(object sender, RoutedEventArgs e)
    {
        DynamicMemoryPanel.IsHitTestVisible = DynamicMemoryToggle.IsOn;
        DynamicMemoryPanel.Opacity = DynamicMemoryToggle.IsOn ? 1.0 : 0.65;
        UpdateMachineEditDraftFromControls();
    }

    private void AdapterSwitchCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingMachineEditControls || _editDraft is null)
        {
            return;
        }

        if (sender is not ComboBox combo || combo.Tag is not string adapterName)
        {
            return;
        }

        var selectedSwitch = combo.SelectedItem?.ToString();
        if (string.Equals(selectedSwitch, "(Disconnected)", StringComparison.Ordinal))
        {
            selectedSwitch = null;
        }

        var updatedAdapters = _editDraft.NetworkAdapters
            .Select(adapter => string.Equals(adapter.AdapterName, adapterName, StringComparison.OrdinalIgnoreCase)
                ? new MachineNetworkAdapterConfig { AdapterName = adapter.AdapterName, SwitchName = selectedSwitch }
                : adapter)
            .ToList();

        var draft = CreateDraft(_editDraft, _editDraft.ChangedFieldKeys);
        _editDraft = new MachineEditDraft
        {
            CpuCount = draft.CpuCount,
            StartupMemoryMb = draft.StartupMemoryMb,
            DynamicMemoryEnabled = draft.DynamicMemoryEnabled,
            MinimumMemoryMb = draft.MinimumMemoryMb,
            MaximumMemoryMb = draft.MaximumMemoryMb,
            MemoryBufferPercent = draft.MemoryBufferPercent,
            NetworkAdapters = updatedAdapters,
            ChangedFieldKeys = draft.ChangedFieldKeys
        };
        UpdateMachineEditDraftFromControls();
    }

    private static bool TryParseLong(string? text, out long value)
    {
        return long.TryParse(text, out value);
    }

    private static bool TryParseInt(string? text, out int value)
    {
        return int.TryParse(text, out value);
    }

    private async Task RefreshRdpReadinessAsync(bool selectedOnly)
    {
        if (!IsMachinesOverviewActive || _machineInventory.Count == 0)
        {
            return;
        }

        if (_isRdpReadinessRefreshRunning)
        {
            return;
        }

        var candidates = selectedOnly && _selectedMachine is not null
            ? [ _selectedMachine ]
            : _machineInventory.ToList();

        _isRdpReadinessRefreshRunning = true;
        _lastRdpReadinessRefreshUtc = DateTimeOffset.UtcNow;

        try
        {
            foreach (var vm in candidates)
            {
                SetRdpReadiness(vm, new MachineRdpReadinessResult
                {
                    State = MachineRdpReadinessState.Checking,
                    ReasonCode = MachineRdpReadinessReasonCodes.CheckFailed,
                    Message = "Checking RDP readiness..."
                });
            }

            foreach (var vm in candidates)
            {
                var readiness = await _machinesCapabilityService.EvaluateRdpReadinessAsync(vm, CancellationToken.None);
                SetRdpReadiness(vm, readiness);
            }
        }
        finally
        {
            _isRdpReadinessRefreshRunning = false;
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

    private void SyncRdpReadinessCache()
    {
        var activeVmKeys = _machineInventory.Select(GetVmReadinessKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var staleKeys = _rdpReadinessByVmKey.Keys.Where(vmKey => !activeVmKeys.Contains(vmKey)).ToList();
        foreach (var vmKey in staleKeys)
        {
            _rdpReadinessByVmKey.Remove(vmKey);
        }

        foreach (var vm in _machineInventory)
        {
            var vmKey = GetVmReadinessKey(vm);
            if (!_rdpReadinessByVmKey.ContainsKey(vmKey))
            {
                _rdpReadinessByVmKey[vmKey] = CreateUnknownReadiness("RDP readiness has not been checked yet.");
            }
        }

        UpdateSelectedRdpReadinessFromCache();
        UpdateRdpReadinessUi();
        UpdateReadinessPollingState();
    }

    private void SetRdpReadiness(MachineInventoryItem vm, MachineRdpReadinessResult readiness)
    {
        var vmKey = GetVmReadinessKey(vm);
        _rdpReadinessByVmKey[vmKey] = readiness;

        if (_selectedMachine is not null &&
            string.Equals(GetVmReadinessKey(_selectedMachine), vmKey, StringComparison.OrdinalIgnoreCase))
        {
            _selectedRdpReadiness = readiness;
            UpdateRdpReadinessUi();
            UpdateMachineActionButtons();
        }
    }

    private void UpdateSelectedRdpReadinessFromCache()
    {
        if (_selectedMachine is null)
        {
            _selectedRdpReadiness = CreateUnknownReadiness("Select a VM to check RDP readiness.");
            return;
        }

        var selectedKey = GetVmReadinessKey(_selectedMachine);
        if (_rdpReadinessByVmKey.TryGetValue(selectedKey, out var readiness))
        {
            _selectedRdpReadiness = readiness;
            return;
        }

        _selectedRdpReadiness = CreateUnknownReadiness("RDP readiness has not been checked yet.");
    }

    private void UpdateRdpReadinessUi()
    {
        if (_selectedRdpReadiness.State == MachineRdpReadinessState.Ready)
        {
            RdpReadinessTextBlock.Text = _selectedRdpReadiness.Message;
            return;
        }

        RdpReadinessTextBlock.Text = $"{_selectedRdpReadiness.Message} ({_selectedRdpReadiness.ReasonCode})";
    }

    private static MachineRdpReadinessResult CreateUnknownReadiness(string message)
    {
        return new MachineRdpReadinessResult
        {
            State = MachineRdpReadinessState.Unknown,
            ReasonCode = MachineRdpReadinessReasonCodes.CheckFailed,
            Message = message
        };
    }

    private static string GetVmReadinessKey(MachineInventoryItem vm)
    {
        if (!string.IsNullOrWhiteSpace(vm.VmId))
        {
            return vm.VmId;
        }

        return vm.VmName;
    }

    private async Task<MachineDeleteScope?> ShowDeleteScopeDialogAsync(MachineInventoryItem vm, MachineDeletePreview preview)
    {
        var vmOnlyRadio = new RadioButton
        {
            Content = "VM registration only",
            IsChecked = preview.DefaultScope == MachineDeleteScope.VmRegistrationOnly
        };
        var vmAndStorageRadio = new RadioButton
        {
            IsChecked = preview.DefaultScope == MachineDeleteScope.VmAndStorage,
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
        content.Children.Add(new TextBlock
        {
            Text = $"Policy: {preview.PolicyMode} — {preview.PolicyMessage}",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTextSecondaryBrush"]
        });

        foreach (var disk in preview.DiskClassifications)
        {
            content.Children.Add(new TextBlock
            {
                Text = $"Disk: {disk.DiskPath} | {disk.Classification} ({disk.Reason})",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTextSecondaryBrush"]
            });
        }

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

    private async Task<bool> ShowDeleteConfirmationDialogAsync(
        MachineInventoryItem vm,
        MachineDeletePreview preview,
        MachineDeleteScope effectiveScope)
    {
        var scopeText = effectiveScope == MachineDeleteScope.VmAndStorage
            ? "VM + associated disks/files"
            : "VM registration only";
        var confirmationCheck = new CheckBox
        {
            Content = $"I confirm I want to delete '{vm.VmName}'."
        };

        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = $"Effective delete scope: {scopeText}",
            TextWrapping = TextWrapping.Wrap,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        content.Children.Add(new TextBlock
        {
            Text = $"Policy: {preview.PolicyMode} — {preview.PolicyMessage}",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ShellTextSecondaryBrush"]
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
        return result == ContentDialogResult.Primary;
    }
}
