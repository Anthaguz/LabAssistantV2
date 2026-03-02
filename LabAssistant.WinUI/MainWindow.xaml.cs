using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using LabAssistant.Business.Machines;
using LabAssistant.Business.Templates;
using LabAssistant.Services.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls;
using LabAssistant.WinUI.Theming;
using LabAssistant.WinUI.ViewModels;
using LabAssistant.WinUI.Views.Diagnostics;
using LabAssistant.WinUI.Views.Machines;
using LabAssistant.WinUI.Views.Templates;
using Microsoft.UI.Dispatching;
using WinRT.Interop;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace LabAssistant.WinUI;

public sealed partial class MainWindow : Window
{
    private readonly ShellViewModel _shellViewModel = new();
    private readonly Dictionary<string, NavigationViewItem> _routeToNavigationItem = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NavigationViewItem> _routeToCapabilityNavigationItem = new(StringComparer.Ordinal);
    private readonly IMachinesCapabilityService _machinesCapabilityService;
    private readonly ITemplatesCapabilityService _templatesCapabilityService;
    private readonly IStructuredLogViewerService _structuredLogViewerService;
    private readonly ObservableCollection<MachineInventoryItem> _machineInventory = [];
    private readonly ObservableCollection<StructuredLogViewerEntry> _structuredLogEntries = [];
    private readonly ObservableCollection<TemplateLibraryItem> _templateLibraryItems = [];
    private readonly Dictionary<string, MachineRdpReadinessResult> _rdpReadinessByVmKey = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<string> _availableSwitches = Array.Empty<string>();
    private ShellCapability _activeCapability;
    private ShellSubview _activeSubview;
    private string _activeRouteKey = string.Empty;
    private MachineInventoryItem? _selectedMachine;
    private MachineRdpReadinessResult _selectedRdpReadiness = CreateUnknownReadiness("Select a VM to check RDP readiness.");
    private MachineEditSnapshot? _loadedEditSnapshot;
    private MachineEditDraft? _editDraft;
    private StructuredLogViewerEntry? _selectedStructuredLogEntry;
    private TemplateLibraryItem? _selectedTemplateLibraryItem;
    private TemplateEditorDocument? _activeTemplateEditorDocument;
    private bool _isMachineActionRunning;
    private bool _isRdpReadinessRefreshRunning;
    private bool _isMachineEditLoading;
    private bool _isMachineEditApplying;
    private bool _isUpdatingMachineEditControls;
    private bool _isSavingDeletionPolicy;
    private bool _isStructuredLogsLoading;
    private bool _isTemplatesLoading;
    private bool _isUpdatingNavigationSelection;
    private bool _isUpdatingTemplatesSubviewSelection;
    private bool _isInsightsOpen;
    private ElementTheme _theme = ElementTheme.Light;
    private int _issueCount = 3;
    private DispatcherQueueTimer? _rdpReadinessTimer;
    private DateTimeOffset _lastRdpReadinessRefreshUtc = DateTimeOffset.MinValue;
    private DateTimeOffset _lastOnDemandRdpRefreshUtc = DateTimeOffset.MinValue;

    private MachinesOverviewView MachinesOverviewView => MachinesOverviewViewHost;
    private DiagnosticsLogsView DiagnosticsLogsView => DiagnosticsLogsViewHost;
    private FrameworkElement MachinesOverviewPanel => MachinesOverviewViewHost;
    private FrameworkElement TemplatesLocalNavPanel => TemplatesLocalNavigationPanel;
    private Button RefreshMachinesButton => MachinesOverviewView.RefreshMachinesButton;
    private ListView MachinesListView => MachinesOverviewView.MachinesListView;
    private TextBlock SelectedVmNameTextBlock => MachinesOverviewView.SelectedVmNameTextBlock;
    private TextBlock SelectedVmStateTextBlock => MachinesOverviewView.SelectedVmStateTextBlock;
    private TextBlock SelectedVmOriginTextBlock => MachinesOverviewView.SelectedVmOriginTextBlock;
    private TextBlock SelectedVmIdTextBlock => MachinesOverviewView.SelectedVmIdTextBlock;
    private TextBlock SelectedVmPathTextBlock => MachinesOverviewView.SelectedVmPathTextBlock;
    private TextBox CpuCountTextBox => MachinesOverviewView.CpuCountTextBox;
    private TextBox StartupMemoryTextBox => MachinesOverviewView.StartupMemoryTextBox;
    private ToggleSwitch DynamicMemoryToggle => MachinesOverviewView.DynamicMemoryToggle;
    private Grid DynamicMemoryPanel => MachinesOverviewView.DynamicMemoryPanel;
    private TextBox MinimumMemoryTextBox => MachinesOverviewView.MinimumMemoryTextBox;
    private TextBox MaximumMemoryTextBox => MachinesOverviewView.MaximumMemoryTextBox;
    private TextBox MemoryBufferTextBox => MachinesOverviewView.MemoryBufferTextBox;
    private StackPanel NetworkAdapterEditorPanel => MachinesOverviewView.NetworkAdapterEditorPanel;
    private Button ApplyMachineEditsButton => MachinesOverviewView.ApplyMachineEditsButton;
    private TextBlock MachineEditDirtyTextBlock => MachinesOverviewView.MachineEditDirtyTextBlock;
    private Button StartVmButton => MachinesOverviewView.StartVmButton;
    private Button StopVmButton => MachinesOverviewView.StopVmButton;
    private Button RestartVmButton => MachinesOverviewView.RestartVmButton;
    private Button OpenConsoleButton => MachinesOverviewView.OpenConsoleButton;
    private Button DeleteVmButton => MachinesOverviewView.DeleteVmButton;
    private Button OpenRdpButton => MachinesOverviewView.OpenRdpButton;
    private TextBlock RdpReadinessTextBlock => MachinesOverviewView.RdpReadinessTextBlock;
    private TextBlock MachinesStatusTextBlock => MachinesOverviewView.MachinesStatusTextBlock;

    private FrameworkElement DiagnosticsLogsPanel => DiagnosticsLogsViewHost;
    private TextBox LogFilterOperationIdTextBox => DiagnosticsLogsView.LogFilterOperationIdTextBox;
    private TextBox LogFilterLevelTextBox => DiagnosticsLogsView.LogFilterLevelTextBox;
    private TextBox LogFilterEventTextBox => DiagnosticsLogsView.LogFilterEventTextBox;
    private TextBox LogFilterTextSearchTextBox => DiagnosticsLogsView.LogFilterTextSearchTextBox;
    private CheckBox LogFilterUseStartDateCheckBox => DiagnosticsLogsView.LogFilterUseStartDateCheckBox;
    private DatePicker LogFilterStartDatePicker => DiagnosticsLogsView.LogFilterStartDatePicker;
    private CheckBox LogFilterUseEndDateCheckBox => DiagnosticsLogsView.LogFilterUseEndDateCheckBox;
    private DatePicker LogFilterEndDatePicker => DiagnosticsLogsView.LogFilterEndDatePicker;
    private Button ApplyLogFiltersButton => DiagnosticsLogsView.ApplyLogFiltersButton;
    private Button ClearLogFiltersButton => DiagnosticsLogsView.ClearLogFiltersButton;
    private Button ReloadLogsButton => DiagnosticsLogsView.ReloadLogsButton;
    private Button OpenRawJsonlButton => DiagnosticsLogsView.OpenRawJsonlButton;
    private TextBlock LogsStatusTextBlock => DiagnosticsLogsView.LogsStatusTextBlock;
    private ListView StructuredLogsListView => DiagnosticsLogsView.StructuredLogsListView;
    private TextBlock SelectedLogEnvelopeTextBlock => DiagnosticsLogsView.SelectedLogEnvelopeTextBlock;
    private TextBox SelectedLogContextTextBox => DiagnosticsLogsView.SelectedLogContextTextBox;
    private TemplatesLibraryView TemplatesLibraryView => TemplatesLibraryViewHost;
    private TemplatesEditorView TemplatesEditorView => TemplatesEditorViewHost;
    private ListView TemplateLibraryListView => TemplatesLibraryView.TemplateLibraryListViewControl;
    private TextBox TemplateSearchTextBox => TemplatesLibraryView.TemplateSearchTextBoxControl;
    private Button ApplyTemplateSearchButton => TemplatesLibraryView.ApplyTemplateSearchButtonControl;
    private Button ClearTemplateSearchButton => TemplatesLibraryView.ClearTemplateSearchButtonControl;
    private Button ReloadTemplatesButton => TemplatesLibraryView.ReloadTemplatesButtonControl;
    private Button OpenTemplateInEditorButton => TemplatesLibraryView.OpenTemplateInEditorButtonControl;
    private Button CreateTemplateButton => TemplatesLibraryView.CreateTemplateButtonControl;
    private Button DeleteTemplateButton => TemplatesLibraryView.DeleteTemplateButtonControl;
    private Button ImportTemplateButton => TemplatesLibraryView.ImportTemplateButtonControl;
    private Button ExportTemplateButton => TemplatesLibraryView.ExportTemplateButtonControl;
    private TextBlock TemplatesLibraryStatusTextBlock => TemplatesLibraryView.TemplatesLibraryStatusTextBlockControl;
    private TextBlock TemplatesLibrarySelectionTextBlock => TemplatesLibraryView.TemplatesLibrarySelectionTextBlockControl;
    private TextBlock SelectedTemplatePathTextBlock => TemplatesLibraryView.SelectedTemplatePathTextBlockControl;
    private TextBox TemplateNameTextBox => TemplatesEditorView.TemplateNameTextBoxControl;
    private TextBox TemplateDescriptionTextBox => TemplatesEditorView.TemplateDescriptionTextBoxControl;
    private TextBlock TemplateEditorContextTextBlock => TemplatesEditorView.TemplateEditorContextTextBlockControl;
    private TextBlock TemplateIdTextBlock => TemplatesEditorView.TemplateIdTextBlockControl;
    private TextBlock TemplateFilePathTextBlock => TemplatesEditorView.TemplateFilePathTextBlockControl;
    private TextBlock TemplateVmCountTextBlock => TemplatesEditorView.TemplateVmCountTextBlockControl;
    private TextBlock TemplateEditorStatusTextBlock => TemplatesEditorView.TemplateEditorStatusTextBlockControl;
    private Button SaveTemplateButton => TemplatesEditorView.SaveTemplateButtonControl;
    private Button SaveTemplateAsButton => TemplatesEditorView.SaveTemplateAsButtonControl;
    private Button ValidateTemplateButton => TemplatesEditorView.ValidateTemplateButtonControl;
    private Button BackToLibraryButton => TemplatesEditorView.BackToLibraryButtonControl;

    public MainWindow()
    {
        InitializeComponent();
        _machinesCapabilityService = App.Services.GetRequiredService<IMachinesCapabilityService>();
        _templatesCapabilityService = App.Services.GetRequiredService<ITemplatesCapabilityService>();
        _structuredLogViewerService = App.Services.GetRequiredService<IStructuredLogViewerService>();
        _activeRouteKey = _shellViewModel.StartupRoute;
        _shellViewModel.TryResolveRoute(_activeRouteKey, out _activeCapability, out _activeSubview);
        MachinesListView.ItemsSource = _machineInventory;
        StructuredLogsListView.ItemsSource = _structuredLogEntries;
        TemplateLibraryListView.ItemsSource = _templateLibraryItems;
        WireMachinesHandlers();
        WireDiagnosticsLogsHandlers();
        WireTemplatesHandlers();
        ConfigureShellIcons();
        ConfigureNavigationView();
        Title = "LabAssistant.WinUI";
        SetInitialSize(1280, 800);
        RootLayout.KeyDown += RootLayout_KeyDown;
        InitializeRdpReadinessTimer();
        RootLayout.Loaded += async (_, _) =>
        {
            RootLayout.Focus(FocusState.Programmatic);
            await EnsureMachinesInventoryAsync(forceRefresh: true);
            await EnsureTemplatesLibraryAsync(forceRefresh: true);
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

    private void WireMachinesHandlers()
    {
        RefreshMachinesButton.Click += RefreshMachinesButton_Click;
        MachinesListView.SelectionChanged += MachinesListView_SelectionChanged;
        CpuCountTextBox.TextChanged += CpuCountTextBox_TextChanged;
        StartupMemoryTextBox.TextChanged += StartupMemoryTextBox_TextChanged;
        MinimumMemoryTextBox.TextChanged += MinimumMemoryTextBox_TextChanged;
        MaximumMemoryTextBox.TextChanged += MaximumMemoryTextBox_TextChanged;
        MemoryBufferTextBox.TextChanged += MemoryBufferTextBox_TextChanged;
        DynamicMemoryToggle.Toggled += DynamicMemoryToggle_Toggled;
        ApplyMachineEditsButton.Click += ApplyMachineEditsButton_Click;
        StartVmButton.Click += StartVmButton_Click;
        StopVmButton.Click += StopVmButton_Click;
        RestartVmButton.Click += RestartVmButton_Click;
        OpenConsoleButton.Click += OpenConsoleButton_Click;
        DeleteVmButton.Click += DeleteVmButton_Click;
        OpenRdpButton.Click += OpenRdpButton_Click;
    }

    private void WireDiagnosticsLogsHandlers()
    {
        ApplyLogFiltersButton.Click += ApplyLogFiltersButton_Click;
        ClearLogFiltersButton.Click += ClearLogFiltersButton_Click;
        ReloadLogsButton.Click += ReloadLogsButton_Click;
        OpenRawJsonlButton.Click += OpenRawJsonlButton_Click;
        StructuredLogsListView.SelectionChanged += StructuredLogsListView_SelectionChanged;
    }

    private void WireTemplatesHandlers()
    {
        TemplateLibraryListView.SelectionChanged += TemplateLibraryListView_SelectionChanged;
        ApplyTemplateSearchButton.Click += ApplyTemplateSearchButton_Click;
        ClearTemplateSearchButton.Click += ClearTemplateSearchButton_Click;
        ReloadTemplatesButton.Click += ReloadTemplatesButton_Click;
        OpenTemplateInEditorButton.Click += OpenTemplateInEditorButton_Click;
        CreateTemplateButton.Click += CreateTemplateButton_Click;
        DeleteTemplateButton.Click += DeleteTemplateButton_Click;
        ImportTemplateButton.Click += ImportTemplateButton_Click;
        ExportTemplateButton.Click += ExportTemplateButton_Click;
        SaveTemplateButton.Click += SaveTemplateButton_Click;
        SaveTemplateAsButton.Click += SaveTemplateAsButton_Click;
        ValidateTemplateButton.Click += ValidateTemplateButton_Click;
        BackToLibraryButton.Click += BackToLibraryButton_Click;
    }

    private void ConfigureShellIcons()
    {
        HamburgerButton.Content = CreateIconGlyph(ShellIconToken.Menu);
        InsightsToggleButton.Content = CreateIconGlyph(ShellIconToken.Insights);
    }

    private void ConfigureNavigationView()
    {
        _routeToNavigationItem.Clear();
        _routeToCapabilityNavigationItem.Clear();
        GlobalNavigationView.MenuItems.Clear();
        GlobalNavigationView.FooterMenuItems.Clear();

        foreach (var capability in _shellViewModel.Capabilities)
        {
            var parentItem = new NavigationViewItem
            {
                Content = capability.DisplayName,
                Tag = capability.Key,
                Icon = new FontIcon { Glyph = capability.Glyph }
            };

            if (!capability.IsFooter)
            {
                foreach (var subview in capability.Subviews)
                {
                    var childItem = new NavigationViewItem
                    {
                        Content = subview.DisplayName,
                        Tag = subview.RouteKey
                    };
                    parentItem.MenuItems.Add(childItem);
                    _routeToNavigationItem[subview.RouteKey] = childItem;
                    _routeToCapabilityNavigationItem[subview.RouteKey] = parentItem;
                }
            }

            if (capability.IsFooter)
            {
                GlobalNavigationView.FooterMenuItems.Add(parentItem);
                _routeToNavigationItem[capability.DefaultSubview.RouteKey] = parentItem;
                _routeToCapabilityNavigationItem[capability.DefaultSubview.RouteKey] = parentItem;
            }
            else
            {
                GlobalNavigationView.MenuItems.Add(parentItem);
            }
        }
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

    private void ApplyState()
    {
        CurrentRouteTextBlock.Text = $"{_activeCapability.DisplayName} / {_activeSubview.DisplayName}";
        ContentTitleTextBlock.Text = _activeCapability.DisplayName;
        ContentDescriptionTextBlock.Text = IsMachinesOverviewActive
            ? "Manage host Hyper-V VMs. Start/stop/restart, open console, or delete with explicit scope."
            : IsTemplatesLibraryActive
                ? "Browse templates and start create/open/import/export flows from one Templates capability context."
                : IsTemplatesEditorActive
                    ? "Edit template metadata, validate, and save through existing template workflows."
            : IsSettingsMachinesActive
                ? "Configure Machines policy defaults."
                : IsDiagnosticsLogsActive
                    ? "Inspect canonical structured logs with envelope fields and dynamic context."
                : $"Subview: {_activeSubview.DisplayName}. Placeholder content until capability migration lands.";
        ThemeToggleButton.Content = _theme == ElementTheme.Light ? "Switch to dark" : "Switch to light";
        RootLayout.RequestedTheme = _theme;
        InsightsPanel.Visibility = _isInsightsOpen ? Visibility.Visible : Visibility.Collapsed;
        IssueBadge.Visibility = _issueCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        IssueBadgeTextBlock.Text = _issueCount.ToString();
        MachinesOverviewPanel.Visibility = IsMachinesOverviewActive ? Visibility.Visible : Visibility.Collapsed;
        TemplatesLocalNavPanel.Visibility = IsTemplatesCapabilityActive ? Visibility.Visible : Visibility.Collapsed;
        SyncTemplatesSubviewSelection();
        UpdateTemplatesUi();
        SettingsMachinesPanel.Visibility = IsSettingsMachinesActive ? Visibility.Visible : Visibility.Collapsed;
        DiagnosticsLogsPanel.Visibility = IsDiagnosticsLogsActive ? Visibility.Visible : Visibility.Collapsed;
        NonMachinesPlaceholderTextBlock.Visibility = (IsMachinesOverviewActive || IsTemplatesCapabilityActive || IsSettingsMachinesActive || IsDiagnosticsLogsActive) ? Visibility.Collapsed : Visibility.Visible;

        QueueNavigationSelectionUpdate();

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

        if (IsTemplatesLibraryActive)
        {
            _ = EnsureTemplatesLibraryAsync(forceRefresh: false);
        }
    }

    private void NavigateToRoute(string routeKey)
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

        if (changedCapability || changedSubview)
        {
            DiscardMachineEditDraft();
        }

        _activeCapability = capability;
        _activeSubview = subview;
        _activeRouteKey = subview.RouteKey;
        ApplyState();

        if (IsMachinesOverviewActive)
        {
            _ = EnsureMachinesInventoryAsync(forceRefresh: false);
        }
    }

    private void HamburgerButton_Click(object sender, RoutedEventArgs e)
    {
        GlobalNavigationView.IsPaneOpen = !GlobalNavigationView.IsPaneOpen;
    }

    private void GlobalNavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (_isUpdatingNavigationSelection)
        {
            return;
        }

        if (args.InvokedItemContainer is not NavigationViewItem invokedItem)
        {
            return;
        }

        if (invokedItem.Tag is not string key)
        {
            return;
        }

        if (_shellViewModel.TryResolveCapability(key, out var capability))
        {
            var isCollapsedCompactPane =
                sender.PaneDisplayMode == NavigationViewPaneDisplayMode.LeftCompact &&
                !sender.IsPaneOpen;

            // In compact mode, parent-icon clicks should expose child options, not force default navigation.
            if (isCollapsedCompactPane && capability.Subviews.Count > 0)
            {
                return;
            }

            NavigateToRoute(capability.DefaultSubview.RouteKey);
            return;
        }

        NavigateToRoute(key);
    }

    private void QueueNavigationSelectionUpdate()
    {
        if (!_routeToCapabilityNavigationItem.TryGetValue(_activeRouteKey, out var selectedNavigationItem))
        {
            return;
        }

        if (ReferenceEquals(GlobalNavigationView.SelectedItem, selectedNavigationItem))
        {
            return;
        }

        _isUpdatingNavigationSelection = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                GlobalNavigationView.SelectedItem = selectedNavigationItem;
            }
            finally
            {
                _isUpdatingNavigationSelection = false;
            }
        });
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

    private void TemplatesSubviewTabView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingTemplatesSubviewSelection)
        {
            return;
        }

        if (TemplatesSubviewTabView.SelectedItem is not TabViewItem selectedTab)
        {
            return;
        }

        if (ReferenceEquals(selectedTab, TemplatesLibraryTabViewItem))
        {
            NavigateToRoute(ShellRouteKeys.TemplatesLibrary);
            return;
        }

        if (ReferenceEquals(selectedTab, TemplatesEditorTabViewItem))
        {
            NavigateToRoute(ShellRouteKeys.TemplatesEditor);
        }
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

    private void RootLayout_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape && GlobalNavigationView.IsPaneOpen)
        {
            GlobalNavigationView.IsPaneOpen = false;
            e.Handled = true;
        }
    }

    private void EscapeAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (GlobalNavigationView.IsPaneOpen)
        {
            GlobalNavigationView.IsPaneOpen = false;
            args.Handled = true;
        }
    }

    private bool IsMachinesOverviewActive =>
        string.Equals(_activeRouteKey, ShellRouteKeys.MachinesOverview, StringComparison.Ordinal);

    private bool IsTemplatesLibraryActive =>
        string.Equals(_activeRouteKey, ShellRouteKeys.TemplatesLibrary, StringComparison.Ordinal);

    private bool IsTemplatesEditorActive =>
        string.Equals(_activeRouteKey, ShellRouteKeys.TemplatesEditor, StringComparison.Ordinal);

    private bool IsTemplatesCapabilityActive =>
        IsTemplatesLibraryActive || IsTemplatesEditorActive;

    private bool IsSettingsMachinesActive =>
        string.Equals(_activeRouteKey, ShellRouteKeys.SettingsMachines, StringComparison.Ordinal);

    private bool IsDiagnosticsLogsActive =>
        string.Equals(_activeRouteKey, ShellRouteKeys.DiagnosticsLogs, StringComparison.Ordinal);

    private void SyncTemplatesSubviewSelection()
    {
        if (!IsTemplatesCapabilityActive)
        {
            return;
        }

        var expectedSelection = IsTemplatesEditorActive ? TemplatesEditorTabViewItem : TemplatesLibraryTabViewItem;
        if (ReferenceEquals(TemplatesSubviewTabView.SelectedItem, expectedSelection))
        {
            return;
        }

        _isUpdatingTemplatesSubviewSelection = true;
        try
        {
            TemplatesSubviewTabView.SelectedItem = expectedSelection;
        }
        finally
        {
            _isUpdatingTemplatesSubviewSelection = false;
        }
    }

    private void UpdateTemplatesUi()
    {
        OpenTemplateInEditorButton.IsEnabled = _selectedTemplateLibraryItem is not null && !_isTemplatesLoading;
        DeleteTemplateButton.IsEnabled = _selectedTemplateLibraryItem is not null && !_isTemplatesLoading;
        ExportTemplateButton.IsEnabled = _selectedTemplateLibraryItem is not null && !_isTemplatesLoading;
        ApplyTemplateSearchButton.IsEnabled = !_isTemplatesLoading;
        ClearTemplateSearchButton.IsEnabled = !_isTemplatesLoading;
        ReloadTemplatesButton.IsEnabled = !_isTemplatesLoading;
        ImportTemplateButton.IsEnabled = !_isTemplatesLoading;
        CreateTemplateButton.IsEnabled = !_isTemplatesLoading;
        SaveTemplateButton.IsEnabled = _activeTemplateEditorDocument is not null && !_isTemplatesLoading;
        SaveTemplateAsButton.IsEnabled = _activeTemplateEditorDocument is not null && !_isTemplatesLoading;
        ValidateTemplateButton.IsEnabled = _activeTemplateEditorDocument is not null && !_isTemplatesLoading;
        BackToLibraryButton.IsEnabled = !_isTemplatesLoading;

        SelectedTemplatePathTextBlock.Text = _selectedTemplateLibraryItem?.FilePath ?? "-";
        TemplatesLibrarySelectionTextBlock.Text = _selectedTemplateLibraryItem is null
            ? "Select a template to open, export, or delete."
            : $"Selected: {_selectedTemplateLibraryItem.Name} ({_selectedTemplateLibraryItem.TemplateId})";

        if (_activeTemplateEditorDocument is null)
        {
            TemplateEditorContextTextBlock.Text = "No template selected.";
            TemplateIdTextBlock.Text = "Template ID: -";
            TemplateFilePathTextBlock.Text = "File path: new template (not saved)";
            TemplateVmCountTextBlock.Text = "VMs: 0";
            TemplateNameTextBox.Text = string.Empty;
            TemplateDescriptionTextBox.Text = string.Empty;
            return;
        }

        TemplateEditorContextTextBlock.Text = string.IsNullOrWhiteSpace(_activeTemplateEditorDocument.SourceFilePath)
            ? "Editing new template draft."
            : "Editing existing template.";
        TemplateIdTextBlock.Text = $"Template ID: {_activeTemplateEditorDocument.Template.Id}";
        TemplateFilePathTextBlock.Text = $"File path: {_activeTemplateEditorDocument.SourceFilePath ?? "new template (not saved)"}";
        TemplateVmCountTextBlock.Text = $"VMs: {_activeTemplateEditorDocument.Template.VmTemplates.Count}";
    }

    private async Task EnsureTemplatesLibraryAsync(bool forceRefresh)
    {
        if (_isTemplatesLoading)
        {
            return;
        }

        if (!forceRefresh && _templateLibraryItems.Count > 0)
        {
            return;
        }

        _isTemplatesLoading = true;
        UpdateTemplatesUi();
        TemplatesLibraryStatusTextBlock.Text = "Loading templates...";

        try
        {
            var result = await _templatesCapabilityService.LoadLibraryAsync(TemplateSearchTextBox.Text);
            _templateLibraryItems.Clear();
            foreach (var item in result.Items)
            {
                _templateLibraryItems.Add(item);
            }

            if (_templateLibraryItems.Count == 0)
            {
                TemplatesLibraryStatusTextBlock.Text = result.Errors.Count == 0
                    ? "No templates found in configured template folder."
                    : $"No templates loaded. {result.Errors[0]}";
            }
            else
            {
                TemplatesLibraryStatusTextBlock.Text = result.Errors.Count == 0
                    ? $"Loaded {_templateLibraryItems.Count} template(s)."
                    : $"Loaded {_templateLibraryItems.Count} template(s) with warnings.";
            }

            if (_selectedTemplateLibraryItem is not null)
            {
                _selectedTemplateLibraryItem = _templateLibraryItems
                    .FirstOrDefault(item => string.Equals(item.FilePath, _selectedTemplateLibraryItem.FilePath, StringComparison.OrdinalIgnoreCase));
                TemplateLibraryListView.SelectedItem = _selectedTemplateLibraryItem;
            }
        }
        catch (Exception ex)
        {
            TemplatesLibraryStatusTextBlock.Text = $"Failed to load templates. {ex.Message}";
        }
        finally
        {
            _isTemplatesLoading = false;
            UpdateTemplatesUi();
        }
    }

    private async Task OpenSelectedTemplateInEditorAsync()
    {
        if (_selectedTemplateLibraryItem is null)
        {
            TemplatesLibraryStatusTextBlock.Text = "Select a template first.";
            return;
        }

        _isTemplatesLoading = true;
        UpdateTemplatesUi();
        try
        {
            _activeTemplateEditorDocument = await _templatesCapabilityService.LoadForEditorAsync(_selectedTemplateLibraryItem.FilePath);
            BindTemplateEditorDocument();
            TemplateEditorStatusTextBlock.Text = "Template loaded.";
            NavigateToRoute(ShellRouteKeys.TemplatesEditor);
        }
        catch (Exception ex)
        {
            TemplateEditorStatusTextBlock.Text = $"Failed to open template. {ex.Message}";
        }
        finally
        {
            _isTemplatesLoading = false;
            UpdateTemplatesUi();
        }
    }

    private void BindTemplateEditorDocument()
    {
        if (_activeTemplateEditorDocument is null)
        {
            UpdateTemplatesUi();
            return;
        }

        TemplateNameTextBox.Text = _activeTemplateEditorDocument.Template.Name;
        TemplateDescriptionTextBox.Text = _activeTemplateEditorDocument.Template.Description ?? string.Empty;
        UpdateTemplatesUi();
    }

    private void PullEditorFieldsIntoDocument()
    {
        if (_activeTemplateEditorDocument is null)
        {
            return;
        }

        var template = _activeTemplateEditorDocument.Template;
        template.Name = TemplateNameTextBox.Text?.Trim() ?? string.Empty;
        template.Description = TemplateDescriptionTextBox.Text?.Trim();
    }

    private async Task<string?> PickTemplateFileForOpenAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".json");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        StorageFile? file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    private async Task<string?> PickTemplateFileForSaveAsync(string suggestedFileName)
    {
        var picker = new FileSavePicker
        {
            SuggestedFileName = suggestedFileName
        };
        picker.FileTypeChoices.Add("JSON template", [".json"]);
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        StorageFile? file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    private void TemplateLibraryListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedTemplateLibraryItem = TemplateLibraryListView.SelectedItem as TemplateLibraryItem;
        UpdateTemplatesUi();
    }

    private async void ApplyTemplateSearchButton_Click(object sender, RoutedEventArgs e)
    {
        await EnsureTemplatesLibraryAsync(forceRefresh: true);
    }

    private async void ClearTemplateSearchButton_Click(object sender, RoutedEventArgs e)
    {
        TemplateSearchTextBox.Text = string.Empty;
        await EnsureTemplatesLibraryAsync(forceRefresh: true);
    }

    private async void ReloadTemplatesButton_Click(object sender, RoutedEventArgs e)
    {
        await EnsureTemplatesLibraryAsync(forceRefresh: true);
    }

    private async void OpenTemplateInEditorButton_Click(object sender, RoutedEventArgs e)
    {
        await OpenSelectedTemplateInEditorAsync();
    }

    private async void CreateTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        _isTemplatesLoading = true;
        UpdateTemplatesUi();
        try
        {
            _activeTemplateEditorDocument = await _templatesCapabilityService.CreateDraftAsync();
            BindTemplateEditorDocument();
            TemplateEditorStatusTextBlock.Text = "New template draft created.";
            NavigateToRoute(ShellRouteKeys.TemplatesEditor);
        }
        finally
        {
            _isTemplatesLoading = false;
            UpdateTemplatesUi();
        }
    }

    private async void DeleteTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTemplateLibraryItem is null)
        {
            TemplatesLibraryStatusTextBlock.Text = "Select a template first.";
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = RootLayout.XamlRoot,
            Title = "Delete Template",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            Content = $"Delete '{_selectedTemplateLibraryItem.Name}'? This removes the template file.",
            DefaultButton = ContentDialogButton.Close
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _isTemplatesLoading = true;
        UpdateTemplatesUi();
        try
        {
            var result = await _templatesCapabilityService.DeleteAsync(_selectedTemplateLibraryItem.FilePath);
            TemplatesLibraryStatusTextBlock.Text = $"{result.UserMessage} (operationId: {result.OperationId})";
            if (result.Success)
            {
                _selectedTemplateLibraryItem = null;
                await EnsureTemplatesLibraryAsync(forceRefresh: true);
            }
        }
        finally
        {
            _isTemplatesLoading = false;
            UpdateTemplatesUi();
        }
    }

    private async void ImportTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        var sourcePath = await PickTemplateFileForOpenAsync();
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            TemplatesLibraryStatusTextBlock.Text = "Import cancelled.";
            return;
        }

        _isTemplatesLoading = true;
        UpdateTemplatesUi();
        try
        {
            var result = await _templatesCapabilityService.ImportAsync(sourcePath);
            TemplatesLibraryStatusTextBlock.Text = $"{result.UserMessage} (operationId: {result.OperationId})";
            if (result.Success)
            {
                await EnsureTemplatesLibraryAsync(forceRefresh: true);
            }
        }
        finally
        {
            _isTemplatesLoading = false;
            UpdateTemplatesUi();
        }
    }

    private async void ExportTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTemplateLibraryItem is null)
        {
            TemplatesLibraryStatusTextBlock.Text = "Select a template first.";
            return;
        }

        var suggestedName = Path.GetFileName(_selectedTemplateLibraryItem.FilePath);
        var destinationPath = await PickTemplateFileForSaveAsync(suggestedName);
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            TemplatesLibraryStatusTextBlock.Text = "Export cancelled.";
            return;
        }

        _isTemplatesLoading = true;
        UpdateTemplatesUi();
        try
        {
            var result = await _templatesCapabilityService.ExportAsync(_selectedTemplateLibraryItem.FilePath, destinationPath);
            TemplatesLibraryStatusTextBlock.Text = $"{result.UserMessage} (operationId: {result.OperationId})";
        }
        finally
        {
            _isTemplatesLoading = false;
            UpdateTemplatesUi();
        }
    }

    private async void SaveTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTemplateEditorDocument is null)
        {
            TemplateEditorStatusTextBlock.Text = "No template loaded.";
            return;
        }

        PullEditorFieldsIntoDocument();
        _isTemplatesLoading = true;
        UpdateTemplatesUi();
        try
        {
            var result = await _templatesCapabilityService.SaveAsync(_activeTemplateEditorDocument);
            TemplateEditorStatusTextBlock.Text = $"{result.UserMessage} (operationId: {result.OperationId})";
            if (result.Success)
            {
                _activeTemplateEditorDocument = new TemplateEditorDocument
                {
                    Template = _activeTemplateEditorDocument.Template,
                    SourceFilePath = result.FilePath
                };
                BindTemplateEditorDocument();
                await EnsureTemplatesLibraryAsync(forceRefresh: true);
            }
        }
        finally
        {
            _isTemplatesLoading = false;
            UpdateTemplatesUi();
        }
    }

    private async void SaveTemplateAsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTemplateEditorDocument is null)
        {
            TemplateEditorStatusTextBlock.Text = "No template loaded.";
            return;
        }

        PullEditorFieldsIntoDocument();
        var suggestedName = string.IsNullOrWhiteSpace(_activeTemplateEditorDocument.Template.Name)
            ? "lab-template"
            : _activeTemplateEditorDocument.Template.Name;
        var destinationPath = await PickTemplateFileForSaveAsync(suggestedName);
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            TemplateEditorStatusTextBlock.Text = "Save As cancelled.";
            return;
        }

        _isTemplatesLoading = true;
        UpdateTemplatesUi();
        try
        {
            var result = await _templatesCapabilityService.SaveAsync(_activeTemplateEditorDocument, destinationPath, saveAs: true);
            TemplateEditorStatusTextBlock.Text = $"{result.UserMessage} (operationId: {result.OperationId})";
            if (result.Success)
            {
                _activeTemplateEditorDocument = new TemplateEditorDocument
                {
                    Template = _activeTemplateEditorDocument.Template,
                    SourceFilePath = result.FilePath
                };
                BindTemplateEditorDocument();
                await EnsureTemplatesLibraryAsync(forceRefresh: true);
            }
        }
        finally
        {
            _isTemplatesLoading = false;
            UpdateTemplatesUi();
        }
    }

    private async void ValidateTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTemplateEditorDocument is null)
        {
            TemplateEditorStatusTextBlock.Text = "No template loaded.";
            return;
        }

        PullEditorFieldsIntoDocument();
        var result = await _templatesCapabilityService.ValidateAsync(_activeTemplateEditorDocument);
        if (result.IsValid)
        {
            TemplateEditorStatusTextBlock.Text = "Template validation passed.";
            return;
        }

        TemplateEditorStatusTextBlock.Text = "Validation failed: " + string.Join(" ", result.Errors);
    }

    private void BackToLibraryButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToRoute(ShellRouteKeys.TemplatesLibrary);
    }

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
