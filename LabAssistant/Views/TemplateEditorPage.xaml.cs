using System.Windows;
using System.Windows.Controls;
using LabAssistant.Models.Templates;
using LabAssistant.ViewModels;

namespace LabAssistant.Views
{
    public partial class TemplateEditorPage : Page
    {
        private readonly TemplateEditorViewModel _viewModel;

        public TemplateEditorPage()
        {
            InitializeComponent();
            _viewModel = new TemplateEditorViewModel();
            DataContext = _viewModel;
        }

        private void AddVm_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.AddVm();
        }

        private void RemoveVm_Click(object sender, RoutedEventArgs e)
        {
            if (VmDataGrid.SelectedItem is not VmTemplate selectedVm)
            {
                System.Windows.MessageBox.Show(
                    "Select a VM row to remove.",
                    "Remove VM",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var result = System.Windows.MessageBox.Show(
                $"Remove VM '{selectedVm.Name}'?",
                "Confirm Remove",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _viewModel.RemoveVm(selectedVm);
            }
        }
    }
}
