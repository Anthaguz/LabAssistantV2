using System.IO;
using System.Text.Json;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Validation;

namespace LabAssistant.Services.Catalog;

public class VhdxCatalogLoader
{
    public VhdxCatalogLoadResult Load(string catalogPath)
    {
        var result = new VhdxCatalogLoadResult();

        if (string.IsNullOrWhiteSpace(catalogPath))
        {
            result.Errors.Add("Catalog path is required.");
            return result;
        }

        if (!File.Exists(catalogPath))
        {
            return result;
        }

        try
        {
            var json = File.ReadAllText(catalogPath);
            var document = JsonSerializer.Deserialize<VhdxCatalogDocument>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (document?.Items != null)
            {
                result.Items.AddRange(document.Items);
            }
            else
            {
                result.Errors.Add("Catalog file did not contain any items.");
                return result;
            }

            var validation = VhdxCatalogValidator.Validate(result.Items);
            result.Errors.AddRange(validation.Errors);
        }
        catch (JsonException ex)
        {
            result.Errors.Add($"Catalog JSON parse error: {ex.Message}");
        }
        catch (IOException ex)
        {
            result.Errors.Add($"Catalog file read error: {ex.Message}");
        }

        return result;
    }
}
