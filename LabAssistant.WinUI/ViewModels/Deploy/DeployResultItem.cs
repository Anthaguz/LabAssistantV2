using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.ViewModels.Deploy;

public sealed class DeployResultItem
{
    public DeployResultItem(string vmName, string status, string summary)
    {
        VmName = vmName;
        Status = status;
        Summary = summary;
    }

    public string VmName { get; }

    public string Status { get; }

    public string Summary { get; }

    public string DisplaySummary
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Summary) || string.Equals(Summary.Trim(), Status.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return Summary.Trim();
        }
    }

    public Visibility SummaryVisibility => string.IsNullOrWhiteSpace(DisplaySummary) ? Visibility.Collapsed : Visibility.Visible;
}
