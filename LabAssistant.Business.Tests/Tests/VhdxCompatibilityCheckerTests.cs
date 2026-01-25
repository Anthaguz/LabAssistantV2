using LabAssistant.Business.Compatibility;
using LabAssistant.Models.Catalog;
using Xunit;

namespace LabAssistant.Business.Tests.Tests;

public class VhdxCompatibilityCheckerTests
{
    [Fact]
    public void Check_ReturnsNoWarnings_WhenOsMatches()
    {
        var required = new VhdxCatalogItem
        {
            Id = "req",
            OsName = "Windows Server",
            OsVersion = "2022",
            Path = "C:/req.vhdx",
            Generation = 2
        };
        var selected = new VhdxCatalogItem
        {
            Id = "sel",
            OsName = "windows server",
            OsVersion = "2022",
            Path = "C:/sel.vhdx",
            Generation = 2
        };

        var warnings = VhdxCompatibilityChecker.Check(required, selected, "vm1");

        Assert.Empty(warnings);
    }

    [Fact]
    public void Check_ReturnsWarning_WhenOsDiffers()
    {
        var required = new VhdxCatalogItem
        {
            Id = "req",
            OsName = "Windows Server",
            OsVersion = "2022",
            Path = "C:/req.vhdx",
            Generation = 2
        };
        var selected = new VhdxCatalogItem
        {
            Id = "sel",
            OsName = "Ubuntu",
            OsVersion = "22.04",
            Path = "C:/sel.vhdx",
            Generation = 2
        };

        var warnings = VhdxCompatibilityChecker.Check(required, selected, "vm1");

        Assert.Single(warnings);
        Assert.Contains("vm1", warnings[0]);
    }
}
