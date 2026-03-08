using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Diagnostics;

public sealed partial class DiagnosticsOverviewView : UserControl
{
    public DiagnosticsOverviewView()
    {
        InitializeComponent();
    }

    public Button DiagnosticsOverviewOpenLogsButtonControl => DiagnosticsOverviewOpenLogsButton;

    public TextBlock DiagnosticsOverviewLogsSummaryTextBlockControl => DiagnosticsOverviewLogsSummaryTextBlock;
}
