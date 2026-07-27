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
        TemplatesFolder = Path.Combine(AppRoot, "Templates");
        ConfigFolder = Path.Combine(AppRoot, "Config");
        CredentialSlotsPath = Path.Combine(ConfigFolder, "credential-slots.json");
        LogsFolder = Path.Combine(AppRoot, "Logs");
        StructuredEventsPath = Path.Combine(LogsFolder, "structured-events.jsonl");
        VmBasePath = Path.Combine(AppRoot, "VMs");
        DifferencingDiskBasePath = Path.Combine(AppRoot, "Disks");
    }

    public string AppRoot { get; }
    public string CatalogFolder { get; }
    public string CatalogPath { get; }

    /// <summary>
    /// Where the app persists saved lab templates (one JSON per template). The harness
    /// seeds a tagged V2 template here and sweeps tagged template files after a run, so
    /// this must mirror the app's AppPaths.TemplatesFolder exactly.
    /// </summary>
    public string TemplatesFolder { get; }

    /// <summary>
    /// Where the app persists deploy credential slots (DPAPI-protected, one JSON array).
    /// The DC scenario clears its own slot here before a run so the credential UI always
    /// prompts fresh - the app caches credentials by slot key, so a stale value would
    /// otherwise silently deploy with the wrong password - and restores the prior store
    /// state afterward. Must mirror the app's AppPaths config folder exactly.
    /// </summary>
    public string ConfigFolder { get; }

    /// <summary>Full path to the app's credential-slots.json store.</summary>
    public string CredentialSlotsPath { get; }

    /// <summary>
    /// Where the app writes its structured event log (JSONL, one event per line). The
    /// harness reads this on a deploy failure to attach the exact slice of app-side
    /// events (operationId, stepKey, result, error) to the finding, so a person or an
    /// agent triaging later has the app's own account of why the deploy failed - not
    /// just a screenshot. Must mirror the app's AppPaths.LogsFolder exactly.
    /// </summary>
    public string LogsFolder { get; }

    /// <summary>Full path to the app's active structured-events.jsonl file.</summary>
    public string StructuredEventsPath { get; }

    public string VmBasePath { get; }
    public string DifferencingDiskBasePath { get; }
}
