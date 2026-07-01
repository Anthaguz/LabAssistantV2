using LabAssistant.WinUI.ViewModels.Assets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Assets;

public sealed partial class AssetsOverviewView : UserControl
{
    public AssetsOverviewViewModel ViewModel { get; }

    public AssetsOverviewView()
    {
        ViewModel = App.Services.GetRequiredService<AssetsOverviewViewModel>();
        InitializeComponent();
    }
}
