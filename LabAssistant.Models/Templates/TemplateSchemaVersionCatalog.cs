using System;

namespace LabAssistant.Models.Templates;

/// <summary>
/// Centralizes supported schema-version constants and execution-engine routing rules.
/// </summary>
public static class TemplateSchemaVersionCatalog
{
    public const string V1SchemaVersion = "1.0.0";
    public const string V2SchemaVersion = "2.0.0";

    public static readonly Version V1Schema = new(1, 0, 0);
    public static readonly Version V2Schema = new(2, 0, 0);

    /// <summary>
    /// Highest schema major the current app can understand for template loading/routing.
    /// </summary>
    public static readonly Version LatestSupportedSchema = V2Schema;

    public static TemplateExecutionEngine Classify(string? schemaVersion, bool isLegacyV0 = false)
    {
        if (isLegacyV0)
        {
            return TemplateExecutionEngine.V1Deployment;
        }

        if (!Version.TryParse(schemaVersion, out var version))
        {
            return TemplateExecutionEngine.V1Deployment;
        }

        return version.Major >= V2Schema.Major
            ? TemplateExecutionEngine.V2UnifiedPlanning
            : TemplateExecutionEngine.V1Deployment;
    }

    public static string GetCanonicalSchemaVersion(TemplateExecutionEngine executionEngine)
    {
        return executionEngine == TemplateExecutionEngine.V2UnifiedPlanning
            ? V2SchemaVersion
            : V1SchemaVersion;
    }
}
