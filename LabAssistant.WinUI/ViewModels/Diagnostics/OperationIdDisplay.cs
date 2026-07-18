using System;
using LabAssistant.Services.Logging;

namespace LabAssistant.WinUI.ViewModels.Diagnostics;

/// <summary>
/// Presentational formatting for structured-log operation ids. Kept free of any WinUI type so the
/// diagnostics view model (which is unit-tested outside the app host) can share the exact wording used
/// by the grid's value converter. Filtering always runs against the raw id, so this is display-only.
/// </summary>
public static class OperationIdDisplay
{
    /// <summary>The text shown in place of the ambient operation-id sentinel.</summary>
    public const string AmbientDisplayText = "(background)";

    /// <summary>
    /// Maps a raw operation id to its display form: the ambient sentinel (stamped on background
    /// <c>diag.*</c> traces not tied to a user operation) becomes <see cref="AmbientDisplayText"/>;
    /// every other value is returned unchanged.
    /// </summary>
    public static string ToDisplay(string? operationId)
    {
        return string.Equals(operationId, StructuredLoggingDefaults.AmbientOperationId, StringComparison.Ordinal)
            ? AmbientDisplayText
            : operationId ?? string.Empty;
    }
}
