using LabAssistant.WinUI.Infrastructure;
using LabAssistant.WinUI.ViewModels.Assets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Assets;

public sealed partial class AssetsBaseDisksView : UserControl
{
    public AssetsBaseDisksViewModel ViewModel { get; }

    public AssetsBaseDisksView()
    {
        ViewModel = App.Services.GetRequiredService<AssetsBaseDisksViewModel>();
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewLifecycle.Run(() => ViewModel.InitializeAsync(), "AssetsBaseDisksView.Initialize");
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewLifecycle.Run(() => ViewModel.CleanupAsync(), "AssetsBaseDisksView.Cleanup");
    }
}
