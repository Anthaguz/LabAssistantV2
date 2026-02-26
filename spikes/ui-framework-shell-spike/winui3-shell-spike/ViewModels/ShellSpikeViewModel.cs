using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.UI.Xaml;

namespace WinUi3ShellSpike.ViewModels;

public sealed class ShellSpikeViewModel : INotifyPropertyChanged
{
    private bool _isCapabilityScope = true;
    private string _activeCapability = "Deploy";
    private bool _denseMode = true;
    private int _nextIssueId = 3;
    private bool _showReadinessDetails;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> Capabilities { get; } =
    [
        "Machines", "Deploy", "Templates", "Assets", "Diagnostics", "Settings"
    ];

    public ObservableCollection<string> ContextItems { get; } = [];
    public ObservableCollection<ShellIssueItem> ActiveIssues { get; } =
    [
        new ShellIssueItem(1, "Readiness", "3 VMs have blocking guest-step config failures"),
        new ShellIssueItem(2, "Diagnostics", "Last export completed with warnings")
    ];
    public ObservableCollection<string> ReadinessDetails { get; } = [];
    public ObservableCollection<string> VmCards { get; } = [];
    public ObservableCollection<string> LogGroups { get; } = [];

    public ICommand ToggleScopeCommand { get; }
    public ICommand SelectCapabilityCommand { get; }
    public ICommand ToggleDenseModeCommand { get; }
    public ICommand AddIssueCommand { get; }
    public ICommand DismissIssueCommand { get; }
    public ICommand ToggleReadinessDetailsCommand { get; }

    public ShellSpikeViewModel()
    {
        ActiveIssues.CollectionChanged += OnActiveIssuesChanged;

        ToggleScopeCommand = new RelayCommand(_ => IsCapabilityScope = !IsCapabilityScope);
        SelectCapabilityCommand = new RelayCommand(param =>
        {
            if (param is string capability && !string.IsNullOrWhiteSpace(capability))
            {
                ActiveCapability = capability;
                IsCapabilityScope = false;
            }
        });
        ToggleDenseModeCommand = new RelayCommand(_ =>
        {
            DenseMode = !DenseMode;
            RebuildContentFixture();
        });
        AddIssueCommand = new RelayCommand(_ =>
            ActiveIssues.Insert(0, new ShellIssueItem(_nextIssueId++, "Shell", $"Synthetic shell issue {_nextIssueId - 1}")));
        DismissIssueCommand = new RelayCommand(param =>
        {
            if (param is ShellIssueItem item)
            {
                ActiveIssues.Remove(item);
            }
        });
        ToggleReadinessDetailsCommand = new RelayCommand(_ => ShowReadinessDetails = !ShowReadinessDetails);

        RebuildContextItems();
        RebuildContentFixture();
    }

    public bool IsCapabilityScope
    {
        get => _isCapabilityScope;
        set
        {
            if (SetField(ref _isCapabilityScope, value))
            {
                OnPropertyChanged(nameof(LeftPaneTitle));
                OnPropertyChanged(nameof(LeftPaneDescription));
                OnPropertyChanged(nameof(CapabilityPaneVisibility));
                OnPropertyChanged(nameof(ContextPaneVisibility));
            }
        }
    }

    public string ActiveCapability
    {
        get => _activeCapability;
        set
        {
            if (SetField(ref _activeCapability, value))
            {
                RebuildContextItems();
                RebuildContentFixture();
                OnPropertyChanged(nameof(LeftPaneDescription));
                OnPropertyChanged(nameof(ContentHeader));
            }
        }
    }

    public bool DenseMode
    {
        get => _denseMode;
        set
        {
            if (SetField(ref _denseMode, value))
            {
                OnPropertyChanged(nameof(DenseModeLabel));
            }
        }
    }

    public bool ShowReadinessDetails
    {
        get => _showReadinessDetails;
        set
        {
            if (SetField(ref _showReadinessDetails, value))
            {
                OnPropertyChanged(nameof(ReadinessDetailsVisibility));
                OnPropertyChanged(nameof(ReadinessDetailsButtonLabel));
            }
        }
    }

    public string LeftPaneTitle => IsCapabilityScope ? "Capability Scope" : "Context Scope";
    public string LeftPaneDescription => IsCapabilityScope ? "Select a top-level capability" : $"{ActiveCapability} contextual items";
    public string ContentHeader => $"{ActiveCapability} Shell Foundation Spike";
    public string DenseModeLabel => DenseMode ? "Dense Fixture: ON" : "Dense Fixture: OFF";
    public string ReadinessDetailsButtonLabel => ShowReadinessDetails
        ? $"Hide Readiness Details ({ReadinessDetails.Count})"
        : $"Show Readiness Details ({ReadinessDetails.Count})";
    public int ActiveIssueCount => ActiveIssues.Count;

    public Visibility CapabilityPaneVisibility => IsCapabilityScope ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ContextPaneVisibility => IsCapabilityScope ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ReadinessDetailsVisibility => ShowReadinessDetails ? Visibility.Visible : Visibility.Collapsed;

    private void OnActiveIssuesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => OnPropertyChanged(nameof(ActiveIssueCount));

    private void RebuildContextItems()
    {
        ContextItems.Clear();
        foreach (var item in ActiveCapability switch
                 {
                     "Deploy" => new[] { "VM: LAB-DC-01", "VM: LAB-APP-01", "Readiness Summary", "Deploy Actions" },
                     "Templates" => new[] { "Template Library", "Selected Template", "VM Entries", "Validation" },
                     "Assets" => new[] { "Base Disks", "Virtual Switches", "Asset Health" },
                     "Diagnostics" => new[] { "Recent Operations", "Filters", "Export Diagnostics" },
                     "Settings" => new[] { "Paths", "Deployment Policy", "Diagnostics & Logs" },
                     "Machines" => new[] { "Host Inventory", "Selections", "Quick Actions" },
                     _ => new[] { "Overview" }
                 })
        {
            ContextItems.Add(item);
        }
    }

    private void RebuildContentFixture()
    {
        ReadinessDetails.Clear();
        VmCards.Clear();
        LogGroups.Clear();

        var readinessCount = DenseMode ? 12 : 3;
        for (var i = 1; i <= readinessCount; i++)
        {
            ReadinessDetails.Add($"{(i % 3 == 0 ? "Fail" : i % 2 == 0 ? "Warn" : "Pass")} | CFG.{i:00} | Fixture readiness detail {i}");
        }

        var vmCount = DenseMode ? 6 : 2;
        for (var i = 1; i <= vmCount; i++)
        {
            VmCards.Add($"{ActiveCapability} Card {i:00}");
        }

        var logCount = DenseMode ? 8 : 3;
        for (var i = 1; i <= logCount; i++)
        {
            LogGroups.Add($"Operation log group {i:00}");
        }

        OnPropertyChanged(nameof(ReadinessDetailsButtonLabel));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record ShellIssueItem(int Id, string Source, string Message);

internal sealed class RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => execute(parameter);
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
