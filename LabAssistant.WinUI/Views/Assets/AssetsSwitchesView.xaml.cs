using LabAssistant.WinUI.ViewModels.Assets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Assets;

public sealed partial class AssetsSwitchesView : UserControl
{
    public AssetsSwitchesViewModel ViewModel { get; }

    public AssetsSwitchesView()
    {
        ViewModel = App.Services.GetRequiredService<AssetsSwitchesViewModel>();
        InitializeComponent();
    }
}
