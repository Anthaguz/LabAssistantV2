namespace LabAssistant.WinUI.ViewModels.Templates;

internal sealed class TemplateVhdxCatalogOption
{
    public TemplateVhdxCatalogOption(
        string id,
        string path,
        string osName,
        string osVersion,
        int generation,
        string? signature,
        string? bootstrapLocalUser = null)
    {
        Id = id;
        Path = path;
        OsName = osName;
        OsVersion = osVersion;
        Generation = generation;
        Signature = signature;
        BootstrapLocalUser = bootstrapLocalUser;
    }

    public string Id { get; }

    public string Path { get; }

    public string OsName { get; }

    public string OsVersion { get; }

    public int Generation { get; }

    public string? Signature { get; }

    /// <summary>Gets the local bootstrap account (base-disk baked-in local admin) advertised by the catalog, if any.</summary>
    public string? BootstrapLocalUser { get; }

    public string DisplayLabel => string.IsNullOrWhiteSpace(OsName) && string.IsNullOrWhiteSpace(OsVersion)
        ? $"{Id} (Gen{Generation})"
        : $"{OsName} {OsVersion} (Gen{Generation})";

    public override string ToString() => $"{DisplayLabel} - {Id}";
}

internal readonly record struct TemplateVhdxNormalizationResult(
    bool RequiresUserResolution,
    TemplateVhdxCatalogOption? EffectiveOption,
    string Message,
    string EffectiveSourceLabel);
