using LabAssistant.Business;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Templates;
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

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IAppSettingsStore>());
        Assert.NotNull(provider.GetRequiredService<IVhdxCatalogStore>());
        Assert.NotNull(provider.GetRequiredService<ILabTemplateStore>());
    }
}
