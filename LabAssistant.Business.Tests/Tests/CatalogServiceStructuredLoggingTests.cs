using LabAssistant.Business.Catalog;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.Logging;
using Xunit;

namespace LabAssistant.Business.Tests;

public class CatalogServiceStructuredLoggingTests
{
    private static readonly HashSet<string> CanonicalCatalogCodes =
    [
        $"0x{LaStatus.AssetsCatalog_CatalogLoaded:X8}",
        $"0x{LaStatus.AssetsCatalog_CatalogLoadedWithErrors:X8}",
        $"0x{LaStatus.AssetsCatalog_CatalogSaved:X8}",
        $"0x{LaStatus.AssetsCatalog_CatalogSavedWithErrors:X8}"
    ];

    [Fact]
    public void LoadCatalog_EmitsCatalogLoaded_WithContext()
    {
        var store = new FakeCatalogStore();
        store.LoadResult.Items.Add(new VhdxCatalogItem { Id = "disk-1", Path = @"C:\base\disk1.vhdx" });
        var logger = new RecordingStructuredLogger();
        var service = new CatalogService(store, new FakeSettingsStore(), logger);

        var result = service.LoadCatalog();

        Assert.Single(result.Items);
        var logEvent = Assert.Single(logger.Events);
        Assert.Equal($"0x{LaStatus.AssetsCatalog_CatalogLoaded:X8}", logEvent.Code);
        Assert.Equal("success", logEvent.Result);
        Assert.False(string.IsNullOrWhiteSpace(logEvent.OperationId));
        Assert.Equal("info", logEvent.Level);
        Assert.False(string.IsNullOrWhiteSpace(logEvent.Ts));
        Assert.True(DateTimeOffset.TryParse(logEvent.Ts, out var parsedTs));
        Assert.Equal(TimeSpan.Zero, parsedTs.Offset);
        Assert.Contains(logEvent.Code, CanonicalCatalogCodes);
        Assert.Equal(1, GetInt(logEvent, "itemCount"));
        Assert.Equal(0, GetInt(logEvent, "errorCount"));
        Assert.Equal(@"C:\catalog\vhdx-catalog.json", GetString(logEvent, "resourcePath"));
        AssertContextKeysAreNonSensitive(logEvent);
    }

    [Fact]
    public void SaveCatalog_WhenValidationErrors_EmitsCatalogSavedFailed()
    {
        var store = new FakeCatalogStore();
        store.SaveResult.Errors.Add("duplicate id");
        var logger = new RecordingStructuredLogger();
        var service = new CatalogService(store, new FakeSettingsStore(), logger);

        var result = service.SaveCatalog(new[] { new VhdxCatalogItem { Id = "disk-1", Path = @"C:\base\disk1.vhdx" } });

        Assert.False(result.IsValid);
        var logEvent = Assert.Single(logger.Events);
        Assert.Equal($"0x{LaStatus.AssetsCatalog_CatalogSavedWithErrors:X8}", logEvent.Code);
        Assert.Equal("failed", logEvent.Result);
        Assert.Equal("warn", logEvent.Level);
        Assert.False(string.IsNullOrWhiteSpace(logEvent.OperationId));
        Assert.False(string.IsNullOrWhiteSpace(logEvent.Ts));
        Assert.Contains(logEvent.Code, CanonicalCatalogCodes);
        Assert.Equal(1, GetInt(logEvent, "itemCount"));
        Assert.Equal(1, GetInt(logEvent, "errorCount"));
        AssertContextKeysAreNonSensitive(logEvent);
    }

    private static void AssertContextKeysAreNonSensitive(StructuredLogEvent logEvent)
    {
        if (logEvent.Context == null)
        {
            return;
        }

        foreach (var key in logEvent.Context.Keys)
        {
            Assert.DoesNotContain("password", key, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", key, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", key, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string? GetString(StructuredLogEvent logEvent, string key)
        => logEvent.Context != null && logEvent.Context.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static int GetInt(StructuredLogEvent logEvent, string key)
        => logEvent.Context != null && logEvent.Context.TryGetValue(key, out var value) && value is int number ? number : -1;

    private sealed class FakeCatalogStore : IVhdxCatalogStore
    {
        public VhdxCatalogLoadResult LoadResult { get; } = new();
        public VhdxCatalogSaveResult SaveResult { get; } = new();

        public VhdxCatalogLoadResult Load(string catalogPath) => LoadResult;
        public VhdxCatalogSaveResult Save(string catalogPath, IEnumerable<VhdxCatalogItem> items) => SaveResult;
        public void EnsureCatalogFileExists(string catalogPath) { }
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

    private sealed class RecordingStructuredLogger : IStructuredLogger
    {
        public List<StructuredLogEvent> Events { get; } = new();

        public void Log(StructuredLogEvent logEvent) => Events.Add(logEvent);

        public void Log(StructuredLogLevel level, string eventName, string operationId, string? result = null, IReadOnlyDictionary<string, object?>? context = null)
        {
            Events.Add(StructuredLogEvent.Create(level, eventName, operationId, result, context));
        }
    }
}
