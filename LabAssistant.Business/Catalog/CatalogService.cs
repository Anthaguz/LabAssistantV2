using System.Collections.Generic;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;

namespace LabAssistant.Business.Catalog;

public sealed class CatalogService
{
    private readonly IVhdxCatalogStore _catalogStore;
    private readonly IAppSettingsStore _settingsStore;

    public CatalogService(IVhdxCatalogStore catalogStore, IAppSettingsStore settingsStore)
    {
        _catalogStore = catalogStore;
        _settingsStore = settingsStore;
    }

    public string CatalogPath => _settingsStore.Settings.CatalogPath;

    public VhdxCatalogLoadResult LoadCatalog()
    {
        return _catalogStore.Load(CatalogPath);
    }

    public VhdxCatalogSaveResult SaveCatalog(IEnumerable<VhdxCatalogItem> items)
    {
        return _catalogStore.Save(CatalogPath, items);
    }
}
