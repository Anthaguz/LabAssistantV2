using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using LabAssistant.Business.Deployment;

namespace LabAssistant.Views;

public partial class DryRunResultsPage : Page
{
    private readonly DryRunDeploymentResult _result;

    public DryRunResultsPage(DryRunDeploymentResult result)
    {
        _result = result;
        InitializeComponent();
        SummaryText.Text = $"Steps: {result.Plan.Steps.Count}";
        StepsList.ItemsSource = result.Plan.Steps;
    }

    private void ExportPlan_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            FileName = "deployment-plan.json"
        };

        if (dialog.ShowDialog() == true)
        {
            var json = JsonSerializer.Serialize(_result.Plan, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dialog.FileName, json);
        }
    }

    private void CopyLogs_Click(object sender, RoutedEventArgs e)
    {
        var logs = _result.Logs.Count == 0
            ? "No logs available."
            : string.Join(System.Environment.NewLine, _result.Logs);
        System.Windows.Clipboard.SetText(logs);
    }
}
