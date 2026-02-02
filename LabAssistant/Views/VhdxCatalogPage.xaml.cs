using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LabAssistant.Views;

public partial class VhdxCatalogPage : Page
{
    private readonly ObservableCollection<VhdxCatalogItem> _items = new();
    private readonly IVhdxCatalogStore _store;
    private readonly IAppSettingsStore _settingsStore;

    public VhdxCatalogPage()
    {
        InitializeComponent();
        _store = App.Services.GetRequiredService<IVhdxCatalogStore>();
        _settingsStore = App.Services.GetRequiredService<IAppSettingsStore>();
        CatalogListView.ItemsSource = _items;
        CatalogPathText.Text = _settingsStore.Settings.CatalogPath;
        LoadCatalog();
    }

    private void LoadCatalog()
    {
        var catalogPath = _settingsStore.Settings.CatalogPath;
        CatalogPathText.Text = catalogPath;

        var result = _store.Load(catalogPath);
        _items.Clear();
        foreach (var item in result.Items)
        {
            _items.Add(item);
        }

        if (result.Errors.Count > 0)
        {
            System.Windows.MessageBox.Show(string.Join(Environment.NewLine, result.Errors), "Catalog Errors", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private bool SaveCatalog()
    {
        var result = _store.Save(_settingsStore.Settings.CatalogPath, _items);
        if (!result.IsValid)
        {
            System.Windows.MessageBox.Show(string.Join(Environment.NewLine, result.Errors), "Catalog Save Errors", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        return true;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new VhdxCatalogEditDialog
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            if (HasDuplicateId(dialog.Item.Id, null))
            {
                System.Windows.MessageBox.Show("Catalog id must be unique.", "VHDX Catalog", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _items.Add(dialog.Item);
            if (!SaveCatalog())
            {
                _items.Remove(dialog.Item);
            }
        }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (CatalogListView.SelectedItem is not VhdxCatalogItem selected)
        {
            System.Windows.MessageBox.Show("Select an item to edit.", "VHDX Catalog", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new VhdxCatalogEditDialog(CloneItem(selected))
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            if (HasDuplicateId(dialog.Item.Id, selected))
            {
                System.Windows.MessageBox.Show("Catalog id must be unique.", "VHDX Catalog", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var original = CloneItem(selected);
            selected.Id = dialog.Item.Id;
            selected.Path = dialog.Item.Path;
            selected.OsName = dialog.Item.OsName;
            selected.OsVersion = dialog.Item.OsVersion;
            selected.Generation = dialog.Item.Generation;
            selected.Notes = dialog.Item.Notes;
            CatalogListView.Items.Refresh();

            if (!SaveCatalog())
            {
                selected.Id = original.Id;
                selected.Path = original.Path;
                selected.OsName = original.OsName;
                selected.OsVersion = original.OsVersion;
                selected.Generation = original.Generation;
                selected.Notes = original.Notes;
                CatalogListView.Items.Refresh();
            }
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (CatalogListView.SelectedItem is not VhdxCatalogItem selected)
        {
            System.Windows.MessageBox.Show("Select an item to delete.", "VHDX Catalog", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = System.Windows.MessageBox.Show($"Delete catalog item '{selected.Id}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            _items.Remove(selected);
            if (!SaveCatalog())
            {
                _items.Add(selected);
            }
        }
    }

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        LoadCatalog();
    }

    private bool HasDuplicateId(string id, VhdxCatalogItem? ignore)
    {
        return _items.Any(item =>
            !ReferenceEquals(item, ignore) &&
            string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    private static VhdxCatalogItem CloneItem(VhdxCatalogItem source)
    {
        return new VhdxCatalogItem
        {
            Id = source.Id,
            Path = source.Path,
            OsName = source.OsName,
            OsVersion = source.OsVersion,
            Generation = source.Generation,
            Notes = source.Notes
        };
    }
}
