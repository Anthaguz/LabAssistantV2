namespace LabAssistant.Services.Diagnostics;

public sealed class DiagnosticsExportOptions
{
    public bool IncludeFullTemplateFile { get; set; }
    public bool IncludeDetailedEnvironmentInformation { get; set; }
}

public sealed class DiagnosticsExportOptionDefinition
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}
