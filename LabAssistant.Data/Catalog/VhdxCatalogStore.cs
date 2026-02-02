using System.Text.Json;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Validation;

namespace LabAssistant.Data.Catalog;

public class VhdxCatalogStore : IVhdxCatalogStore
{
    private readonly VhdxCatalogLoader _loader = new();

    public VhdxCatalogLoadResult Load(string catalogPath)
    {
        EnsureCatalogFileExists(catalogPath);
        return _loader.Load(catalogPath);
    }

    public VhdxCatalogSaveResult Save(string catalogPath, IEnumerable<VhdxCatalogItem> items)
    {
        var result = new VhdxCatalogSaveResult();

        if (string.IsNullOrWhiteSpace(catalogPath))
        {
            result.Errors.Add("Catalog path is required.");
            return result;
        }

        try
        {
            var directory = Path.GetDirectoryName(catalogPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var catalogItems = items?.ToList() ?? new List<VhdxCatalogItem>();
            var validation = VhdxCatalogValidator.Validate(catalogItems);
            if (!validation.IsValid)
            {
                result.Errors.AddRange(validation.Errors);
                return result;
            }

            var document = new VhdxCatalogDocument
            {
                Items = catalogItems
            };

            var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(catalogPath, json);
        }
        catch (UnauthorizedAccessException ex)
        {
            result.Errors.Add($"Catalog file write error: {ex.Message}");
        }
        catch (IOException ex)
        {
            result.Errors.Add($"Catalog file write error: {ex.Message}");
        }

        return result;
    }

    public void EnsureCatalogFileExists(string catalogPath)
    {
        if (string.IsNullOrWhiteSpace(catalogPath))
        {
            return;
        }

        if (File.Exists(catalogPath))
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(catalogPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var document = new VhdxCatalogDocument();
            var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(catalogPath, json);
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (IOException)
        {
        }
    }
}
