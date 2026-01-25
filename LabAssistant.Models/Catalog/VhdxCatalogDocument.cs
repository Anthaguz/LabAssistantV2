using System.Collections.Generic;

namespace LabAssistant.Models.Catalog;

public class VhdxCatalogDocument
{
    public string Version { get; set; } = "v0";
    public List<VhdxCatalogItem> Items { get; set; } = new();
}
