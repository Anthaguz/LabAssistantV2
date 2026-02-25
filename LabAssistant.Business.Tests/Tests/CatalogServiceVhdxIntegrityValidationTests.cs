using LabAssistant.Business.Catalog;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Validation;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;
using Xunit;

namespace LabAssistant.Business.Tests;

public class CatalogServiceVhdxIntegrityValidationTests
{
    [Fact]
    public void SaveCatalog_WhenVhdxInvalid_BlocksSaveAndReturnsValidationError()
    {
        var store = new RecordingCatalogStore();
        var validator = new FakeVhdxIntegrityValidator
        {
            Result = new VhdxIntegrityValidationResult
            {
                Status = VhdxIntegrityStatus.Invalid,
                Path = @"C:\base\fake.vhdx",
                Message = "Invalid"
            }
        };
        var service = new CatalogService(store, new FakeSettingsStore(), new NullStructuredLoggerForTests(), validator);

        var result = service.SaveCatalog(
        [
            new VhdxCatalogItem
            {
                Id = "disk-1",
                Path = @"C:\base\fake.vhdx",
                OsName = "Windows",
                OsVersion = "11",
                Generation = 2
            }
        ]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("not a valid Hyper-V VHDX", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, store.SaveCalls);
    }

    [Fact]
    public void SaveCatalog_WhenVhdxMissing_BlocksSaveAndIncludesPath()
    {
        var store = new RecordingCatalogStore();
        var validator = new FakeVhdxIntegrityValidator
        {
            Result = new VhdxIntegrityValidationResult
            {
                Status = VhdxIntegrityStatus.Missing,
                Path = @"C:\base\missing.vhdx",
                Message = "Missing"
            }
        };
        var service = new CatalogService(store, new FakeSettingsStore(), new NullStructuredLoggerForTests(), validator);

        var result = service.SaveCatalog(
        [
            new VhdxCatalogItem
            {
                Id = "disk-1",
                Path = @"C:\base\missing.vhdx",
                OsName = "Windows",
                OsVersion = "11",
                Generation = 2
            }
        ]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains(@"C:\base\missing.vhdx", StringComparison.Ordinal));
        Assert.Equal(0, store.SaveCalls);
    }

    [Fact]
    public void SaveCatalog_WhenVhdxValid_DelegatesToStore()
    {
        var store = new RecordingCatalogStore();
        var validator = new FakeVhdxIntegrityValidator
        {
            Result = new VhdxIntegrityValidationResult
            {
                Status = VhdxIntegrityStatus.Valid,
                Path = @"C:\base\good.vhdx",
                Message = "Valid"
            }
        };
        var service = new CatalogService(store, new FakeSettingsStore(), new NullStructuredLoggerForTests(), validator);

        var result = service.SaveCatalog(
        [
            new VhdxCatalogItem
            {
                Id = "disk-1",
                Path = @"C:\base\good.vhdx",
                OsName = "Windows",
                OsVersion = "11",
                Generation = 2
            }
        ]);

        Assert.True(result.IsValid);
        Assert.Equal(1, store.SaveCalls);
    }

    private sealed class RecordingCatalogStore : IVhdxCatalogStore
    {
        public int SaveCalls { get; private set; }

        public VhdxCatalogLoadResult Load(string catalogPath) => new();

        public VhdxCatalogSaveResult Save(string catalogPath, IEnumerable<VhdxCatalogItem> items)
        {
            SaveCalls++;
            return new VhdxCatalogSaveResult();
        }

        public void EnsureCatalogFileExists(string catalogPath)
        {
        }
    }

    private sealed class FakeSettingsStore : IAppSettingsStore
    {
        public AppSettings Settings { get; set; } = new() { CatalogPath = @"C:\catalog\vhdx-catalog.json" };
        public string SettingsPath => string.Empty;
        public void LoadOrCreate() { }
        public void Reload() { }
        public void Save() { }
        public void ResetToDefault() { }
        public void SetTemplateFolder(string path) => Settings.TemplateFolder = path;
        public void SetLogFolder(string path) => Settings.LogFolder = path;
        public void SetVmBasePath(string path) => Settings.VmBasePath = path;
        public void SetDifferencingDiskBasePath(string path) => Settings.DifferencingDiskBasePath = path;
    }

    private sealed class FakeVhdxIntegrityValidator : IVhdxIntegrityValidator
    {
        public VhdxIntegrityValidationResult Result { get; set; } = new() { Status = VhdxIntegrityStatus.Valid, Message = "Valid" };

        public Task<VhdxIntegrityValidationResult> ValidateAsync(string path, VhdxIntegrityValidationDepth depth = VhdxIntegrityValidationDepth.Full, CancellationToken cancellationToken = default)
            => Task.FromResult(WithPath(path));

        private VhdxIntegrityValidationResult WithPath(string path)
            => new()
            {
                Status = Result.Status,
                Path = string.IsNullOrWhiteSpace(Result.Path) ? path : Result.Path,
                Message = Result.Message,
                Detail = Result.Detail
            };
    }

    private sealed class NullStructuredLoggerForTests : IStructuredLogger
    {
        public void Log(StructuredLogEvent logEvent) { }

        public void Log(StructuredLogLevel level, string eventName, string operationId, string? result = null, IReadOnlyDictionary<string, object?>? context = null) { }
    }
}
