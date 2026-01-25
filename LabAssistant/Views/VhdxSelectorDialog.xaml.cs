using System.Collections.Generic;
using System.Linq;
using System.Windows;
using LabAssistant.Models.Catalog;

namespace LabAssistant.Views;

public partial class VhdxSelectorDialog : Window
{
    private readonly List<VhdxSelectorItem> _items;

    public VhdxSelectorDialog(IEnumerable<VhdxCatalogItem> items)
    {
        InitializeComponent();
        _items = items
            .Select(item => new VhdxSelectorItem(item))
            .ToList();
        CatalogListBox.ItemsSource = _items;
    }

    public VhdxCatalogItem? SelectedItem
        => (CatalogListBox.SelectedItem as VhdxSelectorItem)?.Item;

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem == null)
        {
            System.Windows.MessageBox.Show("Select a VHDX first.", "Select VHDX", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private sealed class VhdxSelectorItem
    {
        public VhdxSelectorItem(VhdxCatalogItem item)
        {
            Item = item;
            DisplayText = $"{item.Id} - {item.OsName} {item.OsVersion}";
        }

        public VhdxCatalogItem Item { get; }
        public string DisplayText { get; }
    }
}
