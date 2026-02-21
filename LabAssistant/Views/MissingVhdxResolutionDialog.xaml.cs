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
        private readonly MissingVhdxResolutionDialogViewModel _viewModel;

        public MissingVhdxResolutionDialog(
            IEnumerable<MissingVhdxReference> missingReferences,
            CatalogService catalogService)
        {
            InitializeComponent();
            _viewModel = new MissingVhdxResolutionDialogViewModel(missingReferences, catalogService);
            _viewModel.StateChanged += UpdateOkState;
            var load = _viewModel.LoadCatalog();
            if (!load.IsSuccess)
            {
                System.Windows.MessageBox.Show(
                    string.Join(Environment.NewLine, load.Errors),
                    "Catalog Errors",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            UpdateOkState();

            DataContext = this;
        }

        public ObservableCollection<MissingVhdxResolutionItem> Items => _viewModel.Items;

        public ObservableCollection<VhdxCatalogOption> CatalogOptions => _viewModel.CatalogOptions;

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

            var result = _viewModel.ImportCatalogItem(dialog.Item);
            if (!result.IsSuccess)
            {
                System.Windows.MessageBox.Show(
                    string.Join(Environment.NewLine, result.Errors),
                    "Catalog Save Errors",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            EmptyCatalogHint.Visibility = _viewModel.IsCatalogEmpty
                ? Visibility.Visible
                : Visibility.Collapsed;
            UpdateOkState();
        }

        private void UpdateOkState()
        {
            OkButton.IsEnabled = _viewModel.CanResolve;
            EmptyCatalogHint.Visibility = _viewModel.IsCatalogEmpty
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public IReadOnlyList<ResolvedVhdxSelection> GetResolvedSelections()
        {
            return Items
                .Where(item => item.SelectedOption != null)
                .Select(item => new ResolvedVhdxSelection(item.Reference.Vm, item.SelectedOption!.Item))
                .ToList();
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
