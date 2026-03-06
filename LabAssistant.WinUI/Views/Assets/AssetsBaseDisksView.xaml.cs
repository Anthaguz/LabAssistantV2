using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Assets;

public sealed partial class AssetsBaseDisksView : UserControl
{
    public AssetsBaseDisksView()
    {
        InitializeComponent();
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
