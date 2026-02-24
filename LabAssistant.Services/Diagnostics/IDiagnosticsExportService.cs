namespace LabAssistant.Services.Diagnostics;

public interface IDiagnosticsExportService
{
    IReadOnlyList<DiagnosticsExportOptionDefinition> GetAvailableOptions();
    DiagnosticsExportResult Export(DiagnosticsExportRequest request);
}
