using LabAssistant.Models.PowerShell;
using LabAssistant.Models.Configuration;
using LabAssistant.Services.FileSystem;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace LabAssistant.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<ISessionResolver, SessionResolver>();
        services.AddTransient<IPersistentPowerShellSession, PersistentPowerShellSession>();
        services.AddSingleton<IPowerShellExecutor, PowerShellExecutor>();

        services.AddTransient<Func<IPersistentPowerShellSession>>(provider =>
            () => provider.GetRequiredService<IPersistentPowerShellSession>()
        );

        services.AddTransient<Func<PowerShellHandle>>(_ => () => new PowerShellHandle());
        services.AddTransient<Func<IPersistentPowerShellSession, IHyperVService>>(
            _ => session => new HyperVService(session)
        );
        services.AddSingleton<IDeploymentFileSystem, DeploymentFileSystem>();
        services.AddSingleton<ILogEventSink>(provider =>
        {
            var settingsStore = provider.GetRequiredService<IAppSettingsStore>();
            var appPaths = provider.GetRequiredService<IAppPaths>();
            var logFolder = string.IsNullOrWhiteSpace(settingsStore.Settings.LogFolder)
                ? appPaths.LogsFolder
                : settingsStore.Settings.LogFolder;
            var filePath = Path.Combine(logFolder, "structured-events.jsonl");
            return new JsonLinesLogEventSink(filePath);
        });
        services.AddSingleton<IStructuredLogger>(provider =>
            new StructuredLogger(provider.GetServices<ILogEventSink>()));

        return services;
    }
}
