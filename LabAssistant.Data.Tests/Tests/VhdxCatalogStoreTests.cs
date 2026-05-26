using System;
using System.Collections.Generic;
using System.IO;
using LabAssistant.Data.Catalog;
using LabAssistant.Models.Catalog;
using Xunit;

namespace LabAssistant.Data.Tests;

public class VhdxCatalogStoreTests
{
    [Fact]
    public void Load_CreatesEmptyCatalog_WhenMissing()
    {
        var store = new VhdxCatalogStore();
        var path = BuildTempPath();

        var result = store.Load(path);

        Assert.True(result.IsValid);
        Assert.Empty(result.Items);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void SaveAndLoad_RoundTripsItems()
    {
        var store = new VhdxCatalogStore();
        var path = BuildTempPath();
        var items = new List<VhdxCatalogItem>
        {
            new()
            {
                Id = "win-2022",
                Path = "C:/Lab/Win2022.vhdx",
                OsName = "Windows Server",
                OsVersion = "2022",
                Generation = 2,
                BootstrapProfile = new VhdxBootstrapProfile
                {
                    ExpectedLocalUser = "Administrator",
                    LocalCredentialSlotRef = "disk.win-2022.local-admin",
                    GuestOsFamily = "WindowsServer",
                    GuestTransport = "powershell-direct",
                    Notes = "Prepared for PowerShell Direct"
                }
            }
        };

        var saveResult = store.Save(path, items);
        var loadResult = store.Load(path);

        Assert.True(saveResult.IsValid);
        Assert.True(loadResult.IsValid);
        Assert.Single(loadResult.Items);
        Assert.Equal("win-2022", loadResult.Items[0].Id);
        Assert.NotNull(loadResult.Items[0].BootstrapProfile);
        Assert.Equal("disk.win-2022.local-admin", loadResult.Items[0].BootstrapProfile?.LocalCredentialSlotRef);
        Assert.Equal("powershell-direct", loadResult.Items[0].BootstrapProfile?.GuestTransport);
    }

    [Fact]
    public void Load_ReturnsError_WhenJsonIsInvalid()
    {
        var store = new VhdxCatalogStore();
        var path = BuildTempPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ invalid json");

        var result = store.Load(path);

        Assert.NotEmpty(result.Errors);
    }

    private static string BuildTempPath()
    {
        var folder = Path.Combine(Path.GetTempPath(), "LabAssistantTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "vhdx-catalog.json");
    }
}
