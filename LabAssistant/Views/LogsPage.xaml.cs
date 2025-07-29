using LabAssistant.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;

namespace LabAssistant.Views
{
    public partial class LogsPage : Page
    {
        public LogsPage()
        {
            InitializeComponent();

            DataContext = App.Services.GetRequiredService<DeploymentViewModel>();
            // this.DataContext = new DeploymentViewModel(); // Attach ViewModel here
        }
    }
}
