using System;

namespace LabAssistant.Models.Templates;

public enum TemplateSchemaCompatibilityStatus
{
    Allow,
    AllowWithWarning,
    AllowWithUpcast,
    Block
}

public sealed class TemplateSchemaCompatibilityDecision
{
    public TemplateSchemaCompatibilityDecision(
        TemplateSchemaCompatibilityStatus status,
        Version? sourceVersion,
        string? warning,
        string? error)
    {
        Status = status;
        SourceVersion = sourceVersion;
        Warning = warning;
        Error = error;
    }

    public TemplateSchemaCompatibilityStatus Status { get; }
    public Version? SourceVersion { get; }
    public string? Warning { get; }
    public string? Error { get; }
    public bool IsBlocked => Status == TemplateSchemaCompatibilityStatus.Block;
}

public static class TemplateSchemaCompatibilityGate
{
    public static TemplateSchemaCompatibilityDecision Evaluate(
        string? schemaVersion,
        Version currentSchemaVersion,
        bool isLegacyV0 = false)
    {
        var currentMajor = currentSchemaVersion.Major;

        if (isLegacyV0)
        {
            if (currentMajor == 0)
            {
                return new TemplateSchemaCompatibilityDecision(
                    TemplateSchemaCompatibilityStatus.Block,
                    null,
                    null,
                    "Legacy version 'v0' is not supported by this app schema baseline.");
            }

            var previousMajorVersion = new Version(currentMajor - 1, 0, 0);
            return new TemplateSchemaCompatibilityDecision(
                TemplateSchemaCompatibilityStatus.AllowWithUpcast,
                previousMajorVersion,
                "Legacy template format 'v0' is within support window and will be migrated to current schema.",
                null);
        }

        if (string.IsNullOrWhiteSpace(schemaVersion) || !Version.TryParse(schemaVersion, out var sourceVersion))
        {
            return new TemplateSchemaCompatibilityDecision(
                TemplateSchemaCompatibilityStatus.Block,
                null,
                null,
                $"Template schemaVersion '{schemaVersion}' is invalid. Use semantic version format like '{currentSchemaVersion}'.");
        }

        if (sourceVersion.Major > currentMajor)
        {
            return new TemplateSchemaCompatibilityDecision(
                TemplateSchemaCompatibilityStatus.Block,
                sourceVersion,
                null,
                $"Template schemaVersion '{sourceVersion}' is newer than this LabAssistant version supports. Please update LabAssistant.");
        }

        if (sourceVersion.Major < currentMajor - 1)
        {
            return new TemplateSchemaCompatibilityDecision(
                TemplateSchemaCompatibilityStatus.Block,
                sourceVersion,
                null,
                $"Template schemaVersion '{sourceVersion}' is older than the supported window (N and N-1). Open and resave it in a newer LabAssistant release first.");
        }

        if (sourceVersion.Major == currentMajor - 1)
        {
            return new TemplateSchemaCompatibilityDecision(
                TemplateSchemaCompatibilityStatus.AllowWithUpcast,
                sourceVersion,
                $"Template schemaVersion '{sourceVersion}' is previous major and will be migrated to current schema '{currentSchemaVersion}'.",
                null);
        }

        if (sourceVersion > currentSchemaVersion)
        {
            return new TemplateSchemaCompatibilityDecision(
                TemplateSchemaCompatibilityStatus.AllowWithWarning,
                sourceVersion,
                $"Template schemaVersion '{sourceVersion}' is newer minor/patch than supported '{currentSchemaVersion}'. Continuing with compatible fields only.",
                null);
        }

        return new TemplateSchemaCompatibilityDecision(
            TemplateSchemaCompatibilityStatus.Allow,
            sourceVersion,
            null,
            null);
    }
}
