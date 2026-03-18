using LabAssistant.Business.Templates;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Deploy;

public readonly record struct DeployFromTemplateViewState(
    string TemplateSummaryText,
    string TemplateRemediationText,
    string ActionStatusText,
    string ReadinessSummaryText,
    string SharedIssuesSummaryText,
    string GlobalIssuesBadgeText,
    string LifecycleState,
    int ProgressPercent,
    string ProgressSummary);

public sealed partial class DeployFromTemplateView : UserControl
{
    public DeployFromTemplateView()
    {
        InitializeComponent();
        DeployTemplateSelectorComboBox.SelectionChanged += OnTemplateSelectionChanged;
        DeployReloadTemplatesButton.Click += OnDeployReloadTemplatesRequested;
        DeployEvaluateReadinessButton.Click += OnDeployEvaluateReadinessRequested;
        DeployOpenResultsPanelButton.Click += OnDeployOpenResultsPanelRequested;
        DeployResolveSuggestionsButton.Click += OnDeployResolveSuggestionsRequested;
        DeployOpenTemplateEditorButton.Click += OnDeployOpenTemplateEditorRequested;
        DeployStartButton.Click += OnDeployStartRequested;
    }

    public event SelectionChangedEventHandler? TemplateSelectionChanged;

    public event RoutedEventHandler? ReloadTemplatesRequested;

    public event RoutedEventHandler? EvaluateReadinessRequested;

    public event RoutedEventHandler? OpenResultsPanelRequested;

    public event RoutedEventHandler? ResolveSuggestionsRequested;

    public event RoutedEventHandler? OpenTemplateEditorRequested;

    public event RoutedEventHandler? StartDeployRequested;

    public TemplateLibraryItem? SelectedTemplateLibraryItem
    {
        get => DeployTemplateSelectorComboBox.SelectedItem as TemplateLibraryItem;
        set => DeployTemplateSelectorComboBox.SelectedItem = value;
    }

    public void SetTemplateItemsSource(object? itemsSource)
    {
        DeployTemplateSelectorComboBox.ItemsSource = itemsSource;
    }

    public void SetTemplateSelectorDisplayMemberPath(string displayMemberPath)
    {
        DeployTemplateSelectorComboBox.DisplayMemberPath = displayMemberPath;
    }

    public void SetSharedIssueSummariesItemsSource(object? itemsSource)
    {
        DeploySharedIssuesListView.ItemsSource = itemsSource;
    }

    public void ApplyWorkspaceState(DeployFromTemplateViewState state)
    {
        DeployTemplateSummaryTextBlock.Text = state.TemplateSummaryText;
        DeployTemplateRemediationTextBlock.Text = state.TemplateRemediationText;
        DeployActionStatusTextBlock.Text = state.ActionStatusText;
        DeployReadinessSummaryTextBlock.Text = state.ReadinessSummaryText;
        DeploySharedIssuesSummaryTextBlock.Text = state.SharedIssuesSummaryText;
        DeployGlobalIssuesBadgeTextBlock.Text = state.GlobalIssuesBadgeText;
        DeployOverallStateTextBlock.Text = state.LifecycleState;
        DeployProgressBar.Value = state.ProgressPercent;
        DeployProgressSummaryTextBlock.Text = state.ProgressSummary;
    }

    public void SetInteractionState(
        bool isTemplateSelectorEnabled,
        bool isReloadEnabled,
        bool isEvaluateReadinessEnabled,
        bool isResolveSuggestionsEnabled,
        bool isOpenTemplateEditorEnabled,
        bool isStartDeployEnabled)
    {
        DeployTemplateSelectorComboBox.IsEnabled = isTemplateSelectorEnabled;
        DeployReloadTemplatesButton.IsEnabled = isReloadEnabled;
        DeployEvaluateReadinessButton.IsEnabled = isEvaluateReadinessEnabled;
        DeployResolveSuggestionsButton.IsEnabled = isResolveSuggestionsEnabled;
        DeployOpenTemplateEditorButton.IsEnabled = isOpenTemplateEditorEnabled;
        DeployStartButton.IsEnabled = isStartDeployEnabled;
    }

    public void SetResultsPanelLauncherState(string buttonText, bool isEnabled, string summaryText)
    {
        DeployOpenResultsPanelButton.Content = buttonText;
        DeployOpenResultsPanelButton.IsEnabled = isEnabled;
        DeployResultsPanelSummaryTextBlock.Text = summaryText;
    }

    private void OnTemplateSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        TemplateSelectionChanged?.Invoke(this, e);
    }

    private void OnDeployReloadTemplatesRequested(object sender, RoutedEventArgs e)
    {
        ReloadTemplatesRequested?.Invoke(this, e);
    }

    private void OnDeployEvaluateReadinessRequested(object sender, RoutedEventArgs e)
    {
        EvaluateReadinessRequested?.Invoke(this, e);
    }

    private void OnDeployOpenResultsPanelRequested(object sender, RoutedEventArgs e)
    {
        OpenResultsPanelRequested?.Invoke(this, e);
    }

    private void OnDeployResolveSuggestionsRequested(object sender, RoutedEventArgs e)
    {
        ResolveSuggestionsRequested?.Invoke(this, e);
    }

    private void OnDeployOpenTemplateEditorRequested(object sender, RoutedEventArgs e)
    {
        OpenTemplateEditorRequested?.Invoke(this, e);
    }

    private void OnDeployStartRequested(object sender, RoutedEventArgs e)
    {
        StartDeployRequested?.Invoke(this, e);
    }
}
