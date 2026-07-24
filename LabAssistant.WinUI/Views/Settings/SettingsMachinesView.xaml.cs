using LabAssistant.WinUI.Infrastructure;
using LabAssistant.WinUI.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Settings;

/// <summary>
/// Settings > Machines subview. Binds directly to <see cref="SettingsMachinesViewModel"/> via
/// <c>x:Bind</c>; the view model loads the current deletion policy on its <c>Loaded</c> lifecycle
/// and persists changes. Replaces the former inline Settings panel and its MainWindow code-behind.
/// </summary>
public sealed partial class SettingsMachinesView : UserControl
{
    public SettingsMachinesViewModel ViewModel { get; }

    public SettingsMachinesView()
    {
        ViewModel = App.Services.GetRequiredService<SettingsMachinesViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        Loaded += (_, _) => ViewLifecycle.Run(() => ViewModel.InitializeAsync(), "SettingsMachinesView.Initialize");
        Unloaded += (_, _) => ViewLifecycle.Run(() => ViewModel.CleanupAsync(), "SettingsMachinesView.Cleanup");
    }
}
