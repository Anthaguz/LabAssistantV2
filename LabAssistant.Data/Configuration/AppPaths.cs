using System;
using System.IO;
using LabAssistant.Models.Configuration;

namespace LabAssistant.Data.Configuration;

public sealed class AppPaths : IAppPaths
{
    public AppPaths(string? appRoot = null)
    {
        var baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        AppRoot = string.IsNullOrWhiteSpace(appRoot)
            ? Path.Combine(baseFolder, "LabAssistant")
            : appRoot;
        ConfigFolder = Path.Combine(AppRoot, "Config");
        CatalogFolder = Path.Combine(AppRoot, "Catalog");
        TemplatesFolder = Path.Combine(AppRoot, "Templates");
        LogsFolder = Path.Combine(AppRoot, "Logs");
        VmBasePath = Path.Combine(AppRoot, "VMs");
        DifferencingDiskBasePath = Path.Combine(AppRoot, "Disks");
        CatalogPath = Path.Combine(CatalogFolder, "vhdx-catalog.json");
    }

    public string AppRoot { get; }
    public string ConfigFolder { get; }
    public string CatalogFolder { get; }
    public string TemplatesFolder { get; }
    public string LogsFolder { get; }
    public string VmBasePath { get; }
    public string DifferencingDiskBasePath { get; }
    public string CatalogPath { get; }
}
