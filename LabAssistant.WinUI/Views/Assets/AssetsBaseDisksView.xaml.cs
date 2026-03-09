using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Assets;

public sealed partial class AssetsBaseDisksView : UserControl
{
    private const double CompactLayoutThreshold = 1040;

    public AssetsBaseDisksView()
    {
        InitializeComponent();
        SizeChanged += AssetsBaseDisksView_SizeChanged;
        UpdateLayoutMode(CompactLayoutThreshold + 1);
    }

    private void AssetsBaseDisksView_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
    {
        UpdateLayoutMode(e.NewSize.Width);
    }

    private void UpdateLayoutMode(double width)
    {
        var useStackedLayout = width < CompactLayoutThreshold;
        AssetsBaseDisksListColumnDefinition.Width = new GridLength(1, GridUnitType.Star);
        AssetsBaseDisksDetailsColumnDefinition.Width = useStackedLayout ? new GridLength(0) : new GridLength(1.4, GridUnitType.Star);
        AssetsBaseDisksPrimaryRowDefinition.Height = new GridLength(1, GridUnitType.Star);
        AssetsBaseDisksStateRowDefinition.Height = GridLength.Auto;
        AssetsBaseDisksDetailsRowDefinition.Height = useStackedLayout ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        Grid.SetRow(AssetsBaseDisksListRegion, 0);
        Grid.SetColumn(AssetsBaseDisksListRegion, 0);

        Grid.SetRow(AssetsBaseDisksDetailsRegion, useStackedLayout ? 2 : 0);
        Grid.SetColumn(AssetsBaseDisksDetailsRegion, useStackedLayout ? 0 : 1);

        Grid.SetRow(AssetsBaseDisksStateRegion, 1);
        Grid.SetColumn(AssetsBaseDisksStateRegion, 0);
        Grid.SetColumnSpan(AssetsBaseDisksStateRegion, useStackedLayout ? 1 : 2);
    }

    public Border AssetsBaseDisksActionsRegionControl => AssetsBaseDisksActionsRegion;

    public Button AssetsBaseDisksRefreshButtonControl => AssetsBaseDisksRefreshButton;

    public Button AssetsBaseDisksImportButtonControl => AssetsBaseDisksImportButton;

    public Button AssetsBaseDisksValidateButtonControl => AssetsBaseDisksValidateButton;

    public Button AssetsBaseDisksRemoveButtonControl => AssetsBaseDisksRemoveButton;

    public Border AssetsBaseDisksStatusRegionControl => AssetsBaseDisksStatusRegion;

    public TextBlock AssetsBaseDisksStatusTextBlockControl => AssetsBaseDisksStatusTextBlock;

    public Border AssetsBaseDisksListRegionControl => AssetsBaseDisksListRegion;

    public ListView AssetsBaseDisksListViewControl => AssetsBaseDisksListView;

    public Border AssetsBaseDisksDetailsRegionControl => AssetsBaseDisksDetailsRegion;

    public Border AssetsBaseDisksMetadataEditRegionControl => AssetsBaseDisksMetadataEditRegion;

    public TextBlock AssetsBaseDisksSelectedDiskSummaryTextBlockControl => AssetsBaseDisksSelectedDiskSummaryTextBlock;

    public TextBlock AssetsBaseDisksSelectedDiskValidationTextBlockControl => AssetsBaseDisksSelectedDiskValidationTextBlock;

    public TextBlock AssetsBaseDisksReferenceWarningTextBlockControl => AssetsBaseDisksReferenceWarningTextBlock;

    public TextBox AssetsBaseDisksOsNameTextBoxControl => AssetsBaseDisksOsNameTextBox;

    public TextBox AssetsBaseDisksOsVersionTextBoxControl => AssetsBaseDisksOsVersionTextBox;

    public TextBox AssetsBaseDisksPathTextBoxControl => AssetsBaseDisksPathTextBox;

    public Button AssetsBaseDisksBrowsePathButtonControl => AssetsBaseDisksBrowsePathButton;

    public TextBox AssetsBaseDisksGenerationTextBoxControl => AssetsBaseDisksGenerationTextBox;

    public TextBox AssetsBaseDisksNotesTextBoxControl => AssetsBaseDisksNotesTextBox;

    public Button AssetsBaseDisksSaveMetadataButtonControl => AssetsBaseDisksSaveMetadataButton;

    public Border AssetsBaseDisksLoadingStatePanelControl => AssetsBaseDisksLoadingStatePanel;

    public Border AssetsBaseDisksEmptyStatePanelControl => AssetsBaseDisksEmptyStatePanel;

    public Border AssetsBaseDisksErrorStatePanelControl => AssetsBaseDisksErrorStatePanel;

    public TextBlock AssetsBaseDisksLoadingStateTextBlockControl => AssetsBaseDisksLoadingStateTextBlock;

    public TextBlock AssetsBaseDisksEmptyStateTextBlockControl => AssetsBaseDisksEmptyStateTextBlock;

    public TextBlock AssetsBaseDisksErrorStateTextBlockControl => AssetsBaseDisksErrorStateTextBlock;
}
