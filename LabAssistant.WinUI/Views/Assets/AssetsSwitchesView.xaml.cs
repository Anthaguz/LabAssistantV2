using LabAssistant.WinUI.Infrastructure;
using LabAssistant.WinUI.ViewModels.Assets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Assets;

public sealed partial class AssetsSwitchesView : UserControl
{
    public AssetsSwitchesViewModel ViewModel { get; }

    public AssetsSwitchesView()
    {
        ViewModel = App.Services.GetRequiredService<AssetsSwitchesViewModel>();
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewLifecycle.Run(() => ViewModel.InitializeAsync(), "AssetsSwitchesView.Initialize");
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewLifecycle.Run(() => ViewModel.CleanupAsync(), "AssetsSwitchesView.Cleanup");
    }
}
