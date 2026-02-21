using System;
using System.Windows;
using System.Windows.Controls;
using LabAssistant.Models.Catalog;
using LabAssistant.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace LabAssistant.Views;

public partial class VhdxCatalogPage : Page
{
    private readonly VhdxCatalogPageViewModel _viewModel;

    public VhdxCatalogPage()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<VhdxCatalogPageViewModel>();
        CatalogListView.ItemsSource = _viewModel.Items;
        CatalogPathText.Text = _viewModel.CatalogPath;
        LoadCatalog();
    }

    private void LoadCatalog()
    {
        CatalogPathText.Text = _viewModel.CatalogPath;
        var result = _viewModel.LoadCatalog();
        if (!result.IsSuccess)
        {
            System.Windows.MessageBox.Show(string.Join(Environment.NewLine, result.Errors), "Catalog Save Errors", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new VhdxCatalogEditDialog
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            var result = _viewModel.AddItem(dialog.Item);
            if (!result.IsSuccess)
            {
                System.Windows.MessageBox.Show(string.Join(Environment.NewLine, result.Errors), "VHDX Catalog", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        var dialog = new VhdxCatalogEditDialog(VhdxCatalogPageViewModel.CloneItem(selected))
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            var result = _viewModel.UpdateItem(selected, dialog.Item);
            CatalogListView.Items.Refresh();
            if (!result.IsSuccess)
            {
                System.Windows.MessageBox.Show(string.Join(Environment.NewLine, result.Errors), "VHDX Catalog", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            var save = _viewModel.DeleteItem(selected);
            if (!save.IsSuccess)
            {
                System.Windows.MessageBox.Show(string.Join(Environment.NewLine, save.Errors), "VHDX Catalog", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        LoadCatalog();
    }
}
