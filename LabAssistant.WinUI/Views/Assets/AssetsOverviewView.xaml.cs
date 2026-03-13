using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Assets;

public sealed partial class AssetsOverviewView : UserControl
{
    public AssetsOverviewView()
    {
        InitializeComponent();
    }

    public Button AssetsOverviewOpenBaseDisksButtonControl => AssetsOverviewOpenBaseDisksButton;

    public Button AssetsOverviewOpenSwitchesButtonControl => AssetsOverviewOpenSwitchesButton;

    public TextBlock AssetsOverviewBaseDisksSummaryTextBlockControl => AssetsOverviewBaseDisksSummaryTextBlock;

    public TextBlock AssetsOverviewSwitchesSummaryTextBlockControl => AssetsOverviewSwitchesSummaryTextBlock;

    public void SetBaseDisksSummary(string text)
    {
        AssetsOverviewBaseDisksSummaryTextBlock.Text = text;
    }

    public void SetSwitchesSummary(string text)
    {
        AssetsOverviewSwitchesSummaryTextBlock.Text = text;
    }
}
