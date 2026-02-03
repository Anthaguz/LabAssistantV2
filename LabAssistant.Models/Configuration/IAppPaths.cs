namespace LabAssistant.Models.Configuration;

public interface IAppPaths
{
    string AppRoot { get; }
    string ConfigFolder { get; }
    string CatalogFolder { get; }
    string TemplatesFolder { get; }
    string LogsFolder { get; }
    string VmBasePath { get; }
    string DifferencingDiskBasePath { get; }
    string CatalogPath { get; }
}
