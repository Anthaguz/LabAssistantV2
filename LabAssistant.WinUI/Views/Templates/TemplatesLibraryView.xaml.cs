using LabAssistant.WinUI.ViewModels.Templates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Templates;

public sealed partial class TemplatesLibraryView : UserControl
{
    public TemplatesLibraryViewModel ViewModel { get; }

    public TemplatesLibraryView()
    {
        ViewModel = App.Services.GetRequiredService<TemplatesLibraryViewModel>();
        InitializeComponent();
        InitializeBridge();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    partial void InitializeBridge();

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachBridge();
        await ViewModel.InitializeAsync();
    }

    private async void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachBridge();
        await ViewModel.CleanupAsync();
    }
}
