using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LabAssistant.WinUI.Infrastructure;
using LabAssistant.WinUI.Models.Deploy;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.ViewModels.Deploy;

public partial class DeployProgressViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<DeployVmProgressItem> _vmProgress = new();

    [ObservableProperty]
    private string _overallStatus = "Idle";

    [ObservableProperty]
    private double _progressPercent;

    [ObservableProperty]
    private string _progressSummary = "No deployment started.";

    [ObservableProperty]
    private bool _hasResults;

    [ObservableProperty]
    private ObservableCollection<DeployResultItem> _results = new();

    public IReadOnlyList<DeployVmProgressItem> VmProgressItems => VmProgress;

    public string OverallStatusText => OverallStatus;

    public double ProgressPercentValue => ProgressPercent;

    public string ProgressSummaryText => ProgressSummary;

    public IReadOnlyList<DeployResultItem> ResultItems => Results;

    public bool HasVmProgress => VmProgress.Count > 0;

    public Visibility VmProgressVisibility => HasVmProgress ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ResultsVisibility => HasResults ? Visibility.Visible : Visibility.Collapsed;

    public Visibility LoadingVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmptyStateVisibility => !IsLoading && !HasVmProgress ? Visibility.Visible : Visibility.Collapsed;

    public string EmptyStateMessage => string.IsNullOrWhiteSpace(ProgressSummary) ? "No deployment started." : ProgressSummary;

    public void Reset() => UpdateState("Idle", 0, "No deployment started.", []);

    public void ShowLoading(string statusMessage)
    {
        IsLoading = true;
        OverallStatus = "Loading";
        ProgressPercent = 0;
        ProgressSummary = statusMessage;
        ReplaceItems(VmProgress, []);
        ReplaceItems(Results, []);
        HasResults = false;
        NotifyDerivedStateChanged();
    }

    public void UpdateState(string overallStatus, double progressPercent, string progressSummary, IReadOnlyList<DeployVmResultRow> rows)
    {
        IsLoading = false;
        OverallStatus = string.IsNullOrWhiteSpace(overallStatus) ? "Idle" : overallStatus;
        ProgressPercent = progressPercent;
        ProgressSummary = string.IsNullOrWhiteSpace(progressSummary) ? "No deployment started." : progressSummary;

        ReplaceItems(VmProgress, rows.Select(DeployVmProgressItem.FromRow));

        var showResults = rows.Count > 0 && !IsProgressState(OverallStatus);
        ReplaceItems(Results, showResults ? rows.Select(row => new DeployResultItem(row.VmName, row.Status, row.Summary)) : []);
        HasResults = showResults && Results.Count > 0;
        NotifyDerivedStateChanged();
    }

    partial void OnOverallStatusChanged(string value) => OnPropertyChanged(nameof(OverallStatusText));

    partial void OnProgressPercentChanged(double value) => OnPropertyChanged(nameof(ProgressPercentValue));

    partial void OnProgressSummaryChanged(string value)
    {
        OnPropertyChanged(nameof(ProgressSummaryText));
        OnPropertyChanged(nameof(EmptyStateMessage));
    }

    private static bool IsProgressState(string status) =>
        string.Equals(status, "Running", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "Starting", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "Evaluating", StringComparison.OrdinalIgnoreCase);

    private void NotifyDerivedStateChanged()
    {
        OnPropertyChanged(nameof(VmProgressItems));
        OnPropertyChanged(nameof(ResultItems));
        OnPropertyChanged(nameof(OverallStatusText));
        OnPropertyChanged(nameof(ProgressPercentValue));
        OnPropertyChanged(nameof(ProgressSummaryText));
        OnPropertyChanged(nameof(HasVmProgress));
        OnPropertyChanged(nameof(VmProgressVisibility));
        OnPropertyChanged(nameof(ResultsVisibility));
        OnPropertyChanged(nameof(LoadingVisibility));
        OnPropertyChanged(nameof(EmptyStateVisibility));
        OnPropertyChanged(nameof(EmptyStateMessage));
    }

    private static void ReplaceItems<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }
}
