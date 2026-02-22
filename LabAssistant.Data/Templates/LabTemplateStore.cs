using System.Text.Json;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Templates;
using LabAssistant.Models.Validation;

namespace LabAssistant.Data.Templates;

public class LabTemplateStore : ILabTemplateStore
{
    private static readonly Version CurrentSchemaVersion = new(LabTemplate.CurrentSchemaVersion);
    private static readonly JsonSerializerOptions SaveOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions LoadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IReadOnlyList<string> LastLoadWarnings { get; private set; } = Array.Empty<string>();

    public LabTemplateLoadResult LoadFromFolder(string templatesFolder, IEnumerable<VhdxCatalogItem> catalogItems)
    {
        var result = new LabTemplateLoadResult();
        LastLoadWarnings = Array.Empty<string>();

        if (string.IsNullOrWhiteSpace(templatesFolder))
        {
            result.Errors.Add("Templates folder is required.");
            return result;
        }

        if (!Directory.Exists(templatesFolder))
        {
            result.Errors.Add($"Templates folder not found: {templatesFolder}");
            return result;
        }

        var files = Directory.GetFiles(templatesFolder, "*.json", SearchOption.TopDirectoryOnly);
        if (files.Length == 0)
        {
            result.Errors.Add("No template files found.");
            return result;
        }

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var template = JsonSerializer.Deserialize<LabTemplate>(json, LoadOptions);

                if (template == null)
                {
                    result.Errors.Add($"Template file did not parse: {file}");
                    continue;
                }

                var isLegacyV0 = IsLegacyV0Json(json);
                if (!TryNormalizeLoadedTemplate(template, isLegacyV0, out var normalized, out var warnings, out var loadError))
                {
                    result.Errors.Add($"{file}: {loadError}");
                    continue;
                }

                foreach (var warning in warnings)
                {
                    result.Warnings.Add($"{file}: {warning}");
                }

                var validation = LabTemplateValidator.Validate(normalized, catalogItems);
                if (!validation.IsValid)
                {
                    foreach (var error in validation.Errors)
                    {
                        result.Errors.Add($"{file}: {error}");
                    }
                }

                if (validation.MissingVhdxIds.Count > 0)
                {
                    foreach (var missingId in validation.MissingVhdxIds)
                    {
                        result.Errors.Add($"{file}: missing VHDX reference '{missingId}'.");
                    }
                }

                result.Templates.Add(normalized);
            }
            catch (JsonException ex)
            {
                result.Errors.Add($"Template JSON parse error ({file}): {ex.Message}");
            }
            catch (IOException ex)
            {
                result.Errors.Add($"Template file read error ({file}): {ex.Message}");
            }
        }

        LastLoadWarnings = result.Warnings.ToList();
        return result;
    }

    public LabTemplate LoadFromFile(string filePath)
    {
        LastLoadWarnings = Array.Empty<string>();
        var json = File.ReadAllText(filePath);
        var template = JsonSerializer.Deserialize<LabTemplate>(json, LoadOptions);
        if (template == null)
        {
            throw new InvalidOperationException("Template file could not be loaded.");
        }

        var isLegacyV0 = IsLegacyV0Json(json);
        if (!TryNormalizeLoadedTemplate(template, isLegacyV0, out var normalized, out var warnings, out var error))
        {
            throw new InvalidOperationException(error);
        }

        LastLoadWarnings = warnings.ToList();
        return normalized;
    }

    public void SaveToFile(string filePath, LabTemplate template)
    {
        template = NormalizeForSave(template);
        var json = JsonSerializer.Serialize(template, SaveOptions);
        File.WriteAllText(filePath, json);
    }

    public string SaveToFolder(string folderPath, string templateName, LabTemplate template)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new ArgumentException("Template folder path is required.", nameof(folderPath));
        }

        Directory.CreateDirectory(folderPath);
        var fileName = GetTemplateFileName(templateName, folderPath);
        var filePath = Path.Combine(folderPath, fileName);
        SaveToFile(filePath, template);
        return filePath;
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "lab-template" : sanitized;
    }

    private static string GetTemplateFileName(string templateName, string folderPath)
    {
        var baseName = SanitizeFileName(templateName);
        var fileName = $"{baseName}.json";
        var filePath = Path.Combine(folderPath, fileName);
        var suffix = 1;

        while (File.Exists(filePath))
        {
            fileName = $"{baseName}-{suffix}.json";
            filePath = Path.Combine(folderPath, fileName);
            suffix++;
        }

        return fileName;
    }

    private static bool TryNormalizeLoadedTemplate(
        LabTemplate template,
        bool isLegacyV0,
        out LabTemplate normalized,
        out List<string> warnings,
        out string error)
    {
        warnings = new List<string>();
        error = string.Empty;
        normalized = NormalizeForSave(template);

        if (isLegacyV0 || string.Equals(normalized.SchemaVersion, "v0", StringComparison.OrdinalIgnoreCase))
        {
            normalized.SchemaVersion = LabTemplate.CurrentSchemaVersion;
            warnings.Add("Legacy template format 'v0' was migrated in memory to canonical schema version 1.0.0.");
            return true;
        }

        if (!Version.TryParse(normalized.SchemaVersion, out var parsedVersion))
        {
            error = $"Template schemaVersion '{normalized.SchemaVersion}' is invalid. Use semantic version format like '1.0.0'.";
            return false;
        }

        if (parsedVersion.Major != CurrentSchemaVersion.Major)
        {
            error =
                $"Template schemaVersion '{normalized.SchemaVersion}' is not supported. Supported major version is '{CurrentSchemaVersion.Major}.x.x'.";
            return false;
        }

        if (parsedVersion > CurrentSchemaVersion)
        {
            warnings.Add(
                $"Template schemaVersion '{normalized.SchemaVersion}' is newer than supported '{LabTemplate.CurrentSchemaVersion}'. Continuing with compatible fields only.");
        }

        return true;
    }

    private static LabTemplate NormalizeForSave(LabTemplate source)
    {
        var normalized = source;

        if (string.IsNullOrWhiteSpace(normalized.SchemaVersion) || string.Equals(normalized.SchemaVersion, "v0", StringComparison.OrdinalIgnoreCase))
        {
            normalized.SchemaVersion = LabTemplate.CurrentSchemaVersion;
        }

        if (normalized.TemplateRevision <= 0)
        {
            normalized.TemplateRevision = 1;
        }

        if (string.IsNullOrWhiteSpace(normalized.CreatedWithAppVersion) || !Version.TryParse(normalized.CreatedWithAppVersion, out _))
        {
            normalized.CreatedWithAppVersion = "0.0.0";
        }

        if (string.IsNullOrWhiteSpace(normalized.TemplateType))
        {
            normalized.TemplateType = LabTemplate.SupportedTemplateType;
        }

        for (var i = 0; i < normalized.VmTemplates.Count; i++)
        {
            var vm = normalized.VmTemplates[i];
            if (!string.IsNullOrWhiteSpace(vm.VmId))
            {
                continue;
            }

            normalized.VmTemplates[i] = new VmTemplate
            {
                VmId = Guid.NewGuid().ToString("N"),
                Name = vm.Name,
                MemoryMb = vm.MemoryMb,
                CpuCount = vm.CpuCount,
                VhdxId = vm.VhdxId,
                VhdPath = vm.VhdPath,
                VhdxSignature = vm.VhdxSignature,
                SwitchName = vm.SwitchName
            };
        }

        return normalized;
    }

    private static bool IsLegacyV0Json(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("version", out var versionProperty))
            {
                return false;
            }

            return versionProperty.ValueKind == JsonValueKind.String
                   && string.Equals(versionProperty.GetString(), "v0", StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
