using LabAssistant.Models.Catalog;

namespace LabAssistant.Models.Validation;

/// <summary>
/// Validates VHDX catalog entries.
/// </summary>
public static class VhdxCatalogValidator
{
    public static VhdxCatalogValidationResult Validate(IEnumerable<VhdxCatalogItem> items)
    {
        var result = new VhdxCatalogValidationResult();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                result.Errors.Add("Catalog item id is required.");
            }
            else if (!seenIds.Add(item.Id))
            {
                result.Errors.Add($"Duplicate catalog id: {item.Id}.");
            }

            if (string.IsNullOrWhiteSpace(item.Path))
            {
                result.Errors.Add($"Catalog item '{item.Id}' path is required.");
            }

            if (string.IsNullOrWhiteSpace(item.OsName))
            {
                result.Errors.Add($"Catalog item '{item.Id}' OS name is required.");
            }

            if (string.IsNullOrWhiteSpace(item.OsVersion))
            {
                result.Errors.Add($"Catalog item '{item.Id}' OS version is required.");
            }

            if (item.Generation <= 0)
            {
                result.Errors.Add($"Catalog item '{item.Id}' generation must be positive.");
            }
        }

        return result;
    }
}
