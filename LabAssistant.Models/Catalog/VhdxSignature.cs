using System.Collections.Generic;
using System.Linq;

namespace LabAssistant.Models.Catalog;

public static class VhdxSignature
{
    public static string Build(VhdxCatalogItem item)
    {
        var osName = Normalize(item.OsName);
        var osVersion = Normalize(item.OsVersion);
        var generation = item.Generation.ToString();
        var parts = new List<string>
        {
            $"os={osName}",
            $"ver={osVersion}",
            $"gen={generation}"
        };

        if (item.SizeBytes.HasValue && item.SizeBytes.Value > 0)
        {
            parts.Add($"size={item.SizeBytes.Value}");
        }

        return string.Join("|", parts);
    }

    public static IReadOnlyList<VhdxCatalogItem> FindMatches(string signature, IEnumerable<VhdxCatalogItem> items)
    {
        var normalized = Normalize(signature);
        return items
            .Where(item => string.Equals(Normalize(item.Signature), normalized, System.StringComparison.Ordinal))
            .ToList();
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
    }
}
