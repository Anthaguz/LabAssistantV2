using LabAssistant.Models.Templates;

namespace LabAssistant.Business.Templates;

public interface ITemplatesCapabilityService
{
    Task<TemplateLibraryLoadResult> LoadLibraryAsync(string? searchText = null, CancellationToken cancellationToken = default);

    Task<TemplateEditorDocument> CreateDraftAsync(CancellationToken cancellationToken = default);

    Task<TemplateEditorDocument> LoadForEditorAsync(string filePath, CancellationToken cancellationToken = default);

    Task<TemplateOperationResult> SaveAsync(
        TemplateEditorDocument document,
        string? targetFilePath = null,
        bool saveAs = false,
        CancellationToken cancellationToken = default);

    Task<TemplateValidationSummaryResult> ValidateAsync(TemplateEditorDocument document, CancellationToken cancellationToken = default);

    Task<TemplateOperationResult> DeleteAsync(string filePath, CancellationToken cancellationToken = default);

    Task<TemplateOperationResult> ImportAsync(string sourceFilePath, CancellationToken cancellationToken = default);

    Task<TemplateOperationResult> ExportAsync(
        string sourceFilePath,
        string destinationFilePath,
        CancellationToken cancellationToken = default);
}

public sealed class TemplateLibraryLoadResult
{
    public IReadOnlyList<TemplateLibraryItem> Items { get; init; } = Array.Empty<TemplateLibraryItem>();

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

public sealed class TemplateLibraryItem
{
    public string TemplateId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public int VmCount { get; init; }

    public string SchemaVersion { get; init; } = string.Empty;

    public int TemplateRevision { get; init; }

    public string FilePath { get; init; } = string.Empty;
}

public sealed class TemplateEditorDocument
{
    public LabTemplate Template { get; init; } = new();

    public string? SourceFilePath { get; init; }
}

public sealed class TemplateValidationSummaryResult
{
    public bool IsValid { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

public sealed class TemplateOperationResult
{
    public bool Success { get; init; }

    public string OperationId { get; init; } = string.Empty;

    public string UserMessage { get; init; } = string.Empty;

    public string? FilePath { get; init; }
}
