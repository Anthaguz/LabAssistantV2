using LabAssistant.Business.Catalog;
using LabAssistant.Business.Templates;
using LabAssistant.Data.Templates;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Templates;
using LabAssistant.Services.Logging;
using Xunit;

namespace LabAssistant.Business.Tests.Tests;

public sealed class TemplatesCapabilityServiceTests
{
    [Fact]
    public async Task LoadLibraryAsync_ReturnsTemplates_AndAppliesSearchFilter()
    {
        var tempRoot = CreateTempFolder();
        try
        {
            var templateStore = new LabTemplateStore();
            templateStore.SaveToFile(Path.Combine(tempRoot, "alpha.json"), CreateTemplate("alpha-template", "Alpha"));
            templateStore.SaveToFile(Path.Combine(tempRoot, "beta.json"), CreateTemplate("beta-template", "Beta"));

            var service = CreateService(tempRoot, templateStore);

            var all = await service.LoadLibraryAsync();
            var filtered = await service.LoadLibraryAsync("beta");

            Assert.Equal(2, all.Items.Count);
            Assert.Single(filtered.Items);
            Assert.Equal("beta-template", filtered.Items[0].TemplateId);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_PersistsTemplate_AndReturnsPath()
    {
        var tempRoot = CreateTempFolder();
        try
        {
            var templateStore = new LabTemplateStore();
            var service = CreateService(tempRoot, templateStore);
            var document = await service.CreateDraftAsync();
            document.Template.Name = "Saved Template";
            document.Template.VmTemplates =
            [
                new VmTemplate
                {
                    VmId = Guid.NewGuid().ToString("N"),
                    Name = "vm01",
                    MemoryMb = 2048,
                    CpuCount = 2,
                    VhdPath = @"C:\base\vm01.vhdx"
                }
            ];

            var result = await service.SaveAsync(document);

            Assert.True(result.Success);
            Assert.False(string.IsNullOrWhiteSpace(result.FilePath));
            Assert.True(File.Exists(result.FilePath));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DeleteAsync_RemovesTemplateFile()
    {
        var tempRoot = CreateTempFolder();
        try
        {
            var templateStore = new LabTemplateStore();
            var filePath = Path.Combine(tempRoot, "delete-me.json");
            templateStore.SaveToFile(filePath, CreateTemplate("delete-template", "Delete Me"));

            var service = CreateService(tempRoot, templateStore);
            var result = await service.DeleteAsync(filePath);

            Assert.True(result.Success);
            Assert.False(File.Exists(filePath));
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static ITemplatesCapabilityService CreateService(string templateFolder, ILabTemplateStore templateStore)
    {
        var settingsStore = new FakeAppSettingsStore(templateFolder);
        var catalogStore = new FakeCatalogStore();
        var catalogService = new CatalogService(catalogStore, settingsStore, NullStructuredLogger.Instance);
        var validationService = new TemplateValidationService(catalogService);
        return new TemplatesCapabilityService(
            settingsStore,
            catalogStore,
            templateStore,
            validationService,
            NullStructuredLogger.Instance);
    }

    private static LabTemplate CreateTemplate(string id, string name)
    {
        return new LabTemplate
        {
            Id = id,
            Name = name,
            VmTemplates =
            [
                new VmTemplate
                {
                    VmId = Guid.NewGuid().ToString("N"),
                    Name = "vm01",
                    MemoryMb = 2048,
                    CpuCount = 2,
                    VhdPath = @"C:\base\vm01.vhdx"
                }
            ]
        };
    }

    private static string CreateTempFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "LabAssistantTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private sealed class FakeCatalogStore : IVhdxCatalogStore
    {
        public VhdxCatalogLoadResult Load(string catalogPath)
        {
            return new VhdxCatalogLoadResult();
        }

        public VhdxCatalogSaveResult Save(string catalogPath, IEnumerable<VhdxCatalogItem> items)
        {
            return new VhdxCatalogSaveResult();
        }

        public void EnsureCatalogFileExists(string catalogPath)
        {
        }
    }

    private sealed class FakeAppSettingsStore : IAppSettingsStore
    {
        public FakeAppSettingsStore(string templateFolder)
        {
            Settings = new AppSettings
            {
                TemplateFolder = templateFolder,
                CatalogPath = Path.Combine(templateFolder, "catalog.json")
            };
        }

        public AppSettings Settings { get; }

        public string SettingsPath => Path.Combine(Settings.TemplateFolder, "settings.json");

        public void LoadOrCreate()
        {
        }

        public void Reload()
        {
        }

        public void Save()
        {
        }

        public void ResetToDefault()
        {
        }

        public void SetTemplateFolder(string path)
        {
            Settings.TemplateFolder = path;
        }

        public void SetLogFolder(string path)
        {
            Settings.LogFolder = path;
        }

        public void SetVmBasePath(string path)
        {
            Settings.VmBasePath = path;
        }

        public void SetDifferencingDiskBasePath(string path)
        {
            Settings.DifferencingDiskBasePath = path;
        }
    }
}
