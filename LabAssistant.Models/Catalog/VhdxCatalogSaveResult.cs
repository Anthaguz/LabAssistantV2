using System.Collections.Generic;

namespace LabAssistant.Models.Catalog;

public class VhdxCatalogSaveResult
{
    public List<string> Errors { get; } = new();

    public bool IsValid => Errors.Count == 0;
}
