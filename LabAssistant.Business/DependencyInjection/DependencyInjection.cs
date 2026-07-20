using LabAssistant.Business.Catalog;
using LabAssistant.Business.Deployment;
using LabAssistant.Business.Machines;
using LabAssistant.Business.Planning;
using LabAssistant.Business.Runtime;
using LabAssistant.Business.Templates;
using LabAssistant.Business.Assets;
using LabAssistant.Data.Catalog;
using LabAssistant.Data.Configuration;
using LabAssistant.Data.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Templates;
using LabAssistant.Services.FileSystem;
using Microsoft.Extensions.DependencyInjection;

namespace LabAssistant.Business;

public static class BusinessServiceCollectionExtensions
{
    public static IServiceCollection AddBusinessServices(this IServiceCollection services)
    {
        services.AddSingleton<CatalogService>();
        services.AddSingleton<IAssetsBaseDisksCapabilityService, AssetsBaseDisksCapabilityService>();
        services.AddSingleton<IAssetsSwitchesCapabilityService, AssetsSwitchesCapabilityService>();
        services.AddSingleton<MissingVhdxResolutionService>();
        services.AddSingleton<TemplateSelectionService>();
        services.AddSingleton<TemplateValidationService>();
        services.AddSingleton<ITemplatesCapabilityService, TemplatesCapabilityService>();
        services.AddSingleton<IV2PlanningCapabilityService, V2PlanningCapabilityService>();
        services.AddSingleton<IV2RuntimeCapabilityService, V2RuntimeCapabilityService>();

        services.AddTransient<VirtualSwitchProvider>();
        services.AddSingleton<IDeploymentPreflightCheck, BaseVhdxIntegrityPreflightCheck>();
        services.AddSingleton<IDeploymentPreflightCheck, GuestStepConfigurationCompletenessPreflightCheck>();
        services.AddSingleton<IDeploymentPreflightCheck, DestinationPathStoragePreflightCheck>();
        services.AddSingleton<IDeploymentPreflightService, DeploymentPreflightService>();
        services.AddSingleton<IVmCleanupOrchestrator, VmCleanupOrchestrator>();
        services.AddSingleton<IMachinesCapabilityService, MachinesCapabilityService>();

        return services;
    }

    public static IServiceCollection AddPersistenceServices(this IServiceCollection services)
    {
        services.AddSingleton<IAppPaths, AppPaths>();
        services.AddSingleton<IAppSettingsStore, AppSettingsStore>();
        services.AddSingleton<ILocalCredentialSlotStore, LocalCredentialSlotStore>();
        services.AddSingleton<IVhdxCatalogStore, VhdxCatalogStore>();
        services.AddSingleton<ILabTemplateStore, LabTemplateStore>();
        return services;
    }
}
