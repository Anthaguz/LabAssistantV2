using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Assets;

public sealed partial class AssetsSwitchesView : UserControl
{
    private const double CompactLayoutThreshold = 1040;

    public AssetsSwitchesView()
    {
        InitializeComponent();
        SizeChanged += AssetsSwitchesView_SizeChanged;
        UpdateLayoutMode(CompactLayoutThreshold + 1);
    }

    private void AssetsSwitchesView_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
    {
        UpdateLayoutMode(e.NewSize.Width);
    }

    private void UpdateLayoutMode(double width)
    {
        var useStackedLayout = width < CompactLayoutThreshold;
        AssetsSwitchesListColumnDefinition.Width = new GridLength(1, GridUnitType.Star);
        AssetsSwitchesDetailsColumnDefinition.Width = useStackedLayout ? new GridLength(0) : new GridLength(1.4, GridUnitType.Star);
        AssetsSwitchesPrimaryRowDefinition.Height = new GridLength(1, GridUnitType.Star);
        AssetsSwitchesStateRowDefinition.Height = GridLength.Auto;
        AssetsSwitchesDetailsRowDefinition.Height = useStackedLayout ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        Grid.SetRow(AssetsSwitchesListRegion, 0);
        Grid.SetColumn(AssetsSwitchesListRegion, 0);

        Grid.SetRow(AssetsSwitchesDetailsRegion, useStackedLayout ? 2 : 0);
        Grid.SetColumn(AssetsSwitchesDetailsRegion, useStackedLayout ? 0 : 1);

        Grid.SetRow(AssetsSwitchesStateRegion, 1);
        Grid.SetColumn(AssetsSwitchesStateRegion, 0);
        Grid.SetColumnSpan(AssetsSwitchesStateRegion, useStackedLayout ? 1 : 2);
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
