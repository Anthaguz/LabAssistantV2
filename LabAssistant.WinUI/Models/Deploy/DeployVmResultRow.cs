namespace LabAssistant.WinUI.Models.Deploy;

public sealed record DeployVmResultRow(
    string VmName,
    string Status,
    string Summary,
    int ProgressPercent,
    IReadOnlyList<DeployTimelineStepRow> TimelineSteps,
    bool IsExpandable = true)
{
    public string DisplaySummary
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Summary))
            {
                return string.Empty;
            }

            var normalizedStatus = Status.Trim();
            var normalizedSummary = Summary.Trim();
            if (string.Equals(normalizedSummary, normalizedStatus, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            if ((string.Equals(normalizedStatus, "Succeeded", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(normalizedStatus, "Completed", StringComparison.OrdinalIgnoreCase)) &&
                normalizedSummary.StartsWith("Completed", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return normalizedSummary;
        }
    }
}
