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

    public Border AssetsBaseDisksLoadingStatePanelControl => AssetsBaseDisksLoadingStatePanel;

    public Border AssetsBaseDisksEmptyStatePanelControl => AssetsBaseDisksEmptyStatePanel;

    public Border AssetsBaseDisksErrorStatePanelControl => AssetsBaseDisksErrorStatePanel;
}
