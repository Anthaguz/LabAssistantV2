using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using LabAssistant.Business.Machines;

namespace LabAssistant.WinUI.Views.Machines;

public sealed partial class MachinesOverviewView : UserControl
{
    private const double CompactLayoutThreshold = 1024;
    private bool _isUpdatingSelection;
    private bool _isUpdatingEditControls;

    public event EventHandler? RefreshRequested;
    public event EventHandler? SelectedMachineChanged;
    public event EventHandler? MachineEditChanged;
    public event EventHandler? ApplyMachineEditsRequested;
    public event EventHandler? StartMachineRequested;
    public event EventHandler? StopMachineRequested;
    public event EventHandler? RestartMachineRequested;
    public event EventHandler? OpenConsoleRequested;
    public event EventHandler? OpenRdpRequested;
    public event EventHandler? DeleteMachineRequested;

    public MachinesOverviewView()
    {
        InitializeComponent();
        SizeChanged += MachinesOverviewView_SizeChanged;
        WireHandlers();
        UpdateLayoutMode(CompactLayoutThreshold + 1);
    }

    public MachineInventoryItem? SelectedMachine => MachinesListView.SelectedItem as MachineInventoryItem;

    public void SetInventorySource(object? itemsSource)
    {
        MachinesListView.ItemsSource = itemsSource;
    }

    public void SetStatusText(string message)
    {
        MachinesStatusTextBlock.Text = message;
    }

    public void SetSelectedMachine(MachineInventoryItem? selectedMachine)
    {
        _isUpdatingSelection = true;
        try
        {
            MachinesListView.SelectedItem = selectedMachine;
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    public void UpdateMachineDetails(MachineInventoryItem? selectedMachine)
    {
        if (selectedMachine is null)
        {
            SelectedVmNameTextBlock.Text = "Name: (none)";
            SelectedVmStateTextBlock.Text = "State: -";
            SelectedVmOriginTextBlock.Text = "Origin: -";
            SelectedVmIdTextBlock.Text = "VM Id: -";
            SelectedVmPathTextBlock.Text = "Path: -";
            ClearEditControls();
            return;
        }

        SelectedVmNameTextBlock.Text = $"Name: {selectedMachine.VmName}";
        SelectedVmStateTextBlock.Text = $"State: {selectedMachine.State}";
        SelectedVmOriginTextBlock.Text = $"Origin: {selectedMachine.OriginLabel}";
        SelectedVmIdTextBlock.Text = $"VM Id: {selectedMachine.VmId}";
        SelectedVmPathTextBlock.Text = $"Path: {(selectedMachine.VmPath ?? "-")}";
    }

    public void UpdateActionState(
        bool isInventoryRefreshing,
        bool canRunActions,
        bool canOpenRdp,
        string rdpTooltipText,
        bool canApplyEdits)
    {
        RefreshMachinesButton.IsEnabled = !isInventoryRefreshing;
        StartVmButton.IsEnabled = canRunActions;
        StopVmButton.IsEnabled = canRunActions;
        RestartVmButton.IsEnabled = canRunActions;
        OpenConsoleButton.IsEnabled = canRunActions;
        DeleteVmButton.IsEnabled = canRunActions;
        OpenRdpButton.IsEnabled = canOpenRdp;
        ToolTipService.SetToolTip(OpenRdpButton, rdpTooltipText);
        ApplyMachineEditsButton.IsEnabled = canApplyEdits;
    }

    public void UpdateRdpReadiness(MachineRdpReadinessResult readiness)
    {
        RdpReadinessTextBlock.Text = readiness.State == MachineRdpReadinessState.Ready
            ? readiness.Message
            : $"{readiness.Message} ({readiness.ReasonCode})";
    }

    public void SetEditDirtyIndicator(bool hasChanges)
    {
        MachineEditDirtyTextBlock.Visibility = hasChanges ? Visibility.Visible : Visibility.Collapsed;
    }

    public void ClearEditControls()
    {
        _isUpdatingEditControls = true;
        try
        {
            CpuCountTextBox.Text = string.Empty;
            StartupMemoryTextBox.Text = string.Empty;
            DynamicMemoryToggle.IsOn = false;
            MinimumMemoryTextBox.Text = string.Empty;
            MaximumMemoryTextBox.Text = string.Empty;
            MemoryBufferTextBox.Text = string.Empty;
            NetworkAdapterEditorPanel.Children.Clear();
            ApplyDynamicMemoryVisualState(isEnabled: false);
        }
        finally
        {
            _isUpdatingEditControls = false;
        }
    }

    public void ApplyEditDraft(MachineEditDraft? editDraft, IReadOnlyList<string> availableSwitches)
    {
        if (editDraft is null)
        {
            ClearEditControls();
            return;
        }

        _isUpdatingEditControls = true;
        try
        {
            CpuCountTextBox.Text = editDraft.CpuCount.ToString();
            StartupMemoryTextBox.Text = editDraft.StartupMemoryMb.ToString();
            DynamicMemoryToggle.IsOn = editDraft.DynamicMemoryEnabled;
            MinimumMemoryTextBox.Text = editDraft.MinimumMemoryMb.ToString();
            MaximumMemoryTextBox.Text = editDraft.MaximumMemoryMb.ToString();
            MemoryBufferTextBox.Text = editDraft.MemoryBufferPercent.ToString();
            ApplyDynamicMemoryVisualState(editDraft.DynamicMemoryEnabled);
            RenderNetworkAdapterEditors(editDraft, availableSwitches);
        }
        finally
        {
            _isUpdatingEditControls = false;
        }
    }

    public MachineEditFormValues CaptureEditFormValues()
    {
        var adapters = NetworkAdapterEditorPanel.Children
            .OfType<Grid>()
            .Select(row => row.Children.OfType<ComboBox>().SingleOrDefault())
            .Where(combo => combo is not null && combo.Tag is string)
            .Select(combo =>
            {
                var selectedSwitch = combo!.SelectedItem?.ToString();
                if (string.Equals(selectedSwitch, "(Disconnected)", StringComparison.Ordinal))
                {
                    selectedSwitch = null;
                }

                return new MachineNetworkAdapterConfig
                {
                    AdapterName = (string)combo.Tag,
                    SwitchName = selectedSwitch
                };
            })
            .ToList();

        return new MachineEditFormValues(
            CpuCountTextBox.Text,
            StartupMemoryTextBox.Text,
            DynamicMemoryToggle.IsOn,
            MinimumMemoryTextBox.Text,
            MaximumMemoryTextBox.Text,
            MemoryBufferTextBox.Text,
            adapters);
    }

    private void WireHandlers()
    {
        RefreshMachinesButton.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);
        MachinesListView.SelectionChanged += MachinesListView_SelectionChanged;
        CpuCountTextBox.TextChanged += MachineEditInput_Changed;
        StartupMemoryTextBox.TextChanged += MachineEditInput_Changed;
        MinimumMemoryTextBox.TextChanged += MachineEditInput_Changed;
        MaximumMemoryTextBox.TextChanged += MachineEditInput_Changed;
        MemoryBufferTextBox.TextChanged += MachineEditInput_Changed;
        DynamicMemoryToggle.Toggled += DynamicMemoryToggle_Toggled;
        ApplyMachineEditsButton.Click += (_, _) => ApplyMachineEditsRequested?.Invoke(this, EventArgs.Empty);
        StartVmButton.Click += (_, _) => StartMachineRequested?.Invoke(this, EventArgs.Empty);
        StopVmButton.Click += (_, _) => StopMachineRequested?.Invoke(this, EventArgs.Empty);
        RestartVmButton.Click += (_, _) => RestartMachineRequested?.Invoke(this, EventArgs.Empty);
        OpenConsoleButton.Click += (_, _) => OpenConsoleRequested?.Invoke(this, EventArgs.Empty);
        OpenRdpButton.Click += (_, _) => OpenRdpRequested?.Invoke(this, EventArgs.Empty);
        DeleteVmButton.Click += (_, _) => DeleteMachineRequested?.Invoke(this, EventArgs.Empty);
    }

    private void MachinesOverviewView_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
    {
        UpdateLayoutMode(e.NewSize.Width);
    }

    private void UpdateLayoutMode(double width)
    {
        var useStackedLayout = width < CompactLayoutThreshold;
        MachinesListColumnDefinition.Width = useStackedLayout ? new GridLength(1, GridUnitType.Star) : new GridLength(2, GridUnitType.Star);
        MachinesSplitterColumnDefinition.Width = useStackedLayout ? new GridLength(0) : new GridLength(12);
        MachinesDetailsColumnDefinition.Width = useStackedLayout ? new GridLength(0) : new GridLength(3, GridUnitType.Star);
        MachinesListRowDefinition.Height = new GridLength(1, GridUnitType.Star);
        MachinesDetailsRowDefinition.Height = useStackedLayout ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        Grid.SetRow(MachinesInventoryRegion, 0);
        Grid.SetColumn(MachinesInventoryRegion, 0);

        Grid.SetRow(MachinesDetailsRegion, useStackedLayout ? 1 : 0);
        Grid.SetColumn(MachinesDetailsRegion, useStackedLayout ? 0 : 2);
    }

    private void MachinesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingSelection)
        {
            return;
        }

        SelectedMachineChanged?.Invoke(this, EventArgs.Empty);
    }

    private void MachineEditInput_Changed(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingEditControls)
        {
            return;
        }

        MachineEditChanged?.Invoke(this, EventArgs.Empty);
    }

    private void DynamicMemoryToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingEditControls)
        {
            return;
        }

        ApplyDynamicMemoryVisualState(DynamicMemoryToggle.IsOn);
        MachineEditChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AdapterSwitchCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingEditControls)
        {
            return;
        }

        MachineEditChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyDynamicMemoryVisualState(bool isEnabled)
    {
        DynamicMemoryPanel.IsHitTestVisible = isEnabled;
        DynamicMemoryPanel.Opacity = isEnabled ? 1.0 : 0.65;
    }

    private void RenderNetworkAdapterEditors(MachineEditDraft editDraft, IReadOnlyList<string> availableSwitches)
    {
        NetworkAdapterEditorPanel.Children.Clear();

        foreach (var adapter in editDraft.NetworkAdapters)
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
            foreach (var switchName in availableSwitches)
            {
                combo.Items.Add(switchName);
            }

            combo.SelectedItem = string.IsNullOrWhiteSpace(adapter.SwitchName) ? "(Disconnected)" : adapter.SwitchName;
            combo.SelectionChanged += AdapterSwitchCombo_SelectionChanged;
            Grid.SetColumn(combo, 1);
            row.Children.Add(combo);

            NetworkAdapterEditorPanel.Children.Add(row);
        }
    }
}

public sealed record MachineEditFormValues(
    string? CpuCountText,
    string? StartupMemoryText,
    bool DynamicMemoryEnabled,
    string? MinimumMemoryText,
    string? MaximumMemoryText,
    string? MemoryBufferText,
    IReadOnlyList<MachineNetworkAdapterConfig> NetworkAdapters);
