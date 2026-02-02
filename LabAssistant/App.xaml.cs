using LabAssistant.Business;
using LabAssistant.Models.Configuration;
using LabAssistant.Services.Logging;
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

            // Configure DI
            var serviceCollection = new ServiceCollection();
            InfrastructureServices.AddInfrastructureServices(serviceCollection);    // Services
            serviceCollection.AddBusinessServices();          // Business
            serviceCollection.AddPersistenceServices();       // Data-backed persistence
            serviceCollection.AddLabAssistantViewModels();    // ViewModels

            Services = serviceCollection.BuildServiceProvider();
            var settingsStore = Services.GetRequiredService<IAppSettingsStore>();
            settingsStore.LoadOrCreate();
            DebugLogger.SetLogFolder(settingsStore.Settings.LogFolder);
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}
