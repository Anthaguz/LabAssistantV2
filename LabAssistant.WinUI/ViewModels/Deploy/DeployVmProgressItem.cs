using LabAssistant.WinUI.Models.Deploy;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.ViewModels.Deploy;

public sealed class DeployVmProgressItem
{
    public DeployVmProgressItem(
        string vmName,
        string status,
        string summary,
        int progressPercent,
        IReadOnlyList<DeployTimelineStepRow> timelineSteps,
        bool isExpandable)
    {
        VmName = vmName;
        Status = status;
        Summary = summary;
        ProgressPercent = progressPercent;
        TimelineSteps = timelineSteps;
        IsExpandable = isExpandable;
    }

    public string VmName { get; }

    public string Status { get; }

    public string Summary { get; }

    public int ProgressPercent { get; }

    public IReadOnlyList<DeployTimelineStepRow> TimelineSteps { get; }

    public bool IsExpandable { get; }

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

    public Visibility SummaryVisibility => string.IsNullOrWhiteSpace(DisplaySummary) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ExpanderVisibility => IsExpandable ? Visibility.Visible : Visibility.Collapsed;

    public Visibility FlatCardVisibility => IsExpandable ? Visibility.Collapsed : Visibility.Visible;

    public static DeployVmProgressItem FromRow(DeployVmResultRow row) => new(
        row.VmName,
        row.Status,
        row.Summary,
        row.ProgressPercent,
        row.TimelineSteps,
        row.IsExpandable);
}
