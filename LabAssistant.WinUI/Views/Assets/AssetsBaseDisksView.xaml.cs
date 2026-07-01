using LabAssistant.WinUI.ViewModels.Assets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Assets;

public sealed partial class AssetsBaseDisksView : UserControl
{
    public AssetsBaseDisksViewModel ViewModel { get; }

    public AssetsBaseDisksView()
    {
        ViewModel = App.Services.GetRequiredService<AssetsBaseDisksViewModel>();
        InitializeComponent();
    }
}
