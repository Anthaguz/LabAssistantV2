using Microsoft.Extensions.DependencyInjection;
using LabAssistant.ViewModels;

namespace LabAssistant;

public static class ViewModelRegistration
{
    public static IServiceCollection AddLabAssistantViewModels(this IServiceCollection services)
    {
        //services.AddSingleton<MainViewModel>();
        services.AddSingleton<IErrorFeedService, ErrorFeedService>();
        services.AddSingleton<DeploymentViewModel>();
        services.AddTransient<TemplateEditorViewModel>();
        services.AddTransient<TemplateDetailsViewModel>();
        services.AddTransient<VhdxCatalogPageViewModel>();
        // Add more view models here
        return services;
    }
}
