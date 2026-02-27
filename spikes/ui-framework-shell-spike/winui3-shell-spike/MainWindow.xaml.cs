using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;
using WinUi3ShellSpike.ViewModels;

namespace WinUi3ShellSpike;

public sealed partial class MainWindow : Window
{
    private ShellSpikeViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        RootGrid.DataContext = ViewModel;
        Title = "WinUI 3 Shell Spike";
        SetWindowSize(1320, 840);
    }

    private void CapabilityListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListView { SelectedItem: string capability })
        {
            ViewModel.SelectCapabilityCommand.Execute(capability);
        }
    }

    private void DismissIssue_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ShellIssueItem issue })
        {
            ViewModel.DismissIssueCommand.Execute(issue);
        }
    }

    private void SetWindowSize(int width, int height)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        appWindow?.Resize(new Windows.Graphics.SizeInt32(width, height));
    }
}
