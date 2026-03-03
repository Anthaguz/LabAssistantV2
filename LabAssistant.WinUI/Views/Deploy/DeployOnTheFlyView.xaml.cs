using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Deploy;

public sealed partial class DeployOnTheFlyView : UserControl
{
    public DeployOnTheFlyView()
    {
        InitializeComponent();
    }

    public Border DeployOnTheFlyVmEntriesPanelControl => DeployOnTheFlyVmEntriesPanel;

    public ListView DeployOnTheFlyVmEntriesListViewControl => DeployOnTheFlyVmEntriesListView;

    public Button DeployOnTheFlyAddVmButtonControl => DeployOnTheFlyAddVmButton;

    public Button DeployOnTheFlyRemoveVmButtonControl => DeployOnTheFlyRemoveVmButton;

    public TextBox DeployOnTheFlyVmNameTextBoxControl => DeployOnTheFlyVmNameTextBox;

    public TextBox DeployOnTheFlyVmMemoryTextBoxControl => DeployOnTheFlyVmMemoryTextBox;

    public TextBox DeployOnTheFlyVmCpuTextBoxControl => DeployOnTheFlyVmCpuTextBox;

    public TextBox DeployOnTheFlyVmVhdPathTextBoxControl => DeployOnTheFlyVmVhdPathTextBox;

    public TextBox DeployOnTheFlyVmSwitchesTextBoxControl => DeployOnTheFlyVmSwitchesTextBox;

    public Button DeployOnTheFlyApplyVmChangesButtonControl => DeployOnTheFlyApplyVmChangesButton;

    public Border DeployOnTheFlyReadinessSummaryPanelControl => DeployOnTheFlyReadinessSummaryPanel;

    public TextBlock DeployOnTheFlyOverallStateTextBlockControl => DeployOnTheFlyOverallStateTextBlock;

    public TextBlock DeployOnTheFlyReadinessSummaryTextBlockControl => DeployOnTheFlyReadinessSummaryTextBlock;

    public Button DeployOnTheFlyEvaluateButtonControl => DeployOnTheFlyEvaluateButton;

    public Button DeployOnTheFlyResolveSuggestionsButtonControl => DeployOnTheFlyResolveSuggestionsButton;

    public Button DeployOnTheFlyOpenTemplateEditorButtonControl => DeployOnTheFlyOpenTemplateEditorButton;

    public Button DeployOnTheFlyStartButtonControl => DeployOnTheFlyStartButton;

    public TextBlock DeployOnTheFlyStatusTextBlockControl => DeployOnTheFlyStatusTextBlock;
}
