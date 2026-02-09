//using LabAssistant.Business;
//using LabAssistant.Business.Deployment;
//using LabAssistant.Models.PowerShell;
//using LabAssistant.Services.Configuration;
//using LabAssistant.Services.HyperV;
//using LabAssistant.Services.Logging;
//using LabAssistant.Services.PowerShell;
//using Microsoft.Extensions.DependencyInjection;

//namespace LabAssistant.Business;

//public static class DependencyInjection
//{
//    public static IServiceCollection AddLabAssistantServices(this IServiceCollection services)
//    {
//        // Steps (registered as transient so each pipeline gets a clean instance)
//        services.AddTransient<CreateVmStep>();
//        services.AddTransient<EnableGuestServicesStep>();
//        services.AddTransient<ConfigureVmStep>();
//        services.AddTransient<StartVmStep>();
//        services.AddTransient<SetTimeZoneStep>();
//        services.AddTransient<InstallSoftwareStep>();
//        services.AddTransient<CheckHyperVStep>();
//        services.AddTransient<CreateVhdStep>();

//        // PowerShell and session management
//        services.AddSingleton<ISessionResolver, SessionResolver>();
//        services.AddTransient<IPersistentPowerShellSession, PersistentPowerShellSession>();
//        services.AddSingleton<IPowerShellExecutor, PowerShellExecutor>();

//        // Factory for creating new PowerShellHandle
//        services.AddTransient<Func<PowerShellHandle>>(_ => () => new PowerShellHandle());

//        // Factory for creating HyperVService from a session
//        services.AddTransient<Func<IPersistentPowerShellSession, IHyperVService>>(provider => session =>
//            new HyperVService(session)
//        );

//        // Factory for building a deployment pipeline for a given PowerShellHandle
//        services.AddTransient<Func<PowerShellHandle, DeploymentStep>>(provider => handle =>
//        {
//            var resolver = provider.GetRequiredService<ISessionResolver>();

//            return new CreateVmStep(resolver, provider.GetRequiredService<Func<IPersistentPowerShellSession, IHyperVService>>())
//                .SetNext(provider.GetRequiredService<ConfigureVmStep>())
//                .SetNext(provider.GetRequiredService<StartVmStep>())
//                .SetNext(provider.GetRequiredService<EnableGuestServicesStep>())
//                .SetNext(provider.GetRequiredService<SetTimeZoneStep>())
//                .SetNext(provider.GetRequiredService<InstallSoftwareStep>());
//        });

//        // Multi-VM coordinator
//        services.AddSingleton<MultiVmDeploymentCoordinator>();

//        // ViewModel
//        //services.AddTransient<DeploymentViewModel>();

//        return services;
//    }
//}

using LabAssistant.Business.Deployment;
using LabAssistant.Business.Templates;
using LabAssistant.Data.Catalog;
using LabAssistant.Data.Configuration;
using LabAssistant.Data.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Templates;
using Microsoft.Extensions.DependencyInjection;

namespace LabAssistant.Business;

public static class BusinessServiceCollectionExtensions
{
    public static IServiceCollection AddBusinessServices(this IServiceCollection services)
    {
        // Steps (registered as transient so each pipeline gets a clean instance)
        services.AddTransient<CreateVmFolderStep>();
        services.AddTransient<CreateVhdStep>();
        services.AddTransient<CreateVmStep>();
        services.AddTransient<AddNicToVmStep>();
        services.AddTransient<StartVmStep>();
        services.AddTransient<ConfigureVmStep>();
        services.AddTransient<EnableGuestServicesStep>();
        services.AddTransient<DisableVmCheckpoints>();
        services.AddTransient<SetTimeZoneStep>();
        services.AddTransient<InstallSoftwareStep>();
        services.AddTransient<CheckHyperVStep>();
        services.AddSingleton<MissingVhdxResolutionService>();

        services.AddTransient<VirtualSwitchProvider>();
        services.AddTransient<DeploymentPipelineBuilder>();
        services.AddSingleton<MultiVmDeploymentCoordinator>();

        return services;
    }

    public static IServiceCollection AddPersistenceServices(this IServiceCollection services)
    {
        services.AddSingleton<IAppPaths, AppPaths>();
        services.AddSingleton<IAppSettingsStore, AppSettingsStore>();
        services.AddSingleton<IVhdxCatalogStore, VhdxCatalogStore>();
        services.AddSingleton<ILabTemplateStore, LabTemplateStore>();
        return services;
    }
}
