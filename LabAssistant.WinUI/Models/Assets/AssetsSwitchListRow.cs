using System.ComponentModel;
using System.Runtime.CompilerServices;
using LabAssistant.Business.Assets;

namespace LabAssistant.WinUI.Models.Assets;

public sealed class AssetsSwitchListRow : INotifyPropertyChanged
{
    private string _validationSummary = "Validation has not been evaluated.";
    private string _deleteSummary = "No delete assessment has been performed.";

    public AssetsSwitchListRow(AssetsSwitchRecord record)
    {
        Name = record.Name;
        SwitchType = record.SwitchType;
        AdapterName = record.AdapterName;
        DisplayName = record.Name;
        SecondaryText = string.IsNullOrWhiteSpace(record.AdapterName)
            ? record.SwitchType
            : $"{record.SwitchType} - {record.AdapterName}";
    }

    public string Name { get; }

    public string SwitchType { get; }

    public string? AdapterName { get; }

    public string DisplayName { get; }

    public string SecondaryText { get; }

    public string ValidationSummary
    {
        get => _validationSummary;
        set => SetProperty(ref _validationSummary, value);
    }

    public string DeleteSummary
    {
        get => _deleteSummary;
        set => SetProperty(ref _deleteSummary, value);
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
