using System.Collections.Generic;

namespace LabAssistant.Models.Catalog;

public class VhdxCatalogLoadResult
{
    public List<VhdxCatalogItem> Items { get; } = new();
    public List<string> Errors { get; } = new();

    public bool IsValid => Errors.Count == 0;
}
