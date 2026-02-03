using System;
using System.IO;
using LabAssistant.Data.Configuration;
using Xunit;

namespace LabAssistant.Data.Tests;

public class AppPathsTests
{
    [Fact]
    public void Constructor_UsesAppRootToBuildPaths()
    {
        var appRoot = Path.Combine(Path.GetTempPath(), "LabAssistantTests", Guid.NewGuid().ToString("N"));
        var paths = new AppPaths(appRoot);

        Assert.Equal(appRoot, paths.AppRoot);
        Assert.Equal(Path.Combine(appRoot, "Config"), paths.ConfigFolder);
        Assert.Equal(Path.Combine(appRoot, "Catalog"), paths.CatalogFolder);
        Assert.Equal(Path.Combine(appRoot, "Templates"), paths.TemplatesFolder);
        Assert.Equal(Path.Combine(appRoot, "Logs"), paths.LogsFolder);
        Assert.Equal(Path.Combine(appRoot, "VMs"), paths.VmBasePath);
        Assert.Equal(Path.Combine(appRoot, "Disks"), paths.DifferencingDiskBasePath);
        Assert.Equal(Path.Combine(appRoot, "Catalog", "vhdx-catalog.json"), paths.CatalogPath);
    }
}
