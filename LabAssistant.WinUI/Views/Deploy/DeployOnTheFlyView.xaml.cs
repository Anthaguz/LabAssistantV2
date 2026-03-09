using LabAssistant.Models.Templates;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Deploy;

public sealed partial class DeployOnTheFlyView : UserControl
{
    public event Action<VmTemplate>? VmRemoveRequested;

    public DeployOnTheFlyView()
    {
        InitializeComponent();
    }

    public Border DeployOnTheFlyVmEntriesPanelControl => DeployOnTheFlyVmEntriesPanel;

    public ListView DeployOnTheFlyVmEntriesListViewControl => DeployOnTheFlyVmEntriesListView;

    public Button DeployOnTheFlyAddVmButtonControl => DeployOnTheFlyAddVmButton;

    public Button DeployOnTheFlyRemoveVmButtonControl => DeployOnTheFlyRemoveVmButton;

    public TextBlock DeployOnTheFlyEditorIssueSummaryTextBlockControl => DeployOnTheFlyEditorIssueSummaryTextBlock;

    public TextBox DeployOnTheFlyVmNameTextBoxControl => DeployOnTheFlyVmNameTextBox;

    public TextBox DeployOnTheFlyVmMemoryTextBoxControl => DeployOnTheFlyVmMemoryTextBox;

    public TextBox DeployOnTheFlyVmCpuTextBoxControl => DeployOnTheFlyVmCpuTextBox;

    public ComboBox DeployOnTheFlyVmVhdxCatalogComboBoxControl => DeployOnTheFlyVmVhdxCatalogComboBox;

    public ComboBox DeployOnTheFlyVmSwitchComboBoxControl => DeployOnTheFlyVmSwitchComboBox;

    public TextBlock DeployOnTheFlyVmSwitchGuidanceTextBlockControl => DeployOnTheFlyVmSwitchGuidanceTextBlock;

    public TextBlock DeployOnTheFlyVmVhdxGuidanceTextBlockControl => DeployOnTheFlyVmVhdxGuidanceTextBlock;

    public Button DeployOnTheFlyApplyVmChangesButtonControl => DeployOnTheFlyApplyVmChangesButton;

    public Border DeployOnTheFlyReadinessSummaryPanelControl => DeployOnTheFlyReadinessSummaryPanel;

    public TextBlock DeployOnTheFlyOverallStateTextBlockControl => DeployOnTheFlyOverallStateTextBlock;

    public ProgressBar DeployOnTheFlyProgressBarControl => DeployOnTheFlyProgressBar;

    public TextBlock DeployOnTheFlyProgressSummaryTextBlockControl => DeployOnTheFlyProgressSummaryTextBlock;

    public Button DeployOnTheFlyOpenResultsPanelButtonControl => DeployOnTheFlyOpenResultsPanelButton;

    public TextBlock DeployOnTheFlyResultsPanelSummaryTextBlockControl => DeployOnTheFlyResultsPanelSummaryTextBlock;

    public TextBlock DeployOnTheFlyGlobalIssuesBadgeTextBlockControl => DeployOnTheFlyGlobalIssuesBadgeTextBlock;

    public TextBlock DeployOnTheFlyReadinessSummaryTextBlockControl => DeployOnTheFlyReadinessSummaryTextBlock;

    public Button DeployOnTheFlyEvaluateButtonControl => DeployOnTheFlyEvaluateButton;

    public Button DeployOnTheFlyResolveSuggestionsButtonControl => DeployOnTheFlyResolveSuggestionsButton;

    public Button DeployOnTheFlyOpenTemplateEditorButtonControl => DeployOnTheFlyOpenTemplateEditorButton;

    public Button DeployOnTheFlyStartButtonControl => DeployOnTheFlyStartButton;

    public TextBlock DeployOnTheFlyStatusTextBlockControl => DeployOnTheFlyStatusTextBlock;

    private void DeployOnTheFlyRowRemoveButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Button { Tag: VmTemplate vmEntry })
        {
            VmRemoveRequested?.Invoke(vmEntry);
        }
    }
}
