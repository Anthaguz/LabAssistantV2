using System.Collections.Generic;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Validation;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;

namespace LabAssistant.Business.Catalog;

public sealed class CatalogService
{
    private readonly IVhdxCatalogStore _catalogStore;
    private readonly IAppSettingsStore _settingsStore;
    private readonly IStructuredLogger _structuredLogger;
    private readonly IVhdxIntegrityValidator? _vhdxIntegrityValidator;

    public CatalogService(
        IVhdxCatalogStore catalogStore,
        IAppSettingsStore settingsStore,
        IStructuredLogger? structuredLogger = null,
        IVhdxIntegrityValidator? vhdxIntegrityValidator = null)
    {
        _catalogStore = catalogStore;
        _settingsStore = settingsStore;
        _structuredLogger = structuredLogger ?? NullStructuredLogger.Instance;
        _vhdxIntegrityValidator = vhdxIntegrityValidator;
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
            var integrityErrors = ValidateCatalogVhdxIntegrity(itemList);
            if (integrityErrors.Count > 0)
            {
                var failed = new VhdxCatalogSaveResult();
                failed.Errors.AddRange(integrityErrors);
                EmitCatalogSaveLog(operationId, itemList, failed);
                return failed;
            }

            var result = _catalogStore.Save(CatalogPath, itemList);
            EmitCatalogSaveLog(operationId, itemList, result);
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

    private void EmitCatalogSaveLog(string operationId, IReadOnlyCollection<VhdxCatalogItem> itemList, VhdxCatalogSaveResult result)
    {
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
    }

    private List<string> ValidateCatalogVhdxIntegrity(IReadOnlyCollection<VhdxCatalogItem> itemList)
    {
        var errors = new List<string>();
        if (_vhdxIntegrityValidator == null)
        {
            return errors;
        }

        foreach (var item in itemList)
        {
            if (string.IsNullOrWhiteSpace(item.Path))
            {
                continue;
            }

            var integrity = _vhdxIntegrityValidator
                .ValidateAsync(item.Path, VhdxIntegrityValidationDepth.Full)
                .GetAwaiter()
                .GetResult();

            if (integrity.IsValid)
            {
                continue;
            }

            var reason = integrity.Status switch
            {
                VhdxIntegrityStatus.Missing => "file not found",
                VhdxIntegrityStatus.Unreadable => "unreadable or inaccessible",
                VhdxIntegrityStatus.Invalid => "not a valid Hyper-V VHDX",
                _ => "invalid"
            };

            errors.Add($"Catalog item '{item.Id}' base VHDX is {reason}: {item.Path}");
        }

        return errors;
    }
}
