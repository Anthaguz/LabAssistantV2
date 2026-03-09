using System.ComponentModel;
using System.Runtime.CompilerServices;
using LabAssistant.Models.Templates;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace LabAssistant.WinUI.Models.Deploy;

public sealed class DeployOnTheFlyVmEntryRow : INotifyPropertyChanged
{
    private string _displayName;
    private string _secondaryText;
    private string _issueBadgeText = string.Empty;
    private string _issueSummary = string.Empty;
    private Visibility _issueBadgeVisibility = Visibility.Collapsed;
    private Visibility _issueSummaryVisibility = Visibility.Collapsed;
    private Brush? _issueBrush;

    public DeployOnTheFlyVmEntryRow(VmTemplate vmEntry)
    {
        VmEntry = vmEntry;
        _displayName = string.IsNullOrWhiteSpace(vmEntry.Name) ? "Unnamed VM" : vmEntry.Name.Trim();
        _secondaryText = string.Empty;
    }

    public VmTemplate VmEntry { get; }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public string SecondaryText
    {
        get => _secondaryText;
        set => SetProperty(ref _secondaryText, value);
    }

    public string IssueBadgeText
    {
        get => _issueBadgeText;
        set => SetProperty(ref _issueBadgeText, value);
    }

    public string IssueSummary
    {
        get => _issueSummary;
        set => SetProperty(ref _issueSummary, value);
    }

    public Visibility IssueBadgeVisibility
    {
        get => _issueBadgeVisibility;
        set => SetProperty(ref _issueBadgeVisibility, value);
    }

    public Visibility IssueSummaryVisibility
    {
        get => _issueSummaryVisibility;
        set => SetProperty(ref _issueSummaryVisibility, value);
    }

    public Brush? IssueBrush
    {
        get => _issueBrush;
        set => SetProperty(ref _issueBrush, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
