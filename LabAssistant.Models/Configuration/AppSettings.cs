using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LabAssistant.Models.Configuration;

public class AppSettings
{
    public string TemplateFolder { get; set; } = string.Empty;            // Where lab templates (JSON) are stored
    public string LogFolder { get; set; } = string.Empty;                 // Where logs go
    public string VmBasePath { get; set; } = string.Empty;                // Where VMs and their config/data are stored
    public string DifferencingDiskBasePath { get; set; } = string.Empty;  // Where the base image (parent VHDX) is stored

    public int DefaultVmMemoryMb { get; set; } = 2048;
    public int DefaultCpuCount { get; set; } = 2;

    public string CatalogPath { get; set; } = string.Empty;

    public List<TemplateVhdxSelection> TemplateSelections { get; set; } = new();

    public bool PerVmFailFast { get; set; } = true;
    public bool StopAllOnAnyVmFailure { get; set; } = false;
    public List<string> NonBlockingOptionalSteps { get; set; } = new();

    public string MachineDeletionPolicy { get; set; } = "AskEveryTime";

    /// <summary>
    /// User-selected colour theme. Serialized as its string name so the settings file stays
    /// human-readable and stable across enum reordering. Defaults to <see cref="AppTheme.Light"/>,
    /// which also covers older settings files written before this field existed.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AppTheme Theme { get; set; } = AppTheme.Light;
}
