using LabAssistant.Models.Catalog;
using LabAssistant.Models.Configuration;
using LabAssistant.Models.Templates;
using LabAssistant.Models.Validation;
using LabAssistant.Services.Logging;

namespace LabAssistant.Business.Templates;

public sealed class TemplatesCapabilityService : ITemplatesCapabilityService
{
    private readonly IAppSettingsStore _settingsStore;
    private readonly IVhdxCatalogStore _catalogStore;
    private readonly ILabTemplateStore _templateStore;
    private readonly TemplateValidationService _validationService;
    private readonly IStructuredLogger _structuredLogger;

    public TemplatesCapabilityService(
        IAppSettingsStore settingsStore,
        IVhdxCatalogStore catalogStore,
        ILabTemplateStore templateStore,
        TemplateValidationService validationService,
        IStructuredLogger? structuredLogger = null)
    {
        _settingsStore = settingsStore;
        _catalogStore = catalogStore;
        _templateStore = templateStore;
        _validationService = validationService;
        _structuredLogger = structuredLogger ?? NullStructuredLogger.Instance;
    }

    public Task<TemplateLibraryLoadResult> LoadLibraryAsync(string? searchText = null, CancellationToken cancellationToken = default)
    {
        var operationId = Guid.NewGuid().ToString("N");
        var templatesFolder = _settingsStore.Settings.TemplateFolder;
        var items = new List<TemplateLibraryItem>();
        var errors = new List<string>();

        try
        {
            if (string.IsNullOrWhiteSpace(templatesFolder))
            {
                errors.Add("Template folder is not configured.");
                return Task.FromResult(new TemplateLibraryLoadResult { Items = items, Errors = errors });
            }

            if (!Directory.Exists(templatesFolder))
            {
                errors.Add($"Template folder not found: {templatesFolder}");
                return Task.FromResult(new TemplateLibraryLoadResult { Items = items, Errors = errors });
            }

            var catalogResult = _catalogStore.Load(_settingsStore.Settings.CatalogPath);
            if (catalogResult.Errors.Count > 0)
            {
                errors.AddRange(catalogResult.Errors.Select(error => $"Catalog: {error}"));
            }

            foreach (var filePath in Directory.GetFiles(templatesFolder, "*.json", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var template = _templateStore.LoadFromFile(filePath);
                    var validation = LabTemplateValidator.Validate(template, catalogResult.Items);
                    if (!validation.IsValid)
                    {
                        foreach (var validationError in validation.Errors)
                        {
                            errors.Add($"{Path.GetFileName(filePath)}: {validationError}");
                        }
                    }

                    if (_templateStore.LastLoadWarnings.Count > 0)
                    {
                        errors.AddRange(_templateStore.LastLoadWarnings.Select(warning => $"{Path.GetFileName(filePath)}: {warning}"));
                    }

                    items.Add(new TemplateLibraryItem
                    {
                        TemplateId = template.Id,
                        Name = string.IsNullOrWhiteSpace(template.Name) ? "<unnamed template>" : template.Name,
                        Description = template.Description ?? string.Empty,
                        VmCount = template.VmTemplates.Count,
                        SchemaVersion = template.SchemaVersion,
                        TemplateRevision = template.TemplateRevision,
                        FilePath = filePath
                    });
                }
                catch (Exception ex)
                {
                    errors.Add($"{Path.GetFileName(filePath)}: {ex.Message}");
                }
            }

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var filter = searchText.Trim();
                items = items
                    .Where(item =>
                        item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        item.Description.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        item.TemplateId.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            items = items
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.TemplateId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _structuredLogger.Log(
                StructuredLogLevel.Info,
                "TemplateLibraryLoaded",
                operationId,
                "success",
                new Dictionary<string, object?>
                {
                    ["templateFolder"] = templatesFolder,
                    ["templateCount"] = items.Count,
                    ["errorCount"] = errors.Count
                });

            return Task.FromResult(new TemplateLibraryLoadResult
            {
                Items = items,
                Errors = errors
            });
        }
        catch (Exception ex)
        {
            _structuredLogger.Log(
                StructuredLogLevel.Error,
                "TemplateLibraryLoaded",
                operationId,
                "failed",
                new Dictionary<string, object?>
                {
                    ["templateFolder"] = templatesFolder,
                    ["exceptionType"] = ex.GetType().Name,
                    ["errorMessage"] = ex.Message
                });

            return Task.FromResult(new TemplateLibraryLoadResult
            {
                Items = Array.Empty<TemplateLibraryItem>(),
                Errors = [$"Failed to load templates: {ex.Message}"]
            });
        }
    }

    public Task<TemplateEditorDocument> CreateDraftAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var draft = new LabTemplate
        {
            Name = "New Template",
            Description = "Update template metadata and save."
        };

        return Task.FromResult(new TemplateEditorDocument
        {
            Template = draft,
            SourceFilePath = null
        });
    }

    public Task<TemplateEditorDocument> LoadForEditorAsync(string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var template = _templateStore.LoadFromFile(filePath);
        return Task.FromResult(new TemplateEditorDocument
        {
            Template = template,
            SourceFilePath = filePath
        });
    }

    public Task<TemplatesVhdxCatalogLoadResult> LoadVhdxCatalogOptionsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var catalogPath = _settingsStore.Settings.CatalogPath;
        var catalogResult = _catalogStore.Load(catalogPath);
        var items = catalogResult.Items
            .OrderBy(item => item.OsName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.OsVersion, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .Select(item => new TemplatesVhdxCatalogItem
            {
                Id = item.Id,
                Path = item.Path,
                OsName = item.OsName,
                OsVersion = item.OsVersion,
                Generation = item.Generation,
                Signature = item.Signature
            })
            .ToList();

        return Task.FromResult(new TemplatesVhdxCatalogLoadResult
        {
            Items = items,
            Errors = catalogResult.Errors.ToList()
        });
    }

    public Task<TemplateOperationResult> SaveAsync(
        TemplateEditorDocument document,
        string? targetFilePath = null,
        bool saveAs = false,
        CancellationToken cancellationToken = default)
    {
        var operationId = Guid.NewGuid().ToString("N");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var validation = _validationService.ValidateForSave(document.Template);
            if (validation.Errors.Count > 0)
            {
                var message = "Save blocked. " + string.Join(" ", validation.Errors);
                _structuredLogger.Log(
                    StructuredLogLevel.Warn,
                    "TemplateSaved",
                    operationId,
                    "validation_failed",
                    new Dictionary<string, object?>
                    {
                        ["templateId"] = document.Template.Id,
                        ["templateName"] = document.Template.Name,
                        ["errorCount"] = validation.Errors.Count
                    });

                return Task.FromResult(new TemplateOperationResult
                {
                    Success = false,
                    OperationId = operationId,
                    UserMessage = message
                });
            }

            string resultingPath;
            if (saveAs)
            {
                if (string.IsNullOrWhiteSpace(targetFilePath))
                {
                    return Task.FromResult(new TemplateOperationResult
                    {
                        Success = false,
                        OperationId = operationId,
                        UserMessage = "Select a destination file for Save As."
                    });
                }

                _templateStore.SaveToFile(targetFilePath, document.Template);
                resultingPath = targetFilePath;
            }
            else if (!string.IsNullOrWhiteSpace(targetFilePath))
            {
                _templateStore.SaveToFile(targetFilePath, document.Template);
                resultingPath = targetFilePath;
            }
            else if (!string.IsNullOrWhiteSpace(document.SourceFilePath))
            {
                _templateStore.SaveToFile(document.SourceFilePath, document.Template);
                resultingPath = document.SourceFilePath;
            }
            else
            {
                var folder = _settingsStore.Settings.TemplateFolder;
                if (string.IsNullOrWhiteSpace(folder))
                {
                    return Task.FromResult(new TemplateOperationResult
                    {
                        Success = false,
                        OperationId = operationId,
                        UserMessage = "Template folder is not configured. Configure it in settings first."
                    });
                }

                resultingPath = _templateStore.SaveToFolder(folder, document.Template.Name, document.Template);
            }

            _structuredLogger.Log(
                StructuredLogLevel.Info,
                "TemplateSaved",
                operationId,
                "success",
                new Dictionary<string, object?>
                {
                    ["templateId"] = document.Template.Id,
                    ["templateName"] = document.Template.Name,
                    ["resourcePath"] = resultingPath,
                    ["saveAs"] = saveAs
                });

            return Task.FromResult(new TemplateOperationResult
            {
                Success = true,
                OperationId = operationId,
                UserMessage = $"Template saved to {resultingPath}",
                FilePath = resultingPath
            });
        }
        catch (Exception ex)
        {
            _structuredLogger.Log(
                StructuredLogLevel.Error,
                "TemplateSaved",
                operationId,
                "failed",
                new Dictionary<string, object?>
                {
                    ["templateId"] = document.Template.Id,
                    ["templateName"] = document.Template.Name,
                    ["exceptionType"] = ex.GetType().Name,
                    ["errorMessage"] = ex.Message
                });

            return Task.FromResult(new TemplateOperationResult
            {
                Success = false,
                OperationId = operationId,
                UserMessage = $"Failed to save template. {ex.Message}"
            });
        }
    }

    public Task<TemplateValidationSummaryResult> ValidateAsync(TemplateEditorDocument document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var summary = _validationService.ValidateForSave(document.Template);
        return Task.FromResult(new TemplateValidationSummaryResult
        {
            IsValid = summary.Errors.Count == 0,
            Errors = summary.Errors.ToList(),
            Warnings = summary.Warnings.ToList()
        });
    }

    public Task<TemplateOperationResult> DeleteAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var operationId = Guid.NewGuid().ToString("N");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(filePath))
            {
                return Task.FromResult(new TemplateOperationResult
                {
                    Success = false,
                    OperationId = operationId,
                    UserMessage = $"Template file not found: {filePath}"
                });
            }

            File.Delete(filePath);

            _structuredLogger.Log(
                StructuredLogLevel.Info,
                "TemplateDeleted",
                operationId,
                "success",
                new Dictionary<string, object?>
                {
                    ["resourcePath"] = filePath
                });

            return Task.FromResult(new TemplateOperationResult
            {
                Success = true,
                OperationId = operationId,
                UserMessage = "Template deleted.",
                FilePath = filePath
            });
        }
        catch (Exception ex)
        {
            _structuredLogger.Log(
                StructuredLogLevel.Error,
                "TemplateDeleted",
                operationId,
                "failed",
                new Dictionary<string, object?>
                {
                    ["resourcePath"] = filePath,
                    ["exceptionType"] = ex.GetType().Name,
                    ["errorMessage"] = ex.Message
                });

            return Task.FromResult(new TemplateOperationResult
            {
                Success = false,
                OperationId = operationId,
                UserMessage = $"Failed to delete template. {ex.Message}"
            });
        }
    }

    public Task<TemplateOperationResult> ImportAsync(string sourceFilePath, CancellationToken cancellationToken = default)
    {
        var operationId = Guid.NewGuid().ToString("N");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(sourceFilePath))
            {
                return Task.FromResult(new TemplateOperationResult
                {
                    Success = false,
                    OperationId = operationId,
                    UserMessage = $"Import source not found: {sourceFilePath}"
                });
            }

            var template = _templateStore.LoadFromFile(sourceFilePath);
            var validation = _validationService.ValidateForSave(template);
            if (validation.Errors.Count > 0)
            {
                return Task.FromResult(new TemplateOperationResult
                {
                    Success = false,
                    OperationId = operationId,
                    UserMessage = "Import blocked. " + string.Join(" ", validation.Errors)
                });
            }

            var folder = _settingsStore.Settings.TemplateFolder;
            if (string.IsNullOrWhiteSpace(folder))
            {
                return Task.FromResult(new TemplateOperationResult
                {
                    Success = false,
                    OperationId = operationId,
                    UserMessage = "Template folder is not configured. Configure it in settings first."
                });
            }

            var importedPath = _templateStore.SaveToFolder(folder, template.Name, template);

            _structuredLogger.Log(
                StructuredLogLevel.Info,
                "TemplateImported",
                operationId,
                "success",
                new Dictionary<string, object?>
                {
                    ["templateId"] = template.Id,
                    ["templateName"] = template.Name,
                    ["sourcePath"] = sourceFilePath,
                    ["resourcePath"] = importedPath
                });

            return Task.FromResult(new TemplateOperationResult
            {
                Success = true,
                OperationId = operationId,
                UserMessage = $"Template imported to {importedPath}",
                FilePath = importedPath
            });
        }
        catch (Exception ex)
        {
            _structuredLogger.Log(
                StructuredLogLevel.Error,
                "TemplateImported",
                operationId,
                "failed",
                new Dictionary<string, object?>
                {
                    ["sourcePath"] = sourceFilePath,
                    ["exceptionType"] = ex.GetType().Name,
                    ["errorMessage"] = ex.Message
                });

            return Task.FromResult(new TemplateOperationResult
            {
                Success = false,
                OperationId = operationId,
                UserMessage = $"Failed to import template. {ex.Message}"
            });
        }
    }

    public Task<TemplateOperationResult> ExportAsync(
        string sourceFilePath,
        string destinationFilePath,
        CancellationToken cancellationToken = default)
    {
        var operationId = Guid.NewGuid().ToString("N");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(sourceFilePath))
            {
                return Task.FromResult(new TemplateOperationResult
                {
                    Success = false,
                    OperationId = operationId,
                    UserMessage = $"Template file not found: {sourceFilePath}"
                });
            }

            var exportFolder = Path.GetDirectoryName(destinationFilePath);
            if (!string.IsNullOrWhiteSpace(exportFolder))
            {
                Directory.CreateDirectory(exportFolder);
            }

            File.Copy(sourceFilePath, destinationFilePath, overwrite: true);

            _structuredLogger.Log(
                StructuredLogLevel.Info,
                "TemplateExported",
                operationId,
                "success",
                new Dictionary<string, object?>
                {
                    ["sourcePath"] = sourceFilePath,
                    ["resourcePath"] = destinationFilePath
                });

            return Task.FromResult(new TemplateOperationResult
            {
                Success = true,
                OperationId = operationId,
                UserMessage = $"Template exported to {destinationFilePath}",
                FilePath = destinationFilePath
            });
        }
        catch (Exception ex)
        {
            _structuredLogger.Log(
                StructuredLogLevel.Error,
                "TemplateExported",
                operationId,
                "failed",
                new Dictionary<string, object?>
                {
                    ["sourcePath"] = sourceFilePath,
                    ["resourcePath"] = destinationFilePath,
                    ["exceptionType"] = ex.GetType().Name,
                    ["errorMessage"] = ex.Message
                });

            return Task.FromResult(new TemplateOperationResult
            {
                Success = false,
                OperationId = operationId,
                UserMessage = $"Failed to export template. {ex.Message}"
            });
        }
    }
}
