using System;
using System.IO;
using System.Linq;
using LabAssistant.Data.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Templates;
using Xunit;

namespace LabAssistant.Data.Tests;

public class LabTemplateStoreTests
{
    [Fact]
    public void SaveToFolder_And_LoadFromFile_RoundTripsTemplate()
    {
        var folder = BuildTempRoot();
        var store = new LabTemplateStore();
        var template = new LabTemplate
        {
            Id = "lab-1",
            Name = "Test Lab",
            SchemaVersion = "1.0.0",
            CreatedWithAppVersion = "1.0.0",
            TemplateType = "lab-template",
            TemplateRevision = 1,
            VmTemplates =
            {
                new VmTemplate { Name = "vm1", MemoryMb = 1024, CpuCount = 1, VhdPath = "C:/base.vhdx" }
            }
        };

        var filePath = store.SaveToFolder(folder, template.Name, template);
        var loaded = store.LoadFromFile(filePath);

        Assert.Equal(template.Id, loaded.Id);
        Assert.Equal(template.Name, loaded.Name);
        Assert.Single(loaded.VmTemplates);
        Assert.Equal("vm1", loaded.VmTemplates[0].Name);
    }

    [Fact]
    public void LoadFromFolder_ReportsInvalidJson()
    {
        var folder = BuildTempRoot();
        File.WriteAllText(Path.Combine(folder, "bad.json"), "{ invalid json");

        var store = new LabTemplateStore();
        var result = store.LoadFromFolder(folder, Array.Empty<VhdxCatalogItem>());

        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void LoadFromFolder_LoadsValidTemplates()
    {
        var folder = BuildTempRoot();
        var store = new LabTemplateStore();
        var template = new LabTemplate
        {
            Id = "lab-1",
            Name = "Test Lab",
            SchemaVersion = "1.0.0",
            CreatedWithAppVersion = "1.0.0",
            TemplateType = "lab-template",
            TemplateRevision = 1,
            VmTemplates = { new VmTemplate { Name = "vm1", MemoryMb = 1024, CpuCount = 1, VhdPath = "C:/base.vhdx" } }
        };

        var filePath = store.SaveToFolder(folder, template.Name, template);
        Assert.True(File.Exists(filePath));

        var result = store.LoadFromFolder(folder, Array.Empty<VhdxCatalogItem>());

        Assert.Empty(result.Errors);
        Assert.Single(result.Templates);
        Assert.Equal("Test Lab", result.Templates.Single().Name);
    }

    private static string BuildTempRoot()
    {
        var folder = Path.Combine(Path.GetTempPath(), "LabAssistantTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }
}
