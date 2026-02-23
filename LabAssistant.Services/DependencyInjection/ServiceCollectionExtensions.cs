using LabAssistant.Models.PowerShell;
using LabAssistant.Services.FileSystem;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.PowerShell;
using Microsoft.Extensions.DependencyInjection;

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

        return services;
    }
}
