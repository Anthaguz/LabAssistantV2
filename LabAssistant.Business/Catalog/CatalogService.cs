using System.Collections.Generic;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Services.Logging;

namespace LabAssistant.Business.Catalog;

public sealed class CatalogService
{
    private readonly IVhdxCatalogStore _catalogStore;
    private readonly IAppSettingsStore _settingsStore;
    private readonly IStructuredLogger _structuredLogger;

    public CatalogService(IVhdxCatalogStore catalogStore, IAppSettingsStore settingsStore, IStructuredLogger? structuredLogger = null)
    {
        _catalogStore = catalogStore;
        _settingsStore = settingsStore;
        _structuredLogger = structuredLogger ?? NullStructuredLogger.Instance;
    }

    public string CatalogPath => _settingsStore.Settings.CatalogPath;

    public VhdxCatalogLoadResult LoadCatalog()
    {
        var operationId = Guid.NewGuid().ToString("N");
        try
        {
            var result = _catalogStore.Load(CatalogPath);
            _structuredLogger.Log(
                result.Errors.Count > 0 ? StructuredLogLevel.Warn : StructuredLogLevel.Info,
                "CatalogLoaded",
                operationId,
                result.Errors.Count > 0 ? "failed" : "success",
                new Dictionary<string, object?>
                {
                    ["resourcePath"] = CatalogPath,
                    ["itemCount"] = result.Items.Count,
                    ["errorCount"] = result.Errors.Count
                });
            return result;
        }
        catch (Exception ex)
        {
            _structuredLogger.Log(
                StructuredLogLevel.Error,
                "CatalogLoaded",
                operationId,
                "failed",
                new Dictionary<string, object?>
                {
                    ["resourcePath"] = CatalogPath,
                    ["exceptionType"] = ex.GetType().Name,
                    ["errorMessage"] = ex.Message
                });
            throw;
        }
    }

    public VhdxCatalogSaveResult SaveCatalog(IEnumerable<VhdxCatalogItem> items)
    {
        var operationId = Guid.NewGuid().ToString("N");
        var itemList = items?.ToList() ?? new List<VhdxCatalogItem>();
        try
        {
            var result = _catalogStore.Save(CatalogPath, itemList);
            _structuredLogger.Log(
                result.Errors.Count > 0 ? StructuredLogLevel.Warn : StructuredLogLevel.Info,
                "CatalogSaved",
                operationId,
                result.Errors.Count > 0 ? "failed" : "success",
                new Dictionary<string, object?>
                {
                    ["resourcePath"] = CatalogPath,
                    ["itemCount"] = itemList.Count,
                    ["errorCount"] = result.Errors.Count
                });
            return result;
        }
        catch (Exception ex)
        {
            _structuredLogger.Log(
                StructuredLogLevel.Error,
                "CatalogSaved",
                operationId,
                "failed",
                new Dictionary<string, object?>
                {
                    ["resourcePath"] = CatalogPath,
                    ["itemCount"] = itemList.Count,
                    ["exceptionType"] = ex.GetType().Name,
                    ["errorMessage"] = ex.Message
                });
            throw;
        }
    }
}
