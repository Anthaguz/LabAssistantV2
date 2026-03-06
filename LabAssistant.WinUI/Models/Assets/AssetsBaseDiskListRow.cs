using System.ComponentModel;
using System.Runtime.CompilerServices;
using LabAssistant.Business.Assets;

namespace LabAssistant.WinUI.Models.Assets;

public sealed class AssetsBaseDiskListRow : INotifyPropertyChanged
{
    private string _validationSummary;
    private string _referenceSummary;

    public AssetsBaseDiskListRow(AssetsBaseDiskRecord record)
    {
        Id = record.Id;
        Path = record.Path;
        OsName = record.OsName;
        OsVersion = record.OsVersion;
        Generation = record.Generation;
        Notes = record.Notes;
        DisplayName = string.IsNullOrWhiteSpace(record.OsVersion)
            ? record.OsName
            : $"{record.OsName} {record.OsVersion}";
        SecondaryText = record.Path;
        _validationSummary = "Validation has not been evaluated.";
        _referenceSummary = "No removal assessment has been performed.";
    }

    public string Id { get; }

    public string Path { get; }

    public string OsName { get; }

    public string OsVersion { get; }

    public int Generation { get; }

    public string? Notes { get; }

    public string DisplayName { get; }

    public string SecondaryText { get; }

    public string ValidationSummary
    {
        get => _validationSummary;
        set => SetProperty(ref _validationSummary, value);
    }

    public string ReferenceSummary
    {
        get => _referenceSummary;
        set => SetProperty(ref _referenceSummary, value);
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
