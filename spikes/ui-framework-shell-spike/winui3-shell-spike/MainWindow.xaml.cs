using Microsoft.UI.Xaml;
using WinUi3ShellSpike.ViewModels;

namespace WinUi3ShellSpike;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new ShellSpikeViewModel();
    }
}
