using LabAssistant.Models.PowerShell;
using LabAssistant.Models.Configuration;
using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.FileSystem;
using LabAssistant.Services.GuestExecution;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;
using LabAssistant.Services.PowerShell;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;

namespace LabAssistant.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton(new SessionPoolOptions
        {
            WarmupCount = 2,
            MaxPoolSize = 4,
            IdleRecycleTimeout = TimeSpan.FromMinutes(5)
        });
        services.AddSingleton<ISessionResolver, SessionResolver>();
        services.AddSingleton<IPowerShellSessionPool, PowerShellSessionPool>();
        services.AddTransient<IPersistentPowerShellSession, PersistentPowerShellSession>();
        services.AddSingleton<IPowerShellExecutor, PowerShellExecutor>();
        services.AddSingleton<IHyperVQueryExecutor, HyperVQueryExecutor>();
        services.AddSingleton<IHyperVAdministrativeCommandExecutor, HyperVAdministrativeCommandExecutor>();
        // The guest executor holds one dedicated, reused PowerShell Direct connection per VM, so it must NOT
        // draw on the shared catalog/admin pool: pinning a pooled session per VM would drain the small pool and
        // stall read queries. Give it its own factory that mints a fresh, dedicated host runspace, budgeted only
        // by the scheduler's concurrent-VM cap. Registered as a singleton so the per-VM session map survives the
        // whole deployment (the runtime service that consumes it is itself a singleton).
        services.AddSingleton<IGuestCommandExecutor>(_ =>
            new HyperVPowerShellDirectGuestCommandExecutor(() => new PersistentPowerShellSession()));

        // Hand out pooled sessions to all consumers. A session is checked out from the pre-warmed,
        // health-monitored pool on demand and returned to it on dispose, so existing consumers that
        // create-and-dispose a session transparently gain pooling without call-site changes.
        services.AddTransient<Func<IPersistentPowerShellSession>>(provider =>
        {
            var pool = provider.GetRequiredService<IPowerShellSessionPool>();
            return () =>
            {
                var handle = pool.CheckoutAsync().GetAwaiter().GetResult();
                return new PooledSessionLease(handle);
            };
        });

        services.AddTransient<Func<PowerShellHandle>>(_ => () => new PowerShellHandle());
        services.AddTransient<Func<IPersistentPowerShellSession, IHyperVService>>(
            provider => session => new HyperVService(session, provider.GetRequiredService<IStructuredLogger>())
        );
        services.AddTransient<IHyperVMachineAdminService, HyperVMachineAdminService>();
        services.AddSingleton<IVhdxFileAccessProbe, VhdxFileAccessProbe>();
        services.AddSingleton<IHyperVVhdxProbe, PowerShellHyperVVhdxProbe>();
        services.AddSingleton<IVhdxIntegrityValidator, VhdxIntegrityValidator>();
        services.AddSingleton<IDeploymentFileSystem, DeploymentFileSystem>();
        services.AddSingleton<IDestinationPathFeasibilityProbe, DestinationPathFeasibilityProbe>();
        services.AddSingleton<IFreeSpaceInfoProvider, FreeSpaceInfoProvider>();
        services.AddSingleton<ILogEventSink>(provider =>
        {
            var settingsStore = provider.GetRequiredService<IAppSettingsStore>();
            var appPaths = provider.GetRequiredService<IAppPaths>();
            var logFolder = string.IsNullOrWhiteSpace(settingsStore.Settings.LogFolder)
                ? appPaths.LogsFolder
                : settingsStore.Settings.LogFolder;
            var filePath = Path.Combine(logFolder, StructuredLoggingDefaults.StructuredEventsFileName);
            return new JsonLinesLogEventSink(filePath);
        });
        services.AddSingleton<IStructuredLogger>(provider =>
            new StructuredLogger(provider.GetServices<ILogEventSink>()));
        services.AddSingleton<IStructuredLogViewerService, StructuredLogViewerService>();
        services.AddSingleton<IDiagnosticsExportService, DiagnosticsExportService>();

        return services;
    }
}
