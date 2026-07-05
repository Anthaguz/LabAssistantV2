using System.ComponentModel;
using System.Runtime.CompilerServices;
using LabAssistant.Models.Templates;

namespace LabAssistant.WinUI.Models.Deploy;

/// <summary>
/// Presentation row for a single Quick Deploy VM entry. Exposes only runtime-independent state
/// (display text plus issue severity/flags) so the view maps severity and visibility through
/// converters and the type stays unit-testable without the WinUI runtime.
/// </summary>
public sealed class DeployQuickDeployVmEntryRow : INotifyPropertyChanged
{
    private string _displayName;
    private string _secondaryText;
    private string _issueBadgeText = string.Empty;
    private string _issueSummary = string.Empty;
    private string _issueSeverity = "None";
    private bool _hasIssueBadge;
    private bool _hasIssueSummary;

    public DeployQuickDeployVmEntryRow(VmTemplate vmEntry)
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

    /// <summary>
    /// Severity token consumed by the view brush converter. One of "Critical", "Warning", or "None".
    /// </summary>
    public string IssueSeverity
    {
        get => _issueSeverity;
        set => SetProperty(ref _issueSeverity, value);
    }

    public bool HasIssueBadge
    {
        get => _hasIssueBadge;
        set => SetProperty(ref _hasIssueBadge, value);
    }

    public bool HasIssueSummary
    {
        get => _hasIssueSummary;
        set => SetProperty(ref _hasIssueSummary, value);
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
