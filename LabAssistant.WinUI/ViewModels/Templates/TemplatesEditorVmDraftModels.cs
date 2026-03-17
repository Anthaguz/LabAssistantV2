namespace LabAssistant.WinUI.ViewModels.Templates;

internal sealed class TemplateVhdxCatalogOption
{
    public TemplateVhdxCatalogOption(
        string id,
        string path,
        string osName,
        string osVersion,
        int generation,
        string? signature)
    {
        Id = id;
        Path = path;
        OsName = osName;
        OsVersion = osVersion;
        Generation = generation;
        Signature = signature;
    }

    public string Id { get; }

    public string Path { get; }

    public string OsName { get; }

    public string OsVersion { get; }

    public int Generation { get; }

    public string? Signature { get; }

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

internal readonly record struct TemplatesEditorVmDraftState(
    string VmIdText,
    string VmName,
    string VmMemoryText,
    string VmCpuText,
    string VmVhdxIdText,
    string VmVhdPathText,
    string VmVhdxSignatureText,
    IReadOnlyList<string> SelectedSwitches,
    TemplateVhdxCatalogOption? SelectedVhdxCatalogOption,
    string VmSwitchGuidanceText,
    string VmVhdxGuidanceText,
    bool RequiresVhdxResolution,
    bool IsEditingNewVmEntry,
    bool HasChanges);

internal readonly record struct TemplatesEditorVmDraftSnapshot(
    string VmName,
    string VmMemoryText,
    string VmCpuText,
    IReadOnlyList<string> SelectedSwitches,
    TemplateVhdxCatalogOption? SelectedVhdxCatalogOption,
    bool RequiresVhdxResolution,
    string VhdxResolutionMessage,
    bool IsEditingNewVmEntry,
    bool HasChanges);
