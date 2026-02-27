using LabAssistant.Business;
using LabAssistant.Models.Configuration;
using LabAssistant.Services.Logging;
using InfrastructureServices = LabAssistant.Services.ServiceCollectionExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI;

public partial class App : Application
{
    private Window? _window;

    public static ServiceProvider Services { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
        RequestedTheme = ApplicationTheme.Light;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);

        var serviceCollection = new ServiceCollection();
        InfrastructureServices.AddInfrastructureServices(serviceCollection);
        serviceCollection.AddBusinessServices();
        serviceCollection.AddPersistenceServices();

        Services = serviceCollection.BuildServiceProvider();
        var settingsStore = Services.GetRequiredService<IAppSettingsStore>();
        settingsStore.LoadOrCreate();
        DebugLogger.SetLogFolder(settingsStore.Settings.LogFolder);

        _window = new MainWindow();
        _window.Activate();
    }
}
