using LabAssistant.Business.Assets;
using LabAssistant.Business.Catalog;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Templates;
using LabAssistant.Models.Validation;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;
using Xunit;

namespace LabAssistant.Business.Tests;

public sealed class AssetsBaseDisksCapabilityServiceTests
{
    [Fact]
    public async Task LoadAsync_ReturnsCatalogItems_AndOperationId()
    {
        var catalogStore = new RecordingCatalogStore(
        [
            new VhdxCatalogItem
            {
                Id = "disk-1",
                Path = @"C:\base\disk-1.vhdx",
                OsName = "Windows Server",
                OsVersion = "2022",
                Generation = 2
            }
        ]);
        var settingsStore = new FakeSettingsStore();
        var service = CreateService(catalogStore, settingsStore);

        var result = await service.LoadAsync();

        Assert.Single(result.Items);
        Assert.Equal("disk-1", result.Items[0].Id);
        Assert.False(string.IsNullOrWhiteSpace(result.OperationId));
    }

    [Fact]
    public async Task SaveAsync_BlocksInvalidMetadata()
    {
        var service = CreateService(new RecordingCatalogStore(), new FakeSettingsStore());

        var result = await service.SaveAsync(new AssetsBaseDiskDraft
        {
            IsNew = true,
            Path = @"C:\base\disk-1.vhdx",
            OsName = "Windows Server",
            OsVersion = string.Empty,
            Generation = 2
        });

        Assert.False(result.Success);
        Assert.Contains("OS version is required", result.UserMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AssessRemoveAsync_ReportsKnownTemplateReferences_AsWarnings()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "LabAssistantTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var settingsStore = new FakeSettingsStore
            {
                Settings = new AppSettings
                {
                    CatalogPath = Path.Combine(tempRoot, "catalog.json"),
                    TemplateFolder = tempRoot
                }
            };
            var catalogStore = new RecordingCatalogStore(
            [
                new VhdxCatalogItem
                {
                    Id = "disk-1",
                    Path = @"C:\base\disk-1.vhdx",
                    OsName = "Windows Server",
                    OsVersion = "2022",
                    Generation = 2,
                    Signature = "sig-1"
                }
            ]);
            var templateStore = new FakeTemplateStore(
                CreateTemplate("Template One", "vm01", vhdxId: "disk-1"));
            var service = CreateService(catalogStore, settingsStore, templateStore: templateStore);

            var result = await service.AssessRemoveAsync("disk-1");

            Assert.True(result.Exists);
            Assert.True(result.CanRemove);
            Assert.NotEmpty(result.WarningReasons);
            Assert.Contains("Active runtime consumer detection is not currently implemented.", result.ReferenceSignalSummary);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RemoveAsync_UpdatesCatalog_WithoutDeletingUnderlyingFile()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "LabAssistantTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var diskPath = Path.Combine(tempRoot, "disk-1.vhdx");
            await File.WriteAllTextAsync(diskPath, "placeholder");
            var settingsStore = new FakeSettingsStore
            {
                Settings = new AppSettings
                {
                    CatalogPath = Path.Combine(tempRoot, "catalog.json"),
                    TemplateFolder = tempRoot
                }
            };
            var catalogStore = new RecordingCatalogStore(
            [
                new VhdxCatalogItem
                {
                    Id = "disk-1",
                    Path = diskPath,
                    OsName = "Windows Server",
                    OsVersion = "2022",
                    Generation = 2
                }
            ]);
            var service = CreateService(catalogStore, settingsStore);

            var result = await service.RemoveAsync("disk-1");

            Assert.True(result.Success);
            Assert.Empty(catalogStore.SavedItems);
            Assert.True(File.Exists(diskPath));
            Assert.Contains("removed from the registry", result.UserMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static IAssetsBaseDisksCapabilityService CreateService(
        RecordingCatalogStore catalogStore,
        FakeSettingsStore settingsStore,
        FakeTemplateStore? templateStore = null,
        FakeVhdxIntegrityValidator? validator = null,
        RecordingStructuredLogger? logger = null)
    {
        var catalogService = new CatalogService(catalogStore, settingsStore, logger ?? new RecordingStructuredLogger(), validator ?? new FakeVhdxIntegrityValidator());
        return new AssetsBaseDisksCapabilityService(
            catalogService,
            settingsStore,
            templateStore ?? new FakeTemplateStore(),
            validator ?? new FakeVhdxIntegrityValidator(),
            logger ?? new RecordingStructuredLogger());
    }

    private static LabTemplate CreateTemplate(string templateName, string vmName, string? vhdxId = null, string? vhdPath = null, string? vhdxSignature = null)
    {
        return new LabTemplate
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = templateName,
            VmTemplates =
            [
                new VmTemplate
                {
                    VmId = Guid.NewGuid().ToString("N"),
                    Name = vmName,
                    VhdxId = vhdxId,
                    VhdPath = vhdPath,
                    VhdxSignature = vhdxSignature,
                    MemoryMb = 2048,
                    CpuCount = 2
                }
            ]
        };
    }

    private sealed class RecordingCatalogStore : IVhdxCatalogStore
    {
        private readonly List<VhdxCatalogItem> _items;

        public RecordingCatalogStore(IEnumerable<VhdxCatalogItem>? items = null)
        {
            _items = items?.Select(Clone).ToList() ?? [];
        }

        public IReadOnlyList<VhdxCatalogItem> SavedItems { get; private set; } = Array.Empty<VhdxCatalogItem>();

        public VhdxCatalogLoadResult Load(string catalogPath)
        {
            var result = new VhdxCatalogLoadResult();
            result.Items.AddRange(_items.Select(Clone));
            return result;
        }

        public VhdxCatalogSaveResult Save(string catalogPath, IEnumerable<VhdxCatalogItem> items)
        {
            SavedItems = items.Select(Clone).ToList();
            _items.Clear();
            _items.AddRange(SavedItems.Select(Clone));
            return new VhdxCatalogSaveResult();
        }

        public void EnsureCatalogFileExists(string catalogPath)
        {
        }

        private static VhdxCatalogItem Clone(VhdxCatalogItem source)
        {
            return new VhdxCatalogItem
            {
                Id = source.Id,
                Path = source.Path,
                OsName = source.OsName,
                OsVersion = source.OsVersion,
                Generation = source.Generation,
                SizeBytes = source.SizeBytes,
                Signature = source.Signature,
                Notes = source.Notes
            };
        }
    }

    private sealed class FakeSettingsStore : IAppSettingsStore
    {
        public AppSettings Settings { get; set; } = new()
        {
            CatalogPath = Path.Combine(Path.GetTempPath(), "catalog.json"),
            TemplateFolder = Path.GetTempPath()
        };

        public string SettingsPath => Path.Combine(Path.GetTempPath(), "settings.json");
        public void LoadOrCreate() { }
        public void Reload() { }
        public void Save() { }
        public void ResetToDefault() { }
        public void SetTemplateFolder(string path) => Settings.TemplateFolder = path;
        public void SetLogFolder(string path) => Settings.LogFolder = path;
        public void SetVmBasePath(string path) => Settings.VmBasePath = path;
        public void SetDifferencingDiskBasePath(string path) => Settings.DifferencingDiskBasePath = path;
    }

    private sealed class FakeTemplateStore(params LabTemplate[] templates) : ILabTemplateStore
    {
        private readonly IReadOnlyList<LabTemplate> _templates = templates;

        public IReadOnlyList<string> LastLoadWarnings => Array.Empty<string>();

        public LabTemplateLoadResult LoadFromFolder(string templatesFolder, IEnumerable<VhdxCatalogItem> catalogItems)
        {
            var result = new LabTemplateLoadResult();
            result.Templates.AddRange(_templates);
            return result;
        }

        public LabTemplate LoadFromFile(string filePath) => throw new NotSupportedException();
        public void SaveToFile(string filePath, LabTemplate template) => throw new NotSupportedException();
        public string SaveToFolder(string folderPath, string templateName, LabTemplate template) => throw new NotSupportedException();
    }

    private sealed class FakeVhdxIntegrityValidator : IVhdxIntegrityValidator
    {
        public Task<VhdxIntegrityValidationResult> ValidateAsync(string path, VhdxIntegrityValidationDepth depth = VhdxIntegrityValidationDepth.Full, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new VhdxIntegrityValidationResult
            {
                Status = VhdxIntegrityStatus.Valid,
                Path = path,
                Message = "Base disk is ready to use."
            });
        }
    }

    private sealed class RecordingStructuredLogger : IStructuredLogger
    {
        public List<StructuredLogEvent> Events { get; } = [];

        public void Log(StructuredLogEvent logEvent) => Events.Add(logEvent);

        public void Log(StructuredLogLevel level, string eventName, string operationId, string? result = null, IReadOnlyDictionary<string, object?>? context = null)
        {
            Events.Add(StructuredLogEvent.Create(level, eventName, operationId, result, context));
        }
    }
}
