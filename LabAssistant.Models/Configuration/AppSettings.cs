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
}
