using LabAssistant.Business.Templates;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using LabAssistant.WinUI.Models.Deploy;

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
        DeployV2CredentialSlotsListView.SelectionChanged += OnDeployV2CredentialSlotSelectionChanged;
        DeployV2SaveCredentialSlotButton.Click += OnDeployV2SaveCredentialSlotRequested;
        DeployV2DisableFirewallCheckBox.Click += OnDeployV2BaseRemoteAccessOptionsChanged;
        DeployV2DisableRdpNlaCheckBox.Click += OnDeployV2BaseRemoteAccessOptionsChanged;
    }

    public event SelectionChangedEventHandler? TemplateSelectionChanged;

    public event RoutedEventHandler? ReloadTemplatesRequested;

    public event RoutedEventHandler? EvaluateReadinessRequested;

    public event RoutedEventHandler? OpenResultsPanelRequested;

    public event RoutedEventHandler? ResolveSuggestionsRequested;

    public event RoutedEventHandler? OpenTemplateEditorRequested;

    public event RoutedEventHandler? StartDeployRequested;

    public event SelectionChangedEventHandler? V2CredentialSlotSelectionChanged;

    public event RoutedEventHandler? SaveV2CredentialSlotRequested;

    public event RoutedEventHandler? V2BaseRemoteAccessOptionsChanged;

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

    public void SetV2BlockersItemsSource(object? itemsSource)
    {
        DeployV2BlockersListView.ItemsSource = itemsSource;
    }

    public void SetV2CredentialSlotsItemsSource(object? itemsSource)
    {
        DeployV2CredentialSlotsListView.ItemsSource = itemsSource;
    }

    public void SetV2WavesItemsSource(object? itemsSource)
    {
        DeployV2WavesListView.ItemsSource = itemsSource;
    }

    public void SetV2DiagnosticsItemsSource(object? itemsSource)
    {
        DeployV2DiagnosticsListView.ItemsSource = itemsSource;
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

    public void SetEvaluateButtonText(string text)
    {
        DeployEvaluateReadinessButton.Content = text;
    }

    public void SetV2ReviewVisibility(bool isVisible)
    {
        DeployV2ReviewPanel.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    internal void ApplyV2ReviewState(bool isVisible, string statusText, DeployV2PlanSummaryRow? summary)
    {
        SetV2ReviewVisibility(isVisible);
        DeployV2StatusTextBlock.Text = statusText;
        DeployV2PlanSummaryTextBlock.Text = summary is null
            ? "No V2 plan has been projected yet."
            : $"{summary.TemplateName} | {summary.ExecutionEngine} | Profile: {summary.DeploymentProfile}\n" +
              $"VMs: {summary.VmCount} | Nodes: {summary.NodeCount} | Unresolved: {summary.UnresolvedRequirementCount}\n" +
              $"{summary.RouterSummary} | {summary.DomainSummary} | {summary.StartabilitySummary}";
    }

    internal void ApplyV2CredentialEditorState(string helpText, string username)
    {
        DeployV2CredentialSlotEditorTextBlock.Text = helpText;
        DeployV2CredentialSlotUsernameTextBox.Text = username;
        DeployV2CredentialSlotPasswordBox.Password = string.Empty;
    }

    internal void ApplyV2BaseRemoteAccessState(DeployV2BaseRemoteAccessRow row)
    {
        DeployV2EnableRemoteDesktopCheckBox.IsChecked = row.EnableRemoteDesktop;
        DeployV2SetPrivateNetworkProfileCheckBox.IsChecked = row.SetPrivateNetworkProfile;
        DeployV2DisableFirewallCheckBox.IsChecked = row.DisableFirewall;
        DeployV2DisableRdpNlaCheckBox.IsChecked = row.DisableRdpNla;
    }

    internal DeployV2CredentialSlotRow? SelectedV2CredentialSlotRow =>
        DeployV2CredentialSlotsListView.SelectedItem as DeployV2CredentialSlotRow;

    public string V2CredentialSlotUsername => DeployV2CredentialSlotUsernameTextBox.Text;

    public string V2CredentialSlotPassword => DeployV2CredentialSlotPasswordBox.Password;

    public bool V2DisableFirewall => DeployV2DisableFirewallCheckBox.IsChecked == true;

    public bool V2DisableRdpNla => DeployV2DisableRdpNlaCheckBox.IsChecked == true;

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

    private void OnDeployV2CredentialSlotSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        V2CredentialSlotSelectionChanged?.Invoke(this, e);
    }

    private void OnDeployV2SaveCredentialSlotRequested(object sender, RoutedEventArgs e)
    {
        SaveV2CredentialSlotRequested?.Invoke(this, e);
    }

    private void OnDeployV2BaseRemoteAccessOptionsChanged(object sender, RoutedEventArgs e)
    {
        V2BaseRemoteAccessOptionsChanged?.Invoke(this, e);
    }
}
