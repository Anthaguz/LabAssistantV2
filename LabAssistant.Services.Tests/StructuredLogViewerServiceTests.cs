using System.Text.Json;
using LabAssistant.Models.Configuration;
using LabAssistant.Services.Logging;
using Xunit;

namespace LabAssistant.Services.Tests;

public class StructuredLogViewerServiceTests
{
    [Fact]
    public async Task LoadAsync_ParsesEnvelope_AndSkipsMalformedLines()
    {
        var root = CreateTempRoot();
        try
        {
            var service = CreateService(root, logFolderOverride: null);
            var filePath = service.GetStructuredLogFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            await File.WriteAllLinesAsync(filePath, new[]
            {
                """{"ts":"2026-03-01T10:00:00Z","level":"info","event":"StepStarted","operationId":"op-1","result":"started","context":{"stepKey":"CreateVm","attempt":1}}""",
                "{not-json}",
                """{"ts":"2026-03-01T10:00:05Z","level":"error","event":"StepFailed","operationId":"op-1","result":"failed","context":{"errorCode":"0x80070002"}}"""
            });

            var result = await service.LoadAsync(new StructuredLogViewerFilter());

            Assert.Equal(3, result.TotalLineCount);
            Assert.Equal(1, result.ParseErrorCount);
            Assert.Equal(2, result.Entries.Count);

            var latest = result.Entries[0];
            Assert.Equal("error", latest.Level);
            Assert.Equal("StepFailed", latest.Event);
            Assert.Equal("op-1", latest.OperationId);
            Assert.Equal("failed", latest.Result);

            using var contextDoc = JsonDocument.Parse(latest.ContextJson);
            Assert.Equal("0x80070002", contextDoc.RootElement.GetProperty("errorCode").GetString());
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task LoadAsync_AppliesOperationLevelEventTextAndTimeFilters()
    {
        var root = CreateTempRoot();
        try
        {
            var service = CreateService(root, logFolderOverride: null);
            var filePath = service.GetStructuredLogFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            await File.WriteAllLinesAsync(filePath, new[]
            {
                """{"ts":"2026-03-01T09:00:00Z","level":"info","event":"DeployStarted","operationId":"op-a","result":"started","context":{"vmName":"vm-a"}}""",
                """{"ts":"2026-03-01T09:05:00Z","level":"warn","event":"StepSkipped","operationId":"op-b","result":"skipped","context":{"skipReason":"not_selected","stepKey":"software"}}""",
                """{"ts":"2026-03-01T09:10:00Z","level":"error","event":"StepFailed","operationId":"op-b","result":"failed","context":{"errorMessage":"network timeout"}}"""
            });

            var filter = new StructuredLogViewerFilter
            {
                OperationId = "op-b",
                Level = "error",
                Event = "Step",
                TextSearch = "timeout",
                StartUtc = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero),
                EndUtc = new DateTimeOffset(2026, 3, 1, 9, 59, 59, TimeSpan.Zero)
            };

            var result = await service.LoadAsync(filter);

            Assert.Single(result.Entries);
            Assert.Equal("StepFailed", result.Entries[0].Event);
            Assert.Equal("op-b", result.Entries[0].OperationId);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void GetStructuredLogFilePath_UsesSettingsLogFolder_WhenProvided()
    {
        var root = CreateTempRoot();
        try
        {
            var customLogsFolder = Path.Combine(root, "CustomLogs");
            var service = CreateService(root, logFolderOverride: customLogsFolder);

            var path = service.GetStructuredLogFilePath();

            Assert.Equal(
                Path.Combine(customLogsFolder, StructuredLoggingDefaults.StructuredEventsFileName),
                path);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    private static StructuredLogViewerService CreateService(string root, string? logFolderOverride)
    {
        var appPaths = new TestAppPaths(root);
        var settingsStore = new TestAppSettingsStore
        {
            Settings = new AppSettings
            {
                LogFolder = logFolderOverride ?? string.Empty
            }
        };

        return new StructuredLogViewerService(settingsStore, appPaths);
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"labassistant-logviewer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void CleanupTempRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class TestAppSettingsStore : IAppSettingsStore
    {
        public AppSettings Settings { get; set; } = new();

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

    private sealed class TestAppPaths(string root) : IAppPaths
    {
        public string AppRoot => root;
        public string ConfigFolder => Path.Combine(root, "Config");
        public string CatalogFolder => Path.Combine(root, "Catalog");
        public string TemplatesFolder => Path.Combine(root, "Templates");
        public string LogsFolder => Path.Combine(root, "Logs");
        public string VmBasePath => Path.Combine(root, "VMs");
        public string DifferencingDiskBasePath => Path.Combine(root, "Diff");
        public string CatalogPath => Path.Combine(root, "Catalog", "catalog.json");
    }
}
