namespace LabAssistant.Models.Catalog;

/// <summary>
/// Catalog entry for a base VHDX image.
/// </summary>
public class VhdxCatalogItem
{
    /// <summary>
    /// Stable identifier used by templates (required).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Absolute or relative path to the VHDX file (required).
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// OS family or name, e.g., "Windows Server" (required).
    /// </summary>
    public string OsName { get; set; } = string.Empty;

    /// <summary>
    /// OS version/build label, e.g., "2022" (required).
    /// </summary>
    public string OsVersion { get; set; } = string.Empty;

    /// <summary>
    /// Hyper-V VM generation, typically 1 or 2 (required).
    /// </summary>
    public int Generation { get; set; }

    /// <summary>
    /// Optional size of the VHDX file in bytes (helps with signature matching).
    /// </summary>
    public long? SizeBytes { get; set; }

    /// <summary>
    /// Stable signature derived from OS info + generation (+ optional size).
    /// </summary>
    public string? Signature { get; set; }

    /// <summary>
    /// Freeform notes about the image (optional).
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Optional bootstrap assumptions associated with the base image for V2 planning.
    /// </summary>
    public VhdxBootstrapProfile? BootstrapProfile { get; set; }
}
