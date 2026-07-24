using LabAssistant.WinUI.Infrastructure;
using LabAssistant.WinUI.ViewModels.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace LabAssistant.WinUI.Views.Diagnostics;

/// <summary>
/// Diagnostics Overview subview. Binds directly to <see cref="DiagnosticsOverviewViewModel"/> via
/// <c>x:Bind</c>; the hosting page wires the view model's capability seam and cross-subview summary
/// refresh. Replaces the former event-forwarding, imperative view-state code-behind.
/// </summary>
public sealed partial class DiagnosticsOverviewView : UserControl
{
    public DiagnosticsOverviewViewModel ViewModel { get; }

    public DiagnosticsOverviewView()
    {
        ViewModel = App.Services.GetRequiredService<DiagnosticsOverviewViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
        Loaded += (_, _) => ViewLifecycle.Run(() => ViewModel.InitializeAsync(), "DiagnosticsOverviewView.Initialize");
        Unloaded += (_, _) => ViewLifecycle.Run(() => ViewModel.CleanupAsync(), "DiagnosticsOverviewView.Cleanup");
    }
}
