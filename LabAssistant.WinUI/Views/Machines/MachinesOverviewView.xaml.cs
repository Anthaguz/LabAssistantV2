using LabAssistant.WinUI.ViewModels.Machines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Machines;

public sealed partial class MachinesOverviewView : UserControl
{
    public MachinesViewModel ViewModel { get; }

    public MachinesOverviewView()
    {
        ViewModel = App.Services.GetRequiredService<MachinesViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        Loaded += async (_, _) => await ViewModel.InitializeAsync();
        Unloaded += async (_, _) => await ViewModel.CleanupAsync();
    }
}
