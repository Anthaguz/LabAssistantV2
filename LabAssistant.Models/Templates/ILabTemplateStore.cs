using LabAssistant.Models.Catalog;

namespace LabAssistant.Models.Templates;

public interface ILabTemplateStore
{
    LabTemplateLoadResult LoadFromFolder(string templatesFolder, IEnumerable<VhdxCatalogItem> catalogItems);
    LabTemplate LoadFromFile(string filePath);
    void SaveToFile(string filePath, LabTemplate template);
    string SaveToFolder(string folderPath, string templateName, LabTemplate template);
}
