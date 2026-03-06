using LabAssistant.Business;
using LabAssistant.WinUI.Diagnostics;
using LabAssistant.Models.Configuration;
using LabAssistant.Services.Logging;
using InfrastructureServices = LabAssistant.Services.ServiceCollectionExtensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using System.Diagnostics;

namespace LabAssistant.WinUI;

public partial class App : Application
{
    private Window? _window;

    public static ServiceProvider Services { get; private set; } = null!;

    public App()
    {
        StartupCrashLogger.MarkPhase("App.ctor", "enter");
        InitializeComponent();
        StartupCrashLogger.MarkPhase("App.ctor", "after InitializeComponent");
        StartupCrashLogger.MarkPhase("App.ctor", "theme unchanged");

        UnhandledException += (_, e) =>
        {
            StartupCrashLogger.LogMessage("Application.UnhandledException", e.Message);
            if (e.Exception is Exception ex)
            {
                StartupCrashLogger.LogException("Application.UnhandledException", ex);
            }
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                StartupCrashLogger.LogException("AppDomain.CurrentDomain.UnhandledException", ex);
            }
            else
            {
                StartupCrashLogger.LogMessage("AppDomain.CurrentDomain.UnhandledException", e.ExceptionObject?.ToString() ?? "<null>");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            StartupCrashLogger.LogException("TaskScheduler.UnobservedTaskException", e.Exception);
        };

#if DEBUG
        // Emit unhandled XAML details in Output so fail-fast dumps have a matching managed breadcrumb.
        UnhandledException += (_, e) =>
        {
            Debug.WriteLine($"[WinUI UnhandledException] {e.Message}");
        };
#endif
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        StartupCrashLogger.MarkPhase("OnLaunched", "enter");
        try
        {
            base.OnLaunched(args);
            StartupCrashLogger.MarkPhase("OnLaunched", "after base.OnLaunched");

            var serviceCollection = new ServiceCollection();
            StartupCrashLogger.MarkPhase("OnLaunched", "before service registration");
            InfrastructureServices.AddInfrastructureServices(serviceCollection);
            serviceCollection.AddBusinessServices();
            serviceCollection.AddPersistenceServices();
            StartupCrashLogger.MarkPhase("OnLaunched", "after service registration");

            Services = serviceCollection.BuildServiceProvider();
            StartupCrashLogger.MarkPhase("OnLaunched", "after service provider build");
            var settingsStore = Services.GetRequiredService<IAppSettingsStore>();
            StartupCrashLogger.MarkPhase("OnLaunched", "before settings load");
            settingsStore.LoadOrCreate();
            StartupCrashLogger.MarkPhase("OnLaunched", "after settings load");
            DebugLogger.SetLogFolder(settingsStore.Settings.LogFolder);
            StartupCrashLogger.MarkPhase("OnLaunched", "after DebugLogger.SetLogFolder");

            StartupCrashLogger.MarkPhase("OnLaunched", "before MainWindow ctor");
            _window = new MainWindow();
            StartupCrashLogger.MarkPhase("OnLaunched", "after MainWindow ctor");
            _window.Activate();
            StartupCrashLogger.MarkPhase("OnLaunched", "after MainWindow.Activate");
        }
        catch (Exception ex)
        {
            StartupCrashLogger.LogException("App.OnLaunched", ex);
            throw;
        }
    }
}
