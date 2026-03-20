using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;

namespace LabAssistant.WinUI.Views.Diagnostics;

public readonly record struct DiagnosticsOverviewViewState(
    string LogsSummaryText,
    string SupportSummaryText);

public sealed partial class DiagnosticsOverviewView : UserControl
{
    public event RoutedEventHandler? OpenLogsRequested;

    public event RoutedEventHandler? OpenSupportExportRequested;

    public DiagnosticsOverviewView()
    {
        InitializeComponent();
    }

    public void ApplyWorkspaceState(DiagnosticsOverviewViewState state)
    {
        DiagnosticsOverviewLogsSummaryTextBlock.Text = state.LogsSummaryText;
        DiagnosticsOverviewSupportSummaryTextBlock.Text = state.SupportSummaryText;
    }

    private void DiagnosticsOverviewOpenLogsButton_Click(object sender, RoutedEventArgs e)
    {
        OpenLogsRequested?.Invoke(this, e);
    }

    private void DiagnosticsOverviewOpenSupportExportButton_Click(object sender, RoutedEventArgs e)
    {
        OpenSupportExportRequested?.Invoke(this, e);
    }
}
