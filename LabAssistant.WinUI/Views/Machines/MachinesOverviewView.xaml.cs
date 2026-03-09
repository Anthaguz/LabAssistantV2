using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Machines;

public sealed partial class MachinesOverviewView : UserControl
{
    private const double CompactLayoutThreshold = 1024;

    public MachinesOverviewView()
    {
        InitializeComponent();
        SizeChanged += MachinesOverviewView_SizeChanged;
        UpdateLayoutMode(CompactLayoutThreshold + 1);
    }

    private void MachinesOverviewView_SizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e)
    {
        UpdateLayoutMode(e.NewSize.Width);
    }

    private void UpdateLayoutMode(double width)
    {
        var useStackedLayout = width < CompactLayoutThreshold;
        MachinesListColumnDefinition.Width = useStackedLayout ? new GridLength(1, GridUnitType.Star) : new GridLength(2, GridUnitType.Star);
        MachinesSplitterColumnDefinition.Width = useStackedLayout ? new GridLength(0) : new GridLength(12);
        MachinesDetailsColumnDefinition.Width = useStackedLayout ? new GridLength(0) : new GridLength(3, GridUnitType.Star);
        MachinesListRowDefinition.Height = new GridLength(1, GridUnitType.Star);
        MachinesDetailsRowDefinition.Height = useStackedLayout ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        Grid.SetRow(MachinesInventoryRegion, 0);
        Grid.SetColumn(MachinesInventoryRegion, 0);

        Grid.SetRow(MachinesDetailsRegion, useStackedLayout ? 1 : 0);
        Grid.SetColumn(MachinesDetailsRegion, useStackedLayout ? 0 : 2);
    }
}
