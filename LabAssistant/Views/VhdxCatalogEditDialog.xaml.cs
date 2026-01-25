using System;
using System.Windows;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Validation;
namespace LabAssistant.Views;

public partial class VhdxCatalogEditDialog : Window
{
    public VhdxCatalogItem Item { get; }

    public VhdxCatalogEditDialog()
        : this(new VhdxCatalogItem { Generation = 1 })
    {
    }

    public VhdxCatalogEditDialog(VhdxCatalogItem item)
    {
        InitializeComponent();
        Item = item;
        LoadItem();
    }

    private void LoadItem()
    {
        PathBox.Text = Item.Path;
        OsNameBox.Text = Item.OsName;
        OsVersionBox.Text = Item.OsVersion;
        GenerationBox.Text = Item.Generation <= 0 ? string.Empty : Item.Generation.ToString();
        NotesBox.Text = Item.Notes ?? string.Empty;
        NotesHint.Visibility = string.IsNullOrWhiteSpace(NotesBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        Title = string.IsNullOrWhiteSpace(Item.Id) ? "Add VHDX Catalog Item" : "Edit VHDX Catalog Item";
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!TryUpdateItem(out var errorMessage))
        {
            System.Windows.MessageBox.Show(errorMessage, "Catalog Item", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void NotesBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        NotesHint.Visibility = string.IsNullOrWhiteSpace(NotesBox.Text) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "VHDX files (*.vhdx)|*.vhdx|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            PathBox.Text = dialog.FileName;
        }
    }

    private bool TryUpdateItem(out string errorMessage)
    {
        var path = PathBox.Text.Trim();
        var osName = OsNameBox.Text.Trim();
        var osVersion = OsVersionBox.Text.Trim();
        var notes = NotesBox.Text.Trim();

        if (!int.TryParse(GenerationBox.Text.Trim(), out var generation) || generation <= 0)
        {
            errorMessage = "Generation must be a positive number.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Item.Id))
        {
            Item.Id = Guid.NewGuid().ToString("N");
        }
        Item.Path = path;
        Item.OsName = osName;
        Item.OsVersion = osVersion;
        Item.Generation = generation;
        Item.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes;

        var validation = VhdxCatalogValidator.Validate(new[] { Item });
        if (!validation.IsValid)
        {
            errorMessage = string.Join(Environment.NewLine, validation.Errors);
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
