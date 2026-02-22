using LabAssistant.Models.Catalog;

namespace LabAssistant.Models.Templates;

public interface ILabTemplateStore
{
    IReadOnlyList<string> LastLoadWarnings { get; }

    LabTemplateLoadResult LoadFromFolder(string templatesFolder, IEnumerable<VhdxCatalogItem> catalogItems);
    LabTemplate LoadFromFile(string filePath);
    void SaveToFile(string filePath, LabTemplate template);
    string SaveToFolder(string folderPath, string templateName, LabTemplate template);
}
