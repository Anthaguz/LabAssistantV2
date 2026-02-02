namespace LabAssistant.Models.Catalog;

public interface IVhdxCatalogStore
{
    VhdxCatalogLoadResult Load(string catalogPath);
    VhdxCatalogSaveResult Save(string catalogPath, IEnumerable<VhdxCatalogItem> items);
    void EnsureCatalogFileExists(string catalogPath);
}
