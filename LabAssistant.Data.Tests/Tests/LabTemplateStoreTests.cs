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

    [Fact]
    public void LoadFromFile_LegacyV0Template_MapsToCanonicalWithWarning()
    {
        var folder = BuildTempRoot();
        var filePath = Path.Combine(folder, "legacy.json");
        File.WriteAllText(filePath, """
                                {
                                  "id": "legacy-lab",
                                  "name": "Legacy Lab",
                                  "version": "v0",
                                  "vmTemplates": [
                                    {
                                      "name": "vm1",
                                      "memoryMb": 1024,
                                      "cpuCount": 1,
                                      "vhdPath": "C:/base.vhdx",
                                      "switchName": "Default Switch"
                                    }
                                  ]
                                }
                                """);

        var store = new LabTemplateStore();
        var loaded = store.LoadFromFile(filePath);

        Assert.Equal(LabTemplate.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Equal(LabTemplate.SupportedTemplateType, loaded.TemplateType);
        Assert.NotEmpty(loaded.VmTemplates[0].VmId);
        Assert.Contains(store.LastLoadWarnings, warning => warning.Contains("migrated", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SaveToFile_AfterLegacyLoad_WritesCanonicalFieldsOnly()
    {
        var folder = BuildTempRoot();
        var legacyPath = Path.Combine(folder, "legacy.json");
        var canonicalPath = Path.Combine(folder, "canonical.json");
        File.WriteAllText(legacyPath, """
                                  {
                                    "id": "legacy-lab",
                                    "name": "Legacy Lab",
                                    "version": "v0",
                                    "vmTemplates": [
                                      {
                                        "name": "vm1",
                                        "memoryMb": 1024,
                                        "cpuCount": 1,
                                        "vhdPath": "C:/base.vhdx",
                                        "switchName": "Default Switch"
                                      }
                                    ]
                                  }
                                  """);

        var store = new LabTemplateStore();
        var template = store.LoadFromFile(legacyPath);
        store.SaveToFile(canonicalPath, template);

        var json = File.ReadAllText(canonicalPath);
        Assert.Contains("\"schemaVersion\"", json);
        Assert.Contains("\"templateRevision\"", json);
        Assert.Contains("\"createdWithAppVersion\"", json);
        Assert.Contains("\"templateType\"", json);
        Assert.Contains("\"vmId\"", json);
        Assert.DoesNotContain("\"version\"", json);
    }

    [Fact]
    public void LoadFromFile_UnsupportedMajorSchema_ThrowsActionableError()
    {
        var folder = BuildTempRoot();
        var filePath = Path.Combine(folder, "unsupported-major.json");
        File.WriteAllText(filePath, """
                                {
                                  "id": "lab-major2",
                                  "name": "Lab Major 2",
                                  "schemaVersion": "2.0.0",
                                  "templateRevision": 1,
                                  "createdWithAppVersion": "1.0.0",
                                  "templateType": "lab-template",
                                  "vmTemplates": [
                                    {
                                      "vmId": "vm-1",
                                      "name": "vm1",
                                      "memoryMb": 1024,
                                      "cpuCount": 1,
                                      "vhdPath": "C:/base.vhdx",
                                      "switchName": "Default Switch"
                                    }
                                  ]
                                }
                                """);

        var store = new LabTemplateStore();
        var ex = Assert.Throws<InvalidOperationException>(() => store.LoadFromFile(filePath));
        Assert.Contains("Supported major version", ex.Message);
    }

    [Fact]
    public void LoadFromFolder_NewerMinorSchema_WarnsAndContinues()
    {
        var folder = BuildTempRoot();
        var filePath = Path.Combine(folder, "minor-newer.json");
        File.WriteAllText(filePath, """
                                {
                                  "id": "lab-minor",
                                  "name": "Lab Minor",
                                  "schemaVersion": "1.1.0",
                                  "templateRevision": 2,
                                  "createdWithAppVersion": "1.0.0",
                                  "templateType": "lab-template",
                                  "vmTemplates": [
                                    {
                                      "vmId": "vm-1",
                                      "name": "vm1",
                                      "memoryMb": 1024,
                                      "cpuCount": 1,
                                      "vhdPath": "C:/base.vhdx",
                                      "switchName": "Default Switch"
                                    }
                                  ]
                                }
                                """);

        var store = new LabTemplateStore();
        var result = store.LoadFromFolder(folder, Array.Empty<VhdxCatalogItem>());

        Assert.Empty(result.Errors);
        Assert.Single(result.Templates);
        Assert.Contains(result.Warnings, warning => warning.Contains("newer than supported", StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildTempRoot()
    {
        var folder = Path.Combine(Path.GetTempPath(), "LabAssistantTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }
}
