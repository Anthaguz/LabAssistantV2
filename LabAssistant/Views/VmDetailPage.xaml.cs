using System.Windows;
using System.Windows.Controls;
using LabAssistant.Models.Configuration;
using LabAssistant.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace LabAssistant.Views
{
    public partial class VmDetailPage : Page
    {
        private readonly System.Action _onBack;

        public VmDetailPage(DeploymentViewModel deploymentViewModel, VmEntryViewModel vmEntry, System.Action onBack)
        {
            InitializeComponent();
            var settingsStore = App.Services.GetRequiredService<IAppSettingsStore>();
            DataContext = new DeployVmConfigContext(deploymentViewModel, vmEntry.DeploymentContext, settingsStore);
            _onBack = onBack;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            _onBack();
        }
    }
}
