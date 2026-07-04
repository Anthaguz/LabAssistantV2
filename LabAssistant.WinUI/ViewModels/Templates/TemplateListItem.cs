using LabAssistant.Business.Templates;
using LabAssistant.Models.Templates;

namespace LabAssistant.WinUI.ViewModels.Templates;

public sealed class TemplateListItem
{
    internal TemplateListItem(TemplateLibraryItem sourceItem, DateTimeOffset lastModified)
    {
        SourceItem = sourceItem;
        Id = sourceItem.TemplateId;
        Name = sourceItem.Name;
        Description = sourceItem.Description;
        SlotCount = sourceItem.VmCount;
        LastModified = lastModified;
        FilePath = sourceItem.FilePath;
        ExecutionEngine = sourceItem.ExecutionEngine;
    }

    public string Id { get; }

    public string Name { get; }

    public string Description { get; }

    public int SlotCount { get; }

    public DateTimeOffset LastModified { get; }

    public string FilePath { get; }

    public TemplateExecutionEngine ExecutionEngine { get; }

    public string DescriptionOrFallback => string.IsNullOrWhiteSpace(Description)
        ? "No description provided."
        : Description;

    public string LastModifiedDisplay => LastModified == default
        ? "Last modified unavailable"
        : $"Last modified {LastModified.LocalDateTime:g}";

    public string SlotCountDisplay => SlotCount == 1 ? "1 VM slot" : $"{SlotCount} VM slots";

    internal TemplateLibraryItem SourceItem { get; }

    internal static TemplateListItem FromLibraryItem(TemplateLibraryItem sourceItem)
    {
        var lastModified = File.Exists(sourceItem.FilePath)
            ? File.GetLastWriteTime(sourceItem.FilePath)
            : default;
        return new TemplateListItem(sourceItem, lastModified);
    }
}
