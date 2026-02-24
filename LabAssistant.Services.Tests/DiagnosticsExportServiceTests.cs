using System.IO.Compression;
using System.Text.Json;
using LabAssistant.Models.Configuration;
using LabAssistant.Services.Diagnostics;
using LabAssistant.Services.Logging;
using Xunit;

namespace LabAssistant.Services.Tests;

public class DiagnosticsExportServiceTests
{
    [Fact]
    public void Export_IncludesExpectedArtifacts_AndFilteredParseableStructuredLogs()
    {
        using var fixture = new DiagnosticsFixture();
        fixture.WriteStructuredLogs(
            """
            {"ts":"2026-02-24T10:00:00Z","level":"info","event":"DeployLabStarted","operationId":"op-1"}
            {"ts":"2026-02-24T10:00:01Z","level":"info","event":"CatalogLoaded","operationId":"op-2"}
            {"ts":"2026-02-24T10:00:02Z","level":"error","event":"DeployLabFailed","operationId":"op-1","result":"failed"}
            """);

        File.WriteAllText(fixture.TemplateFilePath, """{"id":"t-1","name":"Template 1"}""");

        var service = fixture.CreateService();
        var request = new DiagnosticsExportRequest
        {
            DestinationZipPath = fixture.BundlePath,
            Options = new DiagnosticsExportOptions
            {
                IncludeFullTemplateFile = true,
                IncludeDetailedEnvironmentInformation = true
            },
            OperationContext = new DiagnosticsOperationContext
            {
                OperationId = "op-1",
                OperationType = "deploy",
                TerminalState = "Failed",
                Result = "failed",
                TemplateId = "t-1",
                TemplateName = "Template 1",
                VmCount = 2,
                CleanupVmCount = 1,
                ResidualVmCount = 0
            },
            TemplateFilePath = fixture.TemplateFilePath
        };

        var result = service.Export(request);

        Assert.Equal(fixture.BundlePath, result.BundlePath);
        Assert.Contains("bundle/manifest.json", result.IncludedArtifacts);
        Assert.Contains("metadata/runtime-metadata.json", result.IncludedArtifacts);
        Assert.Contains("metadata/operation-context.json", result.IncludedArtifacts);
        Assert.Contains("logs/structured-events.jsonl", result.IncludedArtifacts);
        Assert.Contains("artifacts/template-definition.json", result.IncludedArtifacts);
        Assert.Empty(result.Warnings);

        using var archive = ZipFile.OpenRead(fixture.BundlePath);
        Assert.NotNull(archive.GetEntry("bundle/manifest.json"));
        Assert.NotNull(archive.GetEntry("metadata/runtime-metadata.json"));
        Assert.NotNull(archive.GetEntry("metadata/operation-context.json"));
        Assert.NotNull(archive.GetEntry("logs/structured-events.jsonl"));
        Assert.NotNull(archive.GetEntry("artifacts/template-definition.json"));

        var logLines = ReadZipText(archive, "logs/structured-events.jsonl")
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, logLines.Length);
        foreach (var line in logLines)
        {
            using var parsed = JsonDocument.Parse(line);
            Assert.Equal("op-1", parsed.RootElement.GetProperty("operationId").GetString());
        }
    }

    [Fact]
    public void Export_OptionFlags_ControlOptionalArtifacts()
    {
        using var fixture = new DiagnosticsFixture();
        fixture.WriteStructuredLogs("""{"ts":"2026-02-24T10:00:00Z","level":"info","event":"CatalogLoaded","operationId":"op-c"}""");
        File.WriteAllText(fixture.TemplateFilePath, """{"id":"t-1"}""");
        var service = fixture.CreateService();

        var result = service.Export(new DiagnosticsExportRequest
        {
            DestinationZipPath = fixture.BundlePath,
            Options = new DiagnosticsExportOptions
            {
                IncludeFullTemplateFile = false,
                IncludeDetailedEnvironmentInformation = false
            },
            OperationContext = new DiagnosticsOperationContext { OperationId = "op-c", OperationType = "catalog" },
            TemplateFilePath = fixture.TemplateFilePath
        });

        using var archive = ZipFile.OpenRead(fixture.BundlePath);
        Assert.Null(archive.GetEntry("artifacts/template-definition.json"));
        var runtime = ReadZipJson(archive, "metadata/runtime-metadata.json");
        Assert.False(runtime.RootElement.TryGetProperty("machineName", out _));
        Assert.DoesNotContain("artifacts/template-definition.json", result.IncludedArtifacts);
    }

    [Fact]
    public void Export_WarnsWhenOptionalTemplateRequestedButMissing()
    {
        using var fixture = new DiagnosticsFixture();
        fixture.WriteStructuredLogs("""{"ts":"2026-02-24T10:00:00Z","level":"info","event":"TemplateSaved","operationId":"op-t"}""");
        var service = fixture.CreateService();

        var result = service.Export(new DiagnosticsExportRequest
        {
            DestinationZipPath = fixture.BundlePath,
            Options = new DiagnosticsExportOptions { IncludeFullTemplateFile = true },
            OperationContext = new DiagnosticsOperationContext { OperationId = "op-t", OperationType = "template" },
            TemplateFilePath = Path.Combine(fixture.RootPath, "missing-template.json")
        });

        Assert.Contains(result.Warnings, warning => warning.Contains("Template file not found", StringComparison.Ordinal));
    }

    [Fact]
    public void Export_RuntimeMetadata_DoesNotIncludeEnvironmentVariableDump()
    {
        using var fixture = new DiagnosticsFixture();
        fixture.WriteStructuredLogs("""{"ts":"2026-02-24T10:00:00Z","level":"info","event":"CatalogLoaded","operationId":"op-c"}""");
        var service = fixture.CreateService();

        service.Export(new DiagnosticsExportRequest
        {
            DestinationZipPath = fixture.BundlePath,
            Options = new DiagnosticsExportOptions { IncludeDetailedEnvironmentInformation = true },
            OperationContext = new DiagnosticsOperationContext { OperationId = "op-c", OperationType = "catalog" }
        });

        using var archive = ZipFile.OpenRead(fixture.BundlePath);
        using var runtime = ReadZipJson(archive, "metadata/runtime-metadata.json");
        var root = runtime.RootElement;

        Assert.False(root.TryGetProperty("environmentVariables", out _));
        Assert.False(root.TryGetProperty("env", out _));

        var runtimeJsonText = root.GetRawText();
        Assert.DoesNotContain("token", runtimeJsonText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", runtimeJsonText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetAvailableOptions_UsesPlainLanguageLabels()
    {
        using var fixture = new DiagnosticsFixture();
        var options = fixture.CreateService().GetAvailableOptions();

        Assert.Contains(options, o => o.Label == "Include full template file");
        Assert.Contains(options, o => o.Label == "Include detailed environment information");
    }

    private static JsonDocument ReadZipJson(ZipArchive archive, string entryName)
    {
        return JsonDocument.Parse(ReadZipText(archive, entryName));
    }

    private static string ReadZipText(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName);
        Assert.NotNull(entry);
        using var stream = entry!.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed class DiagnosticsFixture : IDisposable
    {
        public DiagnosticsFixture()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "labassistant-diag-tests", Guid.NewGuid().ToString("N"));
            AppRootPath = Path.Combine(RootPath, "AppData");
            LogsPath = Path.Combine(RootPath, "Logs");
            TemplatesPath = Path.Combine(RootPath, "Templates");
            BundlePath = Path.Combine(RootPath, "exports", "diagnostics.zip");
            TemplateFilePath = Path.Combine(TemplatesPath, "template.json");

            Directory.CreateDirectory(RootPath);
            Directory.CreateDirectory(LogsPath);
            Directory.CreateDirectory(TemplatesPath);
        }

        public string RootPath { get; }
        public string AppRootPath { get; }
        public string LogsPath { get; }
        public string TemplatesPath { get; }
        public string BundlePath { get; }
        public string TemplateFilePath { get; }

        public void WriteStructuredLogs(string content)
        {
            File.WriteAllText(Path.Combine(LogsPath, StructuredLoggingDefaults.StructuredEventsFileName), content.ReplaceLineEndings(Environment.NewLine));
        }

        public DiagnosticsExportService CreateService()
        {
            var appPaths = new FakeAppPaths(AppRootPath, LogsPath);
            var settingsStore = new FakeSettingsStore
            {
                Settings = new AppSettings
                {
                    LogFolder = LogsPath,
                    TemplateFolder = TemplatesPath,
                    CatalogPath = Path.Combine(RootPath, "Catalog", "vhdx-catalog.json")
                }
            };

            return new DiagnosticsExportService(settingsStore, appPaths);
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }

    private sealed class FakeSettingsStore : IAppSettingsStore
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

    private sealed class FakeAppPaths : IAppPaths
    {
        public FakeAppPaths(string appRoot, string logsFolder)
        {
            AppRoot = appRoot;
            LogsFolder = logsFolder;
            ConfigFolder = Path.Combine(appRoot, "Config");
            CatalogFolder = Path.Combine(appRoot, "Catalog");
            TemplatesFolder = Path.Combine(appRoot, "Templates");
            VmBasePath = Path.Combine(appRoot, "VMs");
            DifferencingDiskBasePath = Path.Combine(appRoot, "Disks");
            CatalogPath = Path.Combine(CatalogFolder, "vhdx-catalog.json");
        }

        public string AppRoot { get; }
        public string ConfigFolder { get; }
        public string CatalogFolder { get; }
        public string TemplatesFolder { get; }
        public string LogsFolder { get; }
        public string VmBasePath { get; }
        public string DifferencingDiskBasePath { get; }
        public string CatalogPath { get; }
    }
}
