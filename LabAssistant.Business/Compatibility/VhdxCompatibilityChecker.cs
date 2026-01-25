using System;
using System.Collections.Generic;
using LabAssistant.Models.Catalog;

namespace LabAssistant.Business.Compatibility;

public static class VhdxCompatibilityChecker
{
    public static List<string> Check(VhdxCatalogItem required, VhdxCatalogItem selected, string vmName)
    {
        var warnings = new List<string>();

        if (!string.Equals(required.OsName, selected.OsName, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(required.OsVersion, selected.OsVersion, StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add(
                $"VM '{vmName}' expects {required.OsName} {required.OsVersion}, but selected {selected.OsName} {selected.OsVersion}.");
        }

        return warnings;
    }
}
