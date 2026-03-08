using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Assets;

public sealed partial class AssetsSwitchesView : UserControl
{
    public AssetsSwitchesView()
    {
        InitializeComponent();
    }

    public Button AssetsSwitchesRefreshButtonControl => AssetsSwitchesRefreshButton;

    public Button AssetsSwitchesCreateButtonControl => AssetsSwitchesCreateButton;

    public Button AssetsSwitchesApplyButtonControl => AssetsSwitchesApplyButton;

    public Button AssetsSwitchesDeleteButtonControl => AssetsSwitchesDeleteButton;

    public TextBlock AssetsSwitchesStatusTextBlockControl => AssetsSwitchesStatusTextBlock;

    public Border AssetsSwitchesListRegionControl => AssetsSwitchesListRegion;

    public ListView AssetsSwitchesListViewControl => AssetsSwitchesListView;

    public Border AssetsSwitchesDetailsRegionControl => AssetsSwitchesDetailsRegion;

    public Border AssetsSwitchesEditRegionControl => AssetsSwitchesEditRegion;

    public TextBlock AssetsSwitchesSelectedSwitchValidationTextBlockControl => AssetsSwitchesSelectedSwitchValidationTextBlock;

    public TextBlock AssetsSwitchesDeleteConstraintTextBlockControl => AssetsSwitchesDeleteConstraintTextBlock;

    public TextBox AssetsSwitchesNameTextBoxControl => AssetsSwitchesNameTextBox;

    public ComboBox AssetsSwitchesTypeComboBoxControl => AssetsSwitchesTypeComboBox;

    public TextBox AssetsSwitchesAdapterTextBoxControl => AssetsSwitchesAdapterTextBox;

    public TextBlock AssetsSwitchesAttachedVmsHintTextBlockControl => AssetsSwitchesAttachedVmsHintTextBlock;

    public ListView AssetsSwitchesAttachedVmsListViewControl => AssetsSwitchesAttachedVmsListView;

    public Border AssetsSwitchesLoadingStatePanelControl => AssetsSwitchesLoadingStatePanel;

    public Border AssetsSwitchesEmptyStatePanelControl => AssetsSwitchesEmptyStatePanel;

    public Border AssetsSwitchesErrorStatePanelControl => AssetsSwitchesErrorStatePanel;

    public TextBlock AssetsSwitchesLoadingStateTextBlockControl => AssetsSwitchesLoadingStateTextBlock;

    public TextBlock AssetsSwitchesEmptyStateTextBlockControl => AssetsSwitchesEmptyStateTextBlock;

    public TextBox AssetsSwitchesErrorStateTextBoxControl => AssetsSwitchesErrorStateTextBox;
}
