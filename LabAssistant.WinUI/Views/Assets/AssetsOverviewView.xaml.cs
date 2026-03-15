using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Assets;

public sealed partial class AssetsOverviewView : UserControl
{
    public event EventHandler? OpenBaseDisksRequested;

    public event EventHandler? OpenSwitchesRequested;

    public AssetsOverviewView()
    {
        InitializeComponent();
    }

    public void UpdateSummary(string baseDisksSummaryText, string switchesSummaryText)
    {
        AssetsOverviewBaseDisksSummaryTextBlock.Text = baseDisksSummaryText;
        AssetsOverviewSwitchesSummaryTextBlock.Text = switchesSummaryText;
    }

    private void AssetsOverviewOpenBaseDisksButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        OpenBaseDisksRequested?.Invoke(this, EventArgs.Empty);
    }

    private void AssetsOverviewOpenSwitchesButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        OpenSwitchesRequested?.Invoke(this, EventArgs.Empty);
    }
}
