using LabAssistant.Business;
using LabAssistant.Services.Configuration;
using InfrastructureServices = LabAssistant.Services.ServiceCollectionExtensions;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace LabAssistant
{
    public partial class App : System.Windows.Application
    {
        public static ServiceProvider Services { get; private set; } = null!;
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Load or create the application settings
            SettingsManager.LoadOrCreate();

            // Configure DI
            var serviceCollection = new ServiceCollection();
            InfrastructureServices.AddInfrastructureServices(serviceCollection);    // Services
            serviceCollection.AddBusinessServices();          // Business
            serviceCollection.AddLabAssistantViewModels();    // ViewModels

            Services = serviceCollection.BuildServiceProvider();
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}
