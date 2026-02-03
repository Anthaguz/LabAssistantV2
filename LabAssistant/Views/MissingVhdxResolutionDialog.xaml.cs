using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.ViewModels;

namespace LabAssistant.Views
{
    public partial class MissingVhdxResolutionDialog : Window
    {
        private readonly IVhdxCatalogStore _catalogStore;
        private readonly IAppSettingsStore _settingsStore;
        private List<VhdxCatalogItem> _catalogItems = new();

        public MissingVhdxResolutionDialog(
            IEnumerable<MissingVhdxReference> missingReferences,
            IVhdxCatalogStore catalogStore,
            IAppSettingsStore settingsStore)
        {
            InitializeComponent();
            _catalogStore = catalogStore;
            _settingsStore = settingsStore;

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
            if (HasDuplicateId(dialog.Item.Id))
            {
                MessageBox.Show(
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
            var result = _catalogStore.Load(_settingsStore.Settings.CatalogPath);
            _catalogItems = result.Items.ToList();
            RefreshCatalogOptions();

            if (showErrors && result.Errors.Count > 0)
            {
                MessageBox.Show(
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
            var result = _catalogStore.Save(_settingsStore.Settings.CatalogPath, _catalogItems);
            if (!result.IsValid)
            {
                MessageBox.Show(
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
