namespace LabAssistant.Services.Diagnostics;

public sealed class DiagnosticsOperationContext
{
    public string OperationId { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public string? Result { get; set; }
    public string? TerminalState { get; set; }
    public string? TemplateId { get; set; }
    public string? TemplateName { get; set; }
    public int? VmCount { get; set; }
    public int? CleanupVmCount { get; set; }
    public int? ResidualVmCount { get; set; }
}

public sealed class DiagnosticsExportRequest
{
    public string DestinationZipPath { get; set; } = string.Empty;
    public DiagnosticsExportOptions Options { get; set; } = new();
    public DiagnosticsOperationContext? OperationContext { get; set; }
    public string? TemplateFilePath { get; set; }
    public string? StructuredLogPathOverride { get; set; }
}

public sealed class DiagnosticsExportResult
{
    public string BundlePath { get; init; } = string.Empty;
    public List<string> IncludedArtifacts { get; } = new();
    public List<string> Warnings { get; } = new();
}
