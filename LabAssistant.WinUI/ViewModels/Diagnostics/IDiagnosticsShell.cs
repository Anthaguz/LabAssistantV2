namespace LabAssistant.WinUI.ViewModels.Diagnostics;

/// <summary>
/// Narrow, typed seam the Diagnostics overview view model uses to reach capability-level behavior
/// owned by the hosting <c>DiagnosticsPage</c> (subview navigation and cross-subview status
/// reporting). Implemented by the page; replaces the former delegate-bag shell bridge.
/// </summary>
internal interface IDiagnosticsShell
{
    /// <summary>Switches the Diagnostics capability to the Logs subview.</summary>
    void ShowLogs();

    /// <summary>
    /// Surfaces a support-export status message on the capability's logs status line, matching the
    /// prior behavior where opening the log location reported into the logs workspace.
    /// </summary>
    void ReportSupportStatus(string statusText);
}
