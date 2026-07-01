using LabAssistant.Business;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Templates;
using LabAssistant.Services.PowerShell;
using Microsoft.Extensions.DependencyInjection;
using InfrastructureServices = LabAssistant.Services.ServiceCollectionExtensions;
using Xunit;

namespace LabAssistant.UI.Tests;

public class DependencyInjectionWiringTests
{
    [Fact]
    public void ServiceProvider_BuildsAndResolvesCoreServices()
    {
        var services = new ServiceCollection();
        InfrastructureServices.AddInfrastructureServices(services);
        services.AddBusinessServices();
        services.AddPersistenceServices();

        var sessionPoolDescriptor = Assert.Single(services, service => service.ServiceType == typeof(IPowerShellSessionPool));
        Assert.Equal(ServiceLifetime.Singleton, sessionPoolDescriptor.Lifetime);

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IAppSettingsStore>());
        Assert.NotNull(provider.GetRequiredService<IVhdxCatalogStore>());
        Assert.NotNull(provider.GetRequiredService<ILabTemplateStore>());

        var sessionPoolOptions = provider.GetRequiredService<SessionPoolOptions>();
        Assert.Equal(2, sessionPoolOptions.WarmupCount);
        Assert.Equal(4, sessionPoolOptions.MaxPoolSize);
        Assert.Equal(TimeSpan.FromMinutes(5), sessionPoolOptions.IdleRecycleTimeout);
    }
}
