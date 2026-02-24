using System.IO.Compression;
using System.Text;
using System.Text.Json;
using LabAssistant.Models.Configuration;
using LabAssistant.Services.Logging;

namespace LabAssistant.Services.Diagnostics;

public sealed class DiagnosticsExportService : IDiagnosticsExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly IAppSettingsStore _settingsStore;
    private readonly IAppPaths _appPaths;

    public DiagnosticsExportService(IAppSettingsStore settingsStore, IAppPaths appPaths)
    {
        _settingsStore = settingsStore;
        _appPaths = appPaths;
    }

    public IReadOnlyList<DiagnosticsExportOptionDefinition> GetAvailableOptions()
    {
        return
        [
            new DiagnosticsExportOptionDefinition
            {
                Key = nameof(DiagnosticsExportOptions.IncludeFullTemplateFile),
                Label = "Include full template file",
                Description = "Adds the template file used for the operation when available."
            },
            new DiagnosticsExportOptionDefinition
            {
                Key = nameof(DiagnosticsExportOptions.IncludeDetailedEnvironmentInformation),
                Label = "Include detailed environment information",
                Description = "Adds additional app and system details to help troubleshooting."
            }
        ];
    }

    public DiagnosticsExportResult Export(DiagnosticsExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.DestinationZipPath))
        {
            throw new ArgumentException("Destination zip path is required.", nameof(request));
        }

        var destinationDirectory = Path.GetDirectoryName(request.DestinationZipPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        var result = new DiagnosticsExportResult
        {
            BundlePath = request.DestinationZipPath
        };

        if (File.Exists(request.DestinationZipPath))
        {
            File.Delete(request.DestinationZipPath);
        }

        using var archive = ZipFile.Open(request.DestinationZipPath, ZipArchiveMode.Create);
        var createdUtc = DateTimeOffset.UtcNow;

        AddJsonEntry(archive, "bundle/manifest.json", BuildManifest(request, createdUtc), result);
        AddJsonEntry(archive, "metadata/runtime-metadata.json", BuildRuntimeMetadata(request.Options, createdUtc), result);
        AddJsonEntry(archive, "metadata/operation-context.json", BuildOperationContext(request.OperationContext), result);

        AddStructuredLogsEntry(archive, request, result);

        if (request.Options.IncludeFullTemplateFile)
        {
            AddOptionalTemplateFile(archive, request, result);
        }

        return result;
    }

    private static object BuildManifest(DiagnosticsExportRequest request, DateTimeOffset createdUtc)
    {
        return new
        {
            bundleVersion = "1",
            createdUtc = createdUtc.UtcDateTime.ToString("O"),
            options = new
            {
                includeFullTemplateFile = request.Options.IncludeFullTemplateFile,
                includeDetailedEnvironmentInformation = request.Options.IncludeDetailedEnvironmentInformation
            }
        };
    }

    private object BuildRuntimeMetadata(DiagnosticsExportOptions options, DateTimeOffset createdUtc)
    {
        var baseMetadata = new Dictionary<string, object?>
        {
            ["generatedUtc"] = createdUtc.UtcDateTime.ToString("O"),
            ["appVersion"] = GetAppVersion(),
            ["osVersion"] = Environment.OSVersion.VersionString,
            ["dotnetVersion"] = Environment.Version.ToString()
        };

        if (options.IncludeDetailedEnvironmentInformation)
        {
            baseMetadata["machineName"] = Environment.MachineName;
            baseMetadata["is64BitProcess"] = Environment.Is64BitProcess;
            baseMetadata["processorCount"] = Environment.ProcessorCount;
            baseMetadata["paths"] = new
            {
                appRoot = _appPaths.AppRoot,
                logsFolder = EffectiveLogFolder(),
                templatesFolder = _settingsStore.Settings.TemplateFolder,
                catalogPath = _settingsStore.Settings.CatalogPath
            };
        }

        return baseMetadata;
    }

    private static object BuildOperationContext(DiagnosticsOperationContext? context)
    {
        if (context == null)
        {
            return new { operationId = (string?)null };
        }

        return new
        {
            operationId = NullIfWhiteSpace(context.OperationId),
            operationType = NullIfWhiteSpace(context.OperationType),
            result = NullIfWhiteSpace(context.Result),
            terminalState = NullIfWhiteSpace(context.TerminalState),
            templateId = NullIfWhiteSpace(context.TemplateId),
            templateName = NullIfWhiteSpace(context.TemplateName),
            vmCount = context.VmCount,
            cleanupVmCount = context.CleanupVmCount,
            residualVmCount = context.ResidualVmCount
        };
    }

    private void AddStructuredLogsEntry(ZipArchive archive, DiagnosticsExportRequest request, DiagnosticsExportResult result)
    {
        var structuredLogPath = string.IsNullOrWhiteSpace(request.StructuredLogPathOverride)
            ? Path.Combine(EffectiveLogFolder(), StructuredLoggingDefaults.StructuredEventsFileName)
            : request.StructuredLogPathOverride!;

        if (!File.Exists(structuredLogPath))
        {
            result.Warnings.Add($"Structured log file not found: {structuredLogPath}");
            return;
        }

        var entry = archive.CreateEntry("logs/structured-events.jsonl");
        using var entryStream = entry.Open();
        using var writer = new StreamWriter(entryStream, new UTF8Encoding(false));

        var operationId = request.OperationContext?.OperationId;
        foreach (var line in File.ReadLines(structuredLogPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!ShouldIncludeLogLine(line, operationId))
            {
                continue;
            }

            writer.WriteLine(line);
        }

        result.IncludedArtifacts.Add("logs/structured-events.jsonl");
    }

    private static bool ShouldIncludeLogLine(string line, string? operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            return true;
        }

        using var doc = JsonDocument.Parse(line);
        if (!doc.RootElement.TryGetProperty("operationId", out var opIdElement))
        {
            return false;
        }

        return string.Equals(opIdElement.GetString(), operationId, StringComparison.Ordinal);
    }

    private static void AddJsonEntry(ZipArchive archive, string entryName, object payload, DiagnosticsExportResult result)
    {
        var entry = archive.CreateEntry(entryName);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        writer.Write(json);
        result.IncludedArtifacts.Add(entryName);
    }

    private static void AddOptionalTemplateFile(ZipArchive archive, DiagnosticsExportRequest request, DiagnosticsExportResult result)
    {
        if (string.IsNullOrWhiteSpace(request.TemplateFilePath))
        {
            result.Warnings.Add("Template file inclusion was requested, but no template file path was provided.");
            return;
        }

        if (!File.Exists(request.TemplateFilePath))
        {
            result.Warnings.Add($"Template file not found: {request.TemplateFilePath}");
            return;
        }

        var entry = archive.CreateEntry("artifacts/template-definition.json");
        using var entryStream = entry.Open();
        using var fileStream = File.OpenRead(request.TemplateFilePath);
        fileStream.CopyTo(entryStream);
        result.IncludedArtifacts.Add("artifacts/template-definition.json");
    }

    private string EffectiveLogFolder()
    {
        return string.IsNullOrWhiteSpace(_settingsStore.Settings.LogFolder)
            ? _appPaths.LogsFolder
            : _settingsStore.Settings.LogFolder;
    }

    private static string GetAppVersion()
    {
        return typeof(DiagnosticsExportService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
