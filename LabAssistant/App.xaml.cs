using LabAssistant.Services.Configuration;
using LabAssistant.Business;
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
            serviceCollection.AddLabAssistantServices();      // Business + Services
            serviceCollection.AddLabAssistantViewModels();    // ViewModels

            Services = serviceCollection.BuildServiceProvider();
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}