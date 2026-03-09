using LabAssistant.Models.Templates;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Deploy;

public sealed partial class DeployOnTheFlyView : UserControl
{
    private const double CompactLayoutThreshold = 1120;

    public event Action<VmTemplate>? VmRemoveRequested;

    public DeployOnTheFlyView()
    {
        InitializeComponent();
        SizeChanged += DeployOnTheFlyView_SizeChanged;
        UpdateLayoutMode(CompactLayoutThreshold + 1);
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

    private void DeployOnTheFlyView_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
    {
        UpdateLayoutMode(e.NewSize.Width);
    }

    private void UpdateLayoutMode(double width)
    {
        var useStackedLayout = width < CompactLayoutThreshold;
        DeployOnTheFlyListColumnDefinition.Width = new GridLength(1, GridUnitType.Star);
        DeployOnTheFlyEditorColumnDefinition.Width = useStackedLayout ? new GridLength(0) : new GridLength(1.25, GridUnitType.Star);
        DeployOnTheFlyPrimaryRowDefinition.Height = new GridLength(1, GridUnitType.Star);
        DeployOnTheFlyEditorRowDefinition.Height = useStackedLayout ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        Grid.SetRow(DeployOnTheFlyVmEntriesPanel, 0);
        Grid.SetColumn(DeployOnTheFlyVmEntriesPanel, 0);

        Grid.SetRow(DeployOnTheFlyVmEditorPanel, useStackedLayout ? 1 : 0);
        Grid.SetColumn(DeployOnTheFlyVmEditorPanel, useStackedLayout ? 0 : 1);
    }
}
