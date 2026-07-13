namespace LabAssistant.WinUI.ViewModels.Templates.Builder;

/// <summary>
/// A single line in the Builder's bottom issues bar - the live validation list that replaced the
/// retired Review step. Each row carries one blocker or warning message plus a severity flag so the
/// view can colour the leading label (blockers critical, warnings neutral). Rows are immutable and
/// rebuilt on every projection, so the bindings are one-time.
/// </summary>
public sealed class BuilderValidationIssueRow
{
    public BuilderValidationIssueRow(string message, bool isBlocker)
    {
        Message = message ?? string.Empty;
        IsBlocker = isBlocker;
        SeverityLabel = isBlocker ? "Blocker" : "Warning";
    }

    /// <summary>The human-readable validation message.</summary>
    public string Message { get; }

    /// <summary>True when this issue blocks saving; false for a non-blocking warning.</summary>
    public bool IsBlocker { get; }

    /// <summary>Short severity caption rendered before the message ("Blocker" / "Warning").</summary>
    public string SeverityLabel { get; }
}
