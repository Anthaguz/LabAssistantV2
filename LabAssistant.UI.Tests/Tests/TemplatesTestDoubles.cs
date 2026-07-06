using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LabAssistant.Business.Templates;

namespace LabAssistant.UI.Tests.Tests;

/// <summary>
/// Configurable fake <see cref="ITemplatesCapabilityService"/> for the Templates Library and Editor
/// view model tests. Only the members the migrated view models call are wired; the rest throw so an
/// unexpected call surfaces loudly.
/// </summary>
internal sealed class RecordingTemplatesCapabilityService : ITemplatesCapabilityService
{
    public TemplateLibraryLoadResult LibraryResult { get; set; } = new();
    public Exception? LoadLibraryException { get; set; }
    public Func<string, TemplateEditorDocument>? LoadForEditorFactory { get; set; }
    public Exception? LoadForEditorException { get; set; }
    public TemplateEditorDocument CreateDraftDocument { get; set; } = new();
    public TemplateOperationResult SaveResult { get; set; } = new() { Success = true, UserMessage = "Saved." };
    public TemplateValidationSummaryResult ValidateResult { get; set; } = new() { IsValid = true };
    public TemplateOperationResult DeleteResult { get; set; } = new() { Success = true, UserMessage = "Deleted." };

    public int SaveCallCount { get; private set; }
    public int DeleteCallCount { get; private set; }
    public string? LastDeletedFilePath { get; private set; }
    public string? LastSavedTargetFilePath { get; private set; }
    public string? LastSearchText { get; private set; }

    public Task<TemplateLibraryLoadResult> LoadLibraryAsync(string? searchText = null, CancellationToken cancellationToken = default)
    {
        LastSearchText = searchText;
        if (LoadLibraryException is not null)
        {
            throw LoadLibraryException;
        }

        return Task.FromResult(LibraryResult);
    }

    public Task<TemplateEditorDocument> CreateDraftAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDraftDocument);

    public Task<TemplateEditorDocument> LoadForEditorAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (LoadForEditorException is not null)
        {
            throw LoadForEditorException;
        }

        var document = LoadForEditorFactory?.Invoke(filePath)
            ?? new TemplateEditorDocument { Template = new(), SourceFilePath = filePath };
        return Task.FromResult(document);
    }

    public Task<TemplateOperationResult> SaveAsync(
        TemplateEditorDocument document,
        string? targetFilePath = null,
        bool saveAs = false,
        CancellationToken cancellationToken = default)
    {
        SaveCallCount++;
        LastSavedTargetFilePath = targetFilePath;
        return Task.FromResult(SaveResult);
    }

    public Task<TemplateValidationSummaryResult> ValidateAsync(TemplateEditorDocument document, CancellationToken cancellationToken = default)
        => Task.FromResult(ValidateResult);

    public Task<TemplateOperationResult> DeleteAsync(string filePath, CancellationToken cancellationToken = default)
    {
        DeleteCallCount++;
        LastDeletedFilePath = filePath;
        return Task.FromResult(DeleteResult);
    }

    public Task<TemplatesVhdxCatalogLoadResult> LoadVhdxCatalogOptionsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new TemplatesVhdxCatalogLoadResult());

    public Task<TemplateOperationResult> ImportAsync(string sourceFilePath, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<TemplateOperationResult> ExportAsync(string sourceFilePath, string destinationFilePath, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

/// <summary>Recording <see cref="ITemplatesLibraryHost"/> that captures the cross-subview calls the Library view model makes.</summary>
internal sealed class RecordingTemplatesLibraryHost : LabAssistant.WinUI.ViewModels.Templates.ITemplatesLibraryHost
{
    public List<(TemplateEditorDocument Document, string StatusText)> ShowInEditorCalls { get; } = new();
    public List<string> ReportedEditorStatuses { get; } = new();
    public List<TemplateEditorDocument> ShowInBuilderCalls { get; } = new();
    public int CreateBuilderDraftCallCount { get; private set; }
    public List<TemplateLibraryItem> DeleteConfirmationRequests { get; } = new();
    public bool ConfirmDeleteResult { get; set; }

    public Task ShowTemplateInEditorAsync(TemplateEditorDocument document, string statusText)
    {
        ShowInEditorCalls.Add((document, statusText));
        return Task.CompletedTask;
    }

    public void ReportEditorStatus(string statusText) => ReportedEditorStatuses.Add(statusText);

    public Task ShowTemplateInBuilderAsync(TemplateEditorDocument document)
    {
        ShowInBuilderCalls.Add(document);
        return Task.CompletedTask;
    }

    public Task CreateTemplateBuilderDraftAsync()
    {
        CreateBuilderDraftCallCount++;
        return Task.CompletedTask;
    }

    public Task<bool> ConfirmDeleteTemplateAsync(TemplateLibraryItem templateItem)
    {
        DeleteConfirmationRequests.Add(templateItem);
        return Task.FromResult(ConfirmDeleteResult);
    }
}

/// <summary>Recording <see cref="ITemplatesEditorHost"/> that captures the cross-subview calls the Editor view model makes.</summary>
internal sealed class RecordingTemplatesEditorHost : LabAssistant.WinUI.ViewModels.Templates.ITemplatesEditorHost
{
    public int NavigateToLibraryCallCount { get; private set; }
    public List<bool> ReloadLibraryCalls { get; } = new();
    public List<string> RemoveVmConfirmationRequests { get; } = new();
    public bool ConfirmRemoveVmResult { get; set; }

    public void NavigateToLibrary() => NavigateToLibraryCallCount++;

    public Task ReloadLibraryAsync(bool forceRefresh)
    {
        ReloadLibraryCalls.Add(forceRefresh);
        return Task.CompletedTask;
    }

    public Task<bool> ConfirmRemoveVmAsync(string vmName)
    {
        RemoveVmConfirmationRequests.Add(vmName);
        return Task.FromResult(ConfirmRemoveVmResult);
    }
}
