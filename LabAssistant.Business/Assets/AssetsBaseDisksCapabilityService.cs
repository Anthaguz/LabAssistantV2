using System.IO;
using LabAssistant.Business.Catalog;
using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Templates;
using LabAssistant.Models.Validation;
using LabAssistant.Services.HyperV;
using LabAssistant.Services.Logging;

namespace LabAssistant.Business.Assets;

public sealed class AssetsBaseDisksCapabilityService : IAssetsBaseDisksCapabilityService
{
    private readonly CatalogService _catalogService;
    private readonly IAppSettingsStore _settingsStore;
    private readonly ILabTemplateStore _templateStore;
    private readonly IStructuredLogger _structuredLogger;
    private readonly IVhdxIntegrityValidator _vhdxIntegrityValidator;

    public AssetsBaseDisksCapabilityService(
        CatalogService catalogService,
        IAppSettingsStore settingsStore,
        ILabTemplateStore templateStore,
        IVhdxIntegrityValidator vhdxIntegrityValidator,
        IStructuredLogger? structuredLogger = null)
    {
        _catalogService = catalogService;
        _settingsStore = settingsStore;
        _templateStore = templateStore;
        _vhdxIntegrityValidator = vhdxIntegrityValidator;
        _structuredLogger = structuredLogger ?? NullStructuredLogger.Instance;
    }

    public Task<AssetsBaseDisksCatalogResult> LoadAsync(bool isRefresh = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var operationId = Guid.NewGuid().ToString("N");
        var result = _catalogService.LoadCatalog(operationId);
        var eventName = isRefresh ? "BaseDiskRefreshCompleted" : "BaseDiskListLoaded";

        _structuredLogger.Log(
            result.Errors.Count > 0 ? StructuredLogLevel.Warn : StructuredLogLevel.Info,
            eventName,
            operationId,
            result.Errors.Count > 0 ? "failed" : "success",
            new Dictionary<string, object?>
            {
                ["catalogPath"] = _catalogService.CatalogPath,
                ["baseDiskCount"] = result.Items.Count,
                ["errorCount"] = result.Errors.Count
            });

        return Task.FromResult(new AssetsBaseDisksCatalogResult
        {
            OperationId = operationId,
            Items = result.Items.Select(MapRecord).ToList(),
            Errors = result.Errors.ToList()
        });
    }

    public async Task<AssetsBaseDiskOperationResult> SaveAsync(AssetsBaseDiskDraft draft, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var operationId = Guid.NewGuid().ToString("N");
        var catalog = _catalogService.LoadCatalog(operationId);
        if (catalog.Errors.Count > 0)
        {
            return CreateFailure(operationId, "Unable to load the base disk catalog before saving.", catalog.Errors);
        }

        var workingItems = catalog.Items.Select(CloneItem).ToList();
        var targetId = string.IsNullOrWhiteSpace(draft.Id) ? Guid.NewGuid().ToString("N") : draft.Id;
        var normalized = BuildCatalogItem(draft, targetId!);
        var validation = await ValidateCoreAsync(normalized, cancellationToken);
        if (!string.Equals(validation.Severity, "Pass", StringComparison.Ordinal))
        {
            return CreateFailure(operationId, validation.Summary, validation.Details);
        }

        var existing = workingItems.FirstOrDefault(item => string.Equals(item.Id, normalized.Id, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            if (workingItems.Any(item => string.Equals(item.Id, normalized.Id, StringComparison.OrdinalIgnoreCase)))
            {
                return CreateFailure(operationId, "Catalog id must be unique.", ["Catalog id must be unique."]);
            }

            workingItems.Add(normalized);
        }
        else
        {
            existing.Path = normalized.Path;
            existing.OsName = normalized.OsName;
            existing.OsVersion = normalized.OsVersion;
            existing.Generation = normalized.Generation;
            existing.SizeBytes = normalized.SizeBytes;
            existing.Signature = normalized.Signature;
            existing.Notes = normalized.Notes;
        }

        var saveResult = _catalogService.SaveCatalog(workingItems, [normalized], operationId);
        var eventName = existing is null ? "BaseDiskRegistered" : "BaseDiskMetadataUpdated";
        _structuredLogger.Log(
            saveResult.Errors.Count > 0 ? StructuredLogLevel.Warn : StructuredLogLevel.Info,
            eventName,
            operationId,
            saveResult.Errors.Count > 0 ? "failed" : "success",
            new Dictionary<string, object?>
            {
                ["baseDiskId"] = normalized.Id,
                ["path"] = normalized.Path,
                ["osName"] = normalized.OsName,
                ["osVersion"] = normalized.OsVersion,
                ["generation"] = normalized.Generation,
                ["errorCount"] = saveResult.Errors.Count
            });

        if (saveResult.Errors.Count > 0)
        {
            return CreateFailure(operationId, saveResult.Errors[0], saveResult.Errors);
        }

        return new AssetsBaseDiskOperationResult
        {
            Success = true,
            OperationId = operationId,
            UserMessage = existing is null
                ? "Base disk registered successfully."
                : "Base disk metadata updated successfully.",
            Item = MapRecord(normalized)
        };
    }

    public async Task<AssetsBaseDiskValidationResult> ValidateAsync(AssetsBaseDiskDraft draft, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var operationId = Guid.NewGuid().ToString("N");
        var item = BuildCatalogItem(draft, string.IsNullOrWhiteSpace(draft.Id) ? Guid.NewGuid().ToString("N") : draft.Id!);
        var result = await ValidateCoreAsync(item, cancellationToken);

        _structuredLogger.Log(
            string.Equals(result.Severity, "Pass", StringComparison.Ordinal) ? StructuredLogLevel.Info : StructuredLogLevel.Warn,
            "BaseDiskValidationEvaluated",
            operationId,
            result.Severity.ToLowerInvariant(),
            new Dictionary<string, object?>
            {
                ["baseDiskId"] = item.Id,
                ["path"] = item.Path,
                ["severity"] = result.Severity,
                ["detailCount"] = result.Details.Count
            });

        return new AssetsBaseDiskValidationResult
        {
            OperationId = operationId,
            Severity = result.Severity,
            Summary = result.Summary,
            Details = result.Details
        };
    }

    public Task<AssetsBaseDiskRemovalAssessment> AssessRemoveAsync(string baseDiskId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var operationId = Guid.NewGuid().ToString("N");
        var catalog = _catalogService.LoadCatalog(operationId);
        if (catalog.Errors.Count > 0)
        {
            return Task.FromResult(new AssetsBaseDiskRemovalAssessment
            {
                OperationId = operationId,
                Exists = false,
                CanRemove = false,
                BlockingReasons = ["Unable to load the base disk catalog before removal."],
                WarningReasons = catalog.Errors.ToList(),
                ReferenceSignalSummary = "Reference detection unavailable because catalog load failed."
            });
        }

        var item = catalog.Items.FirstOrDefault(entry => string.Equals(entry.Id, baseDiskId, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return Task.FromResult(new AssetsBaseDiskRemovalAssessment
            {
                OperationId = operationId,
                Exists = false,
                CanRemove = false,
                BlockingReasons = ["The selected base disk was not found in the catalog."],
                ReferenceSignalSummary = "No removal assessment available."
            });
        }

        var warnings = BuildTemplateReferenceWarnings(item, catalog.Items);
        var assessment = new AssetsBaseDiskRemovalAssessment
        {
            OperationId = operationId,
            Exists = true,
            CanRemove = true,
            WarningReasons = warnings,
            ReferenceSignalSummary = warnings.Count > 0
                ? "Known template references found. Active runtime consumer detection is not currently implemented."
                : "No known template references found. Active runtime consumer detection is not currently implemented."
        };

        return Task.FromResult(assessment);
    }

    public async Task<AssetsBaseDiskOperationResult> RemoveAsync(string baseDiskId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var operationId = Guid.NewGuid().ToString("N");
        var catalog = _catalogService.LoadCatalog(operationId);
        if (catalog.Errors.Count > 0)
        {
            return CreateFailure(operationId, "Unable to load the base disk catalog before removal.", catalog.Errors);
        }

        var item = catalog.Items.FirstOrDefault(entry => string.Equals(entry.Id, baseDiskId, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return CreateFailure(operationId, "The selected base disk was not found in the catalog.", ["The selected base disk was not found in the catalog."]);
        }

        var remainingItems = catalog.Items
            .Where(entry => !string.Equals(entry.Id, baseDiskId, StringComparison.OrdinalIgnoreCase))
            .Select(CloneItem)
            .ToList();

        var saveResult = _catalogService.SaveCatalog(remainingItems, [], operationId);
        _structuredLogger.Log(
            saveResult.Errors.Count > 0 ? StructuredLogLevel.Warn : StructuredLogLevel.Info,
            saveResult.Errors.Count > 0 ? "BaseDiskRemoveFailed" : "BaseDiskRemoved",
            operationId,
            saveResult.Errors.Count > 0 ? "failed" : "success",
            new Dictionary<string, object?>
            {
                ["baseDiskId"] = item.Id,
                ["path"] = item.Path,
                ["errorCount"] = saveResult.Errors.Count
            });

        if (saveResult.Errors.Count > 0)
        {
            return CreateFailure(operationId, saveResult.Errors[0], saveResult.Errors);
        }

        return new AssetsBaseDiskOperationResult
        {
            Success = true,
            OperationId = operationId,
            UserMessage = "Base disk removed from the registry.",
            Item = MapRecord(item)
        };
    }

    private async Task<AssetsBaseDiskValidationResult> ValidateCoreAsync(VhdxCatalogItem item, CancellationToken cancellationToken)
    {
        var validationErrors = VhdxCatalogValidator.Validate([item]).Errors.ToList();
        if (validationErrors.Count > 0)
        {
            return new AssetsBaseDiskValidationResult
            {
                Severity = "Block",
                Summary = validationErrors[0],
                Details = validationErrors
            };
        }

        var integrity = await _vhdxIntegrityValidator.ValidateAsync(item.Path, VhdxIntegrityValidationDepth.Full, cancellationToken);
        if (!integrity.IsValid)
        {
            return new AssetsBaseDiskValidationResult
            {
                Severity = "Block",
                Summary = integrity.Message,
                Details = string.IsNullOrWhiteSpace(integrity.Detail)
                    ? [integrity.Message]
                    : [integrity.Message, integrity.Detail]
            };
        }

        return new AssetsBaseDiskValidationResult
        {
            Severity = "Pass",
            Summary = "Base disk is ready to use.",
            Details =
            [
                $"Signature: {item.Signature ?? "Unavailable"}",
                $"Generation: {item.Generation}"
            ]
        };
    }

    private List<string> BuildTemplateReferenceWarnings(VhdxCatalogItem item, IReadOnlyCollection<VhdxCatalogItem> catalogItems)
    {
        var warnings = new List<string>();
        var templateFolder = _settingsStore.Settings.TemplateFolder;
        if (string.IsNullOrWhiteSpace(templateFolder) || !Directory.Exists(templateFolder))
        {
            return warnings;
        }

        var loadResult = _templateStore.LoadFromFolder(templateFolder, catalogItems);
        foreach (var template in loadResult.Templates)
        {
            foreach (var vm in template.VmTemplates.Where(vm => ReferencesItem(vm, item)))
            {
                warnings.Add($"Template '{template.Name}' VM '{vm.Name}' references this base disk.");
            }
        }

        return warnings;
    }

    private static bool ReferencesItem(VmTemplate vm, VhdxCatalogItem item)
    {
        if (!string.IsNullOrWhiteSpace(vm.VhdxId) &&
            string.Equals(vm.VhdxId, item.Id, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(vm.VhdPath) &&
            string.Equals(vm.VhdPath, item.Path, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(vm.VhdxSignature) &&
               !string.IsNullOrWhiteSpace(item.Signature) &&
               string.Equals(vm.VhdxSignature, item.Signature, StringComparison.OrdinalIgnoreCase);
    }

    private static VhdxCatalogItem BuildCatalogItem(AssetsBaseDiskDraft draft, string id)
    {
        var normalizedPath = draft.Path.Trim();
        return new VhdxCatalogItem
        {
            Id = id,
            Path = normalizedPath,
            OsName = draft.OsName.Trim(),
            OsVersion = draft.OsVersion.Trim(),
            Generation = draft.Generation,
            Notes = string.IsNullOrWhiteSpace(draft.Notes) ? null : draft.Notes.Trim(),
            SizeBytes = TryGetFileSize(normalizedPath),
            Signature = VhdxSignature.Build(new VhdxCatalogItem
            {
                Id = id,
                Path = normalizedPath,
                OsName = draft.OsName.Trim(),
                OsVersion = draft.OsVersion.Trim(),
                Generation = draft.Generation,
                SizeBytes = TryGetFileSize(normalizedPath),
                Notes = string.IsNullOrWhiteSpace(draft.Notes) ? null : draft.Notes.Trim()
            })
        };
    }

    private static AssetsBaseDiskRecord MapRecord(VhdxCatalogItem item)
    {
        return new AssetsBaseDiskRecord
        {
            Id = item.Id,
            Path = item.Path,
            OsName = item.OsName,
            OsVersion = item.OsVersion,
            Generation = item.Generation,
            SizeBytes = item.SizeBytes,
            Signature = item.Signature,
            Notes = item.Notes
        };
    }

    private static VhdxCatalogItem CloneItem(VhdxCatalogItem source)
    {
        return new VhdxCatalogItem
        {
            Id = source.Id,
            Path = source.Path,
            OsName = source.OsName,
            OsVersion = source.OsVersion,
            Generation = source.Generation,
            SizeBytes = source.SizeBytes,
            Signature = source.Signature,
            Notes = source.Notes
        };
    }

    private static AssetsBaseDiskOperationResult CreateFailure(string operationId, string userMessage, IReadOnlyList<string> errors)
    {
        return new AssetsBaseDiskOperationResult
        {
            Success = false,
            OperationId = operationId,
            UserMessage = userMessage,
            Errors = errors
        };
    }

    private static long? TryGetFileSize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return File.Exists(path) ? new FileInfo(path).Length : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
