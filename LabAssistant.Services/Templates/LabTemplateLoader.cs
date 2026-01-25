using System.IO;
using System.Text.Json;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Templates;
using LabAssistant.Models.Validation;

namespace LabAssistant.Services.Templates;

public class LabTemplateLoader
{
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
                var template = JsonSerializer.Deserialize<LabTemplate>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

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
}
