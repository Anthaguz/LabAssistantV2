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
        var compatibility = TemplateSchemaCompatibilityGate.Evaluate(
            template.SchemaVersion,
            CurrentSchemaVersion,
            isLegacyV0);

        if (compatibility.IsBlocked)
        {
            error = compatibility.Error ?? "Template schemaVersion is not supported.";
            normalized = template;
            return false;
        }

        if (!string.IsNullOrWhiteSpace(compatibility.Warning))
        {
            warnings.Add(compatibility.Warning);
        }

        normalized = compatibility.Status == TemplateSchemaCompatibilityStatus.AllowWithUpcast
            ? UpcastToCurrent(template)
            : template;

        normalized = NormalizeForSave(normalized);
        return true;
    }

    private static LabTemplate NormalizeForSave(LabTemplate source)
    {
        var normalized = source;

        // Save/export always emits the current canonical schema major (N).
        normalized.SchemaVersion = LabTemplate.CurrentSchemaVersion;

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
            vm.GuestNetworkConfig = NormalizeGuestNetworkPlaceholder(vm.GuestNetworkConfig);
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
                SwitchName = vm.SwitchName,
                TimeZoneConfig = Clone(vm.TimeZoneConfig),
                SoftwareConfig = Clone(vm.SoftwareConfig),
                RoleConfig = Clone(vm.RoleConfig),
                GuestNetworkConfig = Clone(vm.GuestNetworkConfig)
            };
        }

        return normalized;
    }

    private static LabTemplate UpcastToCurrent(LabTemplate source)
    {
        // Deterministic N-1 -> N upcaster (single explicit hop for current support window).
        source.SchemaVersion = LabTemplate.CurrentSchemaVersion;
        return source;
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

    private static GuestNetworkStepConfig? NormalizeGuestNetworkPlaceholder(GuestNetworkStepConfig? config)
    {
        if (config == null)
        {
            return null;
        }

        var hasPayload =
            !string.IsNullOrWhiteSpace(config.IpAddress) ||
            !string.IsNullOrWhiteSpace(config.DefaultGateway) ||
            (config.DnsServers != null && config.DnsServers.Any(server => !string.IsNullOrWhiteSpace(server)));

        if (!config.Enabled && !hasPayload)
        {
            return null;
        }

        return config;
    }

    private static TimeZoneStepConfig? Clone(TimeZoneStepConfig? source)
    {
        if (source == null)
        {
            return null;
        }

        return new TimeZoneStepConfig
        {
            Enabled = source.Enabled,
            TimeZoneId = source.TimeZoneId
        };
    }

    private static SoftwareStepConfig? Clone(SoftwareStepConfig? source)
    {
        if (source == null)
        {
            return null;
        }

        return new SoftwareStepConfig
        {
            Enabled = source.Enabled,
            Packages = source.Packages?.ToList()
        };
    }

    private static RoleStepConfig? Clone(RoleStepConfig? source)
    {
        if (source == null)
        {
            return null;
        }

        return new RoleStepConfig
        {
            Enabled = source.Enabled,
            Roles = source.Roles?.ToList()
        };
    }

    private static GuestNetworkStepConfig? Clone(GuestNetworkStepConfig? source)
    {
        if (source == null)
        {
            return null;
        }

        return new GuestNetworkStepConfig
        {
            Enabled = source.Enabled,
            IpAddress = source.IpAddress,
            DefaultGateway = source.DefaultGateway,
            DnsServers = source.DnsServers?.ToList()
        };
    }
}
