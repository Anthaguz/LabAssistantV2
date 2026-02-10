using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using LabAssistant.Business.Catalog;
using LabAssistant.Models.Catalog;
using LabAssistant.ViewModels;

namespace LabAssistant.Views
{
    public partial class MissingVhdxResolutionDialog : Window
    {
        private readonly CatalogService _catalogService;
        private List<VhdxCatalogItem> _catalogItems = new();

        public MissingVhdxResolutionDialog(
            IEnumerable<MissingVhdxReference> missingReferences,
            CatalogService catalogService)
        {
            InitializeComponent();
            _catalogService = catalogService;

            Items = new ObservableCollection<MissingVhdxResolutionItem>(
                missingReferences.Select(reference => new MissingVhdxResolutionItem(reference)));
            CatalogOptions = new ObservableCollection<VhdxCatalogOption>();

            foreach (var item in Items)
            {
                item.PropertyChanged += Item_PropertyChanged;
            }

            LoadCatalog(showErrors: true);
            UpdateOkState();

            DataContext = this;
        }

        public ObservableCollection<MissingVhdxResolutionItem> Items { get; }

        public ObservableCollection<VhdxCatalogOption> CatalogOptions { get; }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MissingVhdxResolutionItem.SelectedOption))
            {
                UpdateOkState();
            }
        }

        private void Resolve_Click(object sender, RoutedEventArgs e)
        {
            UpdateOkState();
            if (OkButton.IsEnabled)
            {
                DialogResult = true;
            }
        }

        private void ImportVhdx_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new VhdxCatalogEditDialog
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            LoadCatalog(showErrors: false);
            var duplicatePath = FindDuplicatePath(dialog.Item.Path);
            if (duplicatePath != null)
            {
                var choice = System.Windows.MessageBox.Show(
                    $"A catalog entry already exists for this path:{Environment.NewLine}{duplicatePath.Path}{Environment.NewLine}{Environment.NewLine}Use the existing entry instead?",
                    "Duplicate VHDX Path",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (choice != MessageBoxResult.No)
                {
                    return;
                }
            }

            if (HasDuplicateId(dialog.Item.Id))
            {
                System.Windows.MessageBox.Show(
                    "Catalog id must be unique.",
                    "VHDX Catalog",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            _catalogItems.Add(dialog.Item);
            if (!SaveCatalog())
            {
                _catalogItems.Remove(dialog.Item);
                return;
            }

            RefreshCatalogOptions();
        }

        private void UpdateOkState()
        {
            OkButton.IsEnabled = Items.All(item => item.SelectedOption != null);
        }

        private void LoadCatalog(bool showErrors)
        {
            var result = _catalogService.LoadCatalog();
            _catalogItems = result.Items.ToList();
            RefreshCatalogOptions();

            EmptyCatalogHint.Visibility = _catalogItems.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (showErrors && result.Errors.Count > 0)
            {
                System.Windows.MessageBox.Show(
                    string.Join(Environment.NewLine, result.Errors),
                    "Catalog Errors",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void RefreshCatalogOptions()
        {
            CatalogOptions.Clear();
            foreach (var item in _catalogItems)
            {
                CatalogOptions.Add(new VhdxCatalogOption(item));
            }

            foreach (var item in Items)
            {
                if (item.SelectedOption != null &&
                    !CatalogOptions.Any(option => string.Equals(option.Item.Id, item.SelectedOption.Item.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    item.SelectedOption = null;
                }
            }

            UpdateOkState();
        }

        private bool SaveCatalog()
        {
            var result = _catalogService.SaveCatalog(_catalogItems);
            if (!result.IsValid)
            {
                System.Windows.MessageBox.Show(
                    string.Join(Environment.NewLine, result.Errors),
                    "Catalog Save Errors",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private bool HasDuplicateId(string id)
        {
            return _catalogItems.Any(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        private VhdxCatalogItem? FindDuplicatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            return _catalogItems.FirstOrDefault(item =>
                string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase));
        }
    }

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
}
