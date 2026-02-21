using System.ComponentModel;
using LabAssistant.Models.Catalog;

namespace LabAssistant.ViewModels;

public sealed class MissingVhdxResolutionItem : INotifyPropertyChanged
{
    private VhdxCatalogOption? _selectedOption;

    public MissingVhdxResolutionItem(MissingVhdxReference reference)
    {
        Reference = reference;
    }

    public MissingVhdxReference Reference { get; }

    public string VmName => Reference.VmName;

    public string MissingId => Reference.MissingId;

    public VhdxCatalogOption? SelectedOption
    {
        get => _selectedOption;
        set
        {
            if (!ReferenceEquals(_selectedOption, value))
            {
                _selectedOption = value;
                OnPropertyChanged(nameof(SelectedOption));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class VhdxCatalogOption
{
    public VhdxCatalogOption(VhdxCatalogItem item)
    {
        Item = item;
        DisplayName = $"{item.OsName} {item.OsVersion} (Gen {item.Generation})";
    }

    public VhdxCatalogItem Item { get; }

    public string DisplayName { get; }
}
