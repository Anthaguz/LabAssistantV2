using System.Text.Json;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Templates;
using LabAssistant.Models.Validation;

namespace LabAssistant.Data.Templates;

public class LabTemplateStore : ILabTemplateStore
{
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

    public LabTemplateLoadResult LoadFromFolder(string templatesFolder, IEnumerable<VhdxCatalogItem> catalogItems)
    {
        var result = new LabTemplateLoadResult();

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

                var validation = LabTemplateValidator.Validate(template, catalogItems);
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

                result.Templates.Add(template);
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

        return result;
    }

    public LabTemplate LoadFromFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var template = JsonSerializer.Deserialize<LabTemplate>(json, LoadOptions);
        if (template == null)
        {
            throw new InvalidOperationException("Template file could not be loaded.");
        }

        return template;
    }

    public void SaveToFile(string filePath, LabTemplate template)
    {
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
}
