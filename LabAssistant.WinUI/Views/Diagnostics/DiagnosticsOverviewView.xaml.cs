using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Diagnostics;

public sealed partial class DiagnosticsOverviewView : UserControl
{
    public event EventHandler? OpenLogsRequested;

    public event EventHandler? OpenSupportExportRequested;

    public DiagnosticsOverviewView()
    {
        InitializeComponent();
    }

    public void UpdateSummary(string logsSummaryText, string supportSummaryText)
    {
        DiagnosticsOverviewLogsSummaryTextBlock.Text = logsSummaryText;
        DiagnosticsOverviewSupportSummaryTextBlock.Text = supportSummaryText;
    }

    private void DiagnosticsOverviewOpenLogsButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        OpenLogsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void DiagnosticsOverviewOpenSupportExportButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        OpenSupportExportRequested?.Invoke(this, EventArgs.Empty);
    }
}
