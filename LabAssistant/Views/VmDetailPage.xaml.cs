using System.Windows;
using System.Windows.Controls;
using LabAssistant.ViewModels;

namespace LabAssistant.Views
{
    public partial class VmDetailPage : Page
    {
        private readonly System.Action _onBack;

        public VmDetailPage(DeploymentViewModel deploymentViewModel, VmEntryViewModel vmEntry, System.Action onBack)
        {
            InitializeComponent();
            DataContext = new DeployVmConfigContext(deploymentViewModel, vmEntry.DeploymentContext);
            _onBack = onBack;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            _onBack();
        }
    }
}
