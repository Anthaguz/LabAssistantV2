namespace LabAssistant.UITesting.Infrastructure;

/// <summary>
/// Resolves the on-disk locations the app reads and writes under
/// %APPDATA%\LabAssistant. The harness seeds the base-disk catalog and cleans
/// up VM/disk artifacts here, so these must mirror the app's AppPaths exactly.
/// </summary>
public sealed class AppDataLocations
{
    public AppDataLocations(string? appRoot = null)
    {
        var baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        AppRoot = string.IsNullOrWhiteSpace(appRoot)
            ? Path.Combine(baseFolder, "LabAssistant")
            : appRoot;
        CatalogFolder = Path.Combine(AppRoot, "Catalog");
        CatalogPath = Path.Combine(CatalogFolder, "vhdx-catalog.json");
        VmBasePath = Path.Combine(AppRoot, "VMs");
        DifferencingDiskBasePath = Path.Combine(AppRoot, "Disks");
    }

    public string AppRoot { get; }
    public string CatalogFolder { get; }
    public string CatalogPath { get; }
    public string VmBasePath { get; }
    public string DifferencingDiskBasePath { get; }
}
